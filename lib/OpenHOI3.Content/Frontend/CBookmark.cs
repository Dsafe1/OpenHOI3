using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Content.Frontend;

/// <summary>
/// A bookmark of <c>common/bookmarks.txt</c>: a start the lobby offers - its picture, its name and description as
/// localisation keys, its hour as hours since the calendar's start, and the countries it offers as shortcuts. Nothing of it
/// but its date reaches the game. See <c>docs/mechanics/bookmarks.md</c>, section 1.
/// </summary>
public sealed record CBookmark(string Icon, string Name, string Description, long Hours, IReadOnlyList<CCountryTag> Countries)
{
	/// <summary>The bookmark's day.</summary>
	public CDate Date => CDate.FromDayNumber((int)(Hours / CGameState.HoursPerDay));

	/// <summary>The bookmark's hour of its day, nought where the file gives none.</summary>
	public int Hour => (int)(Hours % CGameState.HoursPerDay);
}
