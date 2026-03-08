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
    /// Pre-computed line and branch coverage counts, calculated once at construction time.
    /// </summary>
    private readonly (int CoveredLines, int CoverableLines, int MissedLines, int TotalBranches, int CoveredBranches) _counts = ComputeCounts(Lines);

    /// <summary>
    /// Gets the short class name without namespace.
    /// </summary>
    public string ShortName { get; } = Name.LastIndexOf('.') is var idx and >= 0 ? Name[(idx + 1)..] : Name;

    /// <summary>
    /// Gets the count of covered lines in this class.
    /// </summary>
    public int CoveredLineCount => _counts.CoveredLines;

    /// <summary>
    /// Gets the count of coverable lines in this class.
    /// </summary>
    public int CoverableLineCount => _counts.CoverableLines;

    /// <summary>
    /// Gets the count of missed (not covered) lines in this class.
    /// </summary>
    public int MissedLineCount => _counts.MissedLines;

    /// <summary>
    /// Gets the total branch count across all lines.
    /// </summary>
    public int TotalBranchCount => _counts.TotalBranches;

    /// <summary>
    /// Gets the covered branch count across all lines.
    /// </summary>
    public int CoveredBranchCount => _counts.CoveredBranches;

    /// <summary>
    /// Gets the missed branch count across all lines.
    /// </summary>
    public int MissedBranchCount => _counts.TotalBranches - _counts.CoveredBranches;

    /// <summary>
    /// Computes all line and branch coverage counts in a single pass over the lines collection.
    /// </summary>
    /// <param name="lines">The line coverage entries to analyze.</param>
    /// <returns>A tuple containing the pre-computed coverage counts.</returns>
    private static (int CoveredLines, int CoverableLines, int MissedLines, int TotalBranches, int CoveredBranches) ComputeCounts(IReadOnlyList<LineCoverage> lines)
    {
        var coveredLines = 0;
        var coverableLines = 0;
        var missedLines = 0;
        var totalBranches = 0;
        var coveredBranches = 0;

        foreach (var line in lines)
        {
            if (line.Status is not LineVisitStatus.NotCoverable)
            {
                coverableLines++;
            }

            if (line.Status is LineVisitStatus.Covered or LineVisitStatus.PartiallyCovered)
            {
                coveredLines++;
            }
            else if (line.Status is LineVisitStatus.NotCovered)
            {
                missedLines++;
            }

            if (line.TotalBranches is { } total)
            {
                totalBranches += total;
            }

            if (line.CoveredBranches is { } covered)
            {
                coveredBranches += covered;
            }
        }

        return (coveredLines, coverableLines, missedLines, totalBranches, coveredBranches);
    }
}
