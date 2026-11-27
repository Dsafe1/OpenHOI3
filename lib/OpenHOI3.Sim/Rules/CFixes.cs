using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenHOI3.Sim.Rules;

/// <summary>
/// The fixes by the names a mod's file, a save and the Lua know them under - lower case with underscores, matched case
/// and all. A name, once given out, is kept: a mod's file and every enhanced save hold it.
/// </summary>
public static class CFixes
{
	private static readonly (EFix Fix, string Name)[] Table =
	{
		(EFix.MinisterTechDecay, "minister_tech_decay"),
		(EFix.SimplifiedTerrainSea, "simplified_terrain_sea"),
		(EFix.OffmapIc, "offmap_ic"),
		(EFix.FreshScreens, "fresh_screens"),
		(EFix.MenuResumes, "menu_resumes"),
		(EFix.TooltipsOnScreen, "tooltips_on_screen"),
		(EFix.ShownFigures, "shown_figures"),
		(EFix.UnitOrdinals, "unit_ordinals"),
		(EFix.AirOrdersForAll, "air_orders_for_all"),
		(EFix.KeyWithoutEquals, "key_without_equals"),
		(EFix.UnitFigureDecimals, "unit_figure_decimals"),
		(EFix.YieldModifiers, "yield_modifiers"),
		(EFix.TechRepairCost, "tech_repair_cost"),
		(EFix.TraitDissentImpact, "trait_dissent_impact"),
		(EFix.RaidTraitHours, "raid_trait_hours"),
		(EFix.TraitFortDefence, "trait_fort_defence"),
		(EFix.RadarEfficiency, "radar_efficiency"),
		(EFix.TraitSurpriseChance, "trait_surprise_chance"),
		(EFix.HistoricalModelChoice, "historical_model_choice"),
		(EFix.QueuedHistoricalModel, "queued_historical_model"),
		(EFix.PlaceUnresearchedBuilding, "place_unresearched_building"),
		(EFix.OobLeaderRanks, "oob_leader_ranks"),
		(EFix.HistoryLeaderRanks, "history_leader_ranks"),
		(EFix.SavedEventCandidates, "saved_event_candidates"),
		(EFix.CtrlUnassignSelected, "ctrl_unassign_selected"),
		(EFix.TooltipScroll, "tooltip_scroll"),
		(EFix.AirRegroupAgrees, "air_regroup_agrees"),
		(EFix.DebtRepaid, "debt_repaid"),
		(EFix.DebtRevokeCost, "debt_revoke_cost"),
		(EFix.LendLeasePartial, "lend_lease_partial"),
		(EFix.TacticAggressiveness, "tactic_aggressiveness"),
		(EFix.FriendlyFireTarget, "friendly_fire_target"),
		(EFix.CombinedArmsCondition, "combined_arms_condition"),
		(EFix.RedeployDelayOnce, "redeploy_delay_once"),
		(EFix.InterdictionTarget, "interdiction_target"),
		(EFix.AirRaidWaits, "air_raid_waits"),
		(EFix.MinisterIntel, "minister_intel"),
		(EFix.WeightedSongs, "weighted_songs"),
	};

	/// <summary>Every fix, in the order of their names.</summary>
	public static IEnumerable<EFix> All => Table.OrderBy(entry => entry.Name, StringComparer.Ordinal).Select(entry => entry.Fix);

	/// <summary>A fix's name.</summary>
	public static string Name(EFix fix)
	{
		foreach ((EFix candidate, string name) in Table)
		{
			if (candidate == fix)
			{
				return name;
			}
		}
		throw new ArgumentOutOfRangeException(nameof(fix), fix, "a fix with no name");
	}

	/// <summary>The fix of a name, or none where no fix has it.</summary>
	public static EFix? Named(string name)
	{
		foreach ((EFix fix, string candidate) in Table)
		{
			if (string.Equals(candidate, name, StringComparison.Ordinal))
			{
				return fix;
			}
		}
		return null;
	}
}
