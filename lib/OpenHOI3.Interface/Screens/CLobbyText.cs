using System;
using System.Globalization;
using System.Linq;
using OpenHOI3.Interface.Messages;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the lobby's rows and its country panel say, as the original writes them. See <c>docs/mechanics/frontend.md</c>,
/// section 3; <c>reversing/findings/FINDINGS-frontend.md</c>, B.2 to B.6.
/// </summary>
public static class CLobbyText
{
	/// <summary>The name a loaded save goes by in the year label: SAVED_GAMES, "Saved Game".</summary>
	public const string SavedGameKey = "SAVED_GAMES";

	private static readonly string[] Months =
	{
		"January", "February", "March", "April", "May", "June",
		"July", "August", "September", "October", "November", "December",
	};

	/// <summary>A row's date: " 13:00, 1 January, 1936", a space first.</summary>
	public static string Date(Func<string, string> localise, long hours)
	{
		long at = Math.Max(hours, 0);
		return CMessageText.Date(localise, CDate.FromDayNumber((int)(at / CGameState.HoursPerDay)), (int)(at % CGameState.HoursPerDay));
	}

	/// <summary>WORLD_YEAR: "§Y$NAME$§W, $DAY$ of $MONTH$, $YEAR$" - the start's name or "Saved Game", and the world's date.</summary>
	public static string YearLabel(Func<string, string> localise, string name, CDate date)
	{
		return CTextTemplate.Fill(localise("WORLD_YEAR"), ("NAME", name),
			("DAY", date.Day.ToString(CultureInfo.InvariantCulture)), ("MONTH", localise(Months[Math.Clamp(date.Month, 1, 12) - 1])),
			("YEAR", date.Year.ToString(CultureInfo.InvariantCulture)));
	}

	/// <summary>ATWAR "At War." where the country is on a side of any war, else ATPEACE "At Peace."; its enemies are not named.</summary>
	public static string Wars(Func<string, string> localise, CGameState state, CCountry country)
	{
		bool atWar = state.Wars.Any(war => war.Attackers.Contains(country.Tag) || war.Defenders.Contains(country.Tag));
		return localise(atWar ? "ATWAR" : "ATPEACE");
	}

	/// <summary>The form of government's name, which the panel's <c>selected_fog</c> shows.</summary>
	public static string Government(Func<string, string> localise, CCountry country)
	{
		return country.GovernmentType.Length > 0 ? localise(country.GovernmentType) : "";
	}

	/// <summary>The name of who holds the head of state's post, or nothing.</summary>
	public static string HeadOfState(CCountry country)
	{
		return country.Government.TryGetValue(HeadOfStatePost, out CMinister? head) ? head.Name : "";
	}

	public const string HeadOfStatePost = "head_of_state";

	/// <summary>Whether a country may be played: it owns a province. The rebels and a country of no land may not.</summary>
	public static bool MayPlay(CGameState state, CCountry country)
	{
		return COwnedCounts.Of(state).Count(country.Tag) > 0;
	}

	/// <summary>
	/// Why Play may not be pressed, the first that fails, or null where it may: NOT_SELECT with no country, NOT_EXIST with
	/// one owning no land, then on the saved games' tab CANT_START_REASON_SELECT_SAVE_GAME with no save loaded, and on the
	/// start's tab CANT_START_REASON_SELECT_BOOKMARK with one loaded.
	/// </summary>
	public static string? WhyNotPlay(Func<string, string> localise, CGameState state, CCountry? chosen, bool savesShown,
		bool saveLoaded)
	{
		if (chosen is null)
		{
			return localise("NOT_SELECT");
		}
		if (!MayPlay(state, chosen))
		{
			return CTextTemplate.Fill(localise("NOT_EXIST"), ("TAG", chosen.Name));
		}
		if (savesShown && !saveLoaded)
		{
			return localise("CANT_START_REASON_SELECT_SAVE_GAME");
		}
		if (!savesShown && saveLoaded)
		{
			return localise("CANT_START_REASON_SELECT_BOOKMARK");
		}
		return null;
	}
}
