using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A save's <c>active_war</c> and <c>previous_war</c> blocks: each war's name, whether it is limited, its sides in their
/// order, the hour of its last action and its goals, each filed under its actor as the history's are. A war made in play
/// keeps the name it was written with. See <c>docs/mechanics/savegame.md</c>, section 6.
/// </summary>
public static class CSaveWars
{
	private const string None = "---";

	public static CWar Read(CGameState state, ClausewitzNode block, CSaveReport report)
	{
		var war = new CWar(block.Get("name")?.Value?.Trim('"') ?? "");
		foreach (ClausewitzNode entry in block.Children)
		{
			switch (entry.Key)
			{
				case null:
				case "name":
					break;
				case "limited":
					war.IsLimited = entry.Value == "yes";
					break;
				case "attacker":
					Side(war.Attackers, entry);
					break;
				case "defender":
					Side(war.Defenders, entry);
					break;
				case "action":
					war.Action = CSaveOrders.Hour(entry.Value) ?? 0;
					break;
				// Who began it, where a war has been taken over: nothing here holds it.
				case "original_attacker":
				case "original_defender":
					if (entry.Value?.Trim('"') != None)
					{
						report.Unread(block.Key!, entry.Key);
					}
					break;
				case "war_goal" when entry.IsBlock:
					ReadGoal(state, war, entry, report);
					break;
				default:
					report.Unread(block.Key!, entry.Key);
					break;
			}
		}
		return war;
	}

	private static void Side(List<CCountryTag> side, ClausewitzNode entry)
	{
		if (CCountryTag.TryParse(entry.Value?.Trim('"'), out CCountryTag tag) && !side.Contains(tag))
		{
			side.Add(tag);
		}
	}

	private static void ReadGoal(CGameState state, CWar war, ClausewitzNode block, CSaveReport report)
	{
		if (!CCountryTag.TryParse(block.Get("actor")?.Value?.Trim('"'), out CCountryTag actor)
			|| !CCountryTag.TryParse(block.Get("receiver")?.Value?.Trim('"'), out CCountryTag receiver))
		{
			report.Unread("war_goal", "a goal of no actor or receiver");
			return;
		}
		var goal = new CWarGoal(CCasusBellis.Find(state, block.Get("casus_belli")?.Value?.Trim('"') ?? ""), actor, receiver)
		{
			Country = CCountryTag.TryParse(block.Get("country")?.Value?.Trim('"'), out CCountryTag third) ? third : null,
			Region = state.Regions.FirstOrDefault(region => region.Name == block.Get("region")?.Value?.Trim('"')),
		};
		if (!war.Goals.TryGetValue(actor, out List<CWarGoal>? goals))
		{
			war.Goals[actor] = goals = new List<CWarGoal>();
		}
		goals.Add(goal);
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is { } key && key is not ("casus_belli" or "actor" or "receiver" or "country" or "region"))
			{
				report.Unread("war_goal", key);
			}
		}
	}
}
