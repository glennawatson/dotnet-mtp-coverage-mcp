// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TUnit.Assertions.Extensions;
using TUnit.Core;

using UnitTestMcp.Core.Models;
using UnitTestMcp.Core.Parsers;
using UnitTestMcp.Core.Services;

namespace UnitTestMcp.Tests;

/// <summary>
/// Tests for the <see cref="CoverageService"/>.
/// </summary>
public class CoverageServiceTests
{
    /// <summary>
    /// The coverage service under test.
    /// </summary>
    private readonly CoverageService _service = new();

    /// <summary>
    /// Gets the path to the sample Cobertura XML test fixture.
    /// </summary>
    private static string TestDataPath => Path.Combine(AppContext.BaseDirectory, "TestData", "sample-coverage.cobertura.xml");

    /// <summary>
    /// Verifies GetMissedCoverageForFile returns missed lines for Calculator.cs.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetMissedCoverageForFile_ReturnsCorrectMissedLines()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);
        var missed = _service.GetMissedCoverageForFile(report, "Calculator.cs");

        // Lines 21 and 32 have hits=0 -> NotCovered
        await Assert.That(missed.MissedLines).Count().IsEqualTo(2);
        await Assert.That(missed.MissedLines[0].LineNumber).IsEqualTo(21);
        await Assert.That(missed.MissedLines[1].LineNumber).IsEqualTo(32);
    }

    /// <summary>
    /// Verifies GetMissedCoverageForFile returns partially covered branches.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetMissedCoverageForFile_ReturnsPartialBranches()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);
        var missed = _service.GetMissedCoverageForFile(report, "Calculator.cs");

        // Lines 20 and 30 have partial branch coverage
        await Assert.That(missed.PartiallyMissedBranches).Count().IsEqualTo(2);
    }

    /// <summary>
    /// Verifies GetClassCoverage finds a class by fully qualified name.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetClassCoverage_ByFullName_FindsClass()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);
        var classCoverage = _service.GetClassCoverage(report, "MyProject.Calculator");

        await Assert.That(classCoverage).IsNotNull();
        await Assert.That(classCoverage!.Name).IsEqualTo("MyProject.Calculator");
        await Assert.That(classCoverage.Methods).Count().IsEqualTo(3);
    }

    /// <summary>
    /// Verifies GetClassCoverage finds a class by short name.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetClassCoverage_ByShortName_FindsClass()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);
        var classCoverage = _service.GetClassCoverage(report, "Calculator");

        await Assert.That(classCoverage).IsNotNull();
        await Assert.That(classCoverage!.Name).IsEqualTo("MyProject.Calculator");
    }

    /// <summary>
    /// Verifies GetClassCoverage returns null for non-existent class.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetClassCoverage_NotFound_ReturnsNull()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);
        var classCoverage = _service.GetClassCoverage(report, "NonExistent");

        await Assert.That(classCoverage).IsNull();
    }

    /// <summary>
    /// Verifies GetMethodCoverage finds a method by class and method name.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetMethodCoverage_FindsMethod()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);
        var method = _service.GetMethodCoverage(report, "MyProject.Calculator", "Add");

        await Assert.That(method).IsNotNull();
        await Assert.That(method!.Name).IsEqualTo("Add");
        await Assert.That(method.LineCoverageRate).IsEqualTo(1m);
    }

    /// <summary>
    /// Verifies GetMethodCoverage returns null for non-existent method.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetMethodCoverage_NotFound_ReturnsNull()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);
        var method = _service.GetMethodCoverage(report, "MyProject.Calculator", "NonExistent");

        await Assert.That(method).IsNull();
    }

    /// <summary>
    /// Verifies GetProjectCoverage finds a package by name.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetProjectCoverage_FindsProject()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);
        var project = _service.GetProjectCoverage(report, "MyProject");

        await Assert.That(project).IsNotNull();
        await Assert.That(project!.Name).IsEqualTo("MyProject");
        await Assert.That(project.Classes).Count().IsEqualTo(2);
    }

    /// <summary>
    /// Verifies GetProjectCoverage returns null for non-existent project.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetProjectCoverage_NotFound_ReturnsNull()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);
        var project = _service.GetProjectCoverage(report, "NonExistent");

        await Assert.That(project).IsNull();
    }

    /// <summary>
    /// Verifies GetSolutionMissedCoverage provides an aggregate report.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetSolutionMissedCoverage_ReturnsAggregate()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);
        var missed = _service.GetSolutionMissedCoverage(report);

        await Assert.That(missed.TotalCoverableLines).IsGreaterThan(0);
        await Assert.That(missed.TotalCoveredLines).IsGreaterThan(0);
        await Assert.That(missed.FileReports.Count).IsGreaterThan(0);
    }

    /// <summary>
    /// Verifies that ClassCoverage.MissedLineCount computes correctly.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClassCoverage_MissedLineCount_IsCorrect()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);
        var calculator = report.AllClasses.First(c => c.Name == "MyProject.Calculator");

        // Calculator has 10 lines total, lines 21 and 32 missed
        await Assert.That(calculator.MissedLineCount).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that MethodCoverage.CrapScore computes correctly.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MethodCoverage_CrapScore_ComputesCorrectly()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);
        var calculator = report.AllClasses.First(c => c.Name == "MyProject.Calculator");
        var divideMethod = calculator.Methods.First(m => m.Name == "Divide");

        // Divide: CC=2, coverage=0.75, uncovered=0.25
        // CRAP = 2^2 * 0.25^3 + 2 = 4 * 0.015625 + 2 = 2.0625
        await Assert.That(divideMethod.CrapScore).IsNotNull();
        await Assert.That(divideMethod.CrapScore!.Value).IsGreaterThan(2m);
    }

    /// <summary>
    /// Verifies solution-level coverage rates compute correctly.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CoverageReport_SolutionRates_AreValid()
    {
        var report = await CoberturaParser.ParseFileAsync(TestDataPath);

        await Assert.That(report.LineCoverageRate).IsNotNull();
        await Assert.That(report.LineCoverageRate!.Value).IsGreaterThan(0m);
        await Assert.That(report.LineCoverageRate!.Value).IsLessThanOrEqualTo(1m);
    }

    /// <summary>
    /// Verifies that merging two reports for the same class takes the maximum hits per line,
    /// so a line covered by one test project is not overwritten by zero hits from another.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeReports_SameClass_TakesMaxHitsPerLine()
    {
        // Report A: lines 10 covered, 11 missed, 12 covered
        var reportA = CoberturaParser.ParseString(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage version="1" timestamp="1700000000" line-rate="0.67" branch-rate="1">
              <sources><source>/src</source></sources>
              <packages>
                <package name="Lib" line-rate="0.67" branch-rate="1" complexity="1">
                  <classes>
                    <class name="Lib.Foo" filename="/src/Foo.cs" line-rate="0.67" branch-rate="1" complexity="1">
                      <methods />
                      <lines>
                        <line number="10" hits="5" branch="false" />
                        <line number="11" hits="0" branch="false" />
                        <line number="12" hits="3" branch="false" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        // Report B: lines 10 missed, 11 covered, 12 missed
        var reportB = CoberturaParser.ParseString(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage version="1" timestamp="1700000001" line-rate="0.33" branch-rate="1">
              <sources><source>/src</source></sources>
              <packages>
                <package name="Lib" line-rate="0.33" branch-rate="1" complexity="1">
                  <classes>
                    <class name="Lib.Foo" filename="/src/Foo.cs" line-rate="0.33" branch-rate="1" complexity="1">
                      <methods />
                      <lines>
                        <line number="10" hits="0" branch="false" />
                        <line number="11" hits="7" branch="false" />
                        <line number="12" hits="0" branch="false" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        var merged = CoverageService.MergeReports([reportA, reportB]);

        // All 3 lines should be covered after merge
        await Assert.That(merged.CoverableLineCount).IsEqualTo(3);
        await Assert.That(merged.CoveredLineCount).IsEqualTo(3);
        await Assert.That(merged.MissedLineCount).IsEqualTo(0);

        // Verify the package was merged into one
        await Assert.That(merged.Packages).Count().IsEqualTo(1);
        await Assert.That(merged.Packages[0].Name).IsEqualTo("Lib");
        await Assert.That(merged.Packages[0].Classes).Count().IsEqualTo(1);

        // Verify individual line hits took the max
        var cls = merged.Packages[0].Classes[0];
        await Assert.That(cls.Lines).Count().IsEqualTo(3);
        await Assert.That(cls.Lines[0].Hits).IsEqualTo(5);  // max(5, 0)
        await Assert.That(cls.Lines[1].Hits).IsEqualTo(7);  // max(0, 7)
        await Assert.That(cls.Lines[2].Hits).IsEqualTo(3);  // max(3, 0)
    }

    /// <summary>
    /// Verifies that merging two reports takes the best branch coverage for each line.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeReports_SameClass_TakesBestBranchCoverage()
    {
        // Report A: branch line with 1/2 branches covered
        var reportA = CoberturaParser.ParseString(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage version="1" timestamp="1700000000" line-rate="1" branch-rate="0.5">
              <sources><source>/src</source></sources>
              <packages>
                <package name="Lib" line-rate="1" branch-rate="0.5" complexity="1">
                  <classes>
                    <class name="Lib.Bar" filename="/src/Bar.cs" line-rate="1" branch-rate="0.5" complexity="1">
                      <methods />
                      <lines>
                        <line number="5" hits="3" branch="true" condition-coverage="50% (1/2)" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        // Report B: same branch line but with 2/2 branches covered
        var reportB = CoberturaParser.ParseString(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage version="1" timestamp="1700000001" line-rate="1" branch-rate="1">
              <sources><source>/src</source></sources>
              <packages>
                <package name="Lib" line-rate="1" branch-rate="1" complexity="1">
                  <classes>
                    <class name="Lib.Bar" filename="/src/Bar.cs" line-rate="1" branch-rate="1" complexity="1">
                      <methods />
                      <lines>
                        <line number="5" hits="2" branch="true" condition-coverage="100% (2/2)" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        var merged = CoverageService.MergeReports([reportA, reportB]);

        var cls = merged.Packages[0].Classes[0];
        await Assert.That(cls.Lines[0].Hits).IsEqualTo(3);              // max(3, 2)
        await Assert.That(cls.Lines[0].CoveredBranches).IsEqualTo(2);   // max(1, 2)
        await Assert.That(cls.Lines[0].TotalBranches).IsEqualTo(2);
        await Assert.That(cls.Lines[0].AllBranchesCovered).IsTrue();
    }

    /// <summary>
    /// Verifies that merging reports with disjoint packages preserves both.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeReports_DisjointPackages_PreservesBoth()
    {
        var reportA = CoberturaParser.ParseString(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage version="1" timestamp="1700000000" line-rate="1" branch-rate="1">
              <sources><source>/src</source></sources>
              <packages>
                <package name="PkgA" line-rate="1" branch-rate="1" complexity="1">
                  <classes>
                    <class name="PkgA.Foo" filename="/src/Foo.cs" line-rate="1" branch-rate="1" complexity="1">
                      <methods />
                      <lines>
                        <line number="1" hits="1" branch="false" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        var reportB = CoberturaParser.ParseString(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage version="1" timestamp="1700000001" line-rate="1" branch-rate="1">
              <sources><source>/src</source></sources>
              <packages>
                <package name="PkgB" line-rate="1" branch-rate="1" complexity="1">
                  <classes>
                    <class name="PkgB.Bar" filename="/src/Bar.cs" line-rate="1" branch-rate="1" complexity="1">
                      <methods />
                      <lines>
                        <line number="1" hits="1" branch="false" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        var merged = CoverageService.MergeReports([reportA, reportB]);

        await Assert.That(merged.Packages).Count().IsEqualTo(2);
        await Assert.That(merged.CoveredLineCount).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that merging picks the latest timestamp across reports.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeReports_PicksLatestTimestamp()
    {
        var reportA = CoberturaParser.ParseString(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage version="1" timestamp="1700000000" line-rate="1" branch-rate="1">
              <packages />
            </coverage>
            """);

        var reportB = CoberturaParser.ParseString(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage version="1" timestamp="1700000099" line-rate="1" branch-rate="1">
              <packages />
            </coverage>
            """);

        var merged = CoverageService.MergeReports([reportA, reportB]);

        await Assert.That(merged.Timestamp).IsNotNull();
        await Assert.That(merged.Timestamp!.Value).IsEqualTo(DateTimeOffset.FromUnixTimeSeconds(1700000099));
    }

    /// <summary>
    /// Verifies that merging a single report returns it unchanged.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeReports_SingleReport_PassesThrough()
    {
        var report = CoberturaParser.ParseString(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage version="1" timestamp="1700000000" line-rate="0.8" branch-rate="0.5">
              <sources><source>/src</source></sources>
              <packages>
                <package name="Lib" line-rate="0.8" branch-rate="0.5" complexity="1">
                  <classes>
                    <class name="Lib.Foo" filename="/src/Foo.cs" line-rate="0.8" branch-rate="0.5" complexity="1">
                      <methods />
                      <lines>
                        <line number="1" hits="1" branch="false" />
                        <line number="2" hits="0" branch="false" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        var merged = CoverageService.MergeReports([report]);

        await Assert.That(merged.CoverableLineCount).IsEqualTo(2);
        await Assert.That(merged.CoveredLineCount).IsEqualTo(1);
        await Assert.That(merged.MissedLineCount).IsEqualTo(1);
    }
}
