namespace OpenHOI3.Lua.Api;

/// <summary>
/// The original's <c>ModifierType</c>, the number a script asks <c>CModifier:GetValue</c> for a kind by: each number the
/// place of its kind in the original's list of modifier kinds, its constant the name registered on <c>CModifier</c>.
/// Number 57, <c>threat_resistance</c>, has a place and no constant - the registration skips it - and
/// <c>partisan_efficiency</c>'s constant is spelled <c>_MODIFIER_PARTISAN_EFFICENCY_</c>. The kinds after 82 have no
/// number a script can name. See <c>docs/mechanics/lua.md</c>, section 4.7.
/// </summary>
public static class CModifierTypes
{
	/// <summary>A kind: the constant a script names it by, if any, and its key in the files.</summary>
	public sealed record Entry(string? Constant, string Key);

	/// <summary>By number, from 0.</summary>
	public static readonly Entry[] All =
	{
		new("_MODIFIER_MINIMUM_REVOLT_RISK_", "minimum_revolt_risk"),
		new("_MODIFIER_LOCAL_REVOLT_RISK_", "local_revolt_risk"),
		new("_MODIFIER_GLOBAL_REVOLT_RISK_", "global_revolt_risk"),
		new("_MODIFIER_LOCAL_MANPOWER_", "local_manpower"),
		new("_MODIFIER_GLOBAL_MANPOWER_", "global_manpower"),
		new("_MODIFIER_LOCAL_MANPOWER_MODIFIER_", "local_manpower_modifier"),
		new("_MODIFIER_GLOBAL_MANPOWER_MODIFIER_", "global_manpower_modifier"),
		new("_MODIFIER_ATTRITION_", "attrition"),
		new("_MODIFIER_WAR_EXHAUSTION_", "war_exhaustion"),
		new("_MODIFIER_MAX_WAR_EXHAUSTION_", "max_war_exhaustion"),
		new("_MODIFIER_FORT_LEVEL_", "fort_level"),
		new("_MODIFIER_COASTAL_FORT_LEVEL_", "coastal_fort_level"),
		new("_MODIFIER_INFRASTRUCTURE_", "infrastructure"),
		new("_MODIFIER_LOCAL_INFRASTRUCTURE_", "local_infrastructure"),
		new("_MODIFIER_GLOBAL_INFRASTRUCTURE_", "global_infrastructure"),
		new("_MODIFIER_IC_", "ic"),
		new("_MODIFIER_LOCAL_IC_", "local_ic"),
		new("_MODIFIER_GLOBAL_IC_", "global_ic"),
		new("_MODIFIER_LOCAL_CRUDE_OIL_", "local_crude_oil"),
		new("_MODIFIER_GLOBAL_CRUDE_OIL_", "global_crude_oil"),
		new("_MODIFIER_LOCAL_ENERGY_", "local_energy"),
		new("_MODIFIER_GLOBAL_ENERGY_", "global_energy"),
		new("_MODIFIER_LOCAL_METAL_", "local_metal"),
		new("_MODIFIER_GLOBAL_METAL_", "global_metal"),
		new("_MODIFIER_LOCAL_RARE_MATERIALS_", "local_rare_materials"),
		new("_MODIFIER_GLOBAL_RARE_MATERIALS_", "global_rare_materials"),
		new("_MODIFIER_LOCAL_SUPPLIES_", "local_supplies"),
		new("_MODIFIER_GLOBAL_SUPPLIES_", "global_supplies"),
		new("_MODIFIER_LOCAL_FUEL_", "local_fuel"),
		new("_MODIFIER_GLOBAL_FUEL_", "global_fuel"),
		new("_MODIFIER_LOCAL_MONEY_", "local_money"),
		new("_MODIFIER_GLOBAL_MONEY_", "global_money"),
		new("_MODIFIER_LOCAL_LEADERSHIP_", "local_leadership"),
		new("_MODIFIER_GLOBAL_LEADERSHIP_", "global_leadership"),
		new("_MODIFIER_LOCAL_LEADERSHIP_MODIFIER_", "local_leadership_modifier"),
		new("_MODIFIER_GLOBAL_LEADERSHIP_MODIFIER_", "global_leadership_modifier"),
		new("_MODIFIER_DRIFT_SPEED_", "drift_speed"),
		new("_MODIFIER_SUSEPTIBILITY_", "suseptibility"),
		new("_MODIFIER_INCORPORATE_COST_", "incorporate_cost"),
		new("_MODIFIER_TERRITORIAL_PRIDE_", "territorial_pride"),
		new("_MODIFIER_WAR_CONSUMER_GOODS_DEMAND_", "war_consumer_goods_demand"),
		new("_MODIFIER_PEACE_CONSUMER_GOODS_DEMAND_", "peace_consumer_goods_demand"),
		new("_MODIFIER_ESPIONAGE_BONUS_", "espionage_bonus"),
		new("_MODIFIER_DISSENT_", "dissent"),
		new("_MODIFIER_NATIONAL_UNITY_", "national_unity"),
		new("_MODIFIER_NAVAL_CAPACITY_", "naval_capacity"),
		new("_MODIFIER_AIR_CAPACITY_", "air_capacity"),
		new("_MODIFIER_ALIGN_TOWARDS_", "align_towards"),
		new("_MODIFIER_RADAR_LEVEL_", "radar_level"),
		new("_MODIFIER_SUPPLY_CONSUMPTION_", "supply_consumption"),
		new("_MODIFIER_PEACETIME_MANPOWER_ROTATION_", "peacetime_manpower_rotation"),
		new("_MODIFIER_UNIT_RECRUITMENT_TIME_", "unit_recruitment_time"),
		new("_MODIFIER_UNIT_START_EXPERIENCE_", "unit_start_experience"),
		new("_MODIFIER_UNIT_REPAIR_", "unit_repair"),
		new("_MODIFIER_COUNTER_INTELLIGENCE_", "counter_intelligence"),
		new("_MODIFIER_COUNTER_ESPIONAGE_", "counter_espionage"),
		new("_MODIFIER_THREAT_IMPACT_", "threat_impact"),
		new(null, "threat_resistance"),
		new("_MODIFIER_PEACE_OFFMAP_INTEL_", "peace_offmap_intel"),
		new("_MODIFIER_OFFMAP_LAND_INTEL_", "offmap_land_intel"),
		new("_MODIFIER_OFFMAP_NAVAL_INTEL_", "offmap_naval_intel"),
		new("_MODIFIER_OFFMAP_INDUSTRY_INTEL_", "offmap_industry_intel"),
		new("_MODIFIER_OFFMAP_POLITICAL_INTEL_", "offmap_political_intel"),
		new("_MODIFIER_COMBAT_MOVEMENT_SPEED_", "combat_movement_speed"),
		new("_MODIFIER_ATTACK_REINFORCE_CHANCE_", "attack_reinforce_chance"),
		new("_MODIFIER_DEFEND_REINFORCE_CHANCE_", "defend_reinforce_chance"),
		new("_MODIFIER_COMBAT_WIDTH_", "combat_width"),
		new("_MODIFIER_ORG_REGAIN_", "org_regain"),
		new("_MODIFIER_NATIONAL_UNITY_EFFECT_", "national_unity_effect"),
		new("_MODIFIER_RULING_PARTY_SUPPORT_", "ruling_party_support"),
		new("_MODIFIER_LAND_ORGANISATION_", "land_organisation"),
		new("_MODIFIER_AIR_ORGANISATION_", "air_organisation"),
		new("_MODIFIER_NAVAL_ORGANISATION_", "naval_organisation"),
		new("_MODIFIER_RESEARCH_EFFICIENCY_", "research_efficiency"),
		new("_MODIFIER_INDUSTRIAL_EFFICIENCY_", "industrial_efficiency"),
		new("_MODIFIER_LOCAL_ANTI_AIR_", "local_anti_air"),
		new("_MODIFIER_LOCAL_PARTISAN_SUPPORT_", "local_partisan_support"),
		new("_MODIFIER_RESERVES_PENALTY_SIZE_", "reserves_penalty_size"),
		new("_MODIFIER_NEUTRALITY_CHANGE_", "neutrality_change"),
		new("_MODIFIER_PARTISAN_EFFICENCY_", "partisan_efficiency"),
		new("_MODIFIER_NAVAL_BASE_EFFICIENCY_", "naval_base_efficiency"),
		new("_MODIFIER_SUPPLY_THROUGHPUT_", "supply_throughput"),
		new("_MODIFIER_OFFICER_RECRUITMENT_", "officer_recruitment"),
	};

	/// <summary>The key of the kind a number names, or null for a number past the list.</summary>
	public static string? KeyOf(int number) => number >= 0 && number < All.Length ? All[number].Key : null;
}
