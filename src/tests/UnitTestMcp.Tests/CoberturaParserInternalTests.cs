// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Xml.Linq;

using TUnit.Assertions.Extensions;
using TUnit.Core;

using UnitTestMcp.Core.Models;
using UnitTestMcp.Core.Parsers;

namespace UnitTestMcp.Tests;

/// <summary>
/// Tests for the internal methods of <see cref="CoberturaParser"/>.
/// </summary>
public class CoberturaParserInternalTests
{
    /// <summary>
    /// Verifies ParseTimestamp returns null for null input.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseTimestamp_Null_ReturnsNull()
    {
        var result = CoberturaParser.ParseTimestamp(null);
        await Assert.That(result).IsNull();
    }

    /// <summary>
    /// Verifies ParseTimestamp parses a valid Unix epoch.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseTimestamp_ValidEpoch_ReturnsDateTimeOffset()
    {
        var result = CoberturaParser.ParseTimestamp("1700000000");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value).IsEqualTo(DateTimeOffset.FromUnixTimeSeconds(1700000000));
    }

    /// <summary>
    /// Verifies ParseTimestamp returns null for unparseable input.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseTimestamp_InvalidString_ReturnsNull()
    {
        var result = CoberturaParser.ParseTimestamp("not-a-number");
        await Assert.That(result).IsNull();
    }

    /// <summary>
    /// Verifies ParseDecimalAttribute returns null for missing attribute.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseDecimalAttribute_MissingAttribute_ReturnsNull()
    {
        var element = new XElement("test");
        var result = CoberturaParser.ParseDecimalAttribute(element, "rate");
        await Assert.That(result).IsNull();
    }

    /// <summary>
    /// Verifies ParseDecimalAttribute returns null for NaN.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseDecimalAttribute_NaN_ReturnsNull()
    {
        var element = new XElement("test", new XAttribute("rate", "NaN"));
        var result = CoberturaParser.ParseDecimalAttribute(element, "rate");
        await Assert.That(result).IsNull();
    }

    /// <summary>
    /// Verifies ParseDecimalAttribute parses a period-delimited decimal.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseDecimalAttribute_PeriodDecimal_ParsesCorrectly()
    {
        var element = new XElement("test", new XAttribute("rate", "0.75"));
        var result = CoberturaParser.ParseDecimalAttribute(element, "rate");
        await Assert.That(result).IsEqualTo(0.75m);
    }

    /// <summary>
    /// Verifies ParseDecimalAttribute handles comma-delimited decimals from some locales.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseDecimalAttribute_CommaDecimal_ParsesCorrectly()
    {
        var element = new XElement("test", new XAttribute("rate", "0,75"));
        var result = CoberturaParser.ParseDecimalAttribute(element, "rate");
        await Assert.That(result).IsEqualTo(0.75m);
    }

    /// <summary>
    /// Verifies ParseDecimalAttribute returns null for completely invalid text.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseDecimalAttribute_InvalidText_ReturnsNull()
    {
        var element = new XElement("test", new XAttribute("rate", "abc"));
        var result = CoberturaParser.ParseDecimalAttribute(element, "rate");
        await Assert.That(result).IsNull();
    }

    /// <summary>
    /// Verifies DetermineLineStatus returns NotCovered for zero hits.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetermineLineStatus_ZeroHits_ReturnsNotCovered()
    {
        var result = CoberturaParser.DetermineLineStatus(0, false, null, null);
        await Assert.That(result).IsEqualTo(LineVisitStatus.NotCovered);
    }

    /// <summary>
    /// Verifies DetermineLineStatus returns Covered for non-branch line with hits.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetermineLineStatus_NonBranchWithHits_ReturnsCovered()
    {
        var result = CoberturaParser.DetermineLineStatus(5, false, null, null);
        await Assert.That(result).IsEqualTo(LineVisitStatus.Covered);
    }

    /// <summary>
    /// Verifies DetermineLineStatus returns PartiallyCovered for partial branches.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetermineLineStatus_PartialBranch_ReturnsPartiallyCovered()
    {
        var result = CoberturaParser.DetermineLineStatus(3, true, 1, 2);
        await Assert.That(result).IsEqualTo(LineVisitStatus.PartiallyCovered);
    }

    /// <summary>
    /// Verifies DetermineLineStatus returns Covered for fully covered branches.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetermineLineStatus_FullBranch_ReturnsCovered()
    {
        var result = CoberturaParser.DetermineLineStatus(3, true, 2, 2);
        await Assert.That(result).IsEqualTo(LineVisitStatus.Covered);
    }

    /// <summary>
    /// Verifies DetermineLineStatus returns Covered for branch line without branch info.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetermineLineStatus_BranchWithoutInfo_ReturnsCovered()
    {
        var result = CoberturaParser.DetermineLineStatus(1, true, null, null);
        await Assert.That(result).IsEqualTo(LineVisitStatus.Covered);
    }

    /// <summary>
    /// Verifies ParseLine parses a simple non-branch line.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseLine_SimpleNonBranch_ParsesCorrectly()
    {
        var element = XElement.Parse("""<line number="10" hits="5" branch="false" />""");
        var result = CoberturaParser.ParseLine(element);

        await Assert.That(result.LineNumber).IsEqualTo(10);
        await Assert.That(result.Hits).IsEqualTo(5);
        await Assert.That(result.IsBranch).IsFalse();
        await Assert.That(result.Status).IsEqualTo(LineVisitStatus.Covered);
        await Assert.That(result.CoveredBranches).IsNull();
        await Assert.That(result.TotalBranches).IsNull();
    }

    /// <summary>
    /// Verifies ParseLine parses a branch line with condition coverage.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseLine_BranchWithCondition_ParsesCorrectly()
    {
        var element = XElement.Parse("""<line number="20" hits="3" branch="true" condition-coverage="50% (1/2)" />""");
        var result = CoberturaParser.ParseLine(element);

        await Assert.That(result.LineNumber).IsEqualTo(20);
        await Assert.That(result.IsBranch).IsTrue();
        await Assert.That(result.CoveredBranches).IsEqualTo(1);
        await Assert.That(result.TotalBranches).IsEqualTo(2);
        await Assert.That(result.Status).IsEqualTo(LineVisitStatus.PartiallyCovered);
    }

    /// <summary>
    /// Verifies ParseLine handles branch line without condition-coverage attribute.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseLine_BranchWithoutCondition_HasNullBranches()
    {
        var element = XElement.Parse("""<line number="5" hits="1" branch="true" />""");
        var result = CoberturaParser.ParseLine(element);

        await Assert.That(result.IsBranch).IsTrue();
        await Assert.That(result.CoveredBranches).IsNull();
        await Assert.That(result.TotalBranches).IsNull();
        await Assert.That(result.Status).IsEqualTo(LineVisitStatus.Covered);
    }

    /// <summary>
    /// Verifies ParseLine defaults line number and hits to 0 when attributes are missing.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseLine_MissingAttributes_DefaultsToZero()
    {
        var element = XElement.Parse("""<line />""");
        var result = CoberturaParser.ParseLine(element);

        await Assert.That(result.LineNumber).IsEqualTo(0);
        await Assert.That(result.Hits).IsEqualTo(0);
        await Assert.That(result.IsBranch).IsFalse();
        await Assert.That(result.Status).IsEqualTo(LineVisitStatus.NotCovered);
    }

    /// <summary>
    /// Verifies ParseLines returns empty list for null element.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseLines_NullElement_ReturnsEmptyList()
    {
        var result = CoberturaParser.ParseLines(null);
        await Assert.That(result).Count().IsEqualTo(0);
    }

    /// <summary>
    /// Verifies ParseLines returns sorted lines using binary search insertion.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseLines_UnsortedInput_ReturnsSorted()
    {
        var element = XElement.Parse(
            """
            <lines>
              <line number="30" hits="1" branch="false" />
              <line number="10" hits="2" branch="false" />
              <line number="20" hits="3" branch="false" />
            </lines>
            """);

        var result = CoberturaParser.ParseLines(element);

        await Assert.That(result).Count().IsEqualTo(3);
        await Assert.That(result[0].LineNumber).IsEqualTo(10);
        await Assert.That(result[1].LineNumber).IsEqualTo(20);
        await Assert.That(result[2].LineNumber).IsEqualTo(30);
    }

    /// <summary>
    /// Verifies ParseLines deduplicates lines with the same line number.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseLines_DuplicateLineNumbers_Deduplicates()
    {
        var element = XElement.Parse(
            """
            <lines>
              <line number="10" hits="1" branch="false" />
              <line number="10" hits="5" branch="false" />
            </lines>
            """);

        var result = CoberturaParser.ParseLines(element);

        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].LineNumber).IsEqualTo(10);
    }

    /// <summary>
    /// Verifies ParseMethod falls back to class lines when method has no lines.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseMethod_NoMethodLines_FallsBackToClassLines()
    {
        var methodElement = XElement.Parse(
            """<method name="Foo" signature="()" line-rate="1" branch-rate="1" complexity="1" />""");
        var classLines = new List<LineCoverage>
        {
            new(10, 5, LineVisitStatus.Covered),
            new(11, 3, LineVisitStatus.Covered),
        };

        var result = CoberturaParser.ParseMethod(methodElement, classLines);

        await Assert.That(result.Name).IsEqualTo("Foo");
        await Assert.That(result.Lines).Count().IsEqualTo(2);
    }

    /// <summary>
    /// Verifies ParseMethod uses its own lines when present.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseMethod_HasOwnLines_UsesOwnLines()
    {
        var methodElement = XElement.Parse(
            """
            <method name="Bar" signature="(int)" line-rate="1" branch-rate="1" complexity="1">
              <lines>
                <line number="5" hits="1" branch="false" />
              </lines>
            </method>
            """);
        var classLines = new List<LineCoverage>
        {
            new(10, 5, LineVisitStatus.Covered),
            new(11, 3, LineVisitStatus.Covered),
        };

        var result = CoberturaParser.ParseMethod(methodElement, classLines);

        await Assert.That(result.Lines).Count().IsEqualTo(1);
        await Assert.That(result.Lines[0].LineNumber).IsEqualTo(5);
    }

    /// <summary>
    /// Verifies ParseClass parses all fields correctly.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseClass_ParsesAllFields()
    {
        var element = XElement.Parse(
            """
            <class name="Ns.Foo" filename="/src/Foo.cs" line-rate="0.5" branch-rate="0.75" complexity="3">
              <methods>
                <method name="Go" signature="()" line-rate="1" branch-rate="1" complexity="1">
                  <lines>
                    <line number="1" hits="1" branch="false" />
                  </lines>
                </method>
              </methods>
              <lines>
                <line number="1" hits="1" branch="false" />
                <line number="2" hits="0" branch="false" />
              </lines>
            </class>
            """);

        var result = CoberturaParser.ParseClass(element);

        await Assert.That(result.Name).IsEqualTo("Ns.Foo");
        await Assert.That(result.FileName).IsEqualTo("/src/Foo.cs");
        await Assert.That(result.Methods).Count().IsEqualTo(1);
        await Assert.That(result.Lines).Count().IsEqualTo(2);
        await Assert.That(result.CoveredLineCount).IsEqualTo(1);
        await Assert.That(result.MissedLineCount).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies ParsePackage parses all fields correctly.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParsePackage_ParsesAllFields()
    {
        var element = XElement.Parse(
            """
            <package name="MyPkg" line-rate="0.8" branch-rate="0.6" complexity="5">
              <classes>
                <class name="MyPkg.A" filename="/a.cs" line-rate="1" branch-rate="1" complexity="1">
                  <methods />
                  <lines>
                    <line number="1" hits="1" branch="false" />
                  </lines>
                </class>
              </classes>
            </package>
            """);

        var result = CoberturaParser.ParsePackage(element);

        await Assert.That(result.Name).IsEqualTo("MyPkg");
        await Assert.That(result.Classes).Count().IsEqualTo(1);
        await Assert.That(result.CoveredLineCount).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies BranchCoverageRegex matches the expected pattern.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BranchCoverageRegex_MatchesPattern()
    {
        var regex = CoberturaParser.BranchCoverageRegex();
        var match = regex.Match("50% (1/2)");

        await Assert.That(match.Success).IsTrue();
        await Assert.That(match.Groups["covered"].Value).IsEqualTo("1");
        await Assert.That(match.Groups["total"].Value).IsEqualTo("2");
    }

    /// <summary>
    /// Verifies BranchCoverageRegex does not match invalid input.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BranchCoverageRegex_NoMatch_ReturnsFalse()
    {
        var regex = CoberturaParser.BranchCoverageRegex();
        var match = regex.Match("no match here");

        await Assert.That(match.Success).IsFalse();
    }

    /// <summary>
    /// Verifies LineNumberComparer sorts by line number.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LineNumberComparer_SortsByLineNumber()
    {
        var a = new LineCoverage(10, 1, LineVisitStatus.Covered);
        var b = new LineCoverage(5, 1, LineVisitStatus.Covered);

        var result = CoberturaParser.LineNumberComparer.Compare(a, b);
        await Assert.That(result).IsGreaterThan(0); // 10 > 5
    }
}
