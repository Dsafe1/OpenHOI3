using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Interface.Text;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the end of a game says: the <c>VCConfirm</c> dialog, and the end-of-game screen <c>back_end</c> with its three factions and its result,
/// worked out afresh from the world as it stands. See <c>docs/mechanics/endgame.md</c>.
/// </summary>
public static class CEndGameText
{
	public const string DialogName = "VCConfirm";
	public const string ScreenName = "back_end";
	public const string MemberRowName = "backend_faction_member_entry";
	public const string ConditionRowName = "end_vc_entry";

	/// <summary>The three factions the screen has, by their tags and in this order [exe].</summary>
	public static readonly IReadOnlyList<string> Factions = new[] { "allies", "axis", "comintern" };

	private const int Strict = 5;

	private static readonly string[] ShortMonths = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

	/// <summary>
	/// The faction the dialog names: of the game's factions in their order, the one with most of its chosen conditions met - the last on a tie, so
	/// that with nothing met it is the last of all. Null only where the game has no factions.
	/// </summary>
	public static string? Leading(CGameState state)
	{
		int best = 0;
		string? leading = null;
		foreach ((string faction, _) in state.VictoryConditions)
		{
			int met = CVictoryConditions.Met(state, faction);
			if (best <= met)
			{
				best = met;
				leading = faction;
			}
		}
		return leading;
	}

	public static string DialogTitle(Func<string, string> localise, CGameState state, CCountry player)
	{
		return localise(Leading(state) == player.Faction ? "CONFIRM_VC_VICTORY" : "CONFIRM_VC_DEFEAT");
	}

	public static string DialogText(Func<string, string> localise, CGameState state)
	{
		return CTextTemplate.Fill(localise("CONFIRM_VC_TEXT"), ("FACTION", Leading(state) is { } leading ? localise(leading) : ""));
	}

	/// <summary>A faction's victory points, whole.</summary>
	public static int Points(CGameState state, string faction) => (int)(CFactionProgress.Progress(state, faction) / 1000);

	public static string PointsLine(Func<string, string> localise, CGameState state, string faction)
	{
		return CTextTemplate.Fill(localise("END_VICTORY_POINTS"), ("NUM", Points(state, faction).ToString(CultureInfo.InvariantCulture)));
	}

	public static string ConditionsLine(Func<string, string> localise, CGameState state, string faction)
	{
		return CTextTemplate.Fill(localise("END_VICTORY_CONDITIONS"),
			("NUM", CVictoryConditions.Met(state, faction).ToString(CultureInfo.InvariantCulture)),
			("TOTAL", CVictoryConditions.Chosen(state, faction).ToString(CultureInfo.InvariantCulture)));
	}

	/// <summary>A faction's bar: its share of the three factions' points, cut; the bare points x 100 where they have none between them.</summary>
	public static int Bar(CGameState state, string faction)
	{
		int points = Points(state, faction);
		int total = Factions.Sum(f => Points(state, f));
		return total == 0 ? points * 100 : points * 100 / total;
	}

	/// <summary>A faction's conditions on the screen, as they were chosen.</summary>
	public static List<CVictoryCondition> Conditions(CGameState state, string faction)
	{
		foreach ((string name, List<CVictoryCondition> conditions) in state.VictoryConditions)
		{
			if (name == faction)
			{
				return conditions.FindAll(condition => condition.Chosen);
			}
		}
		return new List<CVictoryCondition>();
	}

	/// <summary>A met condition's date: the year, the short month and the day; nothing where it is not met.</summary>
	public static string Date(Func<string, string> localise, CVictoryCondition condition)
	{
		if (!condition.Met || condition.Date is not { } hours)
		{
			return "";
		}
		CDate date = CDate.FromDayNumber((int)(hours / CGameState.HoursPerDay));
		return string.Create(CultureInfo.InvariantCulture, $"{date.Year} {localise(ShortMonths[date.Month - 1])} {date.Day}");
	}

	/// <summary>
	/// The result as three factions' conditions met and points give it: which of the three, or -1 for a stalemate, and its tier - 0 a world order,
	/// 1 strategic, 2 operational, 3 tactical. Each tier asks a strict most, so that no two factions can reach one.
	/// </summary>
	public static (int Faction, int Tier) Result(int[] met, int[] points, int toWin)
	{
		var slots = new[] { -1, -1, -1, -1, -1, -1 };
		for (int f = 0; f < met.Length; f++)
		{
			int a = (f + 1) % met.Length;
			int b = (f + 2) % met.Length;
			int tier = Strict;
			bool mostMet = met[f] > met[a] && met[f] > met[b];
			if (met[f] >= toWin && mostMet)
			{
				tier = points[f] > points[a] + points[b] ? 0 : 1;
			}
			else if (points[f] > points[a] && points[f] > points[b])
			{
				if (points[f] > points[a] + points[b])
				{
					tier = 2;
				}
				else if (mostMet)
				{
					tier = 3;
				}
			}
			slots[tier] = f;
		}
		for (int tier = 0; tier < Strict; tier++)
		{
			if (slots[tier] >= 0)
			{
				return (slots[tier], tier);
			}
		}
		return (-1, -1);
	}

	public static (int Faction, int Tier) Result(CGameState state)
	{
		return Result(Factions.Select(f => CVictoryConditions.Met(state, f)).ToArray(), Factions.Select(f => Points(state, f)).ToArray(),
			CVictoryConditions.ToWin(state));
	}

	/// <summary>The screen's <c>result</c>.</summary>
	public static string ResultText(Func<string, string> localise, CGameState state, CCountry player)
	{
		(int index, int tier) = Result(state);
		if (index < 0)
		{
			return localise("STALEMATE");
		}
		string faction = Factions[index];
		if (tier == 0)
		{
			CIdeologyGroup? group = CFactions.Group(state, faction);
			return (group is null ? "" : localise(group.Name)) + " " + localise("WORLD_ORDER");
		}
		string level = tier == 1 ? "STRATEGIC" : tier == 2 ? "OPERATIONAL" : "TACTICAL";
		return localise(level + (faction == player.Faction ? "_VICTORY" : "_DEFEAT"));
	}
}
