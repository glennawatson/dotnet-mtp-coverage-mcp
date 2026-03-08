// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace UnitTestMcp.Core.Models;

/// <summary>
/// Top-level coverage report containing all packages parsed from a Cobertura XML file.
/// </summary>
/// <remarks>
/// Coverage model concepts adapted from ReportGenerator by Daniel Palme.
/// See https://github.com/danielpalme/ReportGenerator.
/// </remarks>
/// <param name="Packages">The package-level coverage entries.</param>
/// <param name="SourceDirectories">The source directories referenced by the report.</param>
/// <param name="Timestamp">The UTC timestamp when the coverage was generated, if available.</param>
public sealed record CoverageReport(
    IReadOnlyList<PackageCoverage> Packages,
    IReadOnlyList<string> SourceDirectories,
    DateTimeOffset? Timestamp)
{
    /// <summary>
    /// Pre-computed aggregate line and branch coverage counts from all packages, calculated once at construction time.
    /// </summary>
    private readonly (int CoveredLines, int CoverableLines, int MissedLines, int TotalBranches, int CoveredBranches) _counts = ComputeCounts(Packages);

    /// <summary>
    /// Gets the total covered line count across all packages.
    /// </summary>
    public int CoveredLineCount => _counts.CoveredLines;

    /// <summary>
    /// Gets the total coverable line count across all packages.
    /// </summary>
    public int CoverableLineCount => _counts.CoverableLines;

    /// <summary>
    /// Gets the total missed line count across all packages.
    /// </summary>
    public int MissedLineCount => _counts.MissedLines;

    /// <summary>
    /// Gets the overall line coverage rate (0.0 to 1.0), or null if no coverable lines exist.
    /// </summary>
    public decimal? LineCoverageRate =>
        _counts.CoverableLines > 0 ? (decimal)_counts.CoveredLines / _counts.CoverableLines : null;

    /// <summary>
    /// Gets the total branch count across all packages.
    /// </summary>
    public int TotalBranchCount => _counts.TotalBranches;

    /// <summary>
    /// Gets the covered branch count across all packages.
    /// </summary>
    public int CoveredBranchCount => _counts.CoveredBranches;

    /// <summary>
    /// Gets the missed branch count across all packages.
    /// </summary>
    public int MissedBranchCount => _counts.TotalBranches - _counts.CoveredBranches;

    /// <summary>
    /// Gets the overall branch coverage rate (0.0 to 1.0), or null if no branches exist.
    /// </summary>
    public decimal? BranchCoverageRate =>
        _counts.TotalBranches > 0 ? (decimal)_counts.CoveredBranches / _counts.TotalBranches : null;

    /// <summary>
    /// Gets all classes across all packages, materialized as a list for efficient repeated access.
    /// </summary>
    public IReadOnlyList<ClassCoverage> AllClasses { get; } = CollectAllClasses(Packages);

    /// <summary>
    /// Collects all classes from all packages into a single materialized list.
    /// </summary>
    /// <param name="packages">The package coverage entries to collect from.</param>
    /// <returns>A flat list of all class coverage entries.</returns>
    private static List<ClassCoverage> CollectAllClasses(IReadOnlyList<PackageCoverage> packages)
    {
        var totalClasses = 0;
        foreach (var p in packages)
        {
            totalClasses += p.Classes.Count;
        }

        var result = new List<ClassCoverage>(totalClasses);
        foreach (var p in packages)
        {
            result.AddRange(p.Classes);
        }

        return result;
    }

    /// <summary>
    /// Aggregates pre-computed line and branch counts from all packages in a single pass.
    /// </summary>
    /// <param name="packages">The package coverage entries to aggregate.</param>
    /// <returns>A tuple containing the aggregated coverage counts.</returns>
    private static (int CoveredLines, int CoverableLines, int MissedLines, int TotalBranches, int CoveredBranches) ComputeCounts(IReadOnlyList<PackageCoverage> packages)
    {
        var coveredLines = 0;
        var coverableLines = 0;
        var missedLines = 0;
        var totalBranches = 0;
        var coveredBranches = 0;

        foreach (var p in packages)
        {
            coveredLines += p.CoveredLineCount;
            coverableLines += p.CoverableLineCount;
            missedLines += p.MissedLineCount;
            totalBranches += p.TotalBranchCount;
            coveredBranches += p.CoveredBranchCount;
        }

        return (coveredLines, coverableLines, missedLines, totalBranches, coveredBranches);
    }
}
