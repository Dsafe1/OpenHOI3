using System;
using System.Collections.Generic;

namespace OpenHOI3.Interface.Modifiers;

/// <summary>
/// The modifier kinds the original knows, in the order it builds them, each with how it is
/// written. Read from the original's definitions: `reversing/findings/FINDINGS-topbartooltip.md`,
/// section 10. See <c>docs/mechanics/modifiers.md</c>, section 8.
/// </summary>
public static class CModifierKinds
{
	/// <summary>
	/// The names of the 32 mission efficiencies the original makes at startup end in this, and they are both percentages
	/// and better for more; a file writes the mission's key alone.
	/// </summary>
	private const string MissionEfficiencySuffix = Sim.Missions.CMissionEfficiency.ModifierSuffix;

	/// <summary>In the original's order. <c>naval_intel_boost</c> is built twice there; it is here once, at its first place.</summary>
	public static IReadOnlyList<CModifierKind> All { get; } = new CModifierKind[]
	{
		new("minimum_revolt_risk", "MINIMUM_REVOLT_RISK", false, false),
		new("local_revolt_risk", "LOCAL_REVOLT_RISK", false, false),
		new("global_revolt_risk", "GLOBAL_REVOLT_RISK", false, false),
		new("local_manpower", "MANPOWER", false, true),
		new("global_manpower", "MANPOWER", false, true),
		new("local_manpower_modifier", "LOCAL_MANPOWER", true, true),
		new("global_manpower_modifier", "GLOBAL_MANPOWER", true, true),
		new("attrition", "ATTRITION", false, false),
		new("war_exhaustion", "WAR_EXHAUSTION", false, false),
		new("max_war_exhaustion", "MAX_WAR_EXHAUSTION", false, false),
		new("fort_level", "FORT_LEVEL", false, true),
		new("coastal_fort_level", "COASTAL_FORT_LEVEL", false, true),
		new("infrastructure", "MODIFIER_INFRASTRUCTURE", false, true),
		new("local_infrastructure", "MODIFIER_LOCAL_INFRASTRUCTURE", true, true),
		new("global_infrastructure", "MODIFIER_GLOBAL_INFRASTRUCTURE", true, true),
		new("ic", "MODIFIER_IC", false, true),
		new("local_ic", "MODIFIER_LOCAL_IC", true, true),
		new("global_ic", "MODIFIER_GLOBAL_IC", true, true),
		new("local_crude_oil", "MODIFIER_LOCAL_CRUDE_OIL", true, true),
		new("global_crude_oil", "MODIFIER_GLOBAL_CRUDE_OIL", true, true),
		new("local_energy", "MODIFIER_LOCAL_ENERGY", true, true),
		new("global_energy", "MODIFIER_GLOBAL_ENERGY", true, true),
		new("local_metal", "MODIFIER_LOCAL_METAL", true, true),
		new("global_metal", "MODIFIER_GLOBAL_METAL", true, true),
		new("local_rare_materials", "MODIFIER_LOCAL_RARE_MATERIALS", true, true),
		new("global_rare_materials", "MODIFIER_GLOBAL_RARE_MATERIALS", true, true),
		new("local_supplies", "MODIFIER_LOCAL_SUPPLIES", true, true),
		new("global_supplies", "MODIFIER_GLOBAL_SUPPLIES", true, true),
		new("local_fuel", "MODIFIER_LOCAL_FUEL", true, true),
		new("global_fuel", "MODIFIER_GLOBAL_FUEL", true, true),
		new("local_money", "MODIFIER_LOCAL_MONEY", true, true),
		new("global_money", "MODIFIER_GLOBAL_MONEY", true, true),
		new("local_leadership", "MODIFIER_LOCAL_LEADERSHIP", false, true),
		new("global_leadership", "MODIFIER_GLOBAL_LEADERSHIP", false, true),
		new("local_leadership_modifier", "MODIFIER_LOCAL_LEADERSHIP_MODIFIER", true, true),
		new("global_leadership_modifier", "MODIFIER_GLOBAL_LEADERSHIP_MODIFIER", true, true),
		new("drift_speed", "MODIFIER_DRIFT_SPEED", true, true),
		new("suseptibility", "MODIFIER_SUSEPTIBILITY", true, true),
		new("incorporate_cost", "MODIFIER_INCORPORATE_COST", true, false),
		new("territorial_pride", "MODIFIER_TERRITORIAL_PRIDE", true, true),
		new("war_consumer_goods_demand", "MODIFIER_WAR_CONSUMER_GOODS_DEMAND", true, false),
		new("peace_consumer_goods_demand", "MODIFIER_PEACE_CONSUMER_GOODS_DEMAND", true, false),
		new("espionage_bonus", "MODIFIER_ESPIONAGE_BONUS", true, true),
		new("dissent", "MODIFIER_DISSENT", true, false),
		new("national_unity", "MODIFIER_NATIONAL_UNITY", false, true),
		new("naval_capacity", "MODIFIER_NAVAL_CAPACITY", false, true),
		new("air_capacity", "MODIFIER_AIR_CAPACITY", false, true),
		new("align_towards", "MODIFIER_ALIGN_TOWARDS", false, true),
		new("radar_level", "MODIFIER_RADAR_LEVEL", false, true),
		new("supply_consumption", "MODIFIER_SUPPLY_CONSUMPTION", true, false),
		new("peacetime_manpower_rotation", "MODIFIER_PEACETIME_MANPOWER_ROTATION", true, false),
		new("unit_recruitment_time", "MODIFIER_UNIT_RECRUITMENT_TIME", true, false),
		new("unit_start_experience", "MODIFIER_UNIT_START_EXPERIENCE", false, true),
		new("unit_repair", "MODIFIER_UNIT_REPAIR", true, true),
		new("counter_intelligence", "MODIFIER_COUNTER_INTELLIGENCE", true, true),
		new("counter_espionage", "MODIFIER_COUNTER_ESPIONAGE", true, true),
		new("threat_impact", "MODIFIER_THREAT_IMPACT", true, false),
		new("threat_resistance", "MODIFIER_THREAT_RESISTANCE", true, true),
		new("peace_offmap_intel", "MODIFIER_PEACE_OFFMAP_INTEL", true, true),
		new("offmap_land_intel", "MODIFIER_OFFMAP_LAND_INTEL", true, true),
		new("offmap_naval_intel", "MODIFIER_OFFMAP_NAVAL_INTEL", true, true),
		new("offmap_industry_intel", "MODIFIER_OFFMAP_INDUSTRY_INTEL", true, true),
		new("offmap_political_intel", "MODIFIER_OFFMAP_POLITICAL_INTEL", true, true),
		new("combat_movement_speed", "MODIFIER_COMBAT_MOVEMENT_SPEED", true, true),
		new("attack_reinforce_chance", "MODIFIER_ATTACK_REINFORCE_CHANCE", true, true),
		new("defend_reinforce_chance", "MODIFIER_DEFEND_REINFORCE_CHANCE", true, true),
		new("combat_width", "MODIFIER_COMBAT_WIDTH", true, true),
		new("org_regain", "MODIFIER_ORG_REGAIN", true, true),
		new("national_unity_effect", "MODIFIER_NATIONAL_UNITY_EFFECT", true, false),
		new("ruling_party_support", "MODIFIER_RULING_PARTY_SUPPORT", true, true),
		new("land_organisation", "MODIFIER_LAND_ORGANISATION", true, true),
		new("air_organisation", "MODIFIER_AIR_ORGANISATION", true, true),
		new("naval_organisation", "MODIFIER_NAVAL_ORGANISATION", true, true),
		new("research_efficiency", "MODIFIER_RESEARCH_EFFICIENCY", true, true),
		new("industrial_efficiency", "MODIFIER_INDUSTRIAL_EFFICIENCY", true, true),
		new("local_anti_air", "MODIFIER_LOCAL_AA", false, true),
		new("local_partisan_support", "MODIFIER_LOCAL_PARTISAN_SUPPORT", false, false),
		new("reserves_penalty_size", "MODIFIER_RESERVES_PENALTY_SIZE", true, false),
		new("neutrality_change", "MODIFIER_NEUTRALITY_CHANGE", false, false),
		new("partisan_efficiency", "MODIFIER_PARTISAN_EFFICENCY", false, false),
		new("naval_base_efficiency", "MODIFIER_NAVAL_BASE_EFFICIENCY", true, true),
		new("supply_throughput", "MODIFIER_SUPPLY_THROUGHPUT", true, true),
		new("officer_recruitment", "MODIFIER_OFFICER_RECRUITMENT", true, true),
		new("neutrality", "MODIFIER_NEUTRALITY", true, false),
		new("global_resources", "MODIFIER_GLOBAL_RESOURCES", true, true),
		new("local_resources", "MODIFIER_LOCAL_RESOURCES", true, true),
		new("reinforcement_bonus", "MODIFIER_REINFORCEMENT_BONUS", true, true),
		new("air_build_speed", "MODIFIER_AIR_BUILD_SPEED", true, false),
		new("land_build_speed", "MODIFIER_LAND_BUILD_SPEED", true, false),
		new("rocket_build_speed", "MODIFIER_ROCKET_BUILD_SPEED", true, false),
		new("tank_build_speed", "MODIFIER_TANK_BUILD_SPEED", true, false),
		new("naval_build_speed", "MODIFIER_NAVAL_BUILD_SPEED", true, false),
		new("fuel_conversion", "MODIFIER_FUEL_CONVERSION", true, true),
		new("casualty_trickleback", "MODIFIER_TRICKLEBACK", true, true),
		new("hard_attack", "MODIFIER_HARD_ATTACK", true, true),
		new("soft_attack", "MODIFIER_SOFT_ATTACK", true, true),
		new("nuke_research", "MODIFIER_NUKE_RESEARCH", true, true),
		new("amm_movement_speed", "MODIFIER_AMM_MOVEMENT_SPEED", true, true),
		new("winter_effects", "MODIFIER_WINTER_EFFECTS", true, true),
		new("jungle_effects", "MODIFIER_JUNGLE_EFFECTS", true, false),
		new("leader_defence", "MODIFIER_LEADER_DEFENCE", true, true),
		new("sw_national_unity_effect", "MODIFIER_SW_NATIONAL_UNITY_EFFECT", true, false),
		new("land_intel_boost", "MODIFIER_LAND_INTEL_BOOST", false, true),
		new("naval_intel_boost", "MODIFIER_NAVAL_INTEL_BOOST", false, true),
		new("local_underground", "MODIFIER_LOCAL_UNDERGROUND", false, true),
		new("strategic_resource_efficiency", "MODIFIER_STRATEGIC_RESOURCE_EFFICIENCY", true, true),
		new("local_unit_speed", "MODIFIER_LOCAL_UNIT_SPEED", true, true),
	};

	private static readonly Dictionary<string, CModifierKind> ByKey = Index();

	private static Dictionary<string, CModifierKind> Index()
	{
		var byKey = new Dictionary<string, CModifierKind>(StringComparer.OrdinalIgnoreCase);
		foreach (CModifierKind kind in All)
		{
			byKey.TryAdd(kind.Key, kind);
		}
		return byKey;
	}

	/// <summary>
	/// How a kind is written. A mission efficiency, a faction's <c>suseptibility_</c> and - ours - any kind the original
	/// does not build is a percentage that is better for more, as most are; a mission's name is its key and <c>_eff</c>,
	/// any other's its key.
	/// </summary>
	public static CModifierKind Of(string key)
	{
		if (ByKey.TryGetValue(key, out CModifierKind? kind))
		{
			return kind;
		}
		if (MissionIndex(key) >= 0)
		{
			return new CModifierKind(key, key + MissionEfficiencySuffix, true, true);
		}
		return new CModifierKind(key, key, true, true);
	}

	/// <summary>Whether the original builds the kind: its own, the 32 mission efficiencies and a faction's.</summary>
	public static bool IsKnown(string key)
	{
		return ByKey.ContainsKey(key) || MissionIndex(key) >= 0
			|| key.StartsWith(Sim.Modifiers.CModifierKeys.SusceptibilityPrefix, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// The place of a kind in the original's order - its own kinds, then the missions' - and every other kind after all
	/// of them.
	/// </summary>
	public static int Order(string key)
	{
		for (int index = 0; index < All.Count; index++)
		{
			if (string.Equals(All[index].Key, key, StringComparison.OrdinalIgnoreCase))
			{
				return index;
			}
		}
		int mission = MissionIndex(key);
		return All.Count + (mission >= 0 ? mission : Sim.Modifiers.CModifierKeys.Missions.Count);
	}

	private static int MissionIndex(string key)
	{
		IReadOnlyList<string> missions = Sim.Modifiers.CModifierKeys.Missions;
		for (int index = 0; index < missions.Count; index++)
		{
			if (string.Equals(missions[index], key, StringComparison.OrdinalIgnoreCase))
			{
				return index;
			}
		}
		return -1;
	}
}
