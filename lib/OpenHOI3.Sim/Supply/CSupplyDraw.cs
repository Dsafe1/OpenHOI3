using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Supply;

/// <summary>
/// One unit's day at the supplies: it asks for what it uses of the province it draws
/// on, is given what that province has for it, and remembers how much of what it asked
/// for that was. See <c>docs/mechanics/supply.md</c>, section 13.
/// </summary>
public static class CSupplyDraw
{
	private const long Scale = CFixedPoint.Scale;

	/// <summary>
	/// How much dearer supplies are for a friend's unit on a depot that is empty, in
	/// thousandths: a quarter. On one holding its full days they cost what they are.
	/// </summary>
	private const long DearestForAFriend = 250;

	/// <summary>The unit asks for what its brigades use today.</summary>
	public static void Draw(CGameState state, CCountry owner, CUnit unit)
	{
		Draw(state, owner, unit, CUnitSupply.SupplyConsumption(state, owner, unit).Raw,
			CUnitSupply.FuelConsumption(state, owner, unit).Raw);
	}

	/// <summary>
	/// The province a unit draws on: the one it stands in for an army, the one it is
	/// based in for a wing or a fleet, its fleet's port for an air group aboard a
	/// carrier, its carrier's base for a land unit aboard a fleet or an air unit, and its
	/// country's capital for a unit that is nowhere yet.
	/// </summary>
	public static int ProvinceOf(CCountry owner, CUnit unit)
	{
		if ((unit.CarriedBy?.Unit ?? unit.Transport ?? unit.Plane) is { } fleet)
		{
			return fleet.Base != 0 ? fleet.Base : fleet.Location;
		}
		if (!CMovementRules.IsLandUnit(unit) && unit.Base != 0)
		{
			return unit.Base;
		}
		return unit.Location != 0 ? unit.Location : owner.ActingCapital;
	}

	/// <summary>The same, with what it asks for worked out beforehand, in thousandths.</summary>
	public static void Draw(CGameState state, CCountry owner, CUnit unit, long supplies, long fuel)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CProvince here = state.Province(ProvinceOf(owner, unit));
		CProvince depot = state.Province(network.DepotOf(here.Id));
		CCountry? depotHolder = state.Country(depot.Controller);
		long need = CSupplyPass.Held(here.SupplyNeed, EGoods.Supplies);
		long fuelNeed = CSupplyPass.Held(here.SupplyNeed, EGoods.Fuel);

		// On a friend's depot the unit's country pays the friend for what it asks, the
		// more the emptier the province it draws in.
		bool pays = false;
		long price = Scale;
		long fuelPrice = Scale;
		if (depot.Controller != owner.Tag && depotHolder is not null
			&& CSupplyRelations.IsFriendly(state, depotHolder, owner))
		{
			pays = true;
			long days = CSupplyDay.DaysHeld(state);
			price = Price(days, need, CSupplyPass.Held(here.Pool, EGoods.Supplies));
			fuelPrice = Price(days, fuelNeed, CSupplyPass.Held(here.Pool, EGoods.Fuel));
		}

		// A fleet on its way to another port is seen to out of the capital. It is
		// decided after the paying is, and by the need of the port it was based in:
		// the original's order, kept.
		if (unit.Kind == EUnitKind.Navy && unit.Order == EUnitOrder.Rebase && owner.ActingCapital != 0)
		{
			here = state.Province(owner.ActingCapital);
			depot = here;
			depotHolder = state.Country(depot.Controller);
		}

		// Whoever holds the depot has to be a friend, or nothing comes.
		if (depot.Id != 0 && (depotHolder is null || !CSupplyRelations.IsFriendly(state, depotHolder, owner)))
		{
			unit.SuppliesReceived = CFixedPoint.Zero;
			unit.FuelReceived = CFixedPoint.Zero;
			return;
		}

		CCountry? holder = state.Country(here.Controller);
		bool inCapital = holder is not null && holder.ActingCapital != 0 && holder.ActingCapital == unit.Location;

		if (supplies < 1)
		{
			unit.SuppliesReceived = CFixedPoint.One;
		}
		else
		{
			Ask(state, owner, holder, depotHolder, pays, price, supplies, EGoods.Supplies);
			long given = Given(here, need, supplies, EGoods.Supplies, out long share);
			unit.SuppliesReceived = new CFixedPoint(share);
			if (inCapital)
			{
				CSupplyPass.Add(holder!.UnitDrawn, EGoods.Supplies, given);
			}
			CSupplyPass.Add(here.Pool, EGoods.Supplies, -given);
		}

		if (fuel < 1)
		{
			unit.FuelReceived = CFixedPoint.One;
		}
		else
		{
			Ask(state, owner, holder, depotHolder, pays, fuelPrice, fuel, EGoods.Fuel);
			long given = Given(here, fuelNeed, fuel, EGoods.Fuel, out long share);
			unit.FuelReceived = new CFixedPoint(share);
			// What it is given it burns only on the move.
			if (CUnitSupply.BurnsFuel(unit))
			{
				CSupplyPass.Add(here.Pool, EGoods.Fuel, -given);
				if (inCapital)
				{
					CSupplyPass.Add(holder!.UnitDrawn, EGoods.Fuel, given);
				}
			}
		}
	}

	/// <summary>What a friend's unit pays for a thousandth, by how full the province is of its days' worth.</summary>
	private static long Price(long days, long need, long held)
	{
		long full = need < 1 ? Scale : held * Scale / (days * Scale * need / Scale);
		return (Scale - full) * DearestForAFriend / Scale + Scale;
	}

	/// <summary>
	/// Counts what the unit asks for. On its own side's depot that is all; on a
	/// friend's, its country's stockpile pays the friend's.
	/// </summary>
	private static void Ask(CGameState state, CCountry owner, CCountry? holder, CCountry? depotHolder, bool pays,
		long price, long asked, EGoods goods)
	{
		if (!pays)
		{
			if (holder is not null)
			{
				CSupplyPass.Add(holder.UnitDemand, goods, asked);
			}
			return;
		}

		long paid = price * asked / Scale;
		CSupplyPass.Add(owner.TradedAway, goods, paid);
		if (state.Stockpile(owner) is not { } paying || state.Stockpile(depotHolder!) is not { } paidTo)
		{
			return;
		}
		if (CSupplyPass.Held(paying, goods) < paid)
		{
			paid = CSupplyPass.Held(paying, goods);
		}
		CSupplyPass.Add(paying, goods, -paid);
		CSupplyPass.Add(paidTo, goods, paid);
		CSupplyPass.Add(depotHolder!.TradedFor, goods, paid);
		CSupplyPass.Add(depotHolder.UnitDemand, goods, paid);
	}

	/// <summary>
	/// What the province gives a unit of what it asks: all of it where the province
	/// holds what all its units ask for, and otherwise the unit's share of what it holds.
	/// </summary>
	private static long Given(CProvince here, long need, long asked, EGoods goods, out long share)
	{
		long held = CSupplyPass.Held(here.Pool, goods);
		if (held < need)
		{
			// A need of nought makes the share -1, the original's fixed-point division by nought: a pool below nought -
			// a capital's, charged whatever it holds during the day - then gives nearly nothing (supply.md, section 13).
			long ratio = need == 0 ? -1 : asked * Scale / need;
			held = held * ratio / Scale;
		}
		if (held >= asked)
		{
			share = Scale;
			return asked;
		}
		share = held < 1 ? 0 : held * Scale / asked;
		return held;
	}
}
