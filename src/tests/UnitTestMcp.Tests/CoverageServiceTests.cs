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
}
