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

        var coberturaFiles = Directory.EnumerateFiles(directory, "*.cobertura.xml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(directory, "coverage.cobertura.xml", SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

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

        var matchingClasses = report.AllClasses
            .Where(c => NormalizePath(c.FileName).EndsWith(normalizedPath, StringComparison.OrdinalIgnoreCase)
                        || NormalizePath(c.FileName).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var allLines = matchingClasses
            .SelectMany(c => c.Lines)
            .DistinctBy(l => l.LineNumber)
            .OrderBy(l => l.LineNumber)
            .ToList();

        var missedLines = allLines
            .Where(l => l.Status is LineVisitStatus.NotCovered)
            .ToList();

        var partialBranches = allLines
            .Where(l => l.Status is LineVisitStatus.PartiallyCovered)
            .ToList();

        return new FileMissedCoverage(filePath, missedLines, partialBranches);
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

        var fileGroups = report.AllClasses
            .GroupBy(c => c.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var allLines = g
                    .SelectMany(c => c.Lines)
                    .DistinctBy(l => l.LineNumber)
                    .OrderBy(l => l.LineNumber)
                    .ToList();

                var missedLines = allLines.Where(l => l.Status is LineVisitStatus.NotCovered).ToList();
                var partialBranches = allLines.Where(l => l.Status is LineVisitStatus.PartiallyCovered).ToList();

                return new FileMissedCoverage(g.Key, missedLines, partialBranches);
            })
            .Where(f => f.MissedLines.Count > 0 || f.PartiallyMissedBranches.Count > 0)
            .OrderBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SolutionMissedCoverage(
            report.CoveredLineCount,
            report.CoverableLineCount,
            report.CoveredBranchCount,
            report.TotalBranchCount,
            fileGroups);
    }

    /// <summary>
    /// Merges multiple coverage reports into a single combined report.
    /// </summary>
    /// <param name="reports">The reports to merge.</param>
    /// <returns>A single merged <see cref="CoverageReport"/>.</returns>
    private static CoverageReport MergeReports(IReadOnlyList<CoverageReport> reports)
    {
        var allPackages = reports
            .SelectMany(r => r.Packages)
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var mergedClasses = g
                    .SelectMany(p => p.Classes)
                    .GroupBy(c => (Name: c.Name, FileName: c.FileName), StringComparerExtensions.CreateTupleComparer(StringComparer.OrdinalIgnoreCase))
                    .Select(cg => cg.First())
                    .ToList();

                var first = g.First();
                return new PackageCoverage(first.Name, first.LineCoverageRate, first.BranchCoverageRate, first.Complexity, mergedClasses);
            })
            .ToList();

        var allSources = reports.SelectMany(r => r.SourceDirectories).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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
