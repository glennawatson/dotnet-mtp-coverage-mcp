// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace UnitTestMcp.Core.Models;

/// <summary>
/// Coverage information for a package (assembly/project).
/// </summary>
/// <remarks>
/// Coverage model concepts adapted from ReportGenerator by Daniel Palme.
/// See https://github.com/danielpalme/ReportGenerator.
/// </remarks>
/// <param name="Name">The package/assembly name.</param>
/// <param name="LineCoverageRate">Line coverage as a decimal (0.0 to 1.0), or null if unavailable.</param>
/// <param name="BranchCoverageRate">Branch coverage as a decimal (0.0 to 1.0), or null if unavailable.</param>
/// <param name="Complexity">The total cyclomatic complexity, if available.</param>
/// <param name="Classes">The class coverage entries for this package.</param>
public sealed record PackageCoverage(
    string Name,
    decimal? LineCoverageRate,
    decimal? BranchCoverageRate,
    decimal? Complexity,
    IReadOnlyList<ClassCoverage> Classes)
{
    /// <summary>
    /// Pre-computed aggregate line and branch coverage counts from all classes, calculated once at construction time.
    /// </summary>
    private readonly (int CoveredLines, int CoverableLines, int MissedLines, int TotalBranches, int CoveredBranches) _counts = ComputeCounts(Classes);

    /// <summary>
    /// Gets the total covered line count across all classes.
    /// </summary>
    public int CoveredLineCount => _counts.CoveredLines;

    /// <summary>
    /// Gets the total coverable line count across all classes.
    /// </summary>
    public int CoverableLineCount => _counts.CoverableLines;

    /// <summary>
    /// Gets the total missed line count across all classes.
    /// </summary>
    public int MissedLineCount => _counts.MissedLines;

    /// <summary>
    /// Gets the total branch count across all classes.
    /// </summary>
    public int TotalBranchCount => _counts.TotalBranches;

    /// <summary>
    /// Gets the covered branch count across all classes.
    /// </summary>
    public int CoveredBranchCount => _counts.CoveredBranches;

    /// <summary>
    /// Gets the missed branch count across all classes.
    /// </summary>
    public int MissedBranchCount => _counts.TotalBranches - _counts.CoveredBranches;

    /// <summary>
    /// Aggregates pre-computed line and branch counts from all classes in a single pass.
    /// </summary>
    /// <param name="classes">The class coverage entries to aggregate.</param>
    /// <returns>A tuple containing the aggregated coverage counts.</returns>
    private static (int CoveredLines, int CoverableLines, int MissedLines, int TotalBranches, int CoveredBranches) ComputeCounts(IReadOnlyList<ClassCoverage> classes)
    {
        var coveredLines = 0;
        var coverableLines = 0;
        var missedLines = 0;
        var totalBranches = 0;
        var coveredBranches = 0;

        foreach (var c in classes)
        {
            coveredLines += c.CoveredLineCount;
            coverableLines += c.CoverableLineCount;
            missedLines += c.MissedLineCount;
            totalBranches += c.TotalBranchCount;
            coveredBranches += c.CoveredBranchCount;
        }

        return (coveredLines, coverableLines, missedLines, totalBranches, coveredBranches);
    }
}
