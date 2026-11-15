using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Supply;

/// <summary>
/// What a unit brings with it into a province: as many days of what it uses as a
/// province holds for its units, taken out of the province it came from. It is what
/// lets an army walk off its supply lines and fight for a fortnight. See
/// <c>docs/mechanics/supply.md</c>, section 14.
/// </summary>
public static class CSupplyCarry
{
	private const long Scale = CFixedPoint.Scale;

	/// <summary>
	/// Moves a unit's days of supplies and fuel into the province it is entering, out of
	/// the one it is leaving - or, coming off the sea or out of nowhere, out of its
	/// country's stockpile.
	/// </summary>
	/// <param name="from">The province it was in before, or 0 for a unit that was nowhere.</param>
	/// <param name="beforeGame">Before a game is under way a unit from nowhere always takes from the stockpile.</param>
	/// <param name="fromStockpile">
	/// So does a unit from nowhere getting off a fleet, whatever the ground (<c>transport.md</c>, section 4).
	/// </param>
	/// <param name="days">The days it brings; with none said, the days a province holds. A dropped unit brings its own.</param>
	public static void Bring(CGameState state, CCountry owner, CUnit unit, int from, int into, bool beforeGame = false,
		bool fromStockpile = false, long? days = null)
	{
		if (into <= 0 || into >= state.Provinces.Count)
		{
			return;
		}
		CProvince province = state.Provinces[into];
		CGoodsPool? source = state.Stockpile(owner);
		CProvince left = state.Province(from);
		if (left.Id == 0)
		{
			if (!beforeGame && !fromStockpile && province.Owner != owner.Tag)
			{
				// On ground it owns it takes from the stockpile whoever holds it; on ground it holds
				// and another owns, from the depot; on anybody else's it comes with nothing
				// (reversing/findings/FINDINGS-supplypass.md, section 5).
				if (province.Controller != owner.Tag)
				{
					return;
				}
				int depot = CSupplyNetworks.Refresh(state).DepotOf(into);
				if (depot < 1)
				{
					return;
				}
				source = state.Provinces[depot].Pool;
			}
		}
		else if (!left.IsSea)
		{
			source = left.Pool;
		}
		if (source is null || ReferenceEquals(source, province.Pool))
		{
			return;
		}

		long held = (days ?? CSupplyDay.DaysHeld(state)) * Scale;
		Move(source, province.Pool, EGoods.Supplies, CUnitSupply.SupplyConsumption(state, owner, unit).Raw * held / Scale);
		Move(source, province.Pool, EGoods.Fuel, CUnitSupply.FuelConsumption(state, owner, unit).Raw * held / Scale);
	}

	private static void Move(CGoodsPool source, CGoodsPool into, EGoods goods, long wanted)
	{
		long moved = Math.Min(wanted, CSupplyPass.Held(source, goods));
		CSupplyPass.Add(into, goods, moved);
		CSupplyPass.Add(source, goods, -moved);
	}
}
