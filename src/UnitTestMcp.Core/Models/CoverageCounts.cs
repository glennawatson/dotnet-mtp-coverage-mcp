// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace UnitTestMcp.Core.Models;

/// <summary>Cached line and branch totals.</summary>
/// <param name="CoveredLines">Lines executed at least once.</param>
/// <param name="CoverableLines">Lines that can be executed.</param>
/// <param name="MissedLines">Executable lines never executed.</param>
/// <param name="TotalBranches">Branches that can be taken.</param>
/// <param name="CoveredBranches">Branches taken at least once.</param>
[DebuggerDisplay("{CoveredLines}/{CoverableLines} lines; {CoveredBranches}/{TotalBranches} branches")]
internal readonly record struct CoverageCounts(int CoveredLines, int CoverableLines, int MissedLines, int TotalBranches, int CoveredBranches);
