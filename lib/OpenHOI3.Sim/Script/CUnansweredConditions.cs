using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The keywords that answer false and are reported rather than asked, and why:
/// <c>docs/mechanics/triggers.md</c>, section 8. A keyword leaves this list when what it asks
/// about is built.
/// </summary>
public static class CUnansweredConditions
{
	private static readonly Dictionary<string, EScriptGap> Reasons = Build();

	/// <summary>Why the keyword is not asked, or null where it is.</summary>
	public static EScriptGap? Reason(string keyword)
	{
		return Reasons.TryGetValue(keyword, out EScriptGap why) ? why : null;
	}

	private static Dictionary<string, EScriptGap> Build()
	{
		var reasons = new Dictionary<string, EScriptGap>(StringComparer.OrdinalIgnoreCase);
		void Add(EScriptGap why, params string[] keywords)
		{
			foreach (string keyword in keywords)
			{
				reasons.Add(keyword, why);
			}
		}

		// Diplomacy.
		Add(EScriptGap.Unbuilt, "undeclared_war_with");
		// The government and the parties.
		Add(EScriptGap.Unbuilt, "government", "has_removable_minister", "minister_alive");
		Add(EScriptGap.Unbuilt, "ideology", "ideology_group", "organisation", "popularity");
		// Revolts and espionage.
		Add(EScriptGap.Unbuilt, "num_of_revolts", "revolt_percentage", "revolt_risk", "pure_revolt_risk", "nationalism");
		Add(EScriptGap.Unbuilt, "spies", "spy_mission_count", "remove_fow");
		// The AI's missions.
		Add(EScriptGap.Unbuilt, "active_mission", "last_mission", "is_mission_country", "is_mission_province");
		// A country's war record and the last battles.
		Add(EScriptGap.Unbuilt, "war_exhaustion", "lost_ic", "lost_national",
			"strat_allies_impact", "strat_bomb_impact", "strat_convoy_impact",
			"total_sea_battles", "total_offensives", "total_defensives",
			"total_we_bomb", "total_sunk_by_us", "total_of_ours_sunk");
		Add(EScriptGap.Unbuilt, "last_battle_winner_losses", "last_battle_loser_losses", "last_air_battle_winner_losses",
			"last_air_battle_loser_losses", "last_naval_battle_winner_losses", "last_naval_battle_loser_losses");
		Add(EScriptGap.Unbuilt, "blockade", "is_blockaded", "out_of_supply_days");

		Add(EScriptGap.Unread, "num_of_ports", "total_num_of_ports", "has_hostile_spy_mission");

		Add(EScriptGap.BattleOnly, "is_attacker", "frontage_full", "reserves", "skill", "skill_advantage",
			"has_armour_unit", "has_combined_arms", "has_combat_modifier", "is_convoy", "is_winner", "trait",
			"terrain", "province_temperature");
		return reasons;
	}
}
