using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Transport;

/// <summary>
/// Land units carried by a fleet: how much a fleet carries, who may come aboard, getting off, too much aboard, and
/// what a sea battle lays on the passengers. See <c>docs/mechanics/transport.md</c>, sections 1 to 6.
/// </summary>
public static class CTransport
{
	public const string TransportCapability = "transport_capability";

	private const long Scale = CFixedPoint.Scale;

	/// <summary>What a unit weighs aboard: its own brigades' <c>transport_weight</c>, cut to a whole number.</summary>
	public static long Weight(CUnit unit)
	{
		long sum = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			sum += subUnit.Stats[CSubUnitType.TransportWeight].Raw;
		}
		return sum / Scale;
	}

	/// <summary>What a fleet has aboard: its passengers' weights, each cut on its own.</summary>
	public static long Load(CUnit fleet)
	{
		long load = 0;
		foreach (CUnit passenger in fleet.Passengers)
		{
			load += Weight(passenger);
		}
		return load;
	}

	/// <summary>What a fleet - or a transport plane - can carry: its ships' <c>transport_capability</c>, cut.</summary>
	public static long Capacity(CUnit carrier)
	{
		long sum = 0;
		foreach (CSubUnit subUnit in carrier.SubUnits)
		{
			sum += subUnit.Stats[TransportCapability].Raw;
		}
		return sum / Scale;
	}

	/// <summary>
	/// Whether a fleet may take a land unit aboard: one owner, the fleet in no battle, going nowhere and not falling
	/// back, and room for the unit's weight. An ally's fleet never carries.
	/// </summary>
	public static bool CanCarry(CUnit fleet, CUnit unit)
	{
		return fleet.Kind == EUnitKind.Navy && Movement.CMovementRules.IsLandUnit(unit)
			&& !fleet.IsRemoved && !unit.IsRemoved && fleet.Owner == unit.Owner
			&& !fleet.IsFighting && !fleet.IsMoving && !fleet.IsRetreating
			&& Weight(unit) + Load(fleet) <= Capacity(fleet);
	}

	/// <summary>
	/// The fleet a land unit marching into a sea province boards: the first there, in the order the province lists its
	/// units, that may take it aboard. Null for none.
	/// </summary>
	public static CUnit? FleetToBoard(CGameState state, CUnit unit, Provinces.CProvince sea)
	{
		foreach (CUnit other in state.UnitsIn(sea.Id))
		{
			if (CanCarry(other, unit))
			{
				return other;
			}
		}
		return null;
	}

	/// <summary>A passenger going nowhere: it fights no one, is fought by no one, takes no province and holds none.</summary>
	public static bool IsIdlePassenger(CUnit unit)
	{
		return unit.Transport is not null && !unit.IsMoving;
	}

	/// <summary>
	/// Puts a land unit aboard a fleet, at the end of its list. Its dig-in goes; where it is, its path and its place in
	/// the order of battle are left alone.
	/// </summary>
	public static void Board(CUnit fleet, CUnit unit)
	{
		unit.Transport = fleet;
		unit.DigIn = CFixedPoint.Zero;
		if (!fleet.Passengers.Contains(unit))
		{
			fleet.Passengers.Add(unit);
		}
	}

	/// <summary>
	/// A passenger gets off where it stands. Its dig-in goes; with <paramref name="bringSupplies"/> it brings its days
	/// of supplies into the province it stands in, out of <paramref name="from"/>'s pool where that is land or else the
	/// stockpile; it leaves the fleet's list, and looks for a battle where it stands.
	/// </summary>
	/// <param name="from">Where the days come from: the unit's previous province, or the sea on a landing.</param>
	public static void GetOff(CGameState state, CUnit unit, bool bringSupplies, int from)
	{
		if (unit.Transport is not { } fleet)
		{
			return;
		}
		unit.Transport = null;
		unit.DigIn = CFixedPoint.Zero;
		if (bringSupplies && state.Country(unit.Owner) is { } owner)
		{
			CSupplyCarry.Bring(state, owner, unit, from, unit.Location, fromStockpile: true);
		}
		fleet.Passengers.Remove(unit);
		CCombats.CheckForCombat(state, unit, unit.Location);
	}

	/// <summary>Every passenger off where the fleet is, as the transport mission puts them off: bringing nothing.</summary>
	public static void PutAllOff(CGameState state, CUnit fleet)
	{
		foreach (CUnit passenger in new List<CUnit>(fleet.Passengers))
		{
			GetOff(state, passenger, bringSupplies: false, passenger.Previous);
		}
	}

	/// <summary>
	/// Too much aboard: while the load is more than the fleet can carry, a passenger in port is put off where it is,
	/// and one at sea loses its first brigade - a unit with none left is gone - and the load is measured again.
	/// </summary>
	public static void CheckOverload(CGameState state, CUnit fleet)
	{
		while (fleet.Passengers.Count > 0 && Load(fleet) > Capacity(fleet))
		{
			bool measureAgain = false;
			foreach (CUnit passenger in new List<CUnit>(fleet.Passengers))
			{
				if (passenger.IsRemoved)
				{
					fleet.Passengers.Remove(passenger);
					continue;
				}
				if (!state.Province(passenger.Location).IsSea)
				{
					GetOff(state, passenger, bringSupplies: false, passenger.Previous);
					continue;
				}
				if (passenger.SubUnits.Count > 0)
				{
					CSubUnit first = passenger.SubUnits[0];
					passenger.SubUnits.RemoveAt(0);
					first.Unit = null;
					if (passenger.SubUnits.Count == 0)
					{
						Remove(state, passenger);
					}
					measureAgain = true;
					break;
				}
			}
			if (!measureAgain)
			{
				return;
			}
		}
	}

	/// <summary>The test asked of every fleet that has passengers: each hour, each day.</summary>
	public static void CheckEveryOverload(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in new List<CUnit>(country.AllUnits()))
			{
				if (unit.Kind == EUnitKind.Navy && unit.Passengers.Count > 0 && !unit.IsRemoved)
				{
					CheckOverload(state, unit);
				}
			}
		}
	}

	/// <summary>
	/// A round of a sea battle: the damage the fleet's transports took is laid on every brigade aboard, a share each -
	/// organisation by <c>damage * 1000 / (n * 2000)</c> and strength by <c>damage * 1000 / (n * 1000)</c>, n the
	/// brigades aboard - at once, and with no test of whether a passenger breaks.
	/// </summary>
	public static void LayDamage(CUnit fleet, long transportDamage, CGameState? state = null, Combat.CCombatSide? side = null)
	{
		if (transportDamage <= 0 || fleet.Passengers.Count == 0)
		{
			return;
		}
		long brigades = 0;
		foreach (CUnit passenger in fleet.Passengers)
		{
			brigades += passenger.SubUnits.Count;
		}
		if (brigades == 0)
		{
			return;
		}
		long organisation = transportDamage * Scale / (brigades * 2 * Scale);
		long strength = transportDamage * Scale / (brigades * Scale);
		foreach (CUnit passenger in fleet.Passengers)
		{
			foreach (CSubUnit brigade in passenger.SubUnits)
			{
				brigade.Organisation = new CFixedPoint(Math.Max(brigade.Organisation.Raw - organisation, 0));
				if (state is not null && side is not null)
				{
					StrategicWarfare.CWarfareTally.Passenger(state, side, brigade, Math.Min(strength, brigade.Strength.Raw));
				}
				brigade.Strength = new CFixedPoint(Math.Max(brigade.Strength.Raw - strength, 0));
			}
		}
	}

	/// <summary>A fleet - or an air unit - leaving the game takes its passengers with it.</summary>
	public static void RemovePassengers(CGameState state, CUnit carrier)
	{
		foreach (CUnit passenger in new List<CUnit>(carrier.Passengers))
		{
			Remove(state, passenger);
		}
		carrier.Passengers.Clear();
	}

	private static void Remove(CGameState state, CUnit passenger)
	{
		CCombatRetreat.RemoveFromGame(state, passenger);
	}

	/// <summary>
	/// A fleet entering a province brings its passengers: each leaves any battle it was in and enters with it. Where the
	/// fleet ends its path in a land province - a port - each gets off first, bringing its days of supplies into the
	/// province it is leaving; enters the port as a land unit entering land does, bringing them again; and looks for a
	/// battle there.
	/// </summary>
	public static void Follow(CGameState state, CUnit fleet, int province)
	{
		bool arrivedInPort = !state.Province(province).IsSea && !fleet.IsMoving;
		foreach (CUnit passenger in new List<CUnit>(fleet.Passengers))
		{
			CCombats.LeaveAll(state, passenger);
			if (arrivedInPort)
			{
				GetOff(state, passenger, bringSupplies: true, passenger.Previous);
				if (state.Country(passenger.Owner) is { } owner)
				{
					CSupplyCarry.Bring(state, owner, passenger, passenger.Previous, province);
				}
			}
			state.Positions.Move(passenger, passenger.Location, province);
			passenger.Location = province;
			if (arrivedInPort)
			{
				CCombats.CheckForCombat(state, passenger, province);
			}
		}
	}
}
