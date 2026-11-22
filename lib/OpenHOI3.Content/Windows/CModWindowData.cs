using System;
using System.Collections.Generic;

namespace OpenHOI3.Content.Windows;

/// <summary>
/// Everything a page's <c>Collect</c> answered, as the screen applies it (<c>docs/mechanics/modwindows.md</c>, section
/// 3): the window's own elements, and for each list box named its rows in order, each a window of the list's entry
/// type with elements of its own.
/// </summary>
public sealed class CModWindowData
{
	/// <summary>The window's own texts and what of it is shown.</summary>
	public CModWindowRow Window { get; } = new();

	/// <summary>The rows of each list box named, in order.</summary>
	public Dictionary<string, List<CModWindowRow>> Lists { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>The window type a list box's rows are made of: the list's name and <c>_entry</c>.</summary>
	public static string EntryType(string listBox) => listBox + "_entry";
}
