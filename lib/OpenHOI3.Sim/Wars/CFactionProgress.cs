using System.Collections.Generic;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// A faction's progress: the victory points of the provinces its members hold, made again every hour, and its share
/// of the world's. See <c>docs/mechanics/factions.md</c>, section 7.
/// </summary>
public static class CFactionProgress
{
	/// <summary>
	/// In the hour: each group's faction set to nought - the null faction only for a group with none, so never in a
	/// game whose groups all have one - then each province's points, in thousandths, added to its holder's faction.
	/// </summary>
	public static void RunHourly(CGameState state)
	{
		foreach (CIdeologyGroup group in state.IdeologyGroups)
		{
			state.FactionProgress[group.Faction] = 0;
		}
		foreach (CProvince province in state.Provinces)
		{
			if (province.Points == 0 || state.Country(province.Controller) is not { } holder)
			{
				continue;
			}
			state.FactionProgress[holder.Faction] = unchecked(state.FactionProgress.GetValueOrDefault(holder.Faction) + province.Points * 1000);
		}
	}

	/// <summary>The faction's progress, in thousandths of a point.</summary>
	public static int Progress(CGameState state, string faction) => state.FactionProgress.GetValueOrDefault(faction);

	/// <summary>The points of every province of the map, sea and land, in thousandths.</summary>
	public static long Total(CGameState state)
	{
		long total = 0;
		foreach (CProvince province in state.Provinces)
		{
			total += province.Points * 1000L;
		}
		return total;
	}

	/// <summary>
	/// The original's <c>CFaction::GetVictoryPointShare</c>: a percentage in thousandths, cut to a thousandth of the
	/// whole before it is made one - so a tenth of a percent at the finest. The progress itself times a hundred where
	/// the world has no points.
	/// </summary>
	public static long Share(CGameState state, string faction)
	{
		long progress = Progress(state, faction);
		long total = Total(state);
		long share = total != 0 ? progress * 1000 / total : progress;
		return share * 100000 / 1000;
	}
}
