// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace UnitTestMcp.Core.Services;

/// <summary>
/// Extension methods for <see cref="StringComparer"/> to support tuple-based grouping.
/// </summary>
internal static class StringComparerExtensions
{
    /// <summary>
    /// Creates a tuple equality comparer for (string Name, string FileName) keys
    /// using the specified <see cref="StringComparer"/> for both elements.
    /// </summary>
    /// <param name="comparer">The string comparer to use for both tuple elements.</param>
    /// <returns>An equality comparer for named tuples of two strings.</returns>
    public static IEqualityComparer<(string Name, string FileName)> CreateTupleComparer(StringComparer comparer) =>
        new TupleStringComparer(comparer);

    /// <summary>
    /// Equality comparer for named tuples of two strings using a shared <see cref="StringComparer"/>.
    /// </summary>
    /// <param name="comparer">The underlying string comparer for both elements.</param>
    private sealed class TupleStringComparer(StringComparer comparer) : IEqualityComparer<(string Name, string FileName)>
    {
        /// <inheritdoc/>
        public bool Equals((string Name, string FileName) x, (string Name, string FileName) y) =>
            comparer.Equals(x.Name, y.Name) && comparer.Equals(x.FileName, y.FileName);

        /// <inheritdoc/>
        public int GetHashCode((string Name, string FileName) obj) =>
            HashCode.Combine(comparer.GetHashCode(obj.Name), comparer.GetHashCode(obj.FileName));
    }
}
