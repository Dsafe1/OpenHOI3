using System;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// The original's name of each thing an hour puts on a unit, <c>BM_</c> and the rest, in
/// the order of its numbers: what a battle's window names a modifier's icon after and what
/// a <c>has_combat_modifier</c> condition is written with, case and all. See
/// <c>docs/mechanics/combatview.md</c>, section 4, and <c>docs/mechanics/leaders.md</c>,
/// section 11.
/// </summary>
public static class CCombatModifierKeys
{
	public static readonly (ECombatModifier Kind, string Key)[] All =
	{
		(ECombatModifier.Dissent, "BM_DISSENT"),
		(ECombatModifier.Leader, "BM_LEADER_BONUS"),
		(ECombatModifier.Envelopment, "BM_ENVELOPMENT_PENALTY"),
		(ECombatModifier.Stacking, "BM_DIVISION_PENALTY"),
		(ECombatModifier.Experience, "BM_EXPERIENCE"),
		(ECombatModifier.MissionEfficiency, "BM_MISSION_EFFICIENCY"),
		(ECombatModifier.TerritorialPride, "BM_TERRITORIAL_PRIDE"),
		(ECombatModifier.Radio, "BM_RADIO"),
		(ECombatModifier.DugIn, "BM_DUGIN_MODIFIER"),
		(ECombatModifier.Amphibious, "BM_AMPH_PENALTY"),
		(ECombatModifier.Fort, "BM_FORT_MODIFIER"),
		(ECombatModifier.Paratroop, "BM_PARATROOP_PENALTY"),
		(ECombatModifier.River, "BM_RIVER_PENALTY"),
		(ECombatModifier.LackOfSupplies, "BM_LACK_OF_SUPPLIES"),
		(ECombatModifier.MultipleCombats, "BM_MULTIPLE"),
		(ECombatModifier.Difficulty, "BM_DIFFICULTY"),
		(ECombatModifier.CombinedArms, "BM_COMBINED_ARMS"),
		(ECombatModifier.BaseProximity, "BM_BASE_PROXIMITY"),
		(ECombatModifier.PoorScreens, "BM_POOR_SCREEN_PENALTY"),
		(ECombatModifier.Radar, "BM_RADAR_STATION"),
		(ECombatModifier.Intercept, "BM_INTERCEPT"),
		(ECombatModifier.AirCombat, "BM_AIRCOMBAT"),
		(ECombatModifier.Terrain, "BM_TERRAIN"),
		(ECombatModifier.Weather, "BM_WEATHER"),
		(ECombatModifier.Night, "BM_NIGHT_MODIFIER"),
		(ECombatModifier.Surprise, "BM_SURPRISE_BONUS"),
	};

	/// <summary>
	/// The original's names of the modifiers nothing here puts on a unit yet:
	/// encirclement, shore bombardment, the surprise penalty and the armour
	/// advantage. A condition may name them; it never holds.
	/// </summary>
	public static readonly string[] NotBuilt =
	{
		"BM_ENCIRCLEMENT_PENALTY", "BM_SHORE_BOMBARD",
		"BM_SURPRISE_PENALTY", "BM_ARMOR_ADVANTAGE",
	};

	/// <summary>The modifier of a name, as the original matches it: exactly, case and all.</summary>
	public static ECombatModifier? Find(string key)
	{
		foreach ((ECombatModifier kind, string name) in All)
		{
			if (string.Equals(name, key, StringComparison.Ordinal))
			{
				return kind;
			}
		}
		return null;
	}

	/// <summary>Whether the original knows the name at all, built here or not.</summary>
	public static bool IsKnown(string key)
	{
		return Find(key) is not null || Array.IndexOf(NotBuilt, key) >= 0;
	}
}
