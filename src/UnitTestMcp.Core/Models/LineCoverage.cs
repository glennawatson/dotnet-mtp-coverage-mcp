// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace UnitTestMcp.Core.Models;

/// <summary>
/// Coverage data for a single source code line.
/// </summary>
/// <remarks>
/// Coverage model concepts adapted from ReportGenerator by Daniel Palme.
/// See https://github.com/danielpalme/ReportGenerator.
/// </remarks>
/// <param name="LineNumber">The 1-based line number in the source file.</param>
/// <param name="Hits">Number of times the line was executed. -1 means not coverable.</param>
/// <param name="Status">The coverage status of the line.</param>
/// <param name="IsBranch">Whether the line contains a branch point.</param>
/// <param name="CoveredBranches">Number of branches covered at this line, if applicable.</param>
/// <param name="TotalBranches">Total number of branches at this line, if applicable.</param>
public sealed record LineCoverage(
    int LineNumber,
    int Hits,
    LineVisitStatus Status,
    bool IsBranch = false,
    int? CoveredBranches = null,
    int? TotalBranches = null)
{
    /// <summary>
    /// Gets a value indicating whether all branches at this line are covered.
    /// </summary>
    public bool AllBranchesCovered =>
        !IsBranch || (CoveredBranches.HasValue && TotalBranches.HasValue && CoveredBranches.Value == TotalBranches.Value);
}
