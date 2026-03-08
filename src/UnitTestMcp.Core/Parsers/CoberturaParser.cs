// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using UnitTestMcp.Core.Models;

namespace UnitTestMcp.Core.Parsers;

/// <summary>
/// Parses Cobertura XML coverage reports into <see cref="CoverageReport"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// The Cobertura XML format is the standard output from the
/// Microsoft.Testing.Extensions.CodeCoverage MTP extension configured with <c>"format": "cobertura"</c>
/// in <c>testconfig.json</c>.
/// </para>
/// <para>
/// Cobertura XML parsing logic adapted from ReportGenerator by Daniel Palme.
/// See https://github.com/danielpalme/ReportGenerator.
/// Original source: src/ReportGenerator.Core/Parser/CoberturaParser.cs.
/// </para>
/// </remarks>
public static partial class CoberturaParser
{
    /// <summary>
    /// Parses a Cobertura XML file from the given path.
    /// </summary>
    /// <param name="filePath">The path to the Cobertura XML file.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A parsed <see cref="CoverageReport"/>.</returns>
    public static async Task<CoverageReport> ParseFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using var stream = File.OpenRead(filePath);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken).ConfigureAwait(false);

        return ParseDocument(document);
    }

    /// <summary>
    /// Parses a Cobertura XML document from a string.
    /// </summary>
    /// <param name="xml">The XML content.</param>
    /// <returns>A parsed <see cref="CoverageReport"/>.</returns>
    public static CoverageReport ParseString(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        var document = XDocument.Parse(xml);
        return ParseDocument(document);
    }

    /// <summary>
    /// Parses a Cobertura <see cref="XDocument"/> into a <see cref="CoverageReport"/>.
    /// </summary>
    /// <param name="document">The parsed XML document.</param>
    /// <returns>A parsed <see cref="CoverageReport"/>.</returns>
    public static CoverageReport ParseDocument(XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var root = document.Root ?? throw new InvalidOperationException("XML document has no root element.");

        var sourceDirectories = root
            .Element("sources")?
            .Elements("source")
            .Select(e => e.Value)
            .ToList() ?? [];

        var timestamp = ParseTimestamp(root.Attribute("timestamp")?.Value);

        var packages = root
            .Element("packages")?
            .Elements("package")
            .Select(ParsePackage)
            .ToList() ?? [];

        return new CoverageReport(packages, sourceDirectories, timestamp);
    }

    /// <summary>
    /// Gets the compiled regex for parsing branch coverage condition strings.
    /// </summary>
    /// <returns>A regex matching the (covered/total) pattern in condition-coverage attributes.</returns>
    [GeneratedRegex(@"\((?<covered>\d+)/(?<total>\d+)\)$")]
    private static partial Regex BranchCoverageRegex();

    /// <summary>
    /// Parses a package element into a <see cref="PackageCoverage"/>.
    /// </summary>
    /// <param name="packageElement">The XML element representing a package.</param>
    /// <returns>The parsed package coverage data.</returns>
    private static PackageCoverage ParsePackage(XElement packageElement)
    {
        var name = packageElement.Attribute("name")?.Value ?? string.Empty;
        var lineRate = ParseDecimalAttribute(packageElement, "line-rate");
        var branchRate = ParseDecimalAttribute(packageElement, "branch-rate");
        var complexity = ParseDecimalAttribute(packageElement, "complexity");

        var classes = packageElement
            .Element("classes")?
            .Elements("class")
            .Select(ParseClass)
            .ToList() ?? [];

        return new PackageCoverage(name, lineRate, branchRate, complexity, classes);
    }

    /// <summary>
    /// Parses a class element into a <see cref="ClassCoverage"/>.
    /// </summary>
    /// <param name="classElement">The XML element representing a class.</param>
    /// <returns>The parsed class coverage data.</returns>
    private static ClassCoverage ParseClass(XElement classElement)
    {
        var name = classElement.Attribute("name")?.Value ?? string.Empty;
        var fileName = classElement.Attribute("filename")?.Value ?? string.Empty;
        var lineRate = ParseDecimalAttribute(classElement, "line-rate");
        var branchRate = ParseDecimalAttribute(classElement, "branch-rate");
        var complexity = ParseDecimalAttribute(classElement, "complexity");

        var classLines = ParseLines(classElement.Element("lines"));

        var methods = classElement
            .Element("methods")?
            .Elements("method")
            .Select(m => ParseMethod(m, classLines))
            .ToList() ?? [];

        return new ClassCoverage(name, fileName, lineRate, branchRate, complexity, methods, classLines);
    }

    /// <summary>
    /// Parses a method element into a <see cref="MethodCoverage"/>.
    /// </summary>
    /// <param name="methodElement">The XML element representing a method.</param>
    /// <param name="classLines">The class-level lines to fall back on if the method has none.</param>
    /// <returns>The parsed method coverage data.</returns>
    private static MethodCoverage ParseMethod(XElement methodElement, IReadOnlyList<LineCoverage> classLines)
    {
        var name = methodElement.Attribute("name")?.Value ?? string.Empty;
        var signature = methodElement.Attribute("signature")?.Value ?? string.Empty;
        var lineRate = ParseDecimalAttribute(methodElement, "line-rate");
        var branchRate = ParseDecimalAttribute(methodElement, "branch-rate");
        var complexity = ParseDecimalAttribute(methodElement, "complexity");

        var methodLines = ParseLines(methodElement.Element("lines"));

        // If method has no lines of its own, try to correlate from class lines
        if (methodLines.Count == 0 && classLines.Count > 0)
        {
            methodLines = classLines.ToList();
        }

        return new MethodCoverage(name, signature, lineRate, branchRate, complexity, methodLines);
    }

    /// <summary>
    /// Parses line elements from a lines container element.
    /// </summary>
    /// <param name="linesElement">The XML element containing line children, or null.</param>
    /// <returns>A list of parsed line coverage entries, ordered by line number.</returns>
    private static List<LineCoverage> ParseLines(XElement? linesElement)
    {
        if (linesElement is null)
        {
            return [];
        }

        return linesElement
            .Elements("line")
            .Select(ParseLine)
            .OrderBy(l => l.LineNumber)
            .ToList();
    }

    /// <summary>
    /// Parses a single line element into a <see cref="LineCoverage"/>.
    /// </summary>
    /// <param name="lineElement">The XML element representing a single line.</param>
    /// <returns>The parsed line coverage data.</returns>
    private static LineCoverage ParseLine(XElement lineElement)
    {
        var number = int.Parse(lineElement.Attribute("number")?.Value ?? "0", CultureInfo.InvariantCulture);
        var hits = int.Parse(lineElement.Attribute("hits")?.Value ?? "0", CultureInfo.InvariantCulture);
        var isBranch = string.Equals(lineElement.Attribute("branch")?.Value, "true", StringComparison.OrdinalIgnoreCase);

        int? coveredBranches = null;
        int? totalBranches = null;

        if (isBranch)
        {
            var conditionCoverage = lineElement.Attribute("condition-coverage")?.Value;
            if (conditionCoverage is not null)
            {
                var match = BranchCoverageRegex().Match(conditionCoverage);
                if (match.Success)
                {
                    coveredBranches = int.Parse(match.Groups["covered"].Value, CultureInfo.InvariantCulture);
                    totalBranches = int.Parse(match.Groups["total"].Value, CultureInfo.InvariantCulture);
                }
            }
        }

        var status = DetermineLineStatus(hits, isBranch, coveredBranches, totalBranches);

        return new LineCoverage(number, hits, status, isBranch, coveredBranches, totalBranches);
    }

    /// <summary>
    /// Determines the visit status for a line based on its hits and branch information.
    /// </summary>
    /// <param name="hits">The number of times the line was executed.</param>
    /// <param name="isBranch">Whether the line contains a branch point.</param>
    /// <param name="coveredBranches">The number of branches covered, if applicable.</param>
    /// <param name="totalBranches">The total number of branches, if applicable.</param>
    /// <returns>The determined <see cref="LineVisitStatus"/>.</returns>
    private static LineVisitStatus DetermineLineStatus(int hits, bool isBranch, int? coveredBranches, int? totalBranches)
    {
        if (hits <= 0)
        {
            return LineVisitStatus.NotCovered;
        }

        if (isBranch && coveredBranches.HasValue && totalBranches.HasValue && coveredBranches.Value < totalBranches.Value)
        {
            return LineVisitStatus.PartiallyCovered;
        }

        return LineVisitStatus.Covered;
    }

    /// <summary>
    /// Parses a decimal attribute value from a Cobertura XML element.
    /// </summary>
    /// <param name="element">The XML element containing the attribute.</param>
    /// <param name="attributeName">The name of the attribute to parse.</param>
    /// <returns>The parsed decimal value, or null if the attribute is missing, NaN, or unparseable.</returns>
    private static decimal? ParseDecimalAttribute(XElement element, string attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        if (value is null or "NaN")
        {
            return null;
        }

        // Handle locale-independent parsing (Cobertura may use comma or period)
        value = value.Replace(',', '.');

        return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? Math.Round(result, 4, MidpointRounding.AwayFromZero)
            : null;
    }

    /// <summary>
    /// Parses a Unix epoch timestamp from the Cobertura root element.
    /// </summary>
    /// <param name="value">The timestamp attribute value, or null.</param>
    /// <returns>The parsed <see cref="DateTimeOffset"/>, or null if the value is missing or unparseable.</returns>
    private static DateTimeOffset? ParseTimestamp(string? value)
    {
        if (value is null)
        {
            return null;
        }

        // Cobertura timestamps are Unix epoch in seconds
        return long.TryParse(value, CultureInfo.InvariantCulture, out var epoch)
            ? DateTimeOffset.FromUnixTimeSeconds(epoch)
            : null;
    }
}
