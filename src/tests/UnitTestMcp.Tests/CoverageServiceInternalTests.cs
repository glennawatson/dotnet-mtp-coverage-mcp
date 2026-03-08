// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using TUnit.Assertions.Extensions;
using TUnit.Core;

using UnitTestMcp.Core.Models;
using UnitTestMcp.Core.Services;

namespace UnitTestMcp.Tests;

/// <summary>
/// Tests for the internal methods of <see cref="CoverageService"/>.
/// </summary>
public class CoverageServiceInternalTests
{
    /// <summary>
    /// Verifies NormalizePath converts backslashes to forward slashes.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NormalizePath_BackslashesToForward()
    {
        var result = CoverageService.NormalizePath(@"C:\src\Foo.cs");
        await Assert.That(result).IsEqualTo("C:/src/Foo.cs");
    }

    /// <summary>
    /// Verifies NormalizePath leaves forward slashes unchanged.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NormalizePath_ForwardSlashes_Unchanged()
    {
        var result = CoverageService.NormalizePath("/src/Foo.cs");
        await Assert.That(result).IsEqualTo("/src/Foo.cs");
    }

    /// <summary>
    /// Verifies MergeSortedLines returns empty list for empty input.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeSortedLines_EmptyInput_ReturnsEmpty()
    {
        var result = CoverageService.MergeSortedLines([]);
        await Assert.That(result).Count().IsEqualTo(0);
    }

    /// <summary>
    /// Verifies MergeSortedLines merges lines from multiple classes sorted by line number.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeSortedLines_MultipleClasses_MergesSorted()
    {
        var classA = CreateClass(
            "A",
            "/a.cs",
            [
                new LineCoverage(10, 1, LineVisitStatus.Covered),
                new LineCoverage(30, 1, LineVisitStatus.Covered),
            ]);

        var classB = CreateClass(
            "B",
            "/b.cs",
            [
                new LineCoverage(20, 1, LineVisitStatus.Covered),
                new LineCoverage(40, 1, LineVisitStatus.Covered),
            ]);

        var result = CoverageService.MergeSortedLines([classA, classB]);

        await Assert.That(result).Count().IsEqualTo(4);
        await Assert.That(result[0].LineNumber).IsEqualTo(10);
        await Assert.That(result[1].LineNumber).IsEqualTo(20);
        await Assert.That(result[2].LineNumber).IsEqualTo(30);
        await Assert.That(result[3].LineNumber).IsEqualTo(40);
    }

    /// <summary>
    /// Verifies MergeSortedLines deduplicates lines with the same line number.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeSortedLines_DuplicateLines_Deduplicates()
    {
        var classA = CreateClass("A", "/a.cs", [new LineCoverage(10, 5, LineVisitStatus.Covered)]);
        var classB = CreateClass("B", "/b.cs", [new LineCoverage(10, 3, LineVisitStatus.Covered)]);

        var result = CoverageService.MergeSortedLines([classA, classB]);

        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].LineNumber).IsEqualTo(10);
    }

    /// <summary>
    /// Verifies CategorizeLines separates missed and partially covered lines.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CategorizeLines_SeparatesMissedAndPartial()
    {
        var lines = new List<LineCoverage>
        {
            new(1, 5, LineVisitStatus.Covered),
            new(2, 0, LineVisitStatus.NotCovered),
            new(3, 3, LineVisitStatus.PartiallyCovered, true, 1, 2),
            new(4, 0, LineVisitStatus.NotCovered),
            new(5, 1, LineVisitStatus.Covered),
        };

        var result = CoverageService.CategorizeLines("/src/Foo.cs", lines);

        await Assert.That(result.FilePath).IsEqualTo("/src/Foo.cs");
        await Assert.That(result.MissedLines).Count().IsEqualTo(2);
        await Assert.That(result.MissedLines[0].LineNumber).IsEqualTo(2);
        await Assert.That(result.MissedLines[1].LineNumber).IsEqualTo(4);
        await Assert.That(result.PartiallyMissedBranches).Count().IsEqualTo(1);
        await Assert.That(result.PartiallyMissedBranches[0].LineNumber).IsEqualTo(3);
    }

    /// <summary>
    /// Verifies CategorizeLines returns empty lists when all lines are covered.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CategorizeLines_AllCovered_ReturnsEmptyLists()
    {
        var lines = new List<LineCoverage>
        {
            new(1, 5, LineVisitStatus.Covered),
            new(2, 3, LineVisitStatus.Covered),
        };

        var result = CoverageService.CategorizeLines("/src/Foo.cs", lines);

        await Assert.That(result.MissedLines).Count().IsEqualTo(0);
        await Assert.That(result.PartiallyMissedBranches).Count().IsEqualTo(0);
    }

    /// <summary>
    /// Verifies MergeLineCoverage takes maximum hits.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeLineCoverage_TakesMaxHits()
    {
        var a = new LineCoverage(10, 3, LineVisitStatus.Covered);
        var b = new LineCoverage(10, 7, LineVisitStatus.Covered);

        var result = CoverageService.MergeLineCoverage(a, b);

        await Assert.That(result.LineNumber).IsEqualTo(10);
        await Assert.That(result.Hits).IsEqualTo(7);
    }

    /// <summary>
    /// Verifies MergeLineCoverage takes best branch coverage.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeLineCoverage_TakesBestBranches()
    {
        var a = new LineCoverage(10, 3, LineVisitStatus.PartiallyCovered, true, 1, 4);
        var b = new LineCoverage(10, 2, LineVisitStatus.PartiallyCovered, true, 3, 4);

        var result = CoverageService.MergeLineCoverage(a, b);

        await Assert.That(result.CoveredBranches).IsEqualTo(3);
        await Assert.That(result.TotalBranches).IsEqualTo(4);
        await Assert.That(result.IsBranch).IsTrue();
    }

    /// <summary>
    /// Verifies MergeLineCoverage promotes non-branch to branch when one is a branch.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeLineCoverage_PromotesToBranch()
    {
        var a = new LineCoverage(10, 3, LineVisitStatus.Covered);
        var b = new LineCoverage(10, 2, LineVisitStatus.PartiallyCovered, true, 1, 2);

        var result = CoverageService.MergeLineCoverage(a, b);

        await Assert.That(result.IsBranch).IsTrue();
        await Assert.That(result.CoveredBranches).IsEqualTo(1);
        await Assert.That(result.TotalBranches).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies MergeLineCoverage produces Covered status when max hits covers all branches.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeLineCoverage_FullBranch_ReturnsCovered()
    {
        var a = new LineCoverage(10, 3, LineVisitStatus.PartiallyCovered, true, 1, 2);
        var b = new LineCoverage(10, 5, LineVisitStatus.Covered, true, 2, 2);

        var result = CoverageService.MergeLineCoverage(a, b);

        await Assert.That(result.Status).IsEqualTo(LineVisitStatus.Covered);
        await Assert.That(result.CoveredBranches).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies MergeLineCoverage returns NotCovered when both have zero hits.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeLineCoverage_BothZeroHits_ReturnsNotCovered()
    {
        var a = new LineCoverage(10, 0, LineVisitStatus.NotCovered);
        var b = new LineCoverage(10, 0, LineVisitStatus.NotCovered);

        var result = CoverageService.MergeLineCoverage(a, b);

        await Assert.That(result.Status).IsEqualTo(LineVisitStatus.NotCovered);
        await Assert.That(result.Hits).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies DetermineMergedLineStatus returns NotCovered for zero hits.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetermineMergedLineStatus_ZeroHits_NotCovered()
    {
        var result = CoverageService.DetermineMergedLineStatus(0, false, null, null);
        await Assert.That(result).IsEqualTo(LineVisitStatus.NotCovered);
    }

    /// <summary>
    /// Verifies DetermineMergedLineStatus returns Covered for non-branch with hits.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetermineMergedLineStatus_NonBranchWithHits_Covered()
    {
        var result = CoverageService.DetermineMergedLineStatus(5, false, null, null);
        await Assert.That(result).IsEqualTo(LineVisitStatus.Covered);
    }

    /// <summary>
    /// Verifies DetermineMergedLineStatus returns PartiallyCovered for partial branches.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetermineMergedLineStatus_PartialBranch_PartiallyCovered()
    {
        var result = CoverageService.DetermineMergedLineStatus(3, true, 1, 2);
        await Assert.That(result).IsEqualTo(LineVisitStatus.PartiallyCovered);
    }

    /// <summary>
    /// Verifies DetermineMergedLineStatus returns Covered for fully covered branches.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetermineMergedLineStatus_FullBranch_Covered()
    {
        var result = CoverageService.DetermineMergedLineStatus(3, true, 2, 2);
        await Assert.That(result).IsEqualTo(LineVisitStatus.Covered);
    }

    /// <summary>
    /// Verifies DetermineMergedLineStatus returns Covered for branch without branch info.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetermineMergedLineStatus_BranchWithoutInfo_Covered()
    {
        var result = CoverageService.DetermineMergedLineStatus(1, true, null, null);
        await Assert.That(result).IsEqualTo(LineVisitStatus.Covered);
    }

    /// <summary>
    /// Verifies DetermineMergedLineStatus returns NotCovered for negative hits.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetermineMergedLineStatus_NegativeHits_NotCovered()
    {
        var result = CoverageService.DetermineMergedLineStatus(-1, false, null, null);
        await Assert.That(result).IsEqualTo(LineVisitStatus.NotCovered);
    }

    /// <summary>
    /// Verifies MergeClassCoverage short-circuits for a single class.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeClassCoverage_SingleClass_ReturnsSame()
    {
        var cls = CreateClass(
            "Foo",
            "/Foo.cs",
            [new LineCoverage(1, 1, LineVisitStatus.Covered)],
            [new MethodCoverage("Go", "()", 1m, 1m, 1m, [new LineCoverage(1, 1, LineVisitStatus.Covered)])]);

        var result = CoverageService.MergeClassCoverage([cls]);

        await Assert.That(ReferenceEquals(result, cls)).IsTrue();
    }

    /// <summary>
    /// Verifies MergeClassCoverage merges lines from two classes.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeClassCoverage_TwoClasses_MergesLines()
    {
        var classA = CreateClass(
            "Foo",
            "/Foo.cs",
            [
                new LineCoverage(1, 5, LineVisitStatus.Covered),
                new LineCoverage(2, 0, LineVisitStatus.NotCovered),
            ]);

        var classB = CreateClass(
            "Foo",
            "/Foo.cs",
            [
                new LineCoverage(1, 0, LineVisitStatus.NotCovered),
                new LineCoverage(2, 3, LineVisitStatus.Covered),
            ]);

        var result = CoverageService.MergeClassCoverage([classA, classB]);

        await Assert.That(result.Name).IsEqualTo("Foo");
        await Assert.That(result.Lines).Count().IsEqualTo(2);
        await Assert.That(result.Lines[0].Hits).IsEqualTo(5);
        await Assert.That(result.Lines[1].Hits).IsEqualTo(3);
        await Assert.That(result.CoveredLineCount).IsEqualTo(2);
        await Assert.That(result.MissedLineCount).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies MergeClassCoverage deduplicates methods by name and signature.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeClassCoverage_DuplicateMethods_Deduplicates()
    {
        var method = new MethodCoverage("Go", "()", 1m, 1m, 1m, []);
        var classA = CreateClass("Foo", "/Foo.cs", [], [method]);
        var classB = CreateClass("Foo", "/Foo.cs", [], [method]);

        var result = CoverageService.MergeClassCoverage([classA, classB]);

        await Assert.That(result.Methods).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Verifies MergeClassCoverage keeps distinct methods from different classes.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeClassCoverage_DistinctMethods_KeepsBoth()
    {
        var classA = CreateClass(
            "Foo",
            "/Foo.cs",
            [],
            [new MethodCoverage("Go", "()", 1m, 1m, 1m, [])]);

        var classB = CreateClass(
            "Foo",
            "/Foo.cs",
            [],
            [new MethodCoverage("Stop", "(int)", 1m, 1m, 1m, [])]);

        var result = CoverageService.MergeClassCoverage([classA, classB]);

        await Assert.That(result.Methods).Count().IsEqualTo(2);
    }

    /// <summary>
    /// Verifies MergeClassCoverage adds new lines from second class that don't exist in first.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MergeClassCoverage_DisjointLines_MergesBoth()
    {
        var classA = CreateClass("Foo", "/Foo.cs", [new LineCoverage(1, 1, LineVisitStatus.Covered)]);
        var classB = CreateClass("Foo", "/Foo.cs", [new LineCoverage(5, 1, LineVisitStatus.Covered)]);

        var result = CoverageService.MergeClassCoverage([classA, classB]);

        await Assert.That(result.Lines).Count().IsEqualTo(2);
        await Assert.That(result.Lines[0].LineNumber).IsEqualTo(1);
        await Assert.That(result.Lines[1].LineNumber).IsEqualTo(5);
    }

    /// <summary>
    /// Verifies LineNumberComparer sorts by line number correctly.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LineNumberComparer_SortsByLineNumber()
    {
        var a = new LineCoverage(20, 1, LineVisitStatus.Covered);
        var b = new LineCoverage(10, 1, LineVisitStatus.Covered);

        var result = CoverageService.LineNumberComparer.Compare(a, b);
        await Assert.That(result).IsGreaterThan(0);
    }

    /// <summary>
    /// Verifies LineNumberComparer returns zero for equal line numbers.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LineNumberComparer_EqualLineNumbers_ReturnsZero()
    {
        var a = new LineCoverage(10, 5, LineVisitStatus.Covered);
        var b = new LineCoverage(10, 3, LineVisitStatus.Covered);

        var result = CoverageService.LineNumberComparer.Compare(a, b);
        await Assert.That(result).IsEqualTo(0);
    }

    /// <summary>
    /// Creates a <see cref="ClassCoverage"/> with the given name, file, and lines.
    /// </summary>
    /// <param name="name">The class name.</param>
    /// <param name="fileName">The source file path.</param>
    /// <param name="lines">The line coverage entries.</param>
    /// <param name="methods">Optional method coverage entries.</param>
    /// <returns>A new <see cref="ClassCoverage"/> instance.</returns>
    private static ClassCoverage CreateClass(
        string name,
        string fileName,
        List<LineCoverage> lines,
        List<MethodCoverage>? methods = null)
    {
        return new ClassCoverage(
            name,
            fileName,
            null,
            null,
            null,
            methods ?? [],
            lines);
    }
}
