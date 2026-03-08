// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Text;

using ModelContextProtocol.Server;

using UnitTestMcp.Core.Models;
using UnitTestMcp.Core.Services;

namespace UnitTestMcp.Server.Tools;

/// <summary>
/// MCP tools for querying code coverage data from Cobertura XML reports.
/// </summary>
[McpServerToolType]
public static class CoverageTools
{
    /// <summary>
    /// Gets missed lines and branches for a specific source file.
    /// </summary>
    /// <param name="coverageService">The coverage service.</param>
    /// <param name="coberturaFilePath">Path to the Cobertura XML coverage report.</param>
    /// <param name="sourceFilePath">Path to the source file to query.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A markdown string describing missed lines and branches.</returns>
    [McpServerTool]
    [Description("Gets missed lines and branches for a specific source file from a Cobertura XML coverage report.")]
    public static async Task<string> GetMissedCoverageForFile(
        ICoverageService coverageService,
        [Description("Path to the Cobertura XML coverage report file.")] string coberturaFilePath,
        [Description("Path to the source file to query coverage for.")] string sourceFilePath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(coverageService);

        var report = await coverageService.LoadReportAsync(coberturaFilePath, cancellationToken).ConfigureAwait(false);
        var missed = coverageService.GetMissedCoverageForFile(report, sourceFilePath);

        return FormatFileMissedCoverage(missed);
    }

    /// <summary>
    /// Gets overall coverage for a specific class.
    /// </summary>
    /// <param name="coverageService">The coverage service.</param>
    /// <param name="coberturaFilePath">Path to the Cobertura XML coverage report.</param>
    /// <param name="className">The fully qualified or short class name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A markdown string describing class coverage.</returns>
    [McpServerTool]
    [Description("Gets overall coverage summary for a specific class from a Cobertura XML coverage report.")]
    public static async Task<string> GetClassCoverage(
        ICoverageService coverageService,
        [Description("Path to the Cobertura XML coverage report file.")] string coberturaFilePath,
        [Description("Fully qualified or short class name to query.")] string className,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(coverageService);

        var report = await coverageService.LoadReportAsync(coberturaFilePath, cancellationToken).ConfigureAwait(false);
        var classCoverage = coverageService.GetClassCoverage(report, className);

        return classCoverage is null
            ? $"Class '{className}' not found in the coverage report."
            : FormatClassCoverage(classCoverage);
    }

    /// <summary>
    /// Gets overall coverage for a specific method.
    /// </summary>
    /// <param name="coverageService">The coverage service.</param>
    /// <param name="coberturaFilePath">Path to the Cobertura XML coverage report.</param>
    /// <param name="className">The class name containing the method.</param>
    /// <param name="methodName">The method name to query.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A markdown string describing method coverage.</returns>
    [McpServerTool]
    [Description("Gets overall coverage summary for a specific method from a Cobertura XML coverage report.")]
    public static async Task<string> GetMethodCoverage(
        ICoverageService coverageService,
        [Description("Path to the Cobertura XML coverage report file.")] string coberturaFilePath,
        [Description("Class name containing the method.")] string className,
        [Description("Method name to query.")] string methodName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(coverageService);

        var report = await coverageService.LoadReportAsync(coberturaFilePath, cancellationToken).ConfigureAwait(false);
        var method = coverageService.GetMethodCoverage(report, className, methodName);

        return method is null
            ? $"Method '{methodName}' in class '{className}' not found in the coverage report."
            : FormatMethodCoverage(method);
    }

    /// <summary>
    /// Gets overall coverage for a specific project/package.
    /// </summary>
    /// <param name="coverageService">The coverage service.</param>
    /// <param name="coberturaFilePath">Path to the Cobertura XML coverage report.</param>
    /// <param name="projectName">The project/package name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A markdown string describing project coverage.</returns>
    [McpServerTool]
    [Description("Gets overall coverage summary for a specific project/package from a Cobertura XML coverage report.")]
    public static async Task<string> GetProjectCoverage(
        ICoverageService coverageService,
        [Description("Path to the Cobertura XML coverage report file.")] string coberturaFilePath,
        [Description("Project or package name to query.")] string projectName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(coverageService);

        var report = await coverageService.LoadReportAsync(coberturaFilePath, cancellationToken).ConfigureAwait(false);
        var project = coverageService.GetProjectCoverage(report, projectName);

        return project is null
            ? $"Project '{projectName}' not found in the coverage report."
            : FormatProjectCoverage(project);
    }

    /// <summary>
    /// Gets overall coverage for a solution by discovering and merging all Cobertura reports.
    /// </summary>
    /// <param name="coverageService">The coverage service.</param>
    /// <param name="solutionPath">Path to the solution directory or .sln file.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A markdown string describing solution-wide coverage.</returns>
    [McpServerTool]
    [Description("Gets overall coverage summary for an entire solution by discovering and merging all Cobertura XML reports found under the solution directory.")]
    public static async Task<string> GetSolutionCoverage(
        ICoverageService coverageService,
        [Description("Path to the solution directory or .sln file.")] string solutionPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(coverageService);

        var report = await coverageService.LoadSolutionReportAsync(solutionPath, cancellationToken).ConfigureAwait(false);

        if (report.Packages.Count == 0)
        {
            return "No coverage data found. Ensure tests have been run with code coverage collection enabled.";
        }

        var projectLines = string.Join(
            Environment.NewLine,
            report.Packages.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Select(p =>
                $$"""
                ### {{p.Name}}
                - Line Coverage: {{FormatPercent(p.LineCoverageRate)}} ({{p.CoveredLineCount}}/{{p.CoverableLineCount}})
                - Branch Coverage: {{FormatPercent(p.BranchCoverageRate)}}
                - Classes: {{p.Classes.Count}}
                """));

        return $$"""
            # Solution Coverage Summary

            - **Line Coverage**: {{FormatPercent(report.LineCoverageRate)}} ({{report.CoveredLineCount}}/{{report.CoverableLineCount}} lines)
            - **Branch Coverage**: {{FormatPercent(report.BranchCoverageRate)}} ({{report.CoveredBranchCount}}/{{report.TotalBranchCount}} branches)
            - **Missed Lines**: {{report.MissedLineCount}}
            - **Missed Branches**: {{report.MissedBranchCount}}

            ## Projects

            {{projectLines}}
            """;
    }

    /// <summary>
    /// Gets a report of all missed lines and branches across an entire solution.
    /// </summary>
    /// <param name="coverageService">The coverage service.</param>
    /// <param name="solutionPath">Path to the solution directory or .sln file.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A detailed markdown report of all missed coverage.</returns>
    [McpServerTool]
    [Description("Gets a detailed report of ALL missed lines and branches across an entire solution. Discovers and merges all Cobertura XML reports.")]
    public static async Task<string> GetSolutionMissedCoverage(
        ICoverageService coverageService,
        [Description("Path to the solution directory or .sln file.")] string solutionPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(coverageService);

        var report = await coverageService.LoadSolutionReportAsync(solutionPath, cancellationToken).ConfigureAwait(false);

        if (report.Packages.Count == 0)
        {
            return "No coverage data found. Ensure tests have been run with code coverage collection enabled.";
        }

        var missed = coverageService.GetSolutionMissedCoverage(report);
        var fileReportLines = string.Join(Environment.NewLine, missed.FileReports.Select(FormatFileReport));

        return $$"""
            # Solution Missed Coverage Report

            - **Overall Line Coverage**: {{missed.LineCoveragePercent?.ToString("F2") ?? "N/A"}}% ({{missed.TotalCoveredLines}}/{{missed.TotalCoverableLines}})
            - **Overall Branch Coverage**: {{missed.BranchCoveragePercent?.ToString("F2") ?? "N/A"}}% ({{missed.TotalCoveredBranches}}/{{missed.TotalBranches}})
            - **Files with missed coverage**: {{missed.FileReports.Count}}

            {{fileReportLines}}
            """;
    }

    /// <summary>
    /// Formats a single file's missed coverage into a markdown section.
    /// </summary>
    /// <param name="fileReport">The file missed coverage data.</param>
    /// <returns>A markdown-formatted string for this file.</returns>
    private static string FormatFileReport(FileMissedCoverage fileReport)
    {
        var sb = new StringBuilder(256);
        sb.AppendLine($"## {fileReport.FilePath}");

        if (fileReport.MissedLines.Count > 0)
        {
            var ranges = CompactLineRanges(fileReport.MissedLines.Select(l => l.LineNumber).ToList());
            sb.AppendLine()
              .AppendLine($"**Missed Lines** ({fileReport.MissedLines.Count}):")
              .AppendLine($"  Lines: {ranges}");
        }

        if (fileReport.PartiallyMissedBranches.Count > 0)
        {
            sb.AppendLine()
              .AppendLine($"**Partially Covered Branches** ({fileReport.PartiallyMissedBranches.Count}):");

            foreach (var branch in fileReport.PartiallyMissedBranches)
            {
                sb.AppendLine($"  Line {branch.LineNumber}: {branch.CoveredBranches}/{branch.TotalBranches} branches covered");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats missed coverage information for a specific file into a markdown string.
    /// </summary>
    /// <param name="missed">The missed coverage data for the file.</param>
    /// <returns>A markdown-formatted string describing the missed coverage.</returns>
    private static string FormatFileMissedCoverage(FileMissedCoverage missed)
    {
        if (missed.MissedLines.Count == 0 && missed.PartiallyMissedBranches.Count == 0)
        {
            return $$"""
                # Missed Coverage: {{missed.FilePath}}

                No missed coverage found for this file.
                """;
        }

        var missedSection = missed.MissedLines.Count > 0
            ? $$"""
                ## Missed Lines ({{missed.MissedLines.Count}})
                Lines: {{CompactLineRanges(missed.MissedLines.Select(l => l.LineNumber).ToList())}}
                """
            : string.Empty;

        var branchLines = missed.PartiallyMissedBranches.Select(b =>
            $"- Line {b.LineNumber}: {b.CoveredBranches}/{b.TotalBranches} branches covered (hits: {b.Hits})");
        var branchSection = missed.PartiallyMissedBranches.Count > 0
            ? $"""
                ## Partially Covered Branches ({missed.PartiallyMissedBranches.Count})
                {string.Join(Environment.NewLine, branchLines)}
                """
            : string.Empty;

        return $$"""
            # Missed Coverage: {{missed.FilePath}}

            {{missedSection}}

            {{branchSection}}
            """;
    }

    /// <summary>
    /// Formats class coverage into a markdown string.
    /// </summary>
    /// <param name="c">The class coverage data.</param>
    /// <returns>A markdown-formatted string describing the class coverage.</returns>
    private static string FormatClassCoverage(ClassCoverage c)
    {
        var methodLines = string.Join(
            Environment.NewLine,
            c.Methods.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).Select(m =>
                $"- **{m.Name}**: Line {FormatPercent(m.LineCoverageRate)}, Branch {FormatPercent(m.BranchCoverageRate)}, CRAP {m.CrapScore?.ToString("F1") ?? "N/A"}"));

        return $$"""
            # Class Coverage: {{c.Name}}

            - **File**: {{c.FileName}}
            - **Line Coverage**: {{FormatPercent(c.LineCoverageRate)}} ({{c.CoveredLineCount}}/{{c.CoverableLineCount}} lines)
            - **Branch Coverage**: {{FormatPercent(c.BranchCoverageRate)}} ({{c.CoveredBranchCount}}/{{c.TotalBranchCount}} branches)
            - **Missed Lines**: {{c.MissedLineCount}}
            - **Missed Branches**: {{c.MissedBranchCount}}
            - **Complexity**: {{c.Complexity?.ToString("F0") ?? "N/A"}}

            ## Methods
            {{methodLines}}
            """;
    }

    /// <summary>
    /// Formats method coverage into a markdown string.
    /// </summary>
    /// <param name="m">The method coverage data.</param>
    /// <returns>A markdown-formatted string describing the method coverage.</returns>
    private static string FormatMethodCoverage(MethodCoverage m)
    {
        var missedLines = m.Lines.Where(l => l.Status is LineVisitStatus.NotCovered).Select(l => l.LineNumber).ToList();

        var missedSection = missedLines.Count > 0
            ? $$"""

                ## Missed Lines ({{missedLines.Count}})
                Lines: {{CompactLineRanges(missedLines)}}
                """
            : string.Empty;

        return $$"""
            # Method Coverage: {{m.Name}}

            - **Signature**: {{m.Signature}}
            - **Line Coverage**: {{FormatPercent(m.LineCoverageRate)}} ({{m.CoveredLineCount}}/{{m.CoverableLineCount}} lines)
            - **Branch Coverage**: {{FormatPercent(m.BranchCoverageRate)}}
            - **Cyclomatic Complexity**: {{m.CyclomaticComplexity?.ToString("F0") ?? "N/A"}}
            - **CRAP Score**: {{m.CrapScore?.ToString("F1") ?? "N/A"}}{{missedSection}}
            """;
    }

    /// <summary>
    /// Formats project/package coverage into a markdown string.
    /// </summary>
    /// <param name="p">The package coverage data.</param>
    /// <returns>A markdown-formatted string describing the project coverage.</returns>
    private static string FormatProjectCoverage(PackageCoverage p)
    {
        var classLines = string.Join(
            Environment.NewLine,
            p.Classes.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).Select(c =>
                $"- **{c.ShortName}**: Line {FormatPercent(c.LineCoverageRate)}, Branch {FormatPercent(c.BranchCoverageRate)}, Missed {c.MissedLineCount} lines"));

        return $$"""
            # Project Coverage: {{p.Name}}

            - **Line Coverage**: {{FormatPercent(p.LineCoverageRate)}} ({{p.CoveredLineCount}}/{{p.CoverableLineCount}} lines)
            - **Branch Coverage**: {{FormatPercent(p.BranchCoverageRate)}} ({{p.CoveredBranchCount}}/{{p.TotalBranchCount}} branches)
            - **Missed Lines**: {{p.MissedLineCount}}
            - **Missed Branches**: {{p.MissedBranchCount}}
            - **Classes**: {{p.Classes.Count}}

            ## Classes
            {{classLines}}
            """;
    }

    /// <summary>
    /// Formats a decimal coverage rate (0.0-1.0) as a percentage string.
    /// </summary>
    /// <param name="rate">The coverage rate, or null.</param>
    /// <returns>A formatted percentage string like "85.71%" or "N/A".</returns>
    private static string FormatPercent(decimal? rate) =>
        rate.HasValue ? $"{rate.Value * 100:F2}%" : "N/A";

    /// <summary>
    /// Compacts a list of line numbers into human-readable ranges (e.g. "10-15, 20, 25-30").
    /// </summary>
    /// <param name="lines">The sorted line numbers to compact.</param>
    /// <returns>A compact string representation of the line ranges.</returns>
    private static string CompactLineRanges(IReadOnlyList<int> lines)
    {
        if (lines.Count == 0)
        {
            return "none";
        }

        var sb = new StringBuilder(lines.Count * 4);
        var start = lines[0];
        var end = start;

        for (var i = 1; i < lines.Count; i++)
        {
            if (lines[i] == end + 1)
            {
                end = lines[i];
            }
            else
            {
                AppendRange(sb, start, end);
                start = lines[i];
                end = start;
            }
        }

        AppendRange(sb, start, end);
        return sb.ToString();

        static void AppendRange(StringBuilder builder, int rangeStart, int rangeEnd)
        {
            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(rangeStart);
            if (rangeStart != rangeEnd)
            {
                builder.Append('-').Append(rangeEnd);
            }
        }
    }
}
