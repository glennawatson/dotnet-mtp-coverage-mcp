// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TUnit.Assertions.Extensions;
using TUnit.Core;

using UnitTestMcp.Core.Models;
using UnitTestMcp.Core.Parsers;

namespace UnitTestMcp.Tests;

/// <summary>
/// Tests for the <see cref="CoberturaParser"/>.
/// </summary>
public class CoberturaParserTests
{
    /// <summary>
    /// Gets the path to the sample Cobertura XML test fixture.
    /// </summary>
    private static string TestDataPath => Path.Combine(AppContext.BaseDirectory, "TestData", "sample-coverage.cobertura.xml");

    /// <summary>
    /// Verifies that ParseFileAsync loads and parses the sample Cobertura XML file.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseFileAsync_WithSampleFile_ReturnsValidReport()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);

        await Assert.That(report).IsNotNull();
        await Assert.That(report.Packages).Count().IsEqualTo(2);
        await Assert.That(report.SourceDirectories).Count().IsEqualTo(1);
        await Assert.That(report.Timestamp).IsNotNull();
    }

    /// <summary>
    /// Verifies that packages are parsed with correct names.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseFileAsync_ParsesPackageNames()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);

        var packageNames = report.Packages.Select(p => p.Name).Order().ToList();
        await Assert.That(packageNames[0]).IsEqualTo("MyProject");
        await Assert.That(packageNames[1]).IsEqualTo("MyProject.Utils");
    }

    /// <summary>
    /// Verifies that classes are parsed under correct packages.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseFileAsync_ParsesClasses()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);

        var myProject = report.Packages.First(p => p.Name == "MyProject");
        await Assert.That(myProject.Classes).Count().IsEqualTo(2);

        var calculator = myProject.Classes.First(c => c.Name == "MyProject.Calculator");
        await Assert.That(calculator.FileName).IsEqualTo("/home/user/src/MyProject/Calculator.cs");
    }

    /// <summary>
    /// Verifies that methods are parsed for a class.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseFileAsync_ParsesMethods()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);

        var calculator = report.AllClasses.First(c => c.Name == "MyProject.Calculator");
        await Assert.That(calculator.Methods).Count().IsEqualTo(3);

        var addMethod = calculator.Methods.First(m => m.Name == "Add");
        await Assert.That(addMethod.LineCoverageRate).IsEqualTo(1m);
    }

    /// <summary>
    /// Verifies that line coverage is parsed correctly with hits and status.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseFileAsync_ParsesLineCoverage()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);

        var calculator = report.AllClasses.First(c => c.Name == "MyProject.Calculator");

        // Line 10: hits=5, not a branch -> Covered
        var line10 = calculator.Lines.First(l => l.LineNumber == 10);
        await Assert.That(line10.Hits).IsEqualTo(5);
        await Assert.That(line10.Status).IsEqualTo(LineVisitStatus.Covered);
        await Assert.That(line10.IsBranch).IsFalse();

        // Line 21: hits=0 -> NotCovered
        var line21 = calculator.Lines.First(l => l.LineNumber == 21);
        await Assert.That(line21.Status).IsEqualTo(LineVisitStatus.NotCovered);
    }

    /// <summary>
    /// Verifies that branch coverage is parsed correctly.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseFileAsync_ParsesBranchCoverage()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);

        var calculator = report.AllClasses.First(c => c.Name == "MyProject.Calculator");

        // Line 20: branch with 50% (1/2) -> PartiallyCovered
        var line20 = calculator.Lines.First(l => l.LineNumber == 20);
        await Assert.That(line20.IsBranch).IsTrue();
        await Assert.That(line20.CoveredBranches).IsEqualTo(1);
        await Assert.That(line20.TotalBranches).IsEqualTo(2);
        await Assert.That(line20.Status).IsEqualTo(LineVisitStatus.PartiallyCovered);

        // Line 8 in MathExtensions: 100% (2/2) -> Covered
        var mathExt = report.AllClasses.First(c => c.Name == "MyProject.Utils.MathExtensions");
        var line8 = mathExt.Lines.First(l => l.LineNumber == 8);
        await Assert.That(line8.IsBranch).IsTrue();
        await Assert.That(line8.CoveredBranches).IsEqualTo(2);
        await Assert.That(line8.TotalBranches).IsEqualTo(2);
        await Assert.That(line8.Status).IsEqualTo(LineVisitStatus.Covered);
    }

    /// <summary>
    /// Verifies that the timestamp is parsed from the Cobertura root element.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseFileAsync_ParsesTimestamp()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);

        await Assert.That(report.Timestamp).IsNotNull();
        await Assert.That(report.Timestamp!.Value).IsEqualTo(DateTimeOffset.FromUnixTimeSeconds(1700000000));
    }

    /// <summary>
    /// Verifies that ParseString works for inline XML content.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParseString_WithMinimalXml_ReturnsReport()
    {
        var xml =
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

        await Assert.That(report.Packages).Count().IsEqualTo(1);
        await Assert.That(report.Packages[0].Classes[0].Lines).Count().IsEqualTo(1);
    }
}
