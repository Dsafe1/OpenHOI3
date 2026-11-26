using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim.Modifiers;

/// <summary>
/// The keys a block of a modifier may hold, a closed list: the original's modifier definitions, every one made before
/// the first file of modifiers is read - the engine's own 108 (107 keys, <c>naval_intel_boost</c> made twice), one for
/// each of the 32 missions an order can be, by the order's own key, and one <c>suseptibility_</c> for each faction of
/// <c>common/ideologies.txt</c>, made as its block closes. A key that is none of them is logged and dropped by the
/// original; here it is reported and dropped. See <c>docs/mechanics/modifiers.md</c>, section 2.
/// </summary>
public sealed class CModifierKeys
{
	/// <summary>What a faction's own kind is called: this and the faction's tag, in the original's spelling.</summary>
	public const string SusceptibilityPrefix = "suseptibility_";

	/// <summary>The one key of a modifier's own, a picture's frame, which every block of a modifier takes.</summary>
	public const string Icon = "icon";

	/// <summary>The engine's own kinds, in the order it makes them.</summary>
	public static IReadOnlyList<string> Engine { get; } = new[]
	{
		"minimum_revolt_risk", "local_revolt_risk", "global_revolt_risk", "local_manpower", "global_manpower",
		"local_manpower_modifier", "global_manpower_modifier", "attrition", "war_exhaustion", "max_war_exhaustion",
		"fort_level", "coastal_fort_level", "infrastructure", "local_infrastructure", "global_infrastructure", "ic",
		"local_ic", "global_ic", "local_crude_oil", "global_crude_oil", "local_energy", "global_energy", "local_metal",
		"global_metal", "local_rare_materials", "global_rare_materials", "local_supplies", "global_supplies", "local_fuel",
		"global_fuel", "local_money", "global_money", "local_leadership", "global_leadership", "local_leadership_modifier",
		"global_leadership_modifier", "drift_speed", "suseptibility", "incorporate_cost", "territorial_pride",
		"war_consumer_goods_demand", "peace_consumer_goods_demand", "espionage_bonus", "dissent", "national_unity",
		"naval_capacity", "air_capacity", "align_towards", "radar_level", "supply_consumption",
		"peacetime_manpower_rotation", "unit_recruitment_time", "unit_start_experience", "unit_repair",
		"counter_intelligence", "counter_espionage", "threat_impact", "threat_resistance", "peace_offmap_intel",
		"offmap_land_intel", "offmap_naval_intel", "offmap_industry_intel", "offmap_political_intel",
		"combat_movement_speed", "attack_reinforce_chance", "defend_reinforce_chance", "combat_width", "org_regain",
		"national_unity_effect", "ruling_party_support", "land_organisation", "air_organisation", "naval_organisation",
		"research_efficiency", "industrial_efficiency", "local_anti_air", "local_partisan_support",
		"reserves_penalty_size", "neutrality_change", "partisan_efficiency", "naval_base_efficiency", "supply_throughput",
		"officer_recruitment", "neutrality", "global_resources", "local_resources", "reinforcement_bonus",
		"air_build_speed", "land_build_speed", "rocket_build_speed", "tank_build_speed", "naval_build_speed",
		"fuel_conversion", "casualty_trickleback", "hard_attack", "soft_attack", "nuke_research", "amm_movement_speed",
		"winter_effects", "jungle_effects", "leader_defence", "sw_national_unity_effect", "land_intel_boost",
		"naval_intel_boost", "local_underground", "strategic_resource_efficiency", "local_unit_speed",
	};

	/// <summary>
	/// The mission efficiencies, by the key of the order each is for, in the order of the missions: the engine makes
	/// them after its own kinds. Its name for each - what is localised - is the key and <c>_eff</c>; a file writes the
	/// order's key alone, <c>convoy_raid = 0.1</c>.
	/// </summary>
	public static IReadOnlyList<string> Missions => Sim.Missions.CMissionEfficiency.Names;

	/// <summary>
	/// The kinds an enhanced game's files may name beyond the original's: how many units a corps, an army and an army
	/// group may command (<see cref="Units.CCommandLimit"/>). The original logs and drops them as it does any key it
	/// does not know, so a file made for both engines may hold them. <c>docs/enhanced.md</c>.
	/// </summary>
	public static IReadOnlyList<string> Enhanced => Units.CCommandLimit.Kinds;

	/// <summary>The engine's kinds and the missions', with no faction's: what a faction's own modifier may hold before any.</summary>
	public static CModifierKeys WithoutFactions { get; } = new(Array.Empty<string>());

	private readonly HashSet<string> _keys = new(StringComparer.OrdinalIgnoreCase);

	/// <param name="factions">The tags of the factions whose kinds are made, in the file's order.</param>
	/// <param name="enhanced">Whether the files are an enhanced game's, which has kinds of its own.</param>
	public CModifierKeys(IEnumerable<string> factions, bool enhanced = false)
	{
		_keys.UnionWith(Engine);
		_keys.UnionWith(Missions);
		if (enhanced)
		{
			_keys.UnionWith(Enhanced);
		}
		foreach (string faction in factions)
		{
			if (faction.Length > 0)
			{
				_keys.Add(SusceptibilityPrefix + faction);
			}
		}
	}

	/// <summary>How many keys there are: 139 and one a faction, and an enhanced game's three.</summary>
	public int Count => _keys.Count;

	/// <summary>
	/// Whether a block's key is a kind of modifier the original knows. A keyword is matched without case, as the
	/// original's reader lower-cases what it looks up.
	/// </summary>
	public bool Contains(string key)
	{
		return _keys.Contains(key);
	}
}
