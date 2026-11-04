using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Sim.Movement;

/// <summary>
/// The land orders window's synchronised arrival: the latest hour any of several land units would reach a province, by a
/// reckoning of its own - each unit's top speed by the province's own terrain and weather, never the ground of each step,
/// over the length of its way, and its attack delay after. See <c>docs/mechanics/landorders.md</c>, section 4.
/// </summary>
public static class CArrivalEstimate
{
	private const long Scale = CFixedPoint.Scale;

	/// <summary>The latest arrival of the units at the province, as an hour of the game; now where none has a way.</summary>
	public static long Latest(CGameState state, CCountry country, IEnumerable<CUnit> units, CProvince province)
	{
		long latest = state.TotalHours;
		foreach (CUnit unit in units)
		{
			if (Arrival(state, country, unit, province) is { } arrival && arrival > latest)
			{
				latest = arrival;
			}
		}
		return latest;
	}

	/// <summary>One unit's arrival, or null where it has no way there or no speed.</summary>
	public static long? Arrival(CGameState state, CCountry country, CUnit unit, CProvince province)
	{
		if (CRouteFinder.Find(state, country, unit, unit.Location, province.Id) is not { } route)
		{
			return null;
		}
		long length = 0;
		int from = unit.Location;
		foreach (int step in route)
		{
			length += CMovement.LegDistance(state, unit, state.Province(from), state.Province(step)).Raw;
			from = step;
		}

		long speed = CMovementSpeed.SlowestBrigade(unit).Raw;
		if (province.Terrain is { } terrain)
		{
			speed = speed * (Scale + CMovementSpeed.Adjuster(unit, terrain.Name)) / Scale;
		}
		speed = speed * (Scale + CWeatherEffects.Movement(state, province.Weather, country)) / Scale;
		speed = speed * state.Defines[Economy.CManpower.MilitarySection, CMovementSpeed.LandSpeedModifier].Raw / Scale;
		if (speed <= 0)
		{
			return null;
		}
		long hours = (length + speed - 1) / speed;
		return state.TotalHours + hours + unit.AttackDelay.Raw / Scale;
	}
}
