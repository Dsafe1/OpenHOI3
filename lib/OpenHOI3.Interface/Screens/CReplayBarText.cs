using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the bar over a playback says and lists (<c>docs/replay.md</c>, section 16): that the game is a replay, the
/// country looked through, and the countries that may be looked through instead - those owning land, the ones a
/// person played first, then the rest by name.
/// </summary>
public static class CReplayBarText
{
	public const string TitleKey = "OPENHOI_REPLAY_BAR";
	public const string ChooseKey = "OPENHOI_REPLAY_BAR_CHOOSE";
	public const string ChooseTipKey = "OPENHOI_REPLAY_BAR_CHOOSE_TIP";

	public const string Title = "Replay";
	public const string Choose = "Change country";
	public const string ChooseTip = "Looks on through another country: its screens, its units and what it knows of the map. "
		+ "The game played back is not changed.";

	/// <summary>The country looked through as the bar writes it: its flag, where text draws one, and its name.</summary>
	public static string Country(CCountry country) => "@" + country.Tag.Value + " " + country.Name;

	/// <summary>A row of the list: the name, marked where a person played the country.</summary>
	public static string Row(CCountry country, bool played) => played ? country.Name + " *" : country.Name;

	/// <summary>The countries of the list in its order.</summary>
	public static List<CCountry> Countries(CGameState state)
	{
		COwnedCounts owned = COwnedCounts.Of(state);
		return state.Countries.Where(country => owned.Count(country.Tag) > 0 || state.Played.Contains(country.Tag))
			.OrderByDescending(country => state.Played.Contains(country.Tag))
			.ThenBy(country => country.Name, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}
}
