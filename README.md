BenchmarkDotNet.ResultDiff is a simple command line program that takes two directories as parameters
and outputs a diff view for the BenchmarkDotNet results.

The input used is the CSV result format and output is the GitHub flavored version of Markdown.

Each result file comparison is added to output with heading containing the name of benchmark (file name).

See [this PR for Jint](https://github.com/sebastienros/jint/pull/495) for an example how output file contents can be easily pasted to PRs.

## Usage

General workflow that works at least for me

* Run your benchmark on original branch
* Rename the result directory BenchmarkDotNet.Artifacts to for example BenchmarkDotNet.Artifacts_original
* Switch to your optimization branch
* Run benchmarks again
* Run this tool and paths as parameters (BenchmarkDotNet.Artifacts_original BenchmarkDotNet.Artifacts)

`
cd  C:\Sources\BenchmarkDotNet.ResultDiff\src\BenchmarkDotNet.ResultDiff
`

`
dotnet run ..\jint\Jint.Benchmark\BenchmarkDotNet.Artifacts_dev ..\jint\Jint.Benchmark\BenchmarkDotNet.Artifacts_my_feature
`

`
Analyzing pair ArrayBenchmark-report.csv
Wrote results to C:\BenchmarkDotNet.ResultDiff\BenchmarkDotNet.Artifacts_dev_vs_BenchmarkDotNet.Artifacts-github.md
`

## BenchmarkDotNet input

The tool turns these two results tables (taken from markdown output, tool actually uses CSV):

*BenchmarkDotNet.Artifacts_dev\results*

|             Method |   N |        Mean |      Error |     StdDev |     Gen 0 |   Allocated |
|------------------- |---- |------------:|-----------:|-----------:|----------:|------------:|
|              Slice | 100 |    436.2 us |  0.7824 us |  0.6936 us |  161.1328 |   660.16 KB |
|             Concat | 100 |    468.4 us |  1.0230 us |  0.9569 us |  175.7813 |   720.31 KB |
|            Unshift | 100 | 17,912.2 us | 29.0475 us | 24.2560 us | 3562.5000 | 14672.66 KB |
|               Push | 100 | 10,861.8 us | 17.9719 us | 16.8109 us |  343.7500 |  1438.28 KB |
|              Index | 100 | 12,106.5 us |  7.0282 us |  6.5742 us |  390.6250 |   1637.5 KB |
|                Map | 100 |  3,382.7 us | 13.8354 us | 12.9416 us |  765.6250 |  3149.22 KB |
|              Apply | 100 |    569.2 us |  1.1511 us |  1.0767 us |  188.4766 |   774.22 KB |
| JsonStringifyParse | 100 |  4,523.7 us |  6.5277 us |  6.1060 us | 1273.4375 |     5225 KB |

*BenchmarkDotNet.Artifacts_my_feature\results*


|             Method |   N |        Mean |      Error |    StdDev |     Gen 0 |   Allocated |
|------------------- |---- |------------:|-----------:|----------:|----------:|------------:|
|              Slice | 100 |    455.8 us |   3.482 us |  3.086 us |  161.1328 |   660.16 KB |
|             Concat | 100 |    496.6 us |   9.547 us | 10.611 us |  175.7813 |   720.31 KB |
|            Unshift | 100 | 19,023.0 us | 103.525 us | 96.838 us | 3562.5000 | 14672.66 KB |
|               Push | 100 | 11,274.1 us |  31.569 us | 29.530 us |  343.7500 |  1438.28 KB |
|              Index | 100 | 12,471.8 us |  33.521 us | 29.716 us |  390.6250 |  1643.75 KB |
|                Map | 100 |  3,624.8 us |  31.269 us | 29.249 us |  691.4063 |  2833.59 KB |
|              Apply | 100 |    600.3 us |   6.965 us |  6.515 us |  188.4766 |   774.22 KB |
| JsonStringifyParse | 100 |  4,602.2 us |  49.303 us | 43.706 us | 1273.4375 |  5225.78 KB |


## Output

Output for each file single result table that allows easier examination of differences between the results:

*BenchmarkDotNet.Artifacts_dev_vs_BenchmarkDotNet.Artifacts_my_feature-github.md*

## ArrayBenchmark

| **Diff**|Method|N|Mean|Gen 0|Allocated|
|------- |-------|-------|-------:|-------:|-------:|
| Old |Slice|100|436.2 us|161.1328|660.16 KB|
| **New** |	|	| **455.8 us (+4%)** | **161.1328 (0%)** | **660.16 KB (0%)** |
| Old |Concat|100|468.4 us|175.7813|720.31 KB|
| **New** |	|	| **496.6 us (+6%)** | **175.7813 (0%)** | **720.31 KB (0%)** |
| Old |Unshift|100|17,912.2 us|3562.5000|14672.66 KB|
| **New** |	|	| **19,023.0 us (+6%)** | **3562.5000 (0%)** | **14672.66 KB (0%)** |
| Old |Push|100|10,861.8 us|343.7500|1438.28 KB|
| **New** |	|	| **11,274.1 us (+4%)** | **343.7500 (0%)** | **1438.28 KB (0%)** |
| Old |Index|100|12,106.5 us|390.6250|1637.5 KB|
| **New** |	|	| **12,471.8 us (+3%)** | **390.6250 (0%)** | **1643.75 KB (0%)** |
| Old |Map|100|3,382.7 us|765.6250|3149.22 KB|
| **New** |	|	| **3,624.8 us (+7%)** | **691.4063 (-10%)** | **2833.59 KB (-10%)** |
| Old |Apply|100|569.2 us|188.4766|774.22 KB|
| **New** |	|	| **600.3 us (+5%)** | **188.4766 (0%)** | **774.22 KB (0%)** |
| Old |JsonStringifyParse|100|4,523.7 us|1273.4375|5225 KB|
| **New** |	|	| **4,602.2 us (+2%)** | **1273.4375 (0%)** | **5225.78 KB (0%)** |