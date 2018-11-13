using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace BenchmarkDotNet.ResultDiff
{
    /// <summary>
    /// Reads BenchmarkDotNet CSV format results from two directories and creates a rudimentary diff view. 
    /// </summary>
    public static class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("syntax: <old result path> <new result path> [target dir to save files to]");
                return;
            }

            string targetDir = Directory.GetCurrentDirectory();
            if (args.Length == 3)
            {
                targetDir = args[2];
            }

            var oldDir = FindDirectory(args[0]);
            var newDir = FindDirectory(args[1]);

            var pairs = CreateFilePairs(oldDir, newDir);

            var columns = new List<string>
            {
                "Method",
                "FileName",
                "N",
                "Mean",
                "Gen 0/1k Op",
                "Gen 1/1k Op",
                "Gen 2/1k Op",
                "Allocated Memory/Op"
            };

            var oldDirName = oldDir.Name != newDir.Name ? oldDir.Name : oldDir.Parent.Name;
            var newDirName = newDir.Name != oldDir.Name ? newDir.Name : newDir.Parent.Name;
            var targetFile = Path.Combine(targetDir, oldDirName + "_vs_" + newDirName + "-github.md");
            using (var writer = new StreamWriter(targetFile))
            {
                foreach (var pair in pairs)
                {
                    writer.WriteLine("## " + pair.OldFile.Name.Replace("-report.csv", ""));
                    writer.WriteLine();

                    Console.WriteLine("Analyzing pair " + pair.OldFile.Name);

                    using (var oldReader = new StreamReader(pair.OldFile.FullName))
                    using (var newReader = new StreamReader(pair.NewFile.FullName))
                    {
                        ReadLinePair(oldReader, newReader, out var headers);
                        var oldHeaders = headers.Old.Split(";").Select((x, i) => (x, i)).ToDictionary(x => x.Item1, x => x.Item2);
                        var newHeaders = headers.New.Split(";").Select((x, i) => (x, i)).ToDictionary(x => x.Item1, x => x.Item2);

                        var readLines = new List<(string Old, string New)>();
                        while (ReadLinePair(oldReader, newReader, out var lines))
                        {
                            if (string.IsNullOrEmpty(lines.Old))
                            {
                                break;
                            }

                            readLines.Add(lines);
                        }

                        var effectiveHeaders = columns
                            .Where(x => oldHeaders.ContainsKey(x) || newHeaders.ContainsKey(x))
                            .ToList();

                        writer.WriteLine("| **Diff**|" + string.Join("|", effectiveHeaders) + "|");

                        writer.Write("|------- ");
                        foreach (var effectiveHeader in effectiveHeaders)
                        {
                            writer.Write("|-------");
                            if (effectiveHeader.IndexOf("Gen ") > -1 || effectiveHeader == "Allocated" || effectiveHeader == "Mean")
                            {
                                writer.Write(":");
                            }
                        }

                        writer.WriteLine("|");

                        foreach (var lines in readLines)
                        {
                            if (string.IsNullOrEmpty(lines.Old))
                            {
                                break;
                            }

                            var oldie = lines.Old.Split(";");
                            var newbie = lines.New.Split(";");

                            var oldColumnValues = new Dictionary<string, string>();

                            writer.Write("| Old |");
                            foreach (var effectiveHeader in effectiveHeaders)
                            {
                                string value = "-";
                                if (oldHeaders.TryGetValue(effectiveHeader, out var oldHeader))
                                {
                                    value = oldie[oldHeader].TrimStart('"').TrimEnd('"');
                                }
                                oldColumnValues[effectiveHeader] = value;
                                writer.Write(value + "|");
                            }

                            writer.WriteLine();

                            writer.Write("| **New** |");
                            foreach (var effectiveHeader in effectiveHeaders)
                            {
                                if (effectiveHeader == "Method" || effectiveHeader == "N" || effectiveHeader == "FileName")
                                {
                                    writer.Write("\t|");
                                }
                                else
                                {
                                    string value = "-";
                                    if (newHeaders.ContainsKey(effectiveHeader))
                                    {
                                        value = newbie[newHeaders[effectiveHeader]].TrimStart('"').TrimEnd('"');
                                    }

                                    if (oldColumnValues.TryGetValue(effectiveHeader, out var oldString))
                                    {
                                        var oldTokens = oldString.Split(" ");
                                        var newTokens = value.Split(" ");

                                        if (oldTokens.Length == newTokens.Length)
                                        {
                                            bool canCalculateDiff = oldTokens[0] != "-"
                                                                    && newTokens[0] != "-"
                                                                    && oldTokens[0] != "N/A"
                                                                    && newTokens[0] != "N/A";

                                            decimal newMultiplier = 1;
                                            const decimal ConversionFromBigger = 0.0009765625M;

                                            if (canCalculateDiff && oldTokens.Length > 1)
                                            {
                                                var oldUnit = oldTokens[1];
                                                var newUnit = newTokens[1];
                                                if (oldUnit == newUnit)
                                                {
                                                    // ok
                                                }
                                                else if (oldUnit == "MB" && newUnit == "KB"
                                                         || oldUnit == "KB" && newUnit == "B"
                                                         || oldUnit == "GB" && newUnit == "MB"
                                                         || oldUnit == "s" && newUnit == "ms"
                                                         || oldUnit == "ms" && newUnit == "us")
                                                {
                                                    newMultiplier = ConversionFromBigger;
                                                }
                                                else if (oldUnit == "MB" && newUnit == "B")
                                                {
                                                    newMultiplier = ConversionFromBigger * ConversionFromBigger;
                                                }
                                                else
                                                {
                                                    canCalculateDiff = false;
                                                }
                                            }

                                            if (canCalculateDiff)
                                            {
                                                var old = decimal.Parse(oldTokens[0], CultureInfo.InvariantCulture);
                                                var newValue = decimal.Parse(newTokens[0], CultureInfo.InvariantCulture);

                                                var diff = ((newValue * newMultiplier) / old - 1) * 100;
                                                value += $" ({diff:+#;-#;0}%)";
                                            }
                                            else if (oldTokens[0] == "-" || newTokens[0] == "-")
                                            {
                                                // OK
                                            }
                                            else if (decimal.TryParse(oldTokens[0], NumberStyles.Number, CultureInfo.InvariantCulture, out _)
                                                     && newTokens[0] == "-")
                                            {
                                                value += " (-100%)";                                               
                                            }
                                            else
                                            {
                                                Console.Error.WriteLine("Cannot calculate difff for " + oldString + " vs " + value);
                                            }
                                        }
                                    }

                                    writer.Write(" **" + value + "** |");
                                }
                            }

                            writer.WriteLine();
                        }
                    }

                    writer.WriteLine();
                    writer.WriteLine();
                }

                Console.WriteLine("Wrote results to " + targetFile);
            }
        }

        private static List<(FileInfo OldFile, FileInfo NewFile)> CreateFilePairs(DirectoryInfo oldDir, DirectoryInfo newDir)
        {
            var pairs = new List<(FileInfo OldFile, FileInfo NewFile)>();
            foreach (var oldReportFile in oldDir.GetFiles("*-report.csv"))
            {
                var fileName = oldReportFile.Name;
                var newReportFile = new FileInfo(Path.Combine(newDir.FullName, fileName));
                if (newReportFile.Exists)
                {
                    pairs.Add((oldReportFile, newReportFile));
                }
                else
                {
                    // check if new file name format without namespace
                    var tokens = fileName.Split('.');
                    if (tokens.Length > 1)
                    {
                        fileName = tokens[tokens.Length - 2] + "." + tokens[tokens.Length - 1];
                        newReportFile = new FileInfo(Path.Combine(newDir.FullName, fileName));
                        if (newReportFile.Exists)
                        {
                            pairs.Add((oldReportFile, newReportFile));
                        }
                    }
                }
            }

            return pairs;
        }

        private static DirectoryInfo FindDirectory(string path)
        {
            var dir = new DirectoryInfo(path);
            if (!dir.Exists)
            {
                Console.Error.WriteLine("directory does not exist: " + path);
            }

            if (dir.GetFiles("*.csv").Length == 0)
            {
                var resultsDirectory = dir.GetDirectories().FirstOrDefault(x => x.Name == "results");
                if (resultsDirectory != null)
                {
                    dir = resultsDirectory;
                }
            }

            return dir;
        }

        private static bool ReadLinePair(
            TextReader oldReader,
            TextReader newReader,
            out (string Old, string New) pair)
        {
            string oldLine;
            string newLine;
            var ok = (oldLine = oldReader.ReadLine()) != null;
            ok &= (newLine = newReader.ReadLine()) != null;
            pair = (oldLine, newLine);
            return ok;
        }
    }
}