using System;
using System.Collections.Generic;
using System.Text;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What levels of a technology do, as the original writes it into a technology's tooltip:
/// the unit types and the building the first level allows, what the levels add to each unit
/// type the country may use, and what they do for the whole country - every figure the
/// technology's per level times the number of levels. See
/// <c>docs/mechanics/technologyscreen.md</c>, section 9.
/// </summary>
public static class CTechnologyEffectsText
{
	private const string Line = "§W\n";
	private const string PercentLine = "%§W\n";
	private const string TacticEnd = "%§!\n";

	/// <summary>How a country-wide figure's colour is chosen.</summary>
	private enum EColour
	{
		/// <summary>Always green, with no plus.</summary>
		Green,

		/// <summary>Green with a plus above nothing, red below: more is better.</summary>
		More,

		/// <summary>Green below nothing, red with a plus above: less is better.</summary>
		Less,
	}

	/// <summary>
	/// The country-wide figures in the order the original writes them: the effect's key, its
	/// label, its colour, whether it is shown times a hundred, its places and what ends it.
	/// </summary>
	private static readonly (string Key, string Label, EColour Colour, bool Hundred, int Places, string End)[] Figures =
	{
		("ground_defence_effiency", "GDE_FROM_TECH", EColour.Green, true, 2, PercentLine),
		("dig_in_cap", "DIGIN_FROM_TECH", EColour.Green, false, 0, Line),
		("attack_delay", "ATTACK_DELAY_FROM_TECH", EColour.Green, false, 0, ""),
		("division_size", "DIVISION_SIZE_FROM_TECH", EColour.More, false, 0, Line),
		("supply_transfer_cost", "SUPPLY_TRANSFER_COST_TECH", EColour.Less, false, 2, Line),
		("supply_throughput", "SUPPLY_THROUGHPUT_TECH", EColour.More, true, 2, PercentLine),
		("refinery_efficiency", "REFINERY_EFFICIENCY_TECH", EColour.More, true, 2, PercentLine),
		("radar_efficiency", "RADAR_EFFICIENCY_TECH", EColour.More, true, 2, Line),
		("radio_strength", "RADIO_STRENGTH_TECH", EColour.More, false, 0, PercentLine),
		("decryption", "DECRYPTION_TECH", EColour.More, false, 1, Line),
		("encryption", "ENCRYPTION_TECH", EColour.More, false, 1, Line),
		("convoy_build_cost", "CONVOY_COST_TECH", EColour.More, false, 0, Line),
		("convoy_build_time", "CONVOY_TIME_TECH", EColour.More, false, 0, Line),
		("escort_build_cost", "ESCORT_COST_TECH", EColour.More, false, 0, Line),
		("escort_build_time", "ESCORT_TIME_TECH", EColour.More, false, 0, Line),
		("attack_movement_speed", "ATTACK_MOVEMENT_SPEED_TECH", EColour.More, true, 2, PercentLine),
		("reinforce_chance", "REINFORCE_CHANCE_TECH", EColour.More, true, 2, PercentLine),
		("unit_cooperation", "UNIT_COOP_TECH", EColour.More, true, 2, PercentLine),
		("naval_base_efficiency", "NAVAL_BASE_EFF_TECH", EColour.More, true, 2, PercentLine),
		("targeting_chance", "TARGET_CHANCE_TECH", EColour.More, true, 2, PercentLine),
		("targeting_choice", "TARGET_CHOICE_TECH", EColour.More, true, 2, PercentLine),
		("escort_efficiency", "ESCORT_EFFICIENCY_TECH", EColour.More, true, 2, PercentLine),
	};

	/// <summary>The two that are yes or no, written after the first block whatever the level.</summary>
	private static readonly (string Key, string Label)[] Flags =
	{
		(CTechnology.AllowEscorts, "ALLOW_ESCORTS_TECH"), (CTechnology.ListeningStation, "LISTENING_STATION_TECH"),
	};

	/// <summary>The country-wide figures after the two flags, all more-is-better percentages but one.</summary>
	private static readonly (string Key, string Label, EColour Colour)[] Percentages =
	{
		("casualty_trickleback", "CASUALTY_TRICKLEBACK_TECH", EColour.More),
		("maximum_attrition", "MAX_ATTRITION_TECH", EColour.Less),
		("manpower_gain", "MANPOWER_GAIN_TECH", EColour.More),
		("ic_modifier", "IC_MOD_TECH", EColour.More),
		("ic_efficiency", "IC_EFFICIENCY_TECH", EColour.More),
		("energy_to_oil_conversion", "ENERGY_TO_OIL_TECH", EColour.More),
		("ic_to_supplies", "IC_TO_SUPPLIES_TECH", EColour.More),
		("provincial_aa_efficiency", "PROV_AA_TECH", EColour.More),
		("combat_efficiency", "COMBAT_EFF_TECH", EColour.More),
		("research_efficiency", "RESEARC_EFF_TECH", EColour.More),
		("metal_production", "METAL_PROD_TECH", EColour.More),
		("rares_production", "RARES_PROD_TECH", EColour.More),
		("energy_production", "ENERGY_PROD_TECH", EColour.More),
		("leadership_gain", "LEADERSHIP_GAIN_TECH", EColour.More),
		("nuclear_production", "NUKE_PROD_GAIN_TECH", EColour.More),
		("radar_impact", "RADAR_IMPACT_TECH", EColour.More),
		("repair_rate", "REPAIR_RATE_TECH", EColour.More),
		("bomber_targeting", "BOMBER_TARGET_TECH", EColour.More),
		("fighter_targeting", "FIGHTER_TARGET_TECH", EColour.More),
		("frontline_focus", "FRONTLINE_FOCUS_TECH", EColour.More),
		("reserve_focus", "RESERVE_FOCUS_TECH", EColour.More),
		("naval_air_target_chance", "NAVAL_AIR_TARGET_CHANCE_TECH", EColour.More),
	};

	/// <summary>The missions in the original's order of them, each by its name in the files and its label.</summary>
	private static readonly (string Mission, string Label)[] Missions =
	{
		("none", "NO_ORDERS"), ("support_attack", "SUPPORT_ATTACK"), ("strategic_redeployment", "ORDER_STRATEGIC_REDEPLOYMENT"),
		("move_order", "MOVE_ORDER"), ("rebase", "ORDER_REBASE"), ("reserves", "ORDER_RESERVE"), ("patrol", "ORDER_PATROL"),
		("intercept", "ORDER_INTERCEPT"), ("sortie", "ORDER_SORTIE"), ("convoy_escort", "ORDER_CONVOY_ESCORT"),
		("convoy_raid", "ORDER_CONVOY_RAID"), ("air_convoy_raid", "ORDER_AIR_CONVOY_RAID"), ("transport", "ORDER_TRANSPORT"),
		("invasion", "ORDER_INVADE"), ("strategic_bomb", "STRAT_BOMB"), ("logistical_strike", "LOG_BOMB"),
		("runway_cratering", "AIRFIELD_BOMB"), ("installation_strike", "INSTALLATION_BOMB"), ("ground_attack", "GROUNDATTACK_BOMB"),
		("interdiction", "INTERDICT_BOMB"), ("air_intercept", "INTERCEPT"), ("carrier_protection", "CAG_DUTY"),
		("paradrop_mission", "PARADROP_MISSION"), ("port_strike", "PORTSTRIKE_BOMB"), ("naval_strike", "NAVALSTRIKE_BOMB"),
		("rebase_to_carrier", "REBASE_TO_CAG_MISSION"), ("nuke_mission", "NUKE_MISSION"),
		("transport_supplies_mission", "TRANS_SUP_MISSION"), ("rebase_air", "ORDER_AIR_REBASE"), ("air_reserve", "ORDER_AIR_RESERVE"),
		("air_superiority", "AIRSUP"), ("join_fleet", "ORDER_JOIN_FLEET"), ("join_air", "ORDER_JOIN_AIR"),
	};

	/// <summary>
	/// A unit type's figures in the order the original writes them: the label, the figure's
	/// key, its places, whether more is better, and the unit's word after it - DEFAULT_MORALE's
	/// a percentage, shown times a hundred.
	/// </summary>
	private static readonly (string Label, string Key, int Places, bool Better, string Unit)[] Statistics =
	{
		("MAX_STRENGTH", "max_strength", 0, true, ""), ("NEEDED_OFFICERS", "officers", 0, true, ""),
		("DEFAULT_ORG", "default_organisation", 1, true, ""), ("DEFAULT_MORALE", "default_morale", 1, true, "%"),
		("BUILD_COST_IC", "build_cost_ic", 2, false, "IC"), ("BUILD_COST_MP", "build_cost_manpower", 2, false, ""),
		("BUILD_TIME", "build_time", 0, false, "DAYS"), ("MAXIMUM_SPEED", "maximum_speed", 2, true, "KPH"),
		("TRANSPORT_WEIGHT", "transport_weight", 2, false, ""), ("SUPPLY_CONSUMPTION", "supply_consumption", 2, false, ""),
		("FUEL_CONSUMPTION", "fuel_consumption", 2, false, ""), ("DEFENSIVENESS", "defensiveness", 2, true, ""),
		("TOUGHNESS", "toughness", 2, true, ""), ("SOFTNESS", "softness", 2, false, ""),
		("ARMOR_VALUE", "armor_value", 2, true, ""), ("AIR_DEFENCE", "air_defence", 2, true, ""),
		("SUPPRESSION", "suppression", 2, true, ""), ("SOFT_ATTACK", "soft_attack", 2, true, ""),
		("HARD_ATTACK", "hard_attack", 2, true, ""), ("AP_ATTACK", "ap_attack", 2, true, ""),
		("AIR_ATTACK", "air_attack", 2, true, ""), ("COMBAT_WIDTH", "combat_width", 2, false, ""),
		("TRANSPORT_CAPABILITY", "transport_capability", 2, true, ""),
		("AMPH_INVASION_SPEED_BONUS", "amphibious_invasion_speed", 2, true, ""),
		("AMPH_INVASION_DEFENCE_BONUS", "amphibious_invasion_defence", 2, true, ""),
		("RANGE", "range", 0, true, "KM"), ("DISTANCE", "distance", 1, true, "KM"),
		("SURFACE_DETECTION", "surface_detection", 2, true, ""), ("AIR_DETECTION", "air_detection", 2, true, ""),
		("SUB_DETECTION", "sub_detection", 2, true, ""), ("VISIBILITY", "visibility", 2, false, ""),
		("SEA_DEFENCE", "sea_defence", 2, true, ""), ("CONVOY_ATTACK", "convoy_attack", 2, true, ""),
		("SEA_ATTACK", "sea_attack", 2, true, ""), ("SUB_ATTACK", "sub_attack", 2, true, ""),
		("SHORE_BOMBARDMENT", "shore_bombardment", 2, true, ""), ("SURFACE_DEFENCE", "surface_defence", 2, true, ""),
		("STRATEGIC_ATTACK", "strategic_attack", 2, true, ""), ("RADIO_STRENGTH", "radio_strength", 2, false, ""),
		("HULL", "hull", 2, true, ""), ("POSITIONING", "positioning", 2, true, ""),
		("CARRIER_CAPACITY", "carrier_size", 2, true, ""),
	};

	/// <summary>The four circumstances a unit type's adjusters are written for before the terrains, with their labels.</summary>
	private static readonly (string Adjuster, string Label)[] Circumstances =
	{
		("night", "NIGHT_ADJUSTER"), ("fort", "FORT_ADJUSTER"), ("river", "RIVER_ADJUSTER"), ("amphibious", "AMPH_ADJUSTER"),
	};

	/// <summary>
	/// The whole text for a number of levels of a technology, for a country. Given a unit type, only that type's figures
	/// are written of the types it changes, each on a line of its own, as a ship's list of levels has them.
	/// </summary>
	public static string For(Func<string, string> localise, CGameState state, CCountry country, CTechnology technology, int levels,
		CSubUnitType? only = null)
	{
		bool beyondOne = levels > 1 || levels < -1;
		if (levels < 0)
		{
			levels = 1;
		}
		var text = new StringBuilder();
		// What it allows is said for a first level only.
		if (!beyondOne)
		{
			foreach (string key in technology.ActivatesUnits)
			{
				if (state.SubUnitTypes.TryGetValue(key, out CSubUnitType? type) && CUnitAvailability.IsUsableBy(country, type))
				{
					text.Append(localise("ENABLE_UNIT_TECH")).Append(" §Y").Append(localise(key)).Append(Line);
				}
			}
			if (technology.ActivatesBuilding.Length != 0 && HasBuilding(state, technology.ActivatesBuilding))
			{
				text.Append(localise("ENABLE_BUILDING_TECH")).Append(" §Y").Append(localise(technology.ActivatesBuilding)).Append(Line);
			}
		}
		foreach ((string key, CSubUnitStats stats) in technology.UnitEffects)
		{
			if (!state.SubUnitTypes.TryGetValue(key, out CSubUnitType? type) || !CUnitAvailability.IsUsableBy(country, type)
				|| (only is not null && type != only))
			{
				continue;
			}
			string figures = UnitFigures(localise, state, stats, levels).Trim('\n', '\t', '\r', ' ');
			// Each line of them is folded into one, a comma between.
			if (only is null)
			{
				figures = figures.Replace(" \n", ", ");
			}
			text.Append(localise(key + "_short")).Append(": ").Append(figures).Append('\n');
		}
		text.Append(CountryFigures(localise, state, technology, levels));
		return text.ToString();
	}

	/// <summary>
	/// What levels add to a unit type: a line for each figure that is not nothing, and then a
	/// line for each circumstance and each terrain whose adjuster is not.
	/// </summary>
	public static string UnitFigures(Func<string, string> localise, CGameState state, CSubUnitStats stats, int levels)
	{
		var text = new StringBuilder();
		foreach ((string label, string key, int places, bool better, string unit) in Statistics)
		{
			long value = stats[key].Raw * levels;
			if (unit == "%")
			{
				value *= 100;
			}
			text.Append(StatLine(localise, label, value, better, places, unit == "%" || unit.Length == 0 ? unit : localise(unit)));
		}
		foreach ((string adjuster, string label) in Circumstances)
		{
			AdjusterLine(localise, text, stats, adjuster, localise(label), levels);
		}
		foreach (CTerrain terrain in state.Terrains)
		{
			AdjusterLine(localise, text, stats, terrain.Name, localise(terrain.Name), levels);
		}
		return text.ToString();
	}

	/// <summary>
	/// The original's line for one figure of a unit type, as a technology's text asks for it:
	/// green or red by whether it helps, a plus above nothing, nothing at all for nothing.
	/// </summary>
	public static string StatLine(Func<string, string> localise, string label, long value, bool better, int places, string unit)
	{
		if (value == 0)
		{
			return "";
		}
		long colour = better ? value : -value;
		return localise(label) + (colour > 0 ? " §G" : " §R") + (value > 0 ? "+" : "")
			+ CNumberText.Format(new CFixedPoint(value), places) + "§W " + unit + " \n";
	}

	private static void AdjusterLine(Func<string, string> localise, StringBuilder text, CSubUnitStats stats, string adjuster,
		string label, int levels)
	{
		if (!stats.Adjusters.TryGetValue(adjuster, out CModifier? figures))
		{
			return;
		}
		var scaled = new CModifier();
		foreach (string kind in new[] { "attack", "defence", "movement", "attrition" })
		{
			scaled.Set(kind, new CFixedPoint(figures[kind].Raw * levels));
		}
		string line = CBuilderTooltips.Describe(localise, scaled);
		if (line.Length != 0)
		{
			text.Append(label).Append(": ").Append(line).Append('\n');
		}
	}

	/// <summary>What levels do for the whole country.</summary>
	public static string CountryFigures(Func<string, string> localise, CGameState state, CTechnology technology, int levels)
	{
		CModifier effect = technology.EffectPerLevel;
		var text = new StringBuilder();
		foreach ((string key, string label, EColour colour, bool hundred, int places, string end) in Figures)
		{
			long value = effect[key].Raw * levels;
			if (value == 0)
			{
				continue;
			}
			text.Append(localise(label)).Append(Colour(colour, value))
				.Append(CNumberText.Format(new CFixedPoint(hundred ? value * 100 : value), places));
			// The attack delay is in hours.
			text.Append(end.Length != 0 ? end : " " + localise("AD_HOURS") + Line);
		}
		foreach ((string key, string label) in Flags)
		{
			if (effect[key].Raw != 0)
			{
				text.Append(localise(label)).Append(Line);
			}
		}
		foreach ((string key, string label, EColour colour) in Percentages)
		{
			long value = effect[key].Raw * levels;
			if (value != 0)
			{
				text.Append(localise(label)).Append(Colour(colour, value))
					.Append(CNumberText.Format(new CFixedPoint(value * 100), 2)).Append(PercentLine);
			}
		}
		// A tactic's figure is whole percentage points, and always written green with a plus.
		foreach (Sim.Combat.CCombatTactic tactic in state.Tactics)
		{
			long value = effect[tactic.Name].Raw * levels;
			if (value != 0)
			{
				text.Append(localise(tactic.Name)).Append(": §G+").Append(CNumberText.Format(new CFixedPoint(value), 0)).Append(TacticEnd);
			}
		}
		foreach (CCombinedArmsGroup group in state.CombinedArmsGroups)
		{
			long value = effect[group.Name].Raw * levels;
			if (value != 0)
			{
				text.Append(localise(group.Name)).Append(": §G+").Append(CNumberText.Format(new CFixedPoint(value * 100), 0))
					.Append(TacticEnd);
			}
		}
		foreach ((string mission, string label) in Missions)
		{
			long efficiency = technology.MissionEfficiency.GetValueOrDefault(mission).Raw * levels;
			if (efficiency != 0)
			{
				text.Append(localise(label)).Append(localise("ORD_EFF")).Append(efficiency > 0 ? " §G+" : " §R")
					.Append(CNumberText.Format(new CFixedPoint(efficiency * 100), 1)).Append(TacticEnd);
			}
			long reduction = technology.MissionReduction.GetValueOrDefault(mission).Raw * levels;
			if (reduction != 0)
			{
				text.Append(localise(label)).Append(localise("ORD_RED")).Append(reduction > 0 ? " §R+" : " §G")
					.Append(CNumberText.Format(new CFixedPoint(reduction * 100), 1)).Append(TacticEnd);
			}
		}
		bool decayStarted = false;
		foreach (CTechnologyCategory category in state.TechnologyCategories)
		{
			long value = DecayOf(technology, category.Name) * levels;
			if (value == 0)
			{
				continue;
			}
			// A line break between the rest and the first decay, where there is a rest.
			if (!decayStarted && text.Length > 0)
			{
				text.Append('\n');
			}
			decayStarted = true;
			text.Append(localise(category.Name)).Append(' ').Append(localise("DECAY")).Append(": ")
				.Append(value < 0 ? "§G" : "§R+").Append(CNumberText.Format(new CFixedPoint(value * 100), 1)).Append(PercentLine);
		}
		return text.ToString();
	}

	private static string Colour(EColour colour, long value)
	{
		return colour switch
		{
			EColour.Green => ": §G",
			EColour.More => value > 0 ? ": §G+" : ": §R",
			_ => value < 0 ? ": §G" : ": §R+",
		};
	}

	private static long DecayOf(CTechnology technology, string category)
	{
		long sum = 0;
		foreach ((string name, CFixedPoint amount) in technology.Decay)
		{
			if (name.Equals(category, StringComparison.OrdinalIgnoreCase))
			{
				sum = amount.Raw;
			}
		}
		return sum;
	}

	private static bool HasBuilding(CGameState state, string key)
	{
		foreach (CBuilding building in state.Buildings)
		{
			if (building.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}
}
