using System;
using System.Collections.Generic;

namespace OpenHOI3.Content.Script;

/// <summary>
/// The 91 effect keywords and how each reads its value: <c>docs/mechanics/script.md</c>,
/// section 6, from <c>reversing/findings/FINDINGS-effectgrammar.md</c>, section 4.
/// </summary>
public static class CEffectKeywords
{
	private static readonly string[] ThisFrom = { "this", "from" };

	/// <summary>A war goal's block: <c>docs/mechanics/script.md</c>, section 6.</summary>
	private static readonly CBlockRule WarGoal = new CBlockRule()
		.With("casus_belli", new CArgumentRule(EArgumentForm.Name, EScriptName.CasusBelli))
		.With("actor", new CArgumentRule(EArgumentForm.Tag, words: ThisFrom))
		.With("country", new CArgumentRule(EArgumentForm.Tag, words: ThisFrom))
		.With("receiver", new CArgumentRule(EArgumentForm.Tag, words: ThisFrom))
		.With("region", new CArgumentRule(EArgumentForm.Name, EScriptName.Region));

	/// <summary><c>CAIStrategy</c>'s 21 keys, whose values are not looked at here.</summary>
	private static readonly string[] AiStrategyKeys =
	{
		"air_perc", "antagonize", "area_theatre", "armor_bias", "befriend", "building_prov", "conquer_prov",
		"consolidate", "defend_prov", "initialized", "land_perc", "max_subunits", "military_access", "naval_perc",
		"personality", "protect", "rival", "static", "threat", "vassal", "war_with",
	};

	private static readonly Dictionary<string, CArgumentRule> Rules = Build();

	public static IReadOnlyDictionary<string, CArgumentRule> All => Rules;

	public static CArgumentRule? Find(string keyword)
	{
		return Rules.GetValueOrDefault(keyword);
	}

	/// <summary>An ideology's block, the key being an ideology's name.</summary>
	public static readonly CArgumentRule Ideology = new(EArgumentForm.Block, block: new CBlockRule()
		.With("organisation", new CArgumentRule(EArgumentForm.Number))
		.With("popularity", new CArgumentRule(EArgumentForm.Number)));

	/// <summary>A region's block, the key being a region's name.</summary>
	public static readonly CArgumentRule Region = new(EArgumentForm.Block, block: CBlockRule.Limited);

	/// <summary>A unit type's value, the key being a unit type's name.</summary>
	public static readonly CArgumentRule UnitType = new(EArgumentForm.Province, words: new[] { "current", "this", "from" });

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

		Add(new CArgumentRule(EArgumentForm.Tag, words: ThisFrom),
			"add_casus_belli", "casus_belli", "change_controller", "create_alliance", "create_vassal", "do_election",
			"end_guarantee", "end_military_access", "end_non_aggression_pact", "end_war", "guarantee", "inherit",
			"leave_alliance", "military_access", "non_aggression_pact", "release", "secede_province",
			"surrender_inherit");
		Add(new CArgumentRule(EArgumentForm.ProvinceOrTag, words: new[] { "this", "from", "REB" }), "add_core", "remove_core");
		Add(new CArgumentRule(EArgumentForm.Province, words: ThisFrom), "capital");
		Add(new CArgumentRule(EArgumentForm.Number, words: ThisFrom),
			"change_manpower", "dissent", "leadership", "manpower", "neutrality", "officer_pool", "organisation",
			"popularity", "revolt_risk", "split_troops", "war_exhaustion");
		Add(new CArgumentRule(EArgumentForm.Whole, words: ThisFrom),
			"create_revolt", "kill_leader", "local_intel_boost", "national_unity", "remove_fow", "crude_oil", "energy",
			"fuel", "metal", "money", "rare_materials", "supplies");
		Add(new CArgumentRule(EArgumentForm.Event, words: ThisFrom), "country_event", "province_event");
		Add(new CArgumentRule(EArgumentForm.Text),
			"change_province_name", "set_country_flag", "clr_country_flag", "set_province_flag", "clr_province_flag",
			"set_global_flag", "clr_global_flag", "remove_brigade");
		Add(new CArgumentRule(EArgumentForm.Name, EScriptName.OrderOfBattle), "load_oob");
		Add(new CArgumentRule(EArgumentForm.Name, EScriptName.Ideology, ThisFrom), "coup_by");
		Add(new CArgumentRule(EArgumentForm.Yes), "fixed_ai_strategy", "form_government_in_exile");
		Add(new CArgumentRule(EArgumentForm.Name, EScriptName.Government, new[] { "REB", "this", "from" }), "government");
		Add(new CArgumentRule(EArgumentForm.Name, EScriptName.Faction), "join_faction", "leave_faction");
		Add(new CArgumentRule(EArgumentForm.Ignored), "modify_spies");
		Add(new CArgumentRule(EArgumentForm.Tag, words: new[] { "this", "from", "random" }), "release_vassal");
		Add(new CArgumentRule(EArgumentForm.Name, EScriptName.Modifier), "remove_country_modifier", "remove_province_modifier");
		Add(new CArgumentRule(EArgumentForm.NameOrWhole, EScriptName.GovernmentPosition, new[] { "yes" }), "remove_minister");
		Add(new CArgumentRule(EArgumentForm.Name, EScriptName.StrategicResource, new[] { "none" }), "strategic_resource");

		Add(new CArgumentRule(EArgumentForm.Block, block: CBlockRule.Limited),
			"any_controlled", "any_country", "any_neighbor_country", "any_neighbor_province", "any_owned",
			"random_country", "random_owned", "random_neighbor_province", "random_empty_neighbor_province");
		Add(Own(new CBlockRule { TakesChildren = true, TakesLimit = true }
			.With("distance", new CArgumentRule(EArgumentForm.Whole))), "any_nearby_province");
		Add(Own(new CBlockRule { TakesChildren = true }
			.With("chance", new CArgumentRule(EArgumentForm.Whole))), "random");
		Add(Own(new CBlockRule { TakesWeights = true }), "random_list");
		Add(Own(new CBlockRule()
			.With("name", new CArgumentRule(EArgumentForm.Name, EScriptName.Modifier))
			.With("duration", new CArgumentRule(EArgumentForm.Whole))), "add_country_modifier", "add_province_modifier");
		Add(Own(new CBlockRule { TakesUnitTypes = true }
			.With("name", new CArgumentRule(EArgumentForm.Text))
			.With("where", new CArgumentRule(EArgumentForm.Province))), "add_division");
		Add(Own(new CBlockRule { TakesChildren = true }
			.With("target", new CArgumentRule(EArgumentForm.Tag, words: ThisFrom))
			.With("war_goal", new CArgumentRule(EArgumentForm.Block, block: WarGoal))), "add_wargoal");
		Add(Own(new CBlockRule { TakesChildren = true }
			.With("target", new CArgumentRule(EArgumentForm.Tag, words: ThisFrom))
			.With("call_ally", new CArgumentRule(EArgumentForm.Yes))
			.With("limited", new CArgumentRule(EArgumentForm.Yes))
			.With("attacker_goal", new CArgumentRule(EArgumentForm.Block, block: WarGoal))
			.With("defender_goal", new CArgumentRule(EArgumentForm.Block, block: WarGoal))), "war");
		Add(Own(new CBlockRule { TakesChildren = true }
			.With("target", new CArgumentRule(EArgumentForm.Tag, words: ThisFrom))
			.With("region", new CArgumentRule(EArgumentForm.Name, EScriptName.Region))), "undeclared_war");
		Add(Own(new CBlockRule { TakesChildren = true }
			.With("technology_group", new CArgumentRule(EArgumentForm.Name, EScriptName.TechnologyCategory))
			.With("value", new CArgumentRule(EArgumentForm.Number))), "practical");
		Add(Own(new CBlockRule()
			.With("who", new CArgumentRule(EArgumentForm.Tag, words: ThisFrom))
			.With("value", new CArgumentRule(EArgumentForm.Number))), "relation");
		var threat = new CArgumentRule(EArgumentForm.Tag, words: new[] { "all" });
		Add(Own(new CBlockRule()
			.With("who", threat)
			.With("which", threat)
			.With("value", new CArgumentRule(EArgumentForm.Whole))), "threat");
		Add(Own(new CBlockRule()
			.With("which", new CArgumentRule(EArgumentForm.Text))
			.With("value", new CArgumentRule(EArgumentForm.Number))), "change_variable", "set_variable");
		var strategy = new CBlockRule();
		foreach (string key in AiStrategyKeys)
		{
			strategy.With(key, new CArgumentRule(EArgumentForm.Ignored));
		}
		Add(Own(strategy), "add_ai_strategy");
		return rules;
	}

	private static CArgumentRule Own(CBlockRule block)
	{
		return new CArgumentRule(EArgumentForm.Block, block: block);
	}
}
