using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// The paradrop planner's targets, made again once a day: over the country's enemies and then its prepared wars' targets -
/// a country on both is walked twice - each target's capital where no land brigade of anybody stands, and every port of
/// each of its areas of five provinces or fewer where none stands; each scored by its naval base and air base, more for a
/// depot, kept above nought and best first. The original's <c>CEU3AI_RebuildWeakEnemyPorts</c>. See
/// <c>docs/mechanics/moving.md</c>, section 9.1.
/// </summary>
public static class CWeakEnemyPorts
{
	/// <summary>An area of more provinces than this has no port taken.</summary>
	private const int SmallArea = 5;

	public static void Rebuild(CGameState state, CCountry country)
	{
		List<(int Province, float Score)> ports = country.UnitAi.WeakEnemyPorts;
		ports.Clear();
		var targets = new List<CCountryTag>();
		foreach (CCountry enemy in CCountryLists.Enemies(state, country))
		{
			targets.Add(enemy.Tag);
		}
		foreach (Ai.CWarTarget war in country.WarTargets)
		{
			targets.Add(war.Target);
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		foreach (CCountryTag tag in targets)
		{
			if (state.Country(tag) is not { } target)
			{
				continue;
			}
			// The capital as its own field has it, not where the government sits.
			if (target.Capital > 0 && state.Province(target.Capital) is { } capital && !HasLandBrigades(state, capital))
			{
				Add(ports, capital.Id, Score(state, network, capital));
			}
			foreach (CSupplyArea area in network.Areas)
			{
				if (area.Controller != tag || area.Provinces.Count > SmallArea)
				{
					continue;
				}
				foreach (int id in area.Ports)
				{
					CProvince port = state.Province(id);
					if (!HasLandBrigades(state, port))
					{
						Add(ports, id, Score(state, network, port));
					}
				}
			}
		}
	}

	/// <summary>
	/// A hundred a level of naval base built, whole levels; ten a level of air base working, in fractions; a thousand more
	/// for a province that is its own depot - in single precision, as the original sums it.
	/// </summary>
	public static float Score(CGameState state, CSupplyNetwork network, CProvince province)
	{
		long naval = CMovementRules.BuildingLevel(state, province, CMovementRules.NavalBase).Raw / CFixedPoint.Scale;
		long air = CBuildingRoles.Find(state, CMovementRules.AirBase) is { } airBase ? province.BuildingLevel(airBase).Raw : 0;
		float score = (float)((float)(naval * 100.0) + (float)(air / 1000.0) * 10.0);
		if (network.DepotOf(province.Id) == province.Id)
		{
			score = (float)(score + 1000.0);
		}
		return score;
	}

	/// <summary>Whether a land unit of anybody's with a brigade of its own stands in the province.</summary>
	private static bool HasLandBrigades(CGameState state, CProvince province)
	{
		int brigades = 0;
		foreach (CUnit unit in state.UnitsIn(province.Id))
		{
			if (CMovementRules.IsLandUnit(unit))
			{
				brigades += unit.SubUnits.Count;
			}
		}
		return brigades >= 1;
	}

	/// <summary>Kept above nought, by score highest first, a new one after its equals.</summary>
	private static void Add(List<(int Province, float Score)> ports, int province, float score)
	{
		if (!(score > 0f))
		{
			return;
		}
		int at = ports.Count;
		while (at > 0 && score > ports[at - 1].Score)
		{
			at--;
		}
		ports.Insert(at, (province, score));
	}
}
