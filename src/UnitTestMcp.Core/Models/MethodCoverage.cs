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
    public int CoveredLineCount => Lines.Count(l => l.Status is LineVisitStatus.Covered or LineVisitStatus.PartiallyCovered);

    /// <summary>
    /// Gets the count of coverable lines in this method.
    /// </summary>
    public int CoverableLineCount => Lines.Count(l => l.Status is not LineVisitStatus.NotCoverable);
}
