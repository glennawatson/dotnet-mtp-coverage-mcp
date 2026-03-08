// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace UnitTestMcp.Core.Models;

/// <summary>
/// Coverage information for a single class.
/// </summary>
/// <remarks>
/// Coverage model concepts adapted from ReportGenerator by Daniel Palme.
/// See https://github.com/danielpalme/ReportGenerator.
/// </remarks>
/// <param name="Name">The fully qualified class name.</param>
/// <param name="FileName">The source file path for this class.</param>
/// <param name="LineCoverageRate">Line coverage as a decimal (0.0 to 1.0), or null if unavailable.</param>
/// <param name="BranchCoverageRate">Branch coverage as a decimal (0.0 to 1.0), or null if unavailable.</param>
/// <param name="Complexity">The total cyclomatic complexity, if available.</param>
/// <param name="Methods">The method coverage entries for this class.</param>
/// <param name="Lines">All line coverage entries for this class.</param>
public sealed record ClassCoverage(
    string Name,
    string FileName,
    decimal? LineCoverageRate,
    decimal? BranchCoverageRate,
    decimal? Complexity,
    IReadOnlyList<MethodCoverage> Methods,
    IReadOnlyList<LineCoverage> Lines)
{
    /// <summary>
    /// Gets the short class name without namespace.
    /// </summary>
    public string ShortName => Name.Contains('.') ? Name[(Name.LastIndexOf('.') + 1)..] : Name;

    /// <summary>
    /// Gets the count of covered lines in this class.
    /// </summary>
    public int CoveredLineCount => Lines.Count(l => l.Status is LineVisitStatus.Covered or LineVisitStatus.PartiallyCovered);

    /// <summary>
    /// Gets the count of coverable lines in this class.
    /// </summary>
    public int CoverableLineCount => Lines.Count(l => l.Status is not LineVisitStatus.NotCoverable);

    /// <summary>
    /// Gets the count of missed (not covered) lines in this class.
    /// </summary>
    public int MissedLineCount => Lines.Count(l => l.Status is LineVisitStatus.NotCovered);

    /// <summary>
    /// Gets the total branch count across all lines.
    /// </summary>
    public int TotalBranchCount => Lines.Where(l => l.TotalBranches.HasValue).Sum(l => l.TotalBranches!.Value);

    /// <summary>
    /// Gets the covered branch count across all lines.
    /// </summary>
    public int CoveredBranchCount => Lines.Where(l => l.CoveredBranches.HasValue).Sum(l => l.CoveredBranches!.Value);

    /// <summary>
    /// Gets the missed branch count across all lines.
    /// </summary>
    public int MissedBranchCount => TotalBranchCount - CoveredBranchCount;
}
