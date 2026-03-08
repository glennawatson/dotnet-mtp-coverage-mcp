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
    /// Gets the total covered line count across all packages.
    /// </summary>
    public int CoveredLineCount => Packages.Sum(p => p.CoveredLineCount);

    /// <summary>
    /// Gets the total coverable line count across all packages.
    /// </summary>
    public int CoverableLineCount => Packages.Sum(p => p.CoverableLineCount);

    /// <summary>
    /// Gets the total missed line count across all packages.
    /// </summary>
    public int MissedLineCount => Packages.Sum(p => p.MissedLineCount);

    /// <summary>
    /// Gets the overall line coverage rate (0.0 to 1.0), or null if no coverable lines exist.
    /// </summary>
    public decimal? LineCoverageRate =>
        CoverableLineCount > 0 ? (decimal)CoveredLineCount / CoverableLineCount : null;

    /// <summary>
    /// Gets the total branch count across all packages.
    /// </summary>
    public int TotalBranchCount => Packages.Sum(p => p.TotalBranchCount);

    /// <summary>
    /// Gets the covered branch count across all packages.
    /// </summary>
    public int CoveredBranchCount => Packages.Sum(p => p.CoveredBranchCount);

    /// <summary>
    /// Gets the missed branch count across all packages.
    /// </summary>
    public int MissedBranchCount => Packages.Sum(p => p.MissedBranchCount);

    /// <summary>
    /// Gets the overall branch coverage rate (0.0 to 1.0), or null if no branches exist.
    /// </summary>
    public decimal? BranchCoverageRate =>
        TotalBranchCount > 0 ? (decimal)CoveredBranchCount / TotalBranchCount : null;

    /// <summary>
    /// Gets all classes across all packages.
    /// </summary>
    public IEnumerable<ClassCoverage> AllClasses => Packages.SelectMany(p => p.Classes);
}
