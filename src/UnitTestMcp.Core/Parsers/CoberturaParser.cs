// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using UnitTestMcp.Core.Models;

namespace UnitTestMcp.Core.Parsers;

/// <summary>Parses Cobertura XML coverage reports into <see cref="CoverageReport"/> instances.</summary>
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
    /// <summary>Comparer for binary search insertion of <see cref="LineCoverage"/> by line number.</summary>
    internal static readonly IComparer<LineCoverage> LineNumberComparer =
        Comparer<LineCoverage>.Create(static (a, b) => a.LineNumber.CompareTo(b.LineNumber));

    /// <summary>XML attribute containing the line coverage rate.</summary>
    private const string LineRateAttribute = "line-rate";

    /// <summary>XML attribute containing the branch coverage rate.</summary>
    private const string BranchRateAttribute = "branch-rate";

    /// <summary>XML attribute containing cyclomatic complexity.</summary>
    private const string ComplexityAttribute = "complexity";

    /// <summary>Precision used for coverage rates.</summary>
    private const int RateDecimalPlaces = 4;

    /// <summary>Parses a Cobertura XML file from the given path.</summary>
    /// <param name="filePath">The path to the Cobertura XML file.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A parsed <see cref="CoverageReport"/>.</returns>
    public static async Task<CoverageReport> ParseFileAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await using var stream = File.OpenRead(filePath);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken).ConfigureAwait(false);

        return ParseDocument(document);
    }

    /// <summary>Parses a Cobertura XML document from a string.</summary>
    /// <param name="xml">The XML content.</param>
    /// <returns>A parsed <see cref="CoverageReport"/>.</returns>
    public static CoverageReport ParseString(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        var document = XDocument.Parse(xml);
        return ParseDocument(document);
    }

    /// <summary>Parses a Cobertura <see cref="XDocument"/> into a <see cref="CoverageReport"/>.</summary>
    /// <param name="document">The parsed XML document.</param>
    /// <returns>A parsed <see cref="CoverageReport"/>.</returns>
    /// <exception cref="InvalidOperationException">The document has no root element.</exception>
    public static CoverageReport ParseDocument(XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var root = document.Root ?? throw new InvalidOperationException("XML document has no root element.");

        List<string> sourceDirectories = [];
        if (root.Element("sources") is { } sourcesEl)
        {
            foreach (var source in sourcesEl.Elements("source"))
            {
                sourceDirectories.Add(source.Value);
            }
        }

        var timestamp = ParseTimestamp(root.Attribute("timestamp")?.Value);

        List<PackageCoverage> parsedPackages = [];
        if (root.Element("packages") is { } packagesEl)
        {
            foreach (var package in packagesEl.Elements("package"))
            {
                parsedPackages.Add(ParsePackage(package));
            }
        }

        return new(parsedPackages, sourceDirectories, timestamp);
    }

    /// <summary>Gets the compiled regex for parsing branch coverage condition strings.</summary>
    /// <returns>A regex matching the (covered/total) pattern in condition-coverage attributes.</returns>
    [GeneratedRegex(@"\((?<covered>\d+)/(?<total>\d+)\)$")]
    internal static partial Regex BranchCoverageRegex();

    /// <summary>Parses a package element into a <see cref="PackageCoverage"/>.</summary>
    /// <param name="packageElement">The XML element representing a package.</param>
    /// <returns>The parsed package coverage data.</returns>
    internal static PackageCoverage ParsePackage(XElement packageElement)
    {
        var name = packageElement.Attribute("name")?.Value ?? string.Empty;
        var lineRate = ParseDecimalAttribute(packageElement, LineRateAttribute);
        var branchRate = ParseDecimalAttribute(packageElement, BranchRateAttribute);
        var complexity = ParseDecimalAttribute(packageElement, ComplexityAttribute);

        List<ClassCoverage> parsedClasses = [];
        if (packageElement.Element("classes") is { } classesEl)
        {
            foreach (var element in classesEl.Elements("class"))
            {
                parsedClasses.Add(ParseClass(element));
            }
        }

        return new(name, lineRate, branchRate, complexity, parsedClasses);
    }

    /// <summary>Parses a class element into a <see cref="ClassCoverage"/>.</summary>
    /// <param name="classElement">The XML element representing a class.</param>
    /// <returns>The parsed class coverage data.</returns>
    internal static ClassCoverage ParseClass(XElement classElement)
    {
        var name = classElement.Attribute("name")?.Value ?? string.Empty;
        var fileName = classElement.Attribute("filename")?.Value ?? string.Empty;
        var lineRate = ParseDecimalAttribute(classElement, LineRateAttribute);
        var branchRate = ParseDecimalAttribute(classElement, BranchRateAttribute);
        var complexity = ParseDecimalAttribute(classElement, ComplexityAttribute);

        var classLines = ParseLines(classElement.Element("lines"));

        List<MethodCoverage> parsedMethods = [];
        if (classElement.Element("methods") is { } methodsEl)
        {
            foreach (var method in methodsEl.Elements("method"))
            {
                parsedMethods.Add(ParseMethod(method, classLines));
            }
        }

        return new(name, fileName, lineRate, branchRate, complexity, parsedMethods, classLines);
    }

    /// <summary>Parses a method element into a <see cref="MethodCoverage"/>.</summary>
    /// <param name="methodElement">The XML element representing a method.</param>
    /// <param name="classLines">The class-level lines to fall back on if the method has none.</param>
    /// <returns>The parsed method coverage data.</returns>
    internal static MethodCoverage ParseMethod(XElement methodElement, IReadOnlyList<LineCoverage> classLines)
    {
        var name = methodElement.Attribute("name")?.Value ?? string.Empty;
        var signature = methodElement.Attribute("signature")?.Value ?? string.Empty;
        var lineRate = ParseDecimalAttribute(methodElement, LineRateAttribute);
        var branchRate = ParseDecimalAttribute(methodElement, BranchRateAttribute);
        var complexity = ParseDecimalAttribute(methodElement, ComplexityAttribute);

        var methodLines = ParseLines(methodElement.Element("lines"));

        // If method has no lines of its own, try to correlate from class lines
        if (methodLines.Count == 0 && classLines.Count > 0)
        {
            methodLines = [.. classLines];
        }

        return new(name, signature, lineRate, branchRate, complexity, methodLines);
    }

    /// <summary>Parses line elements from a lines container element.</summary>
    /// <param name="linesElement">The XML element containing line children, or null.</param>
    /// <returns>A list of parsed line coverage entries, ordered by line number.</returns>
    internal static List<LineCoverage> ParseLines(XElement? linesElement)
    {
        if (linesElement is null)
        {
            return [];
        }

        var lines = new List<LineCoverage>();

        foreach (var element in linesElement.Elements("line"))
        {
            var line = ParseLine(element);
            var index = lines.BinarySearch(line, LineNumberComparer);
            if (index < 0)
            {
                lines.Insert(~index, line);
            }
        }

        return lines;
    }

    /// <summary>Parses a single line element into a <see cref="LineCoverage"/>.</summary>
    /// <param name="lineElement">The XML element representing a single line.</param>
    /// <returns>The parsed line coverage data.</returns>
    internal static LineCoverage ParseLine(XElement lineElement)
    {
        var numberAttr = lineElement.Attribute("number")?.Value;
        var number = numberAttr is not null ? int.Parse(numberAttr, CultureInfo.InvariantCulture) : 0;
        var hitsAttr = lineElement.Attribute("hits")?.Value;
        var hits = hitsAttr is not null ? int.Parse(hitsAttr, CultureInfo.InvariantCulture) : 0;
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

        return new(number, hits, status, isBranch, coveredBranches, totalBranches);
    }

    /// <summary>Determines the visit status for a line based on its hits and branch information.</summary>
    /// <param name="hits">The number of times the line was executed.</param>
    /// <param name="isBranch">Whether the line contains a branch point.</param>
    /// <param name="coveredBranches">The number of branches covered, if applicable.</param>
    /// <param name="totalBranches">The total number of branches, if applicable.</param>
    /// <returns>The determined <see cref="LineVisitStatus"/>.</returns>
    internal static LineVisitStatus DetermineLineStatus(int hits, bool isBranch, int? coveredBranches, int? totalBranches)
    {
        if (hits <= 0)
        {
            return LineVisitStatus.NotCovered;
        }

        return isBranch && coveredBranches is { } cb && totalBranches is { } tb && cb < tb ? LineVisitStatus.PartiallyCovered : LineVisitStatus.Covered;
    }

    /// <summary>Parses a decimal attribute value from a Cobertura XML element.</summary>
    /// <param name="element">The XML element containing the attribute.</param>
    /// <param name="attributeName">The name of the attribute to parse.</param>
    /// <returns>The parsed decimal value, or null if the attribute is missing, NaN, or unparseable.</returns>
    internal static decimal? ParseDecimalAttribute(XElement element, string attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        if (value is null or "NaN")
        {
            return null;
        }

        // Handle locale-independent parsing (Cobertura may use comma or period)
        if (value.Contains(','))
        {
            value = value.Replace(',', '.');
        }

        return decimal.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var result)
            ? Math.Round(
            result,
            RateDecimalPlaces,
            MidpointRounding.AwayFromZero)
            : null;
    }

    /// <summary>Parses a Unix epoch timestamp from the Cobertura root element.</summary>
    /// <param name="value">The timestamp attribute value, or null.</param>
    /// <returns>The parsed <see cref="DateTimeOffset"/>, or null if the value is missing or unparseable.</returns>
    internal static DateTimeOffset? ParseTimestamp(string? value)
    {
        if (value is null)
        {
            return null;
        }

        // Cobertura timestamps are Unix epoch in seconds
        return long.TryParse(
            value,
            CultureInfo.InvariantCulture,
            out var epoch)
            ? DateTimeOffset.FromUnixTimeSeconds(epoch)
            : null;
    }
}
