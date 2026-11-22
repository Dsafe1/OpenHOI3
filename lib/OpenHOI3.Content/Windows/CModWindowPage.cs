namespace OpenHOI3.Content.Windows;

/// <summary>
/// A window a mod defines for an enhanced game (<c>docs/mechanics/modwindows.md</c>): one Lua file of the mod's
/// <c>script/openhoi/windows</c>, which says which window of its <c>interface/openhoi</c> it fills.
/// </summary>
/// <param name="Name">The page's name: its file's, without the extension. What a click and a collect name it by.</param>
/// <param name="Window">The window type it fills, a top-level <c>windowType</c>.</param>
/// <param name="Title">The localisation key of its name on the menu of pages.</param>
/// <param name="Order">Where it stands on the menu: lower first, and by name among equals.</param>
public sealed record CModWindowPage(string Name, string Window, string Title, int Order);
