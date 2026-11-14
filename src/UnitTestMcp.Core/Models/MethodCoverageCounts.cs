// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace UnitTestMcp.Core.Models;

/// <summary>Cached line totals for a method.</summary>
/// <param name="CoveredLines">Lines executed at least once.</param>
/// <param name="CoverableLines">Lines that can be executed.</param>
[DebuggerDisplay("{CoveredLines}/{CoverableLines} lines")]
internal readonly record struct MethodCoverageCounts(int CoveredLines, int CoverableLines);
