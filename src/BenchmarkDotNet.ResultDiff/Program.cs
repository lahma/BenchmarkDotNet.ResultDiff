using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;

namespace BenchmarkDotNet.ResultDiff
{
    /// <summary>
    /// Reads BenchmarkDotNet CSV format results from two directories and creates a rudimentary diff view. 
    /// </summary>
    public static class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("syntax: <old result path> <new result path> [target dir to save files to]");
                return;
            }

            var targetDir = Directory.GetCurrentDirectory();
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
                "Error",
                "Gen 0/1k Op",
                "Gen 1/1k Op",
                "Gen 2/1k Op",
                "Allocated Memory/Op",
                "Gen 0",
                "Gen 1",
                "Gen 2",
                "Allocated"
            };

            var oldDirName = oldDir.Name != newDir.Name ? oldDir.Name : oldDir.Parent.Name;
            var newDirName = newDir.Name != oldDir.Name ? newDir.Name : newDir.Parent.Name;
            var targetFile = Path.Combine(targetDir, oldDirName + "_vs_" + newDirName + "-github.md");
            using var writer = new StreamWriter(targetFile);

            foreach (var (oldFile, newFile) in pairs)
            {
                writer.WriteLine("## " + oldFile.Name.Replace("-report.csv", ""));
                writer.WriteLine();

                Console.WriteLine("Analyzing pair " + oldFile.Name);

                using (var oldReader = new CsvReader(new StreamReader(oldFile.FullName), CultureInfo.InvariantCulture))
                using (var newReader = new CsvReader(new StreamReader(newFile.FullName), CultureInfo.InvariantCulture))
                {
                    oldReader.Read();
                    newReader.Read();
                        
                    oldReader.ReadHeader();
                    newReader.ReadHeader();

                    var effectiveHeaders = columns
                        .Where(x => oldReader.TryGetField(x, out string _) || newReader.TryGetField(x, out string _))
                        .ToList();

                    writer.WriteLine("| **Diff**|" + string.Join("|", effectiveHeaders) + "|");

                    writer.Write("|------- ");
                    foreach (var effectiveHeader in effectiveHeaders)
                    {
                        writer.Write("|-------");
                        if (effectiveHeader.IndexOf("Gen ", StringComparison.OrdinalIgnoreCase) > -1 || effectiveHeader == "Allocated" || effectiveHeader == "Mean")
                        {
                            writer.Write(":");
                        }
                    }

                    writer.WriteLine("|");

                    while (oldReader.Read() && newReader.Read())
                    {
                        var oldColumnValues = new Dictionary<string, string>();

                        writer.Write("| Old |");
                        foreach (var effectiveHeader in effectiveHeaders)
                        {
                            string value = "-";
                            if (oldReader.TryGetField(effectiveHeader, out string temp))
                            {
                                value = temp;
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
                                if (newReader.TryGetField(effectiveHeader, out string temp))
                                {
                                    value = temp;
                                }

                                if (oldColumnValues.TryGetValue(effectiveHeader, out var oldString))
                                {
                                    var oldResult = SplitResult(oldString);
                                    var newResult = SplitResult(value);

                                    if (string.IsNullOrWhiteSpace(oldResult.Unit) == string.IsNullOrWhiteSpace(newResult.Unit))
                                    {
                                        bool canCalculateDiff = effectiveHeader != "Error" 
                                                                && oldResult.Value != "-"
                                                                && newResult.Value != "-"
                                                                && oldResult.Value != "N/A"
                                                                && newResult.Value != "N/A"
                                                                && oldResult.Value != "NA"
                                                                && newResult.Value != "NA"
                                                                && (decimal.TryParse(oldResult.Value, out var tempOldResult) && tempOldResult != 0);

                                        decimal newMultiplier = 1;
                                        const decimal ConversionFromBigger = 0.0009765625M;

                                        if (canCalculateDiff && oldResult.Unit.Length > 0)
                                        {
                                            var oldUnit = oldResult.Unit;
                                            var newUnit = newResult.Unit;
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
                                            else if (oldUnit == "ms" && newUnit == "s"
                                                     || oldUnit == "μs" && newUnit == "ms"
                                                     || oldUnit == "KB" && newUnit == "MB")
                                            {
                                                newMultiplier = 1 / ConversionFromBigger;
                                            }
                                            else
                                            {
                                                canCalculateDiff = false;
                                            }
                                        }

                                        if (canCalculateDiff)
                                        {
                                            var old = decimal.Parse(oldResult.Value, CultureInfo.InvariantCulture);
                                            var newValue = decimal.Parse(newResult.Value, CultureInfo.InvariantCulture);

                                            var diff = ((newValue * newMultiplier) / old - 1) * 100;
                                            value += $" ({diff:+#;-#;0}%)";
                                        }
                                        else if (oldResult.Value == "-" || newResult.Value == "-")
                                        {
                                            // OK
                                        }
                                        else if (oldResult.Value == "0.0000" && newResult.Value == "0.0000")
                                        {
                                            // OK
                                        }
                                        else if (decimal.TryParse(oldResult.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out _)
                                                 && newResult.Value == "-")
                                        {
                                            value += " (-100%)";                                               
                                        }
                                        else
                                        {
                                            if (effectiveHeader != "Error")
                                            {
                                                Console.Error.WriteLine("Cannot calculate difff for " + oldString + " vs " + value);
                                            }
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

        private static (string Value, string Unit) SplitResult(string result)
        {
            int idx = result.LastIndexOf(' ');
            if (idx != -1)
            {
                return (result.Substring(0, idx), result.Substring(idx + 1));
            }

            return (result, "");
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
    }
}