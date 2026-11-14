// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TUnit.Assertions.Extensions;
using TUnit.Core;

using UnitTestMcp.Core.Models;
using UnitTestMcp.Core.Parsers;

namespace UnitTestMcp.Tests;

/// <summary>Tests for the <see cref="CoberturaParser"/>.</summary>
public class CoberturaParserTests
{
    /// <summary>The calculator class in the sample report.</summary>
    private const string CalculatorClassName = "MyProject.Calculator";

    /// <summary>Gets the path to the sample Cobertura XML test fixture.</summary>
    private static string TestDataPath => Path.Combine(AppContext.BaseDirectory, "TestData", "sample-coverage.cobertura.xml");

    /// <summary>Verifies that ParseFileAsync loads and parses the sample Cobertura XML file.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseFileAsync_WithSampleFile_ReturnsValidReport()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath, CancellationToken.None);

        await Assert.That(report).IsNotNull();
        await Assert.That(report.Packages).Count().IsEqualTo(expected: 2);
        await Assert.That(report.SourceDirectories).Count().IsEqualTo(expected: 1);
        await Assert.That(report.Timestamp).IsNotNull();
    }

    /// <summary>Verifies that packages are parsed with correct names.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseFileAsync_ParsesPackageNames()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath, CancellationToken.None);

        var packageNames = report.Packages.Select(static p => p.Name).Order().ToList();
        await Assert.That(value: packageNames[0]).IsEqualTo("MyProject");
        await Assert.That(value: packageNames[1]).IsEqualTo("MyProject.Utils");
    }

    /// <summary>Verifies that classes are parsed under correct packages.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseFileAsync_ParsesClasses()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath, CancellationToken.None);

        var myProject = report.Packages.First(static p => p.Name == "MyProject");
        await Assert.That(myProject.Classes).Count().IsEqualTo(expected: 2);

        var calculator = myProject.Classes.First(static c => c.Name == CalculatorClassName);
        await Assert.That(calculator.FileName).IsEqualTo("/home/user/src/MyProject/Calculator.cs");
    }

    /// <summary>Verifies that methods are parsed for a class.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseFileAsync_ParsesMethods()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath, CancellationToken.None);

        var calculator = report.AllClasses.First(static c => c.Name == CalculatorClassName);
        await Assert.That(calculator.Methods).Count().IsEqualTo(expected: 3);

        var addMethod = calculator.Methods.First(static m => m.Name == "Add");
        await Assert.That(addMethod.LineCoverageRate).IsEqualTo(expected: 1M);
    }

    /// <summary>Verifies that line coverage is parsed correctly with hits and status.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseFileAsync_ParsesLineCoverage()
    {
        const int coveredLineNumber = 10;
        const int missedLineNumber = 21;
        var report = await CoberturaParser.ParseFileAsync(TestDataPath, CancellationToken.None);

        var calculator = report.AllClasses.First(static c => c.Name == CalculatorClassName);

        // Line 10: hits=5, not a branch -> Covered
        var line10 = calculator.Lines.First(static l => l.LineNumber == coveredLineNumber);
        await Assert.That(line10.Hits).IsEqualTo(expected: 5);
        await Assert.That(line10.Status).IsEqualTo(LineVisitStatus.Covered);
        await Assert.That(line10.IsBranch).IsFalse();

        // Line 21: hits=0 -> NotCovered
        var line21 = calculator.Lines.First(static l => l.LineNumber == missedLineNumber);
        await Assert.That(line21.Status).IsEqualTo(LineVisitStatus.NotCovered);
    }

    /// <summary>Verifies that branch coverage is parsed correctly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseFileAsync_ParsesBranchCoverage()
    {
        const int partiallyCoveredLineNumber = 20;
        const int fullyCoveredLineNumber = 8;
        var report = await CoberturaParser.ParseFileAsync(TestDataPath, CancellationToken.None);

        var calculator = report.AllClasses.First(static c => c.Name == CalculatorClassName);

        // Line 20: branch with 50% (1/2) -> PartiallyCovered
        var line20 = calculator.Lines.First(static l => l.LineNumber == partiallyCoveredLineNumber);
        await Assert.That(line20.IsBranch).IsTrue();
        await Assert.That(line20.CoveredBranches).IsEqualTo(expected: 1);
        await Assert.That(line20.TotalBranches).IsEqualTo(expected: 2);
        await Assert.That(line20.Status).IsEqualTo(LineVisitStatus.PartiallyCovered);

        // Line 8 in MathExtensions: 100% (2/2) -> Covered
        var mathExt = report.AllClasses.First(static c => c.Name == "MyProject.Utils.MathExtensions");
        var line8 = mathExt.Lines.First(static l => l.LineNumber == fullyCoveredLineNumber);
        await Assert.That(line8.IsBranch).IsTrue();
        await Assert.That(line8.CoveredBranches).IsEqualTo(expected: 2);
        await Assert.That(line8.TotalBranches).IsEqualTo(expected: 2);
        await Assert.That(line8.Status).IsEqualTo(LineVisitStatus.Covered);
    }

    /// <summary>Verifies that the timestamp is parsed from the Cobertura root element.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseFileAsync_ParsesTimestamp()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath, CancellationToken.None);

        await Assert.That(report.Timestamp).IsNotNull();
        await Assert.That(report.Timestamp!.Value).IsEqualTo(expected: DateTimeOffset.FromUnixTimeSeconds(seconds: 1_700_000_000));
    }

    /// <summary>Verifies that ParseString works for inline XML content.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseString_WithMinimalXml_ReturnsReport()
    {
        const string xml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage version="1" timestamp="1700000000" line-rate="1" branch-rate="1">
              <sources><source>/src</source></sources>
              <packages>
                <package name="TestPkg" line-rate="1" branch-rate="1" complexity="1">
                  <classes>
                    <class name="TestPkg.Foo" filename="/src/Foo.cs" line-rate="1" branch-rate="1" complexity="1">
                      <methods />
                      <lines>
                        <line number="1" hits="1" branch="false" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;

        var report = CoberturaParser.ParseString(xml);

        await Assert.That(report.Packages).Count().IsEqualTo(expected: 1);
        await Assert.That(value: report.Packages[0].Classes[0].Lines).Count().IsEqualTo(expected: 1);
    }
}
