using System;
using System.Collections.Generic;

namespace OpenHOI3.Content.Windows;

/// <summary>
/// What a page's Lua says of the elements of one window - the page's own, or one row of a list in it - each by the
/// element's name (<c>docs/mechanics/modwindows.md</c>, section 3): a text for a text box or a button, and whether an
/// element is shown. An element not named is left as its file made it.
/// </summary>
public sealed class CModWindowRow
{
	/// <summary>The texts, by element name.</summary>
	public Dictionary<string, string> Texts { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>Whether an element is shown, by element name.</summary>
	public Dictionary<string, bool> Shown { get; } = new(StringComparer.OrdinalIgnoreCase);
}
