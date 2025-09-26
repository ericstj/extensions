// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Shared.Collections;

/// <summary>
/// Provides efficient replacement operations on lists with lazy allocation and minimal copying.
/// Only allocates a new list when replacements are actually needed.
/// </summary>
/// <typeparam name="T">The type of items in the list.</typeparam>
internal ref struct ListReplacer<T>
{
    private readonly IList<T> _originalList;
    private List<T>? _newList;
    private int _currentIndex;

    /// <summary>Initializes a new instance of the <see cref="ListReplacer{T}"/> struct.</summary>
    /// <param name="originalList">The original list to potentially replace items in.</param>
    public ListReplacer(IList<T> originalList)
    {
        _originalList = originalList;
        _newList = null;
        _currentIndex = 0;
    }

    /// <summary>
    /// Gets the current item being processed.
    /// </summary>
    /// <returns>The current item, or default if at end of list.</returns>
    public readonly T Current => _currentIndex < _originalList.Count ? _originalList[_currentIndex] : default!;

    /// <summary>
    /// Gets a value indicating whether there are more items to process.
    /// </summary>
    public readonly bool HasMore => _currentIndex < _originalList.Count;

    /// <summary>
    /// Keeps the current item and advances to the next.
    /// </summary>
    public void Keep()
    {
        EnsureInRange();

        // add if we've already started a new list
        _newList?.Add(_originalList[_currentIndex]);
        _currentIndex++;
    }

    /// <summary>
    /// Skips the current item (removes it) and advances to the next.
    /// </summary>
    public void Skip()
    {
        EnsureInRange();

        _newList ??= CreateNew();
        _currentIndex++;
    }

    /// <summary>
    /// Replaces the current item with a single new item and advances to the next.
    /// </summary>
    /// <param name="replacement">The replacement item.</param>
    public void Replace(T replacement)
    {
        EnsureInRange();

        _newList ??= CreateNew();
        _newList.Add(replacement);
        _currentIndex++;
    }

    /// <summary>
    /// Replaces the current item with multiple new items and advances to the next.
    /// </summary>
    /// <param name="replacements">The replacement items as a read-only span.</param>
    /// <returns>True if there are more items; false if at end.</returns>
    public bool Replace(ReadOnlySpan<T> replacements)
    {
        EnsureInRange();

        _newList ??= CreateNew(replacements.Length - 1);
        foreach (var item in replacements)
        {
            _newList.Add(item);
        }

        _currentIndex++;
        return HasMore;
    }

    /// <summary>
    /// Replaces the current item with multiple new items and advances to the next.
    /// </summary>
    /// <param name="replacements">The replacement items as a read-only span.</param>
    public void Replace(ICollection<T> replacements)
    {
        EnsureInRange();

        _newList ??= CreateNew(replacements.Count - 1);
        foreach (var item in replacements)
        {
            _newList!.Add(item);
        }

        _currentIndex++;
    }

    /// <summary>
    /// Gets the final result list. Returns the original list if no replacements were made.
    /// </summary>
    /// <returns>The original list if unchanged, or a new list with replacements applied.</returns>
    public readonly IList<T> GetResult() => _newList ?? _originalList;

    private readonly void EnsureInRange()
    {
        if (_currentIndex >= _originalList.Count)
        {
            throw new InvalidOperationException("No more items to process.");
        }
    }

    private readonly List<T> CreateNew(int additionalCapacity = 0)
    {
        var newList = new List<T>(_originalList.Count + additionalCapacity);

        // Copy all items up to and excluding the current index
        for (int i = 0; i < _currentIndex; i++)
        {
            newList.Add(_originalList[i]);
        }

        return newList;
    }
}
