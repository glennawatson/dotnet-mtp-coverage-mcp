// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace UnitTestMcp.Core.Services;

/// <summary>
/// Missed coverage summary for an entire solution.
/// </summary>
/// <param name="TotalCoveredLines">Total lines covered.</param>
/// <param name="TotalCoverableLines">Total coverable lines.</param>
/// <param name="TotalCoveredBranches">Total branches covered.</param>
/// <param name="TotalBranches">Total branches.</param>
/// <param name="FileReports">Per-file missed coverage reports.</param>
public sealed record SolutionMissedCoverage(
    int TotalCoveredLines,
    int TotalCoverableLines,
    int TotalCoveredBranches,
    int TotalBranches,
    IReadOnlyList<FileMissedCoverage> FileReports)
{
    /// <summary>
    /// Gets the overall line coverage percentage (0-100).
    /// </summary>
    public decimal? LineCoveragePercent =>
        TotalCoverableLines > 0 ? Math.Round((decimal)TotalCoveredLines / TotalCoverableLines * 100, 2) : null;

    /// <summary>
    /// Gets the overall branch coverage percentage (0-100).
    /// </summary>
    public decimal? BranchCoveragePercent =>
        TotalBranches > 0 ? Math.Round((decimal)TotalCoveredBranches / TotalBranches * 100, 2) : null;
}
