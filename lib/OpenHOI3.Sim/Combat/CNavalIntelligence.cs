using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// How well a country knows a province, as far as a fleet's sighting asks. The original
/// keeps a level from 0 to 9 for every country in every province and works them all out
/// afresh once a day; what is built of it is a unit's own part: land a country's unit
/// stands in is known at 9, a sea province one of its units was in at the day's turn at
/// 2, and at 9 where that unit was a patrol shadowing somebody. See
/// <c>docs/mechanics/navalmissions.md</c>, section 9.
/// </summary>
public static class CNavalIntelligence
{
	/// <summary>How well a country knows a sea province it has a unit in.</summary>
	public const int LevelWithAUnitThere = 2;

	/// <summary>The level of one's own ground, and of water a patrol shadows a fleet in: a sighting with no roll.</summary>
	public const int LevelOfOwnGround = 9;

	/// <summary>How well a fleet's country knows the province the fleet is in.</summary>
	public static int Level(CGameState state, CProvince province, CUnit fleet)
	{
		if (!province.IsSea)
		{
			return fleet.Location == province.Id ? LevelOfOwnGround : 0;
		}
		return state.SeaIntelligence.TryGetValue((province.Id, fleet.Owner), out int level) ? level : 0;
	}

	/// <summary>
	/// The day's refresh: every level is forgotten, and each unit gives its country the
	/// province it is in - at sea, and on land that somebody else holds.
	/// </summary>
	public static void Refresh(CGameState state)
	{
		Dictionary<(int, CCountryTag), int> levels = state.SeaIntelligence;
		levels.Clear();
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.IsRemoved || unit.Location <= 0 || unit.Location >= state.Provinces.Count)
				{
					continue;
				}
				CProvince province = state.Province(unit.Location);
				if (!province.IsSea && province.Controller == unit.Owner)
				{
					continue;                                    // its own ground it knows anyway
				}
				int level = !province.IsSea || (unit.Order == EUnitOrder.Patrol && unit.Mission.LiveTarget is not null)
					? LevelOfOwnGround
					: LevelWithAUnitThere;
				var key = (unit.Location, unit.Owner);
				if (!levels.TryGetValue(key, out int known) || known < level)
				{
					levels[key] = level;
				}
			}
		}
	}
}
