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
/// Tests that verify merge behavior using real-world Cobertura XML files
/// from the ReactiveUI.Binding.SourceGenerators project.
/// </summary>
/// <remarks>
/// The real-world scenario: ReactiveUI.Binding.Tests exercises the runtime library
/// (99.8% coverage), but ReactiveUI.Binding.SourceGenerators.Tests also instruments
/// the same library with 0% coverage (referenced but not exercised).
/// After merging, the ReactiveUI.Binding package should reflect the combined coverage,
/// not be dragged down to 0%.
/// </remarks>
public class RealWorldMergeTests
{
    /// <summary>
    /// Gets the path to the real-world test data directory.
    /// </summary>
    private static string RealWorldDataDir => Path.Combine(AppContext.BaseDirectory, "TestData", "real-world");

    /// <summary>
    /// Gets the path to the full real-world test data directory with all 15 TFM reports.
    /// </summary>
    private static string RealWorldFullDataDir => Path.Combine(AppContext.BaseDirectory, "TestData", "real-world-full");

    /// <summary>
    /// Verifies that merging the SourceGenerators.Tests report (0% for ReactiveUI.Binding)
    /// with the Binding.Tests report (99.8% for ReactiveUI.Binding) produces correct
    /// merged coverage that reflects the actual test coverage.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeReports_ZeroPercentAndHighCoverage_TakesHighCoverage()
    {
        var srcGenReport = await CoberturaParser.ParseFileAsync(
            Path.Combine(RealWorldDataDir, "srcgen-tests.cobertura.xml"));
        var bindingReport = await CoberturaParser.ParseFileAsync(
            Path.Combine(RealWorldDataDir, "binding-tests.cobertura.xml"));

        // Verify preconditions: srcgen has 0% for ReactiveUI.Binding
        var srcGenPkg = srcGenReport.Packages.First(p => p.Name == "ReactiveUI.Binding");
        await Assert.That(srcGenPkg.CoveredLineCount).IsEqualTo(0);
        await Assert.That(srcGenPkg.CoverableLineCount).IsGreaterThan(1000);

        // Verify preconditions: binding tests has ~99.8% for ReactiveUI.Binding
        var bindingPkg = bindingReport.Packages.First(p => p.Name == "ReactiveUI.Binding");
        await Assert.That(bindingPkg.CoveredLineCount).IsGreaterThan(1600);

        // Merge the two reports
        var merged = CoverageService.MergeReports([srcGenReport, bindingReport]);

        // After merge, ReactiveUI.Binding should have the combined (best) coverage
        var mergedPkg = merged.Packages.First(p => p.Name == "ReactiveUI.Binding");
        await Assert.That(mergedPkg.CoveredLineCount)
            .IsGreaterThanOrEqualTo(bindingPkg.CoveredLineCount);
        await Assert.That(mergedPkg.MissedLineCount)
            .IsLessThanOrEqualTo(bindingPkg.MissedLineCount);
    }

    /// <summary>
    /// Verifies that merging all four real-world reports produces correct overall coverage
    /// for the ReactiveUI.Binding package.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeAllReports_ReactiveUIBinding_HasCorrectCoverage()
    {
        var srcGenReport = await CoberturaParser.ParseFileAsync(
            Path.Combine(RealWorldDataDir, "srcgen-tests.cobertura.xml"));
        var bindingReport = await CoberturaParser.ParseFileAsync(
            Path.Combine(RealWorldDataDir, "binding-tests.cobertura.xml"));
        var analyzerReport = await CoberturaParser.ParseFileAsync(
            Path.Combine(RealWorldDataDir, "analyzer-tests.cobertura.xml"));
        var generatedCodeReport = await CoberturaParser.ParseFileAsync(
            Path.Combine(RealWorldDataDir, "generatedcode-tests.cobertura.xml"));

        var merged = CoverageService.MergeReports(
            [srcGenReport, bindingReport, analyzerReport, generatedCodeReport]);

        // ReactiveUI.Binding should have coverage from the binding tests
        var bindingPkg = merged.Packages.First(p => p.Name == "ReactiveUI.Binding");
        await Assert.That(bindingPkg.CoveredLineCount).IsGreaterThan(1600);

        // ReactiveUI.Binding.SourceGenerators should still have its high coverage
        var srcGenPkg = merged.Packages.First(p => p.Name == "ReactiveUI.Binding.SourceGenerators");
        await Assert.That(srcGenPkg.CoveredLineCount).IsGreaterThan(3400);

        // ReactiveUI.Binding.Analyzer should have its coverage
        var analyzerPkg = merged.Packages.First(p => p.Name == "ReactiveUI.Binding.Analyzer");
        await Assert.That(analyzerPkg.CoveredLineCount).IsGreaterThan(280);
    }

    /// <summary>
    /// Verifies that individual class coverage is correctly merged when the same class
    /// appears in multiple reports with different coverage levels.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeReports_IndividualClassLines_TakeMaxHits()
    {
        var srcGenReport = await CoberturaParser.ParseFileAsync(
            Path.Combine(RealWorldDataDir, "srcgen-tests.cobertura.xml"));
        var bindingReport = await CoberturaParser.ParseFileAsync(
            Path.Combine(RealWorldDataDir, "binding-tests.cobertura.xml"));

        var merged = CoverageService.MergeReports([srcGenReport, bindingReport]);

        // Find a specific well-known class
        var mergedClass = merged.AllClasses.First(c =>
            c.Name == "ReactiveUI.Binding.BindingTypeConverter<TFrom, TTo>");

        // This class should be fully covered (from binding tests)
        await Assert.That(mergedClass.CoveredLineCount).IsEqualTo(mergedClass.CoverableLineCount);
        await Assert.That(mergedClass.MissedLineCount).IsEqualTo(0);

        // All lines should have hits > 0
        foreach (var line in mergedClass.Lines)
        {
            await Assert.That(line.Hits).IsGreaterThan(0);
        }
    }

    /// <summary>
    /// Verifies that LoadSolutionReportAsync correctly discovers and merges all reports
    /// from the test data directory, producing correct coverage for ReactiveUI.Binding.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LoadSolutionReportAsync_RealWorldDirectory_MergesCorrectly()
    {
        var service = new CoverageService();
        var report = await service.LoadSolutionReportAsync(RealWorldDataDir);

        // ReactiveUI.Binding should have high coverage (from binding-tests.cobertura.xml)
        var bindingPkg = report.Packages.First(p => p.Name == "ReactiveUI.Binding");
        await Assert.That(bindingPkg.CoveredLineCount).IsGreaterThan(1600);
        await Assert.That(bindingPkg.CoverableLineCount).IsGreaterThan(1600);
    }

    /// <summary>
    /// Verifies that LoadSolutionReportAsync correctly merges all 15 cobertura files
    /// (5 test projects x 3 TFMs) and produces correct coverage for ReactiveUI.Binding.
    /// This reproduces the exact scenario where the MCP tool reported 0% coverage
    /// despite the Binding.Tests report having 99.8% coverage.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LoadSolutionReportAsync_AllTFMs_MergesCorrectly()
    {
        var service = new CoverageService();
        var report = await service.LoadSolutionReportAsync(RealWorldFullDataDir);

        // Should find all packages
        await Assert.That(report.Packages.Select(p => p.Name))
            .Contains("ReactiveUI.Binding");
        await Assert.That(report.Packages.Select(p => p.Name))
            .Contains("ReactiveUI.Binding.SourceGenerators");
        await Assert.That(report.Packages.Select(p => p.Name))
            .Contains("ReactiveUI.Binding.Analyzer");

        // ReactiveUI.Binding should have HIGH coverage (from binding tests)
        var bindingPkg = report.Packages.First(p => p.Name == "ReactiveUI.Binding");
        await Assert.That(bindingPkg.CoveredLineCount).IsGreaterThan(1600);
        await Assert.That(bindingPkg.MissedLineCount).IsLessThan(10);

        // SourceGenerators should also have high coverage
        var srcGenPkg = report.Packages.First(p => p.Name == "ReactiveUI.Binding.SourceGenerators");
        await Assert.That(srcGenPkg.CoveredLineCount).IsGreaterThan(3400);
    }

    /// <summary>
    /// Verifies that the ReactiveUI.Binding.Reactive package has correct coverage
    /// after merging reports where one has 0% and another has 100%.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeReports_ReactivePackage_HasCorrectCoverage()
    {
        var srcGenReport = await CoberturaParser.ParseFileAsync(
            Path.Combine(RealWorldDataDir, "srcgen-tests.cobertura.xml"));
        var bindingReport = await CoberturaParser.ParseFileAsync(
            Path.Combine(RealWorldDataDir, "binding-tests.cobertura.xml"));

        // srcgen has Reactive at 0%, binding tests has it at 100%
        var srcGenReactive = srcGenReport.Packages.First(p => p.Name == "ReactiveUI.Binding.Reactive");
        await Assert.That(srcGenReactive.CoveredLineCount).IsEqualTo(0);

        var bindingReactive = bindingReport.Packages.First(p => p.Name == "ReactiveUI.Binding.Reactive");
        await Assert.That(bindingReactive.CoveredLineCount).IsGreaterThan(0);

        var merged = CoverageService.MergeReports([srcGenReport, bindingReport]);

        var mergedReactive = merged.Packages.First(p => p.Name == "ReactiveUI.Binding.Reactive");
        await Assert.That(mergedReactive.CoveredLineCount)
            .IsGreaterThanOrEqualTo(bindingReactive.CoveredLineCount);
    }
}
