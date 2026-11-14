// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TUnit.Assertions.Extensions;
using TUnit.Core;

using UnitTestMcp.Core.Models;
using UnitTestMcp.Core.Services;

namespace UnitTestMcp.Tests;

/// <summary>Tests for the internal methods of <see cref="CoverageService"/>.</summary>
public class CoverageServiceInternalTests
{
    /// <summary>The source path used for file grouping.</summary>
    private const string SourceFilePath = "/src/Foo.cs";

    /// <summary>The root path used for class merging.</summary>
    private const string RootFilePath = "/Foo.cs";

    /// <summary>Verifies NormalizePath converts backslashes to forward slashes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NormalizePath_BackslashesToForward()
    {
        var result = CoverageService.NormalizePath(@"C:\src\Foo.cs");
        await Assert.That(result).IsEqualTo("C:/src/Foo.cs");
    }

    /// <summary>Verifies NormalizePath leaves forward slashes unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NormalizePath_ForwardSlashes_Unchanged()
    {
        var result = CoverageService.NormalizePath(SourceFilePath);
        await Assert.That(result).IsEqualTo(SourceFilePath);
    }

    /// <summary>Verifies MergeSortedLines returns empty list for empty input.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeSortedLines_EmptyInput_ReturnsEmpty()
    {
        var result = CoverageService.MergeSortedLines([]);
        await Assert.That(result).Count().IsEqualTo(expected: 0);
    }

    /// <summary>Verifies MergeSortedLines merges lines from multiple classes sorted by line number.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeSortedLines_MultipleClasses_MergesSorted()
    {
        var classA = CreateClass(
            "A",
            "/a.cs",
            lines: [
                new LineCoverage(LineNumber: 10, Hits: 1, LineVisitStatus.Covered),
                new LineCoverage(LineNumber: 30, Hits: 1, LineVisitStatus.Covered),
            ]);

        var classB = CreateClass(
            "B",
            "/b.cs",
            lines: [
                new LineCoverage(LineNumber: 20, Hits: 1, LineVisitStatus.Covered),
                new LineCoverage(LineNumber: 40, Hits: 1, LineVisitStatus.Covered),
            ]);

        var result = CoverageService.MergeSortedLines([classA, classB]);

        await Assert.That(result).Count().IsEqualTo(expected: 4);
        await Assert.That(value: result[0].LineNumber).IsEqualTo(expected: 10);
        await Assert.That(value: result[1].LineNumber).IsEqualTo(expected: 20);
        await Assert.That(value: result[2].LineNumber).IsEqualTo(expected: 30);
        await Assert.That(value: result[3].LineNumber).IsEqualTo(expected: 40);
    }

    /// <summary>Verifies MergeSortedLines deduplicates lines with the same line number.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeSortedLines_DuplicateLines_Deduplicates()
    {
        var classA = CreateClass("A", "/a.cs", lines: [new LineCoverage(LineNumber: 10, Hits: 5, LineVisitStatus.Covered)]);
        var classB = CreateClass("B", "/b.cs", lines: [new LineCoverage(LineNumber: 10, Hits: 3, LineVisitStatus.Covered)]);

        var result = CoverageService.MergeSortedLines([classA, classB]);

        await Assert.That(result).Count().IsEqualTo(expected: 1);
        await Assert.That(value: result[0].LineNumber).IsEqualTo(expected: 10);
    }

    /// <summary>Verifies CategorizeLines separates missed and partially covered lines.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CategorizeLines_SeparatesMissedAndPartial()
    {
        var lines = new List<LineCoverage>
        {
            new(LineNumber: 1, Hits: 5, LineVisitStatus.Covered),
            new(LineNumber: 2, Hits: 0, LineVisitStatus.NotCovered),
            new(LineNumber: 3, Hits: 3, LineVisitStatus.PartiallyCovered, true, CoveredBranches: 1, TotalBranches: 2),
            new(LineNumber: 4, Hits: 0, LineVisitStatus.NotCovered),
            new(LineNumber: 5, Hits: 1, LineVisitStatus.Covered),
        };

        var result = CoverageService.CategorizeLines(SourceFilePath, lines);

        await Assert.That(result.FilePath).IsEqualTo(SourceFilePath);
        await Assert.That(result.MissedLines).Count().IsEqualTo(expected: 2);
        await Assert.That(value: result.MissedLines[0].LineNumber).IsEqualTo(expected: 2);
        await Assert.That(value: result.MissedLines[1].LineNumber).IsEqualTo(expected: 4);
        await Assert.That(result.PartiallyMissedBranches).Count().IsEqualTo(expected: 1);
        await Assert.That(value: result.PartiallyMissedBranches[0].LineNumber).IsEqualTo(expected: 3);
    }

    /// <summary>Verifies CategorizeLines returns empty lists when all lines are covered.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CategorizeLines_AllCovered_ReturnsEmptyLists()
    {
        var lines = new List<LineCoverage> { new(LineNumber: 1, Hits: 5, LineVisitStatus.Covered), new(LineNumber: 2, Hits: 3, LineVisitStatus.Covered), };

        var result = CoverageService.CategorizeLines(SourceFilePath, lines);

        await Assert.That(result.MissedLines).Count().IsEqualTo(expected: 0);
        await Assert.That(result.PartiallyMissedBranches).Count().IsEqualTo(expected: 0);
    }

    /// <summary>Verifies MergeLineCoverage takes maximum hits.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeLineCoverage_TakesMaxHits()
    {
        var a = new LineCoverage(LineNumber: 10, Hits: 3, LineVisitStatus.Covered);
        var b = new LineCoverage(LineNumber: 10, Hits: 7, LineVisitStatus.Covered);

        var result = CoverageService.MergeLineCoverage(a, b);

        await Assert.That(result.LineNumber).IsEqualTo(expected: 10);
        await Assert.That(result.Hits).IsEqualTo(expected: 7);
    }

    /// <summary>Verifies MergeLineCoverage takes best branch coverage.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeLineCoverage_TakesBestBranches()
    {
        var a = new LineCoverage(LineNumber: 10, Hits: 3, LineVisitStatus.PartiallyCovered, true, CoveredBranches: 1, TotalBranches: 4);
        var b = new LineCoverage(LineNumber: 10, Hits: 2, LineVisitStatus.PartiallyCovered, true, CoveredBranches: 3, TotalBranches: 4);

        var result = CoverageService.MergeLineCoverage(a, b);

        await Assert.That(result.CoveredBranches).IsEqualTo(expected: 3);
        await Assert.That(result.TotalBranches).IsEqualTo(expected: 4);
        await Assert.That(result.IsBranch).IsTrue();
    }

    /// <summary>Verifies MergeLineCoverage promotes non-branch to branch when one is a branch.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeLineCoverage_PromotesToBranch()
    {
        var a = new LineCoverage(LineNumber: 10, Hits: 3, LineVisitStatus.Covered);
        var b = new LineCoverage(LineNumber: 10, Hits: 2, LineVisitStatus.PartiallyCovered, true, CoveredBranches: 1, TotalBranches: 2);

        var result = CoverageService.MergeLineCoverage(a, b);

        await Assert.That(result.IsBranch).IsTrue();
        await Assert.That(result.CoveredBranches).IsEqualTo(expected: 1);
        await Assert.That(result.TotalBranches).IsEqualTo(expected: 2);
    }

    /// <summary>Verifies MergeLineCoverage produces Covered status when max hits covers all branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeLineCoverage_FullBranch_ReturnsCovered()
    {
        var a = new LineCoverage(LineNumber: 10, Hits: 3, LineVisitStatus.PartiallyCovered, true, CoveredBranches: 1, TotalBranches: 2);
        var b = new LineCoverage(LineNumber: 10, Hits: 5, LineVisitStatus.Covered, true, CoveredBranches: 2, TotalBranches: 2);

        var result = CoverageService.MergeLineCoverage(a, b);

        await Assert.That(result.Status).IsEqualTo(LineVisitStatus.Covered);
        await Assert.That(result.CoveredBranches).IsEqualTo(expected: 2);
    }

    /// <summary>Verifies MergeLineCoverage returns NotCovered when both have zero hits.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeLineCoverage_BothZeroHits_ReturnsNotCovered()
    {
        var a = new LineCoverage(LineNumber: 10, Hits: 0, LineVisitStatus.NotCovered);
        var b = new LineCoverage(LineNumber: 10, Hits: 0, LineVisitStatus.NotCovered);

        var result = CoverageService.MergeLineCoverage(a, b);

        await Assert.That(result.Status).IsEqualTo(LineVisitStatus.NotCovered);
        await Assert.That(result.Hits).IsEqualTo(expected: 0);
    }

    /// <summary>Verifies DetermineMergedLineStatus returns NotCovered for zero hits.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetermineMergedLineStatus_ZeroHits_NotCovered()
    {
        var result = CoverageService.DetermineMergedLineStatus(hits: 0, false, null, null);
        await Assert.That(result).IsEqualTo(LineVisitStatus.NotCovered);
    }

    /// <summary>Verifies DetermineMergedLineStatus returns Covered for non-branch with hits.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetermineMergedLineStatus_NonBranchWithHits_Covered()
    {
        var result = CoverageService.DetermineMergedLineStatus(hits: 5, false, null, null);
        await Assert.That(result).IsEqualTo(LineVisitStatus.Covered);
    }

    /// <summary>Verifies DetermineMergedLineStatus returns PartiallyCovered for partial branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetermineMergedLineStatus_PartialBranch_PartiallyCovered()
    {
        var result = CoverageService.DetermineMergedLineStatus(hits: 3, true, coveredBranches: 1, totalBranches: 2);
        await Assert.That(result).IsEqualTo(LineVisitStatus.PartiallyCovered);
    }

    /// <summary>Verifies DetermineMergedLineStatus returns Covered for fully covered branches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetermineMergedLineStatus_FullBranch_Covered()
    {
        var result = CoverageService.DetermineMergedLineStatus(hits: 3, true, coveredBranches: 2, totalBranches: 2);
        await Assert.That(result).IsEqualTo(LineVisitStatus.Covered);
    }

    /// <summary>Verifies DetermineMergedLineStatus returns Covered for branch without branch info.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetermineMergedLineStatus_BranchWithoutInfo_Covered()
    {
        var result = CoverageService.DetermineMergedLineStatus(hits: 1, true, null, null);
        await Assert.That(result).IsEqualTo(LineVisitStatus.Covered);
    }

    /// <summary>Verifies DetermineMergedLineStatus returns NotCovered for negative hits.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetermineMergedLineStatus_NegativeHits_NotCovered()
    {
        var result = CoverageService.DetermineMergedLineStatus(hits: -1, false, null, null);
        await Assert.That(result).IsEqualTo(LineVisitStatus.NotCovered);
    }

    /// <summary>Verifies MergeClassCoverage short-circuits for a single class.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeClassCoverage_SingleClass_ReturnsSame()
    {
        var cls = CreateClass(
            "Foo",
            RootFilePath,
            lines: [new LineCoverage(LineNumber: 1, Hits: 1, LineVisitStatus.Covered)],
            methods:
            [
                new MethodCoverage(
                    "Go",
                    "()",
                    LineCoverageRate: 1M,
                    BranchCoverageRate: 1M,
                    CyclomaticComplexity: 1M,
                    Lines: [new LineCoverage(LineNumber: 1, Hits: 1, LineVisitStatus.Covered)]),
            ]);

        var result = CoverageService.MergeClassCoverage([cls]);

        await Assert.That(ReferenceEquals(result, cls)).IsTrue();
    }

    /// <summary>Verifies MergeClassCoverage merges lines from two classes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeClassCoverage_TwoClasses_MergesLines()
    {
        var classA = CreateClass(
            "Foo",
            RootFilePath,
            lines: [
                new LineCoverage(LineNumber: 1, Hits: 5, LineVisitStatus.Covered),
                new LineCoverage(LineNumber: 2, Hits: 0, LineVisitStatus.NotCovered),
            ]);

        var classB = CreateClass(
            "Foo",
            RootFilePath,
            lines: [
                new LineCoverage(LineNumber: 1, Hits: 0, LineVisitStatus.NotCovered),
                new LineCoverage(LineNumber: 2, Hits: 3, LineVisitStatus.Covered),
            ]);

        var result = CoverageService.MergeClassCoverage([classA, classB]);

        await Assert.That(result.Name).IsEqualTo("Foo");
        await Assert.That(result.Lines).Count().IsEqualTo(expected: 2);
        await Assert.That(value: result.Lines[0].Hits).IsEqualTo(expected: 5);
        await Assert.That(value: result.Lines[1].Hits).IsEqualTo(expected: 3);
        await Assert.That(result.CoveredLineCount).IsEqualTo(expected: 2);
        await Assert.That(result.MissedLineCount).IsEqualTo(expected: 0);
    }

    /// <summary>Verifies MergeClassCoverage deduplicates methods by name and signature.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeClassCoverage_DuplicateMethods_Deduplicates()
    {
        var method = new MethodCoverage("Go", "()", LineCoverageRate: 1M, BranchCoverageRate: 1M, CyclomaticComplexity: 1M, []);
        var classA = CreateClass("Foo", RootFilePath, [], [method]);
        var classB = CreateClass("Foo", RootFilePath, [], [method]);

        var result = CoverageService.MergeClassCoverage([classA, classB]);

        await Assert.That(result.Methods).Count().IsEqualTo(expected: 1);
    }

    /// <summary>Verifies MergeClassCoverage keeps distinct methods from different classes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeClassCoverage_DistinctMethods_KeepsBoth()
    {
        var classA = CreateClass(
            "Foo",
            RootFilePath,
            [],
            methods: [new MethodCoverage("Go", "()", LineCoverageRate: 1M, BranchCoverageRate: 1M, CyclomaticComplexity: 1M, [])]);

        var classB = CreateClass(
            "Foo",
            RootFilePath,
            [],
            methods: [new MethodCoverage("Stop", "(int)", LineCoverageRate: 1M, BranchCoverageRate: 1M, CyclomaticComplexity: 1M, [])]);

        var result = CoverageService.MergeClassCoverage([classA, classB]);

        await Assert.That(result.Methods).Count().IsEqualTo(expected: 2);
    }

    /// <summary>Verifies MergeClassCoverage adds new lines from second class that don't exist in first.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeClassCoverage_DisjointLines_MergesBoth()
    {
        var classA = CreateClass("Foo", RootFilePath, lines: [new LineCoverage(LineNumber: 1, Hits: 1, LineVisitStatus.Covered)]);
        var classB = CreateClass("Foo", RootFilePath, lines: [new LineCoverage(LineNumber: 5, Hits: 1, LineVisitStatus.Covered)]);

        var result = CoverageService.MergeClassCoverage([classA, classB]);

        await Assert.That(result.Lines).Count().IsEqualTo(expected: 2);
        await Assert.That(value: result.Lines[0].LineNumber).IsEqualTo(expected: 1);
        await Assert.That(value: result.Lines[1].LineNumber).IsEqualTo(expected: 5);
    }

    /// <summary>Verifies LineNumberComparer sorts by line number correctly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LineNumberComparer_SortsByLineNumber()
    {
        var a = new LineCoverage(LineNumber: 20, Hits: 1, LineVisitStatus.Covered);
        var b = new LineCoverage(LineNumber: 10, Hits: 1, LineVisitStatus.Covered);

        var result = CoverageService.LineNumberComparer.Compare(a, b);
        await Assert.That(result).IsGreaterThan(minimum: 0);
    }

    /// <summary>Verifies LineNumberComparer returns zero for equal line numbers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LineNumberComparer_EqualLineNumbers_ReturnsZero()
    {
        var a = new LineCoverage(LineNumber: 10, Hits: 5, LineVisitStatus.Covered);
        var b = new LineCoverage(LineNumber: 10, Hits: 3, LineVisitStatus.Covered);

        var result = CoverageService.LineNumberComparer.Compare(a, b);
        await Assert.That(result).IsEqualTo(expected: 0);
    }

    /// <summary>Creates a <see cref="ClassCoverage"/> with the given name, file, and lines.</summary>
    /// <param name="name">The class name.</param>
    /// <param name="fileName">The source file path.</param>
    /// <param name="lines">The line coverage entries.</param>
    /// <param name="methods">Optional method coverage entries.</param>
    /// <returns>A new <see cref="ClassCoverage"/> instance.</returns>
    private static ClassCoverage CreateClass(
        string name,
        string fileName,
        List<LineCoverage> lines,
        List<MethodCoverage>? methods = null) => new(
        name,
        fileName,
        null,
        null,
        null,
        methods ?? [],
        lines);
}
