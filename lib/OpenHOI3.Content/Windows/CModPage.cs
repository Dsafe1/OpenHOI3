namespace OpenHOI3.Content.Windows;

/// <summary>
/// A page of the window a mod defines for an enhanced game (<c>docs/mechanics/modwindows.md</c>): one Lua file of the
/// mod's <c>script/openhoi/windows</c>, which gathers what it shows and draws it.
/// </summary>
/// <param name="Name">The page's name: its file's, without the extension.</param>
/// <param name="Title">The localisation key of its name, in its tab's list of pages.</param>
/// <param name="Order">Where it stands in that list: lower first, and by name among equals.</param>
/// <param name="Group">The localisation key of the tab it stands under; empty for a page that names none.</param>
/// <param name="GroupOrder">Where its tab stands among the tabs: the lowest any of the tab's pages gives, lower first.</param>
public sealed record CModPage(string Name, string Title, int Order, string Group = "", int GroupOrder = 0,
	string Window = "", string Host = "", string Opener = "")
{
	/// <summary>
	/// Whether the page fills a window of the interface files and is no page of the mod's own windows: it names one
	/// (<c>docs/mechanics/nativepages.md</c>).
	/// </summary>
	public bool IsNative => Window.Length > 0;
}
