using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// How many of some units each of a list of positions has: for each land unit, the first position in the list that is where
/// it stands or where it is bound takes it. Standing there it counts as standing and as heading there - unless it is on its
/// way to a province no enemy holds, when it is counted nowhere; bound there it counts as heading. A unit falling back with
/// less than half its organisation is not counted. Given an agent at war with focus areas, every land unit of each country
/// fighting on our side that holds the first province of one of those areas is counted the same way. The original's
/// <c>CAIUnit::CountUnitsAtPositions</c>. See <c>docs/mechanics/moving.md</c>, section 7.3.
/// </summary>
public static class CPositionCount
{
	private const long HalfOrganisation = 500;

	public static void Count(CGameState state, CCountry country, IEnumerable<CUnit> units, IReadOnlyList<int> positions,
		Action<int> heading, Action<int> standing, bool divisionsOnly, CAgent? withAllies = null)
	{
		CountAt(state, country, units, positions, index => heading(positions[index]), index => standing(positions[index]),
			divisionsOnly, withAllies);
	}

	/// <summary>The same, told by the index of the position in the list: a province listed twice is two records.</summary>
	public static void CountAt(CGameState state, CCountry country, IEnumerable<CUnit> units, IReadOnlyList<int> positions,
		Action<int> heading, Action<int> standing, bool divisionsOnly, CAgent? withAllies = null)
	{
		CountOwn(state, country, units, positions, heading, standing, divisionsOnly);
		if (withAllies is null || withAllies.FocusAreas.Count == 0 || !withAllies.FrontierAtWar)
		{
			return;
		}
		foreach (CCountry other in state.Countries)
		{
			if (other.Tag != country.Tag && CCountryLists.FightingTogether(state, country.Tag, other.Tag)
				&& !CCountryLists.IsEnemy(state, country, other.Tag) && HoldsAFocusArea(state, withAllies, other))
			{
				CountOwn(state, country, other.AllUnits(), positions, heading, standing, divisionsOnly);
			}
		}
	}

	private static bool HoldsAFocusArea(CGameState state, CAgent agent, CCountry other)
	{
		foreach (CSupplyArea area in agent.FocusAreas)
		{
			if (area.Provinces.Count > 0 && state.Province(area.Provinces[0]).Controller == other.Tag)
			{
				return true;
			}
		}
		return false;
	}

	private static void CountOwn(CGameState state, CCountry country, IEnumerable<CUnit> units, IReadOnlyList<int> positions,
		Action<int> heading, Action<int> standing, bool divisionsOnly)
	{
		foreach (CUnit unit in units)
		{
			if (!CTheatres.IsLand(unit) || (divisionsOnly && unit.Kind != EUnitKind.Division))
			{
				continue;
			}
			if (unit.IsRetreating)
			{
				CUnitShares.Of(state, unit, out long _, out long organisation);
				if (organisation < HalfOrganisation)
				{
					continue;
				}
			}
			int bound = CAgentUnits.MovementDestination(unit);
			for (int index = 0; index < positions.Count; index++)
			{
				int position = positions[index];
				if (position == unit.Location)
				{
					if (bound <= 0 || (state.Province(bound).Controller is { } holder && CCountryLists.IsEnemy(state, country, holder)))
					{
						heading(index);
						standing(index);
					}
					break;
				}
				if (position == bound)
				{
					heading(index);
					break;
				}
			}
		}
	}
}
