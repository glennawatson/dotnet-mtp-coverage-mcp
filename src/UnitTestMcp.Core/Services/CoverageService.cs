// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using UnitTestMcp.Core.Models;
using UnitTestMcp.Core.Parsers;

namespace UnitTestMcp.Core.Services;

/// <summary>
/// Default implementation of <see cref="ICoverageService"/>.
/// </summary>
public sealed class CoverageService : ICoverageService
{
    /// <summary>
    /// Comparer for binary search insertion of <see cref="LineCoverage"/> by line number.
    /// </summary>
    internal static readonly IComparer<LineCoverage> LineNumberComparer =
        Comparer<LineCoverage>.Create((a, b) => a.LineNumber.CompareTo(b.LineNumber));

    /// <inheritdoc/>
    public Task<CoverageReport> LoadReportAsync(string filePath, CancellationToken cancellationToken = default) =>
        CoberturaParser.ParseFileAsync(filePath, cancellationToken);

    /// <inheritdoc/>
    public async Task<CoverageReport> LoadSolutionReportAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);

        var directory = File.Exists(solutionPath) ? Path.GetDirectoryName(solutionPath)! : solutionPath;

        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Solution directory not found: {directory}");
        }

        List<string> coberturaFiles =
        [
            .. Directory.EnumerateFiles(directory, "*.cobertura.xml", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(directory, "coverage.cobertura.xml", SearchOption.AllDirectories))
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];

        if (coberturaFiles.Count == 0)
        {
            return new CoverageReport([], [], null);
        }

        var reports = await Task.WhenAll(
            coberturaFiles.Select(f => CoberturaParser.ParseFileAsync(f, cancellationToken))).ConfigureAwait(false);

        return MergeReports(reports);
    }

    /// <inheritdoc/>
    public FileMissedCoverage GetMissedCoverageForFile(CoverageReport report, string filePath)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var normalizedPath = NormalizePath(filePath);

        List<ClassCoverage> matchingClasses =
        [
            .. report.AllClasses
                .Where(c => NormalizePath(c.FileName).EndsWith(normalizedPath, StringComparison.OrdinalIgnoreCase)),
        ];

        var allLines = MergeSortedLines(matchingClasses);

        return CategorizeLines(filePath, allLines);
    }

    /// <inheritdoc/>
    public ClassCoverage? GetClassCoverage(CoverageReport report, string className)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(className);

        return report.AllClasses.FirstOrDefault(c =>
            c.Name.Equals(className, StringComparison.OrdinalIgnoreCase) ||
            c.ShortName.Equals(className, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public MethodCoverage? GetMethodCoverage(CoverageReport report, string className, string methodName)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(className);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        var classCoverage = GetClassCoverage(report, className);

        return classCoverage?.Methods.FirstOrDefault(m =>
            m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public PackageCoverage? GetProjectCoverage(CoverageReport report, string projectName)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);

        return report.Packages.FirstOrDefault(p =>
            p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public SolutionMissedCoverage GetSolutionMissedCoverage(CoverageReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<FileMissedCoverage> fileGroups =
        [
            .. report.AllClasses
                .GroupBy(c => c.FileName, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var allLines = MergeSortedLines(g);
                    return CategorizeLines(g.Key, allLines);
                })
                .Where(f => f.MissedLines.Count > 0 || f.PartiallyMissedBranches.Count > 0)
                .OrderBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase),
        ];

        return new SolutionMissedCoverage(
            report.CoveredLineCount,
            report.CoverableLineCount,
            report.CoveredBranchCount,
            report.TotalBranchCount,
            fileGroups);
    }

    /// <summary>
    /// Merges line coverage entries from multiple classes into a single sorted, deduplicated list.
    /// Uses binary search insertion to maintain sort order, avoiding a post-sort pass.
    /// Each class's lines are already sorted by line number from parsing.
    /// </summary>
    /// <param name="classes">The classes whose lines should be merged.</param>
    /// <returns>A sorted, deduplicated list of line coverage entries.</returns>
    internal static List<LineCoverage> MergeSortedLines(IEnumerable<ClassCoverage> classes)
    {
        var result = new List<LineCoverage>();

        foreach (var cls in classes)
        {
            foreach (var line in cls.Lines)
            {
                var index = result.BinarySearch(line, LineNumberComparer);
                if (index < 0)
                {
                    result.Insert(~index, line);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Categorizes a sorted list of lines into missed and partially covered in a single pass.
    /// </summary>
    /// <param name="filePath">The source file path for the result.</param>
    /// <param name="sortedLines">The sorted, deduplicated line coverage entries.</param>
    /// <returns>A <see cref="FileMissedCoverage"/> with categorized lines.</returns>
    internal static FileMissedCoverage CategorizeLines(string filePath, List<LineCoverage> sortedLines)
    {
        var missedLines = new List<LineCoverage>(sortedLines.Count);
        var partialBranches = new List<LineCoverage>(sortedLines.Count);

        foreach (var line in sortedLines)
        {
            if (line.Status is LineVisitStatus.NotCovered)
            {
                missedLines.Add(line);
            }
            else if (line.Status is LineVisitStatus.PartiallyCovered)
            {
                partialBranches.Add(line);
            }
        }

        return new FileMissedCoverage(filePath, missedLines, partialBranches);
    }

    /// <summary>
    /// Merges multiple coverage reports into a single combined report.
    /// When the same class appears in multiple reports, line coverage data is merged
    /// by taking the maximum hits and best branch coverage for each line.
    /// </summary>
    /// <param name="reports">The reports to merge.</param>
    /// <returns>A single merged <see cref="CoverageReport"/>.</returns>
    internal static CoverageReport MergeReports(IReadOnlyList<CoverageReport> reports)
    {
        // Group packages by name across all reports
        var packageMap = new Dictionary<string, List<PackageCoverage>>(StringComparer.OrdinalIgnoreCase);
        var totalPackageCount = 0;
        foreach (var report in reports)
        {
            foreach (var package in report.Packages)
            {
                if (!packageMap.TryGetValue(package.Name, out var list))
                {
                    list = new List<PackageCoverage>(reports.Count);
                    packageMap[package.Name] = list;
                }

                list.Add(package);
                totalPackageCount++;
            }
        }

        var allPackages = new List<PackageCoverage>(packageMap.Count);
        var classComparer = StringComparerExtensions.CreateTupleComparer(StringComparer.OrdinalIgnoreCase);

        foreach (var (packageName, packageGroup) in packageMap)
        {
            // Group classes by (Name, FileName) across all package instances
            var classMap = new Dictionary<(string Name, string FileName), List<ClassCoverage>>(classComparer);
            var classCount = 0;
            foreach (var pkg in packageGroup)
            {
                foreach (var cls in pkg.Classes)
                {
                    var key = (cls.Name, cls.FileName);
                    if (!classMap.TryGetValue(key, out var classList))
                    {
                        classList = new List<ClassCoverage>(packageGroup.Count);
                        classMap[key] = classList;
                    }

                    classList.Add(cls);
                    classCount++;
                }
            }

            // Merge each class group
            var mergedClasses = new List<ClassCoverage>(classMap.Count);
            foreach (var (_, classGroup) in classMap)
            {
                mergedClasses.Add(MergeClassCoverage(classGroup));
            }

            // Rates are recomputed from merged line data by PackageCoverage.ComputeCounts
            allPackages.Add(new PackageCoverage(packageName, null, null, null, mergedClasses));
        }

        // Collect distinct source directories
        var sourceSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var report in reports)
        {
            foreach (var source in report.SourceDirectories)
            {
                sourceSet.Add(source);
            }
        }

        var allSources = new List<string>(sourceSet);

        // Find latest timestamp
        DateTimeOffset? latestTimestamp = null;
        foreach (var report in reports)
        {
            if (report.Timestamp.HasValue
                && (!latestTimestamp.HasValue || report.Timestamp.Value > latestTimestamp.Value))
            {
                latestTimestamp = report.Timestamp.Value;
            }
        }

        return new CoverageReport(allPackages, allSources, latestTimestamp);
    }

    /// <summary>
    /// Merges multiple <see cref="ClassCoverage"/> instances for the same class into one.
    /// Line coverage is merged by taking the maximum hits and best branch coverage for each line number.
    /// Uses binary search insertion to maintain sorted line order.
    /// </summary>
    /// <param name="classGroup">The class coverage instances to merge (all same Name/FileName).</param>
    /// <returns>A single merged <see cref="ClassCoverage"/>.</returns>
    internal static ClassCoverage MergeClassCoverage(List<ClassCoverage> classGroup)
    {
        if (classGroup.Count == 1)
        {
            return classGroup[0];
        }

        var first = classGroup[0];

        // Count total lines across all instances for pre-allocation
        var totalLines = 0;
        foreach (var cls in classGroup)
        {
            totalLines += cls.Lines.Count;
        }

        // Merge lines: binary search sorted list, merge hits on collision
        var mergedLines = new List<LineCoverage>(first.Lines.Count);
        foreach (var cls in classGroup)
        {
            foreach (var line in cls.Lines)
            {
                var index = mergedLines.BinarySearch(line, LineNumberComparer);
                if (index < 0)
                {
                    mergedLines.Insert(~index, line);
                }
                else
                {
                    // Same line number exists — merge by taking best coverage
                    mergedLines[index] = MergeLineCoverage(mergedLines[index], line);
                }
            }
        }

        // Merge methods: keep first occurrence by (Name, Signature)
        var totalMethods = 0;
        foreach (var cls in classGroup)
        {
            totalMethods += cls.Methods.Count;
        }

        var seenMethods = new HashSet<(string, string)>(totalMethods);
        var mergedMethods = new List<MethodCoverage>(first.Methods.Count);
        foreach (var cls in classGroup)
        {
            foreach (var method in cls.Methods)
            {
                if (seenMethods.Add((method.Name, method.Signature)))
                {
                    mergedMethods.Add(method);
                }
            }
        }

        // Rates are recomputed from merged line data by ClassCoverage.ComputeCounts
        return new ClassCoverage(first.Name, first.FileName, null, null, null, mergedMethods, mergedLines);
    }

    /// <summary>
    /// Merges two <see cref="LineCoverage"/> entries for the same line number,
    /// taking the maximum hits and best branch coverage.
    /// </summary>
    /// <param name="a">The first line coverage entry.</param>
    /// <param name="b">The second line coverage entry.</param>
    /// <returns>A merged line coverage entry with the best coverage data.</returns>
    internal static LineCoverage MergeLineCoverage(LineCoverage a, LineCoverage b)
    {
        var hits = Math.Max(a.Hits, b.Hits);
        var isBranch = a.IsBranch || b.IsBranch;

        int? coveredBranches = null;
        int? totalBranches = null;

        if (isBranch)
        {
            coveredBranches = Math.Max(a.CoveredBranches ?? 0, b.CoveredBranches ?? 0);
            totalBranches = a.TotalBranches ?? b.TotalBranches;
        }

        var status = DetermineMergedLineStatus(hits, isBranch, coveredBranches, totalBranches);
        return new LineCoverage(a.LineNumber, hits, status, isBranch, coveredBranches, totalBranches);
    }

    /// <summary>
    /// Determines the visit status for a merged line based on its hits and branch information.
    /// </summary>
    /// <param name="hits">The number of times the line was executed.</param>
    /// <param name="isBranch">Whether the line contains a branch point.</param>
    /// <param name="coveredBranches">The number of branches covered, if applicable.</param>
    /// <param name="totalBranches">The total number of branches, if applicable.</param>
    /// <returns>The determined <see cref="LineVisitStatus"/>.</returns>
    internal static LineVisitStatus DetermineMergedLineStatus(int hits, bool isBranch, int? coveredBranches, int? totalBranches)
    {
        if (hits <= 0)
        {
            return LineVisitStatus.NotCovered;
        }

        if (isBranch && coveredBranches is { } cb && totalBranches is { } tb && cb < tb)
        {
            return LineVisitStatus.PartiallyCovered;
        }

        return LineVisitStatus.Covered;
    }

    /// <summary>
    /// Normalizes a file path by converting backslashes to forward slashes.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized path with forward slashes.</returns>
    internal static string NormalizePath(string path) =>
        path.Replace('\\', '/');
}
