// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace UnitTestMcp.Core.Models;

/// <summary>
/// Coverage information for a single method.
/// </summary>
/// <remarks>
/// Coverage model concepts adapted from ReportGenerator by Daniel Palme.
/// See https://github.com/danielpalme/ReportGenerator.
/// </remarks>
/// <param name="Name">The method name.</param>
/// <param name="Signature">The method signature.</param>
/// <param name="LineCoverageRate">Line coverage as a decimal (0.0 to 1.0), or null if unavailable.</param>
/// <param name="BranchCoverageRate">Branch coverage as a decimal (0.0 to 1.0), or null if unavailable.</param>
/// <param name="CyclomaticComplexity">The cyclomatic complexity, if available.</param>
/// <param name="Lines">The line coverage entries for this method.</param>
public sealed record MethodCoverage(
    string Name,
    string Signature,
    decimal? LineCoverageRate,
    decimal? BranchCoverageRate,
    decimal? CyclomaticComplexity,
    IReadOnlyList<LineCoverage> Lines)
{
    /// <summary>
    /// Pre-computed line coverage counts, calculated once at construction time.
    /// </summary>
    private readonly (int CoveredLines, int CoverableLines) _counts = ComputeCounts(Lines);

    /// <summary>
    /// Gets the CRAP score (Change Risk Anti-Patterns).
    /// Formula: CC^2 * U^3 + CC where CC = cyclomatic complexity, U = uncovered percentage.
    /// </summary>
    /// <remarks>
    /// Based on the CRAP metric from the Google Testing Blog, as implemented in ReportGenerator.
    /// </remarks>
    public decimal? CrapScore
    {
        get
        {
            if (CyclomaticComplexity is not { } cc || LineCoverageRate is not { } coverage)
            {
                return null;
            }

            var uncovered = 1m - coverage;
            return (cc * cc * uncovered * uncovered * uncovered) + cc;
        }
    }

    /// <summary>
    /// Gets the count of covered lines in this method.
    /// </summary>
    public int CoveredLineCount => _counts.CoveredLines;

    /// <summary>
    /// Gets the count of coverable lines in this method.
    /// </summary>
    public int CoverableLineCount => _counts.CoverableLines;

    /// <summary>
    /// Computes covered and coverable line counts in a single pass over the lines collection.
    /// </summary>
    /// <param name="lines">The line coverage entries to analyze.</param>
    /// <returns>A tuple containing the pre-computed line coverage counts.</returns>
    private static (int CoveredLines, int CoverableLines) ComputeCounts(IReadOnlyList<LineCoverage> lines)
    {
        var coveredLines = 0;
        var coverableLines = 0;

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
        }

        return (coveredLines, coverableLines);
    }
}
