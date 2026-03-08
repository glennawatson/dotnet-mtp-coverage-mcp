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
    /// Gets the total covered line count across all classes.
    /// </summary>
    public int CoveredLineCount => Classes.Sum(c => c.CoveredLineCount);

    /// <summary>
    /// Gets the total coverable line count across all classes.
    /// </summary>
    public int CoverableLineCount => Classes.Sum(c => c.CoverableLineCount);

    /// <summary>
    /// Gets the total missed line count across all classes.
    /// </summary>
    public int MissedLineCount => Classes.Sum(c => c.MissedLineCount);

    /// <summary>
    /// Gets the total branch count across all classes.
    /// </summary>
    public int TotalBranchCount => Classes.Sum(c => c.TotalBranchCount);

    /// <summary>
    /// Gets the covered branch count across all classes.
    /// </summary>
    public int CoveredBranchCount => Classes.Sum(c => c.CoveredBranchCount);

    /// <summary>
    /// Gets the missed branch count across all classes.
    /// </summary>
    public int MissedBranchCount => Classes.Sum(c => c.MissedBranchCount);
}
