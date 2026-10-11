using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Supply;

/// <summary>
/// What the units drawing on each province ask for in a day: the land units standing in
/// it, the wings based there and the fleets whose port it is. See
/// <c>docs/mechanics/supply.md</c>, section 11.
/// </summary>
public static class CSupplyNeed
{
	private const string Rebels = "REB";

	/// <summary>
	/// The supplies and the fuel asked for in every province, in thousandths, by the
	/// province's number.
	/// </summary>
	/// <param name="beforeGame">
	/// Before a game is under way it is not asked whose a land unit is; afterwards one
	/// belonging to an enemy of the province's holder is left out.
	/// </param>
	public static void Count(CGameState state, bool beforeGame, long[] supplies, long[] fuel)
	{
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.IsRemoved)
				{
					continue;
				}
				int province = DrawnOn(state, country, unit, beforeGame);
				if (province <= 0 || province >= supplies.Length)
				{
					continue;
				}
				supplies[province] += CUnitSupply.SupplyConsumption(state, country, unit).Raw;
				fuel[province] += CUnitSupply.FuelConsumption(state, country, unit).Raw;
			}
		}
	}

	/// <summary>The supplies and the fuel asked for in one province, in thousandths.</summary>
	public static void Of(CGameState state, int province, bool beforeGame, out long supplies, out long fuel)
	{
		supplies = 0;
		fuel = 0;
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (!unit.IsRemoved && DrawnOn(state, country, unit, beforeGame) == province)
				{
					supplies += CUnitSupply.SupplyConsumption(state, country, unit).Raw;
					fuel += CUnitSupply.FuelConsumption(state, country, unit).Raw;
				}
			}
		}
	}

	/// <summary>The province whose need a unit is counted in, or 0 for a unit that is counted nowhere.</summary>
	private static int DrawnOn(CGameState state, CCountry owner, CUnit unit, bool beforeGame)
	{
		if (!CMovementRules.IsLandUnit(unit))
		{
			// A wing is counted where it is based and a fleet where its port is, and
			// neither is asked whose side it is on. An air group aboard a carrier is in
			// no province's list.
			return unit.CarriedBy is null ? unit.Base : 0;
		}

		if (unit.Location <= 0 || beforeGame)
		{
			return unit.Location;
		}
		CProvince here = state.Province(unit.Location);
		if (here.Controller == owner.Tag)
		{
			return unit.Location;
		}
		bool rebels = owner.Tag.Value == Rebels || here.Controller is { Value: Rebels };
		return !rebels && !CMovementRules.IsEnemy(state, owner.Tag, here.Controller) ? unit.Location : 0;
	}
}
