// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using UnitTestMcp.Core.Models;

namespace UnitTestMcp.Core.Services;

/// <summary>
/// Missed coverage information for a specific source file.
/// </summary>
/// <param name="FilePath">The source file path.</param>
/// <param name="MissedLines">Lines that were not covered.</param>
/// <param name="PartiallyMissedBranches">Lines with partially covered branches.</param>
public sealed record FileMissedCoverage(
    string FilePath,
    IReadOnlyList<LineCoverage> MissedLines,
    IReadOnlyList<LineCoverage> PartiallyMissedBranches);
