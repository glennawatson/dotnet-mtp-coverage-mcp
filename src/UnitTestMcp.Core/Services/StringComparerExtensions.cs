// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace UnitTestMcp.Core.Services;

/// <summary>Extension methods for <see cref="StringComparer"/> to support tuple-based grouping.</summary>
internal static class StringComparerExtensions
{
    /// <summary>Provides tuple comparers using the same comparison for both strings.</summary>
    /// <param name="comparer">The comparison applied to both tuple elements.</param>
    extension(StringComparer comparer)
    {
        /// <summary>Creates an equality comparer for named tuples of two strings.</summary>
        /// <returns>The tuple comparer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IEqualityComparer<(string Name, string FileName)> CreateTupleComparer() => new TupleStringComparer(comparer);
    }

    /// <summary>Equality comparer for named tuples of two strings using a shared <see cref="StringComparer"/>.</summary>
    /// <param name="comparer">The underlying string comparer for both elements.</param>
    [DebuggerDisplay("Tuple string comparer")]
    private sealed class TupleStringComparer(StringComparer comparer) : IEqualityComparer<(string Name, string FileName)>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals((string Name, string FileName) x, (string Name, string FileName) y) =>
            comparer.Equals(x.Name, y.Name) && comparer.Equals(x.FileName, y.FileName);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode((string Name, string FileName) obj) =>
            HashCode.Combine(comparer.GetHashCode(obj.Name), comparer.GetHashCode(obj.FileName));
    }
}
