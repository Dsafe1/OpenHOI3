using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Convoys;

/// <summary>
/// What a convoy is worth and whether it can sail: the transports and escorts it would
/// have, how much of its load it carries with those it has, what a harbour can land in
/// a day, and whether its two ends and the water between are still open to it. See
/// <c>docs/mechanics/convoys.md</c>, sections 2 and 5.
/// </summary>
public static class CConvoyRules
{
	public const string EconomySection = "economy";
	public const string MilitarySection = "military";
	public const string PathLengthMult = "CONVOY_PATH_LENGTH_MULT";
	public const string TradeWeightMult = "CONVOY_TRADE_WEIGHT_MULT";

	private const string Rebels = "REB";

	/// <summary>A convoy at war wants three escorts more while it is short of transports.</summary>
	private const int EscortsWhenShort = 3;

	/// <summary>And four more where it has above ten transports.</summary>
	private const int EscortsWhenLarge = 4;

	private const int LargeConvoy = 10;

	/// <summary>
	/// What a harbour can land in a day: so much for each level of its naval base, and
	/// more where whoever holds it runs its ports better.
	/// </summary>
	public static long PortCapacity(CGameState state, CProvince province)
	{
		long level = CMovementRules.BuildingLevel(state, province, CMovementRules.NavalBase).Raw;
		long room = state.Defines[MilitarySection, CBaseCrowding.NavalBaseEfficiency].Raw * level / CFixedPoint.Scale;
		long better = CFixedPoint.Scale;
		if (state.Country(province.Controller) is { } holder)
		{
			better += holder.Technology[CBaseCrowding.NavalBaseEfficiencyModifier].Raw
				+ holder.Modifier[CBaseCrowding.NavalBaseEfficiencyModifier].Raw;
		}
		return room * better / CFixedPoint.Scale;
	}

	/// <summary>
	/// How much a depot across the sea can send home in a day: what its harbour lands,
	/// once for each convoy that ends at it from the land its holder's capital stands
	/// on, by how fully that convoy is manned - and never more than the harbour lands
	/// once.
	/// </summary>
	public static long HomewardCapacity(CGameState state, CSupplyNetwork network, CCountry holder, CProvince depot)
	{
		long landed = PortCapacity(state, depot);
		CSupplyArea? home = network.AreaOf(holder.ActingCapital);
		long carried = 0;
		foreach (CCountry owner in state.Countries)
		{
			foreach (CConvoy convoy in owner.Convoys)
			{
				if (convoy.End == depot.Id && network.AreaOf(convoy.Start) == home)
				{
					carried += landed * Efficiency(state, convoy) / CFixedPoint.Scale;
				}
			}
		}
		return Math.Min(carried, landed);
	}

	/// <summary>
	/// The transports a convoy would have: more the more provinces its way goes through
	/// and the larger the harbour at its end, and never fewer than one.
	/// </summary>
	public static int DesiredTransports(CGameState state, CConvoy convoy)
	{
		long length = convoy.Path.Count * CFixedPoint.Scale
			* state.Defines[EconomySection, PathLengthMult].Raw / CFixedPoint.Scale;
		long harbour = CMovementRules.BuildingLevel(state, state.Province(convoy.End), CMovementRules.NavalBase).Raw
			* CFixedPoint.Scale / (10 * CFixedPoint.Scale);
		if (convoy.IsTrade)
		{
			length = length * state.Defines[EconomySection, TradeWeightMult].Raw / CFixedPoint.Scale;
		}
		return (int)((harbour * length / CFixedPoint.Scale + CFixedPoint.Scale) / CFixedPoint.Scale);
	}

	/// <summary>
	/// The escorts a convoy would have. None while its owner is at peace; at war half
	/// its transports and one, more where it is short of transports or large, and five,
	/// three or one more for a convoy a raid sank a ship of in the last three days, the
	/// last week or the last fortnight.
	/// </summary>
	public static int DesiredEscorts(CGameState state, CConvoy convoy)
	{
		if (!state.IsAtWar(convoy.Owner))
		{
			return 0;
		}
		int wanted = convoy.Transports / 2 + 1;
		if (convoy.Transports < DesiredTransports(state, convoy))
		{
			wanted += EscortsWhenShort;
		}
		if (convoy.Transports > LargeConvoy)
		{
			wanted += EscortsWhenLarge;
		}
		if (convoy.LastAttackHour != CConvoy.NeverAttacked)
		{
			long days = state.TotalHours / CGameState.HoursPerDay - convoy.LastAttackHour / CGameState.HoursPerDay;
			wanted += days < 3 ? 5 : days < 7 ? 3 : days < 15 ? 1 : 0;
		}
		return wanted;
	}

	/// <summary>What a single escort is worth against a raider.</summary>
	private const long DefenceOfAnEscort = 5000;

	/// <summary>The technology effect that scales a convoy's escorts, its owner's [exe].</summary>
	public const string EscortEfficiency = "escort_efficiency";

	/// <summary>
	/// What a raider's attack is set against, in thousandths: five for each escort, over
	/// the provinces of the convoy's way - the longer the route, the thinner its escorts
	/// are spread - and then times one and the convoy's owner's <c>escort_efficiency</c>,
	/// each step cut towards nought (<c>navalmissions.md</c>, section 4).
	/// </summary>
	public static long Defence(CGameState state, CConvoy convoy)
	{
		long defence = convoy.Escorts * DefenceOfAnEscort;
		if (convoy.Path.Count != 0)
		{
			defence /= convoy.Path.Count;
		}
		long efficiency = state.Country(convoy.Owner)?.Technology[EscortEfficiency].Raw ?? 0;
		return defence * (CFixedPoint.Scale + efficiency) / CFixedPoint.Scale;
	}

	/// <summary>
	/// How much of what it could carry a convoy carries, in thousandths: its transports
	/// over those it would have. Not held to a whole - a convoy with ships to spare
	/// carries more. A lend-lease convoy's is then multiplied by its own efficiency.
	/// </summary>
	public static long Efficiency(CGameState state, CConvoy convoy)
	{
		int wanted = DesiredTransports(state, convoy);
		long efficiency = wanted == 0 ? CFixedPoint.Scale : convoy.Transports * CFixedPoint.Scale * CFixedPoint.Scale
			/ (wanted * CFixedPoint.Scale);
		return convoy.IsLendLease ? efficiency * convoy.LendLeaseEfficiency / CFixedPoint.Scale : efficiency;
	}

	/// <summary>
	/// The harbour of an area nearest a province across the sea, for the country whose convoy it is: of the area's ports
	/// the one whose own water is connected with the water the province's port opens onto for that country - closed
	/// straits shut it out (<see cref="CMovementRules.SeaConnected"/>) - and lies the shortest straight line from it, the
	/// first of equals. 0 where there is none to be had.
	/// </summary>
	public static int NearestPort(CGameState state, CCountry country, CSupplyArea? area, int target)
	{
		if (area is null || area.Ports.Count == 0 || target <= 0)
		{
			return 0;
		}
		CProvince targetWater = state.Province(state.Province(target).PortSea);
		long least = long.MaxValue;
		int nearest = 0;
		foreach (int port in area.Ports)
		{
			CProvince water = state.Province(state.Province(port).PortSea);
			if (!CMovementRules.SeaConnected(state, country, water, targetWater))
			{
				continue;
			}
			long far = CMapDistance.Between(targetWater, water, state.MapWidth);
			if (far < least)
			{
				least = far;
				nearest = port;
			}
		}
		return nearest;
	}

	/// <summary>Whether a province is a harbour a convoy may be given: owned, with a port and a naval base.</summary>
	public static bool IsHarbour(CGameState state, CProvince province)
	{
		return province.Id != 0 && province.Owner is not null && province.PortSea != 0
			&& CMovementRules.BuildingLevel(state, province, CMovementRules.NavalBase) > CFixedPoint.Zero;
	}

	/// <summary>
	/// Whether a convoy can sail at all. Both its ends have to be in somebody's hands; a
	/// run for resources has to cross from one block of land to another; each end has to
	/// be the owner's or a puppet's of the owner; and its way has to be unbroken.
	/// </summary>
	public static bool IsRouteUsable(CGameState state, CSupplyNetwork network, CConvoy convoy)
	{
		CProvince start = state.Province(convoy.Start);
		CProvince end = state.Province(convoy.End);
		if (start.Controller is null || end.Controller is null)
		{
			return false;
		}
		if (!convoy.IsSupply && network.AreaOf(convoy.End) == network.AreaOf(convoy.Start))
		{
			return false;
		}
		if (convoy.IsTrade || convoy.IsLendLease)
		{
			if (CMovementRules.IsEnemy(state, end.Controller, convoy.Owner)
				|| CMovementRules.IsEnemy(state, start.Controller, convoy.Owner))
			{
				return false;
			}
		}
		else if (!IsOwnersOrPuppets(state, end, convoy.Owner) || !IsOwnersOrPuppets(state, start, convoy.Owner))
		{
			return false;
		}
		return !IsPathBroken(state, convoy);
	}

	/// <summary>
	/// Whether a convoy's way no longer holds: some water on it the owner may not enter, or not connected for the owner
	/// with the harbour it sails from - closed straits shutting it out.
	/// </summary>
	public static bool IsPathBroken(CGameState state, CConvoy convoy)
	{
		if (state.Country(convoy.Owner) is not { } owner)
		{
			return true;
		}
		CProvince start = state.Province(convoy.Start);
		foreach (int id in convoy.Path)
		{
			CProvince province = state.Province(id);
			if (!province.IsSea)
			{
				continue;
			}
			if (!CMovementRules.MayEnter(state, owner, province) || !CMovementRules.SeaConnected(state, owner, province, start))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Whether a province is held by a country or by a puppet of it.</summary>
	public static bool IsOwnersOrPuppets(CGameState state, CProvince province, CCountryTag owner)
	{
		return province.Controller == owner
			|| (state.Country(province.Controller) is { } holder && holder.Overlord == owner);
	}

	/// <summary>Whether a province's holder is at war with a country, or either is the rebels.</summary>
	public static bool IsHostileGround(CGameState state, CProvince province, CCountryTag country)
	{
		if (province.Controller is not { } holder || holder == country)
		{
			return false;
		}
		return CMovementRules.IsEnemy(state, holder, country)
			|| holder.Value.Equals(Rebels, StringComparison.OrdinalIgnoreCase)
			|| country.Value.Equals(Rebels, StringComparison.OrdinalIgnoreCase);
	}
}
