// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace UnitTestMcp.Core.Models;

/// <summary>
/// Represents the coverage status of a single source line.
/// </summary>
/// <remarks>
/// Coverage model concepts adapted from ReportGenerator by Daniel Palme.
/// See https://github.com/danielpalme/ReportGenerator.
/// </remarks>
public enum LineVisitStatus
{
    /// <summary>
    /// Line is not coverable (e.g. comments, braces).
    /// </summary>
    NotCoverable = 0,

    /// <summary>
    /// Line was not executed during tests.
    /// </summary>
    NotCovered = 1,

    /// <summary>
    /// Line was executed but not all branches were taken.
    /// </summary>
    PartiallyCovered = 2,

    /// <summary>
    /// Line was fully executed with all branches covered.
    /// </summary>
    Covered = 3,
}
