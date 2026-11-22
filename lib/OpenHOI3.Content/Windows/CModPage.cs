namespace OpenHOI3.Content.Windows;

/// <summary>
/// A window a mod defines for an enhanced game (<c>docs/mechanics/modwindows.md</c>): one Lua file of the mod's
/// <c>script/openhoi/windows</c>, which gathers what it shows and draws it.
/// </summary>
/// <param name="Name">The page's name: its file's, without the extension.</param>
/// <param name="Title">The localisation key of its name, on the menu of pages and on its own window.</param>
/// <param name="Order">Where it stands on the menu: lower first, and by name among equals.</param>
public sealed record CModPage(string Name, string Title, int Order);
