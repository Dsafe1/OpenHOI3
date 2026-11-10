using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// How many of some units each of a list of positions has: for each land unit, the first position in the list that is where
/// it stands or where it is bound takes it. Standing there it counts as standing and as heading there - unless it is on its
/// way to a province no enemy holds, when it is counted nowhere; bound there it counts as heading. A unit falling back with
/// less than half its organisation is not counted. The original's <c>CAIUnit::CountUnitsAtPositions</c>, without the allies'
/// units the line asks for. See <c>docs/mechanics/moving.md</c>, section 7.3.
/// </summary>
public static class CPositionCount
{
	private const long HalfOrganisation = 500;

	public static void Count(CGameState state, CCountry country, IEnumerable<CUnit> units, IReadOnlyList<int> positions,
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
			int bound = CAgentUnits.Destination(unit);
			foreach (int position in positions)
			{
				if (position == unit.Location)
				{
					if (bound <= 0 || (state.Province(bound).Controller is { } holder && CCountryLists.IsEnemy(state, country, holder)))
					{
						heading(position);
						standing(position);
					}
					break;
				}
				if (position == bound)
				{
					heading(position);
					break;
				}
			}
		}
	}
}
