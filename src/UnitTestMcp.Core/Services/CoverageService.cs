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
    private static readonly IComparer<LineCoverage> LineNumberComparer =
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
    private static List<LineCoverage> MergeSortedLines(IEnumerable<ClassCoverage> classes)
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
    private static FileMissedCoverage CategorizeLines(string filePath, List<LineCoverage> sortedLines)
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
    /// </summary>
    /// <param name="reports">The reports to merge.</param>
    /// <returns>A single merged <see cref="CoverageReport"/>.</returns>
    private static CoverageReport MergeReports(IReadOnlyList<CoverageReport> reports)
    {
        List<PackageCoverage> allPackages =
        [
            .. reports
                .SelectMany(r => r.Packages)
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    List<ClassCoverage> mergedClasses =
                    [
                        .. g.SelectMany(p => p.Classes)
                            .GroupBy(
                                c => (Name: c.Name, FileName: c.FileName),
                                StringComparerExtensions.CreateTupleComparer(StringComparer.OrdinalIgnoreCase))
                            .Select(cg => cg.First()),
                    ];

                    var first = g.First();
                    return new PackageCoverage(first.Name, first.LineCoverageRate, first.BranchCoverageRate, first.Complexity, mergedClasses);
                }),
        ];

        List<string> allSources = [.. reports.SelectMany(r => r.SourceDirectories).Distinct(StringComparer.OrdinalIgnoreCase)];
        var latestTimestamp = reports.Where(r => r.Timestamp.HasValue).Select(r => r.Timestamp!.Value).DefaultIfEmpty().Max();

        return new CoverageReport(allPackages, allSources, latestTimestamp == default ? null : latestTimestamp);
    }

    /// <summary>
    /// Normalizes a file path by converting backslashes to forward slashes.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized path with forward slashes.</returns>
    private static string NormalizePath(string path) =>
        path.Replace('\\', '/');
}
