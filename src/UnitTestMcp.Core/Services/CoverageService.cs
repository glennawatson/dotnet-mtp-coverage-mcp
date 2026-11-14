// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

using UnitTestMcp.Core.Models;
using UnitTestMcp.Core.Parsers;

namespace UnitTestMcp.Core.Services;

/// <summary>Default implementation of <see cref="ICoverageService"/>.</summary>
public sealed class CoverageService : ICoverageService
{
    /// <summary>Comparer for binary search insertion of <see cref="LineCoverage"/> by line number.</summary>
    internal static readonly IComparer<LineCoverage> LineNumberComparer =
        Comparer<LineCoverage>.Create(static (a, b) => a.LineNumber.CompareTo(b.LineNumber));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<CoverageReport> LoadReportAsync(string filePath, CancellationToken cancellationToken) =>
        CoberturaParser.ParseFileAsync(filePath, cancellationToken);

    /// <inheritdoc/>
    public async Task<CoverageReport> LoadSolutionReportAsync(string solutionPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);

        var directory = File.Exists(solutionPath) ? Path.GetDirectoryName(solutionPath)! : solutionPath;

        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Solution directory not found: {directory}");
        }

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<string>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.cobertura.xml", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (paths.Add(path))
            {
                files.Add(path);
            }
        }

        if (files.Count == 0)
        {
            return new([], [], null);
        }

        if (files.Count == 1)
        {
            var report = await CoberturaParser.ParseFileAsync(files[0], cancellationToken).ConfigureAwait(false);
            return MergeReports([report]);
        }

        var tasks = new List<Task<CoverageReport>>(files.Count);
        foreach (var file in files)
        {
            tasks.Add(CoberturaParser.ParseFileAsync(file, cancellationToken));
        }

        var reports = await Task.WhenAll(tasks).ConfigureAwait(false);

        return MergeReports(reports);
    }

    /// <inheritdoc/>
    public FileMissedCoverage GetMissedCoverageForFile(CoverageReport report, string filePath)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var normalizedPath = NormalizePath(filePath);

        var matchingClasses = new List<ClassCoverage>();
        foreach (var item in report.AllClasses)
        {
            if (NormalizePath(item.FileName).EndsWith(normalizedPath, StringComparison.OrdinalIgnoreCase))
            {
                matchingClasses.Add(item);
            }
        }

        var allLines = MergeSortedLines(matchingClasses);

        return CategorizeLines(filePath, allLines);
    }

    /// <inheritdoc/>
    public ClassCoverage? GetClassCoverage(CoverageReport report, string className)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(className);

        foreach (var item in report.AllClasses)
        {
            if (item.Name.Equals(
                className,
                StringComparison.OrdinalIgnoreCase)
                || item.ShortName.Equals(className, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public MethodCoverage? GetMethodCoverage(CoverageReport report, string className, string methodName)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(className);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        var classCoverage = GetClassCoverage(report, className);

        if (classCoverage is not null)
        {
            foreach (var method in classCoverage.Methods)
            {
                if (method.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
                {
                    return method;
                }
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public PackageCoverage? GetProjectCoverage(CoverageReport report, string projectName)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);

        foreach (var package in report.Packages)
        {
            if (package.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase))
            {
                return package;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public SolutionMissedCoverage GetSolutionMissedCoverage(CoverageReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var classesByFile = new Dictionary<string, List<ClassCoverage>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in report.AllClasses)
        {
            ref var group = ref CollectionsMarshal.GetValueRefOrAddDefault(classesByFile, item.FileName, out _);
            group ??= [];
            group.Add(item);
        }

        var fileGroups = new List<FileMissedCoverage>(classesByFile.Count);
        foreach (var (path, classes) in classesByFile)
        {
            var file = CategorizeLines(path, MergeSortedLines(classes));
            if (file.MissedLines.Count > 0 || file.PartiallyMissedBranches.Count > 0)
            {
                fileGroups.Add(file);
            }
        }

        fileGroups.Sort(static (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.FilePath, b.FilePath));

        return new(
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

    /// <summary>Categorizes a sorted list of lines into missed and partially covered in a single pass.</summary>
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

        return new(filePath, missedLines, partialBranches);
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
        foreach (var report in reports)
        {
            foreach (var package in report.Packages)
            {
                ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(packageMap, package.Name, out _);
                list ??= [with(reports.Count)];
                list.Add(package);
            }
        }

        var allPackages = new List<PackageCoverage>(packageMap.Count);

        foreach (var (packageName, packageGroup) in packageMap)
        {
            allPackages.Add(MergePackageCoverage(packageName, packageGroup));
        }

        // Collect distinct source directories
        var sourceSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var report in reports)
        {
            foreach (var source in report.SourceDirectories)
            {
                _ = sourceSet.Add(source);
            }
        }

        List<string> allSources = [.. sourceSet];

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

        return new(allPackages, allSources, latestTimestamp);
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

        var mergedLines = MergeClassLines(classGroup);

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
        return new(first.Name, first.FileName, null, null, null, mergedMethods, mergedLines);
    }

    /// <summary>Merges two <see cref="LineCoverage"/> entries for the same line number, taking the maximum hits and best branch coverage.</summary>
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
        return new(a.LineNumber, hits, status, isBranch, coveredBranches, totalBranches);
    }

    /// <summary>Determines the visit status for a merged line based on its hits and branch information.</summary>
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

        return isBranch && coveredBranches is { } cb && totalBranches is { } tb && cb < tb ? LineVisitStatus.PartiallyCovered : LineVisitStatus.Covered;
    }

    /// <summary>Normalizes a file path by converting backslashes to forward slashes.</summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized path with forward slashes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string NormalizePath(string path) =>
        path.Replace('\\', '/');

    /// <summary>Merges classes belonging to matching packages.</summary>
    /// <param name="packageName">The name of the merged package.</param>
    /// <param name="packages">The packages to merge.</param>
    /// <returns>The package with merged class coverage.</returns>
    private static PackageCoverage MergePackageCoverage(string packageName, List<PackageCoverage> packages)
    {
        var classMap = new Dictionary<(string Name, string FileName), List<ClassCoverage>>(
            StringComparer.OrdinalIgnoreCase.CreateTupleComparer());
        foreach (var package in packages)
        {
            foreach (var item in package.Classes)
            {
                ref var group = ref CollectionsMarshal.GetValueRefOrAddDefault(classMap, (item.Name, item.FileName), out _);
                group ??= [with(packages.Count)];
                group.Add(item);
            }
        }

        var classes = new List<ClassCoverage>(classMap.Count);
        foreach (var group in classMap.Values)
        {
            classes.Add(MergeClassCoverage(group));
        }

        return new(packageName, null, null, null, classes);
    }

    /// <summary>Merges matching line numbers using the best coverage from each class.</summary>
    /// <param name="classes">The class instances to combine.</param>
    /// <returns>The merged lines in line number order.</returns>
    private static List<LineCoverage> MergeClassLines(List<ClassCoverage> classes)
    {
        var lines = new List<LineCoverage>(classes[0].Lines.Count);
        foreach (var item in classes)
        {
            foreach (var line in item.Lines)
            {
                var index = lines.BinarySearch(line, LineNumberComparer);
                if (index < 0)
                {
                    lines.Insert(~index, line);
                }
                else
                {
                    lines[index] = MergeLineCoverage(lines[index], line);
                }
            }
        }

        return lines;
    }
}
