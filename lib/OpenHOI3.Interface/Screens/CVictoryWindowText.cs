using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Interface.Script;
using OpenHOI3.Interface.Text;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the victory conditions' two windows say - <c>vc_window</c> in play and the lobby's <c>victory_conditions_window</c> - and alert 14.
/// See <c>docs/mechanics/endgame.md</c>, sections 5 to 7.
/// </summary>
public static class CVictoryWindowText
{
	public const string WindowName = "vc_window";
	public const string RowName = "vc_entry";
	public const string ButtonName = "menubar_goals_button";
	public const string ButtonTooltip = "VC_BUTTON_TOOLTIP";
	public const string SelectWindowName = "victory_conditions_window";
	public const string SelectRowName = "condition_entry";
	public const string SelectButtonName = "victoryconditions_button";

	/// <summary>The frame of a row's background and of the screen's row: 2 where met, else 1.</summary>
	public static int Frame(CVictoryCondition condition) => condition.Met ? 2 : 1;

	/// <summary>Which of the window's three pictures goes with a faction, by its exact tag; null for any other.</summary>
	public static string? Picture(string faction) => faction switch
	{
		"axis" => "vc_bg_axis",
		"allies" => "vc_bg_allies",
		"comintern" => "vc_bg_com",
		_ => null,
	};

	/// <summary><c>vc_&lt;f&gt;_progress_text</c>: met, a slash and the number each faction chooses, which is more than it takes to win.</summary>
	public static string ProgressText(CGameState state, string faction)
	{
		return Count(CVictoryConditions.Met(state, faction), CVictoryConditions.Number(state));
	}

	/// <summary><c>vc_&lt;f&gt;_progress</c>, in hundredths.</summary>
	public static int Progress(CGameState state, string faction)
	{
		return CVictoryConditions.Met(state, faction) * 100 / CVictoryConditions.Number(state);
	}

	private static string Count(int a, int b) => a.ToString(CultureInfo.InvariantCulture) + " / " + b.ToString(CultureInfo.InvariantCulture);

	/// <summary>A row's tooltip: the condition's trigger written out, with no country in scope.</summary>
	public static string RowTooltip(CScriptWords words, CVictoryCondition condition)
	{
		return CConditionText.For(words, condition.Trigger, CEventScope.Start(null));
	}

	/// <summary>Whether a faction but the player's has met three quarters of what wins: the factions it counts, in their order.</summary>
	public static List<string> AlertFactions(CGameState state, CCountry player)
	{
		int toWin = CVictoryConditions.ToWin(state);
		var factions = new List<string>();
		foreach ((string faction, _) in state.VictoryConditions)
		{
			if (faction != player.Faction && CVictoryConditions.Met(state, faction) * 100 / toWin >= 75)
			{
				factions.Add(faction);
			}
		}
		return factions;
	}

	/// <summary>Alert 14's tooltip: its delayed half names the last of the factions, each having written over the one before.</summary>
	public static CTooltip AlertTooltip(Func<string, string> localise, CGameState state, CCountry player)
	{
		string delayed = "";
		foreach (string faction in AlertFactions(state, player))
		{
			delayed = CTextTemplate.Fill(localise("VC_DANGER"), ("FACTION", localise(faction)),
				("NUM", CVictoryConditions.Met(state, faction).ToString(CultureInfo.InvariantCulture)));
		}
		return new CTooltip { Text = localise("VC_ALERT"), Delayed = delayed };
	}

	/// <summary>Whether the player may choose in the lobby: its faction's first member.</summary>
	public static bool MayChoose(CGameState state, CCountry? player)
	{
		return player is not null && player.Faction.Length > 0 && CCountryLists.FactionMembers(state, player.Faction).FirstOrDefault() == player;
	}

	/// <summary>Why the lobby's button is greyed; nothing where it is not.</summary>
	public static string? ButtonDisabledKey(CGameState state, CCountry? player, bool saveLoaded)
	{
		return saveLoaded ? "VC_BUTTON_DISABLED_SAVE" : MayChoose(state, player) ? null : "VC_BUTTON_DISABLED_INVALID";
	}

	/// <summary>The lobby window's every condition of the faction, in the file's order.</summary>
	public static List<CVictoryCondition> AllOf(CGameState state, string faction)
	{
		foreach ((string name, List<CVictoryCondition> conditions) in state.VictoryConditions)
		{
			if (name == faction)
			{
				return conditions;
			}
		}
		return new List<CVictoryCondition>();
	}

	public static string SelectHeader(Func<string, string> localise, string faction)
	{
		return CTextTemplate.Fill(localise("VC_FACTION_HEADER"), ("FACTION", localise(faction)));
	}

	/// <summary>The lobby's <c>info</c>: ticked and the number, the ticked in red where they differ.</summary>
	public static string SelectInfo(int ticked, int number)
	{
		return ticked == number ? Count(ticked, number)
			: "§R" + ticked.ToString(CultureInfo.InvariantCulture) + "§W / " + number.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary><c>accept_button</c>'s tooltip, only while the ticks are not the number.</summary>
	public static string AcceptTooltip(Func<string, string> localise, int ticked, int number)
	{
		return ticked == number ? "" : CTextTemplate.Fill(localise("VC_ACCEPT_DISABLED"),
			("NUM", ticked.ToString(CultureInfo.InvariantCulture)), ("TOTAL", number.ToString(CultureInfo.InvariantCulture)));
	}

	/// <summary>
	/// The ticks fitted to the number: too many lose some at random, too few take random conditions of the faction's not named. The draw is the caller's; the
	/// result is what the game keeps.
	/// </summary>
	public static List<string> Fit(IEnumerable<string> ticked, IReadOnlyList<string> all, int number, Func<int, int> draw)
	{
		var names = ticked.ToList();
		while (names.Count > number)
		{
			names.RemoveAt(draw(names.Count));
		}
		List<string> rest = all.Where(name => !names.Contains(name)).ToList();
		while (names.Count < number && rest.Count > 0)
		{
			int at = draw(rest.Count);
			names.Add(rest[at]);
			rest.RemoveAt(at);
		}
		return names;
	}
}
