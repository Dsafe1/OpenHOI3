using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenHOI3.Content.Windows;

/// <summary>
/// A tab of the window a mod defines, and the pages listed under it (<c>docs/mechanics/modwindows.md</c>, section 5).
/// Nothing declares the tabs: a tab is there because a page names it, so a submod's page adds to a tab or brings a new
/// one by its file alone.
/// </summary>
/// <param name="Key">The localisation key of the tab's name, which is what makes two pages one tab's; empty for the pages that name none.</param>
/// <param name="Pages">Its pages, in the list's order.</param>
public sealed record CModPageGroup(string Key, IReadOnlyList<CModPage> Pages)
{
	/// <summary>
	/// The pages sorted into tabs: the tabs by the lowest <see cref="CModPage.GroupOrder"/> any of their pages gives and
	/// then by key, a tab's pages by their <see cref="CModPage.Order"/> and then by name.
	/// </summary>
	public static List<CModPageGroup> Of(IEnumerable<CModPage> pages)
	{
		return pages.GroupBy(page => page.Group, StringComparer.Ordinal)
			.Select(group => (Key: group.Key, Order: group.Min(page => page.GroupOrder),
				Pages: group.OrderBy(page => page.Order).ThenBy(page => page.Name, StringComparer.Ordinal).ToList()))
			.OrderBy(group => group.Order).ThenBy(group => group.Key, StringComparer.Ordinal)
			.Select(group => new CModPageGroup(group.Key, group.Pages))
			.ToList();
	}
}
