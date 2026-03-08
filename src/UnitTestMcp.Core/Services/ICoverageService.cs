// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using UnitTestMcp.Core.Models;

namespace UnitTestMcp.Core.Services;

/// <summary>
/// Service for querying parsed code coverage data.
/// </summary>
public interface ICoverageService
{
    /// <summary>
    /// Loads a Cobertura XML coverage report from the specified file path.
    /// </summary>
    /// <param name="filePath">The path to the Cobertura XML file.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The parsed coverage report.</returns>
    Task<CoverageReport> LoadReportAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers and merges all Cobertura XML reports found under a solution directory.
    /// </summary>
    /// <param name="solutionPath">The path to the solution directory or .sln file.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A merged coverage report.</returns>
    Task<CoverageReport> LoadSolutionReportAsync(string solutionPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the missed lines and branches for a specific source file.
    /// </summary>
    /// <param name="report">The coverage report.</param>
    /// <param name="filePath">The source file path to query.</param>
    /// <returns>The missed coverage entries for the file.</returns>
    FileMissedCoverage GetMissedCoverageForFile(CoverageReport report, string filePath);

    /// <summary>
    /// Gets overall coverage for a specific class.
    /// </summary>
    /// <param name="report">The coverage report.</param>
    /// <param name="className">The fully qualified or short class name.</param>
    /// <returns>The class coverage, or null if not found.</returns>
    ClassCoverage? GetClassCoverage(CoverageReport report, string className);

    /// <summary>
    /// Gets overall coverage for a specific method.
    /// </summary>
    /// <param name="report">The coverage report.</param>
    /// <param name="className">The class name containing the method.</param>
    /// <param name="methodName">The method name.</param>
    /// <returns>The method coverage, or null if not found.</returns>
    MethodCoverage? GetMethodCoverage(CoverageReport report, string className, string methodName);

    /// <summary>
    /// Gets overall coverage for a specific project/package.
    /// </summary>
    /// <param name="report">The coverage report.</param>
    /// <param name="projectName">The project/package name.</param>
    /// <returns>The package coverage, or null if not found.</returns>
    PackageCoverage? GetProjectCoverage(CoverageReport report, string projectName);

    /// <summary>
    /// Gets a summary report of all missed lines and branches across the entire solution.
    /// </summary>
    /// <param name="report">The coverage report.</param>
    /// <returns>A full missed coverage report.</returns>
    SolutionMissedCoverage GetSolutionMissedCoverage(CoverageReport report);
}
