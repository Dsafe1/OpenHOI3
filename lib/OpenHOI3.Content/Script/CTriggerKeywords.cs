using System;
using System.Collections.Generic;

namespace OpenHOI3.Content.Script;

/// <summary>
/// The 153 trigger keywords and how each reads its value: <c>docs/mechanics/script.md</c>,
/// section 5, from <c>reversing/findings/FINDINGS-triggergrammar.md</c>, section 8.
/// </summary>
public static class CTriggerKeywords
{
	private static readonly string[] ThisFrom = { "this", "from" };

	private static readonly Dictionary<string, CArgumentRule> Rules = Build();

	public static IReadOnlyDictionary<string, CArgumentRule> All => Rules;

	public static CArgumentRule? Find(string keyword)
	{
		return Rules.GetValueOrDefault(keyword);
	}

	private static Dictionary<string, CArgumentRule> Build()
	{
		var rules = new Dictionary<string, CArgumentRule>(StringComparer.OrdinalIgnoreCase);
		void Add(CArgumentRule rule, params string[] keywords)
		{
			foreach (string keyword in keywords)
			{
				rules.Add(keyword, rule);
			}
		}

		Add(new CArgumentRule(EArgumentForm.Block, block: CBlockRule.Children),
			"and", "or", "not", "any_core", "any_neighbor_country", "any_neighbor_province", "any_owned_province", "enemy");

		Add(new CArgumentRule(EArgumentForm.Yes),
			"ai", "always", "can_create_vassals", "empty", "frontage_full", "government_in_exile", "has_armour_unit",
			"has_combined_arms", "has_empty_adjacent_province", "has_removable_minister", "has_strategic_resource",
			"is_attacker", "is_blockaded", "is_capital", "is_convoy", "is_in_any_faction", "is_mission_country",
			"is_mission_province", "is_subject", "is_winner", "port", "remove_fow", "reserves", "war");

		Add(new CArgumentRule(EArgumentForm.Whole, words: ThisFrom),
			"air_battles_fought", "land_battles_fought", "num_of_cities", "units_in_province");
		Add(new CArgumentRule(EArgumentForm.Province, words: ThisFrom), "capital");
		Add(new CArgumentRule(EArgumentForm.Province, words: ThisFrom), "controls");
		Add(new CArgumentRule(EArgumentForm.Province), "owns");

		Add(new CArgumentRule(EArgumentForm.Tag, words: ThisFrom),
			"alliance_with", "casus_belli", "country_units_in_province", "guarantee", "has_wargoal",
			"is_possible_vassal", "military_access", "neighbour", "non_aggression_pact", "owned_by", "tag",
			"truce_with", "undeclared_war_with", "vassal_of", "war_with");

		Add(new CArgumentRule(EArgumentForm.Number),
			"base_neutrality", "blockade", "crude_oil", "dissent", "enemy_ic_ratio", "energy", "faction_progress",
			"is_threatend", "leadership", "lost_IC", "lost_national", "manpower", "manpower_percentage",
			"max_manpower", "max_manpower_greater_than", "metal", "money", "national_unity", "neutrality",
			"num_of_convoys", "organisation", "popularity", "rare_materials", "revolt_percentage", "spies",
			"strat_allies_impact", "strat_bomb_impact", "strat_convoy_impact", "supplies", "surrender_progress",
			"total_amount_of_planes", "total_amount_of_ships", "total_defensives", "total_ic", "total_of_ours_sunk",
			"total_offensives", "total_sea_battles", "total_sunk_by_us", "total_we_bomb", "war_exhaustion");

		Add(new CArgumentRule(EArgumentForm.Whole),
			"last_air_battle_loser_losses", "last_air_battle_winner_losses", "last_battle_loser_losses",
			"last_battle_winner_losses", "last_naval_battle_loser_losses", "last_naval_battle_winner_losses",
			"minister_alive", "month", "nationalism", "naval_battles_fought", "num_in_faction", "num_of_allies",
			"num_of_ports", "num_of_revolts", "num_of_vassals", "out_of_supply_days", "province_id",
			"province_temperature", "pure_revolt_risk", "revolt_risk", "skill", "skill_advantage",
			"total_amount_of_brigades", "total_amount_of_divisions", "total_num_of_ports", "year");

		Add(new CArgumentRule(EArgumentForm.Text),
			"brigade_exist", "brigade_in_combat", "has_combat_modifier", "has_country_flag", "has_global_flag",
			"has_leader", "has_province_flag", "last_mission", "terrain", "active_mission");

		Add(new CArgumentRule(EArgumentForm.Name, EScriptName.Continent, ThisFrom), "continent");
		Add(new CArgumentRule(EArgumentForm.Tag, words: new[] { "this", "from", "owner" }), "controlled_by");
		Add(new CArgumentRule(EArgumentForm.Date), "date");
		Add(new CArgumentRule(EArgumentForm.Tag, words: new[] { "yes", "no" }), "exists");
		// A faction nothing has is the null faction, which is how "none" means none.
		Add(new CArgumentRule(EArgumentForm.Name, EScriptName.Faction, new[] { "this", "none" }), "faction");
		Add(new CArgumentRule(EArgumentForm.Name, EScriptName.Government), "government");
		Add(new CArgumentRule(EArgumentForm.Name, EScriptName.Building), "has_building");
		Add(new CArgumentRule(EArgumentForm.Name, EScriptName.Modifier), "has_country_modifier", "has_province_modifier");
		Add(new CArgumentRule(EArgumentForm.Name, EScriptName.Ideology), "ideology");
		Add(new CArgumentRule(EArgumentForm.Name, EScriptName.IdeologyGroup), "ideology_group");
		Add(new CArgumentRule(EArgumentForm.ProvinceOrTag, words: new[] { "this", "from", "REB" }), "is_core");
		Add(new CArgumentRule(EArgumentForm.Name, EScriptName.Region), "region");
		Add(new CArgumentRule(EArgumentForm.Name, EScriptName.StrategicResource, new[] { "none" }), "strategic_resource");
		Add(new CArgumentRule(EArgumentForm.Name, EScriptName.Trait), "trait");
		Add(new CArgumentRule(EArgumentForm.Ignored), "unit_has_leader", "unit_in_battle", "has_hostile_spy_mission");

		Add(Own(new CBlockRule()
			.With("which", new CArgumentRule(EArgumentForm.Text))
			.With("value", new CArgumentRule(EArgumentForm.Number))), "check_variable");
		Add(Own(new CBlockRule()
			.With("type", new CArgumentRule(EArgumentForm.Name, EScriptName.CasusBelli))
			.With("actor", new CArgumentRule(EArgumentForm.Tag, words: ThisFrom))), "has_cb");
		Add(Own(new CBlockRule()
			.With("who", new CArgumentRule(EArgumentForm.Tag, words: ThisFrom))
			.With("value", new CArgumentRule(EArgumentForm.Whole))), "relation");
		Add(Own(new CBlockRule()
			.With("which", new CArgumentRule(EArgumentForm.Tag, words: ThisFrom))
			.With("value", new CArgumentRule(EArgumentForm.Whole))), "threat");
		Add(Own(new CBlockRule()
			.With("type", new CArgumentRule(EArgumentForm.Text))
			.With("value", new CArgumentRule(EArgumentForm.Number))), "spy_mission_count");
		return rules;
	}

	/// <summary>An ideology's block, the key being an ideology's name.</summary>
	public static readonly CArgumentRule Ideology = Own(new CBlockRule()
		.With("organisation", new CArgumentRule(EArgumentForm.Whole))
		.With("popularity", new CArgumentRule(EArgumentForm.Whole)));

	private static CArgumentRule Own(CBlockRule block)
	{
		return new CArgumentRule(EArgumentForm.Block, block: block);
	}
}
