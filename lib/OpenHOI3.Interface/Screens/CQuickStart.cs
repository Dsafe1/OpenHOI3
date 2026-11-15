using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// One of the Quick Start panel's four buttons (<c>easymode_panel</c>): the button's name, the hour its game begins at and
/// the country it plays. The dates and tags are the executable's, not a file's - no bookmark is read, and the defines'
/// date range does not hold them - and each begins at its day's hour 1, where a bookmark of the same day begins at hour 0
/// (<c>docs/mechanics/bookmarks.md</c>, section 4; <c>reversing/findings/FINDINGS-bookmarks.md</c>, section 1.5). Japan's
/// 7 December 1941 is no bookmark's date.
/// </summary>
public sealed record CQuickStart(string ButtonName, CDate Date, CCountryTag Tag)
{
	/// <summary>The hour of its day a Quick Start game begins at [exe].</summary>
	public const int Hour = 1;

	/// <summary>The hour the game begins at, as hours since the calendar's start.</summary>
	public long Hours => (long)Date.DayNumber * CGameState.HoursPerDay + Hour;

	/// <summary>The four, in the order the original wires them [exe].</summary>
	public static readonly IReadOnlyList<CQuickStart> All = new[]
	{
		new CQuickStart("quickstart_ger", new CDate(1939, 9, 1), new CCountryTag("GER")),
		new CQuickStart("quickstart_usa", new CDate(1936, 1, 1), new CCountryTag("USA")),
		new CQuickStart("quickstart_jap", new CDate(1941, 12, 7), new CCountryTag("JAP")),
		new CQuickStart("quickstart_sov", new CDate(1941, 6, 22), new CCountryTag("SOV")),
	};
}
