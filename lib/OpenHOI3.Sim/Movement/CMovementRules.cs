using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Sim.Movement;

/// <summary>
/// Where a unit may go: whose ground a country may walk on, which province a unit may
/// stand in, and which step of a route it may take. The rules are the original's, in
/// <c>docs/mechanics/movement.md</c>, section 3, and for a fleet and an air unit in
/// <c>docs/mechanics/fleetsandair.md</c>, section 1 - less what they ask of things this
/// project does not have yet, which those sections list.
/// </summary>
public static class CMovementRules
{
	public const string NavalBase = "naval_base";
	public const string AirBase = "air_base";

	/// <summary>The section of the defines that names the canals and straits.</summary>
	public const string MapSection = "map";

	/// <summary>How much of the sky may be cloud before nothing flies there.</summary>
	public const string AllowedToFlyThreshold = "ALLOWEDTOFLYTHRESHOLD";

	/// <summary>
	/// The five waters a country can close: each a sea province, and the land province
	/// whose holder decides who may sail it.
	/// </summary>
	private static readonly (string Water, string Blocker)[] Canals =
	{
		("SUEZ", "SUEZ_BLOCKER"),
		("PANAMA", "PANAMA_BLOCKER"),
		("BALTIC", "BALTIC_BLOCKER"),
		("BLACKSEA", "BLACKSEA_BLOCKER"),
		("GIBRALTAR", "GIBRALTAR_BLOCKER"),
	};

	public static bool IsLandUnit(CUnit unit)
	{
		return unit.Kind != EUnitKind.Navy && unit.Kind != EUnitKind.Air;
	}

	/// <summary>Whether two countries are fighting each other in some war, or one of them is the rebels.</summary>
	public static bool IsEnemy(CGameState state, CCountryTag? a, CCountryTag? b)
	{
		if (a is not { } first || b is not { } second || first == second)
		{
			return false;
		}
		// The rebels are at war with everybody, and everybody with them, with no war between them (rebels.md, section 13).
		if (first.Value == Revolts.CRebelFactions.Rebels || second.Value == Revolts.CRebelFactions.Rebels)
		{
			return true;
		}
		foreach (CWar war in state.Wars)
		{
			if ((war.Attackers.Contains(first) && war.Defenders.Contains(second))
				|| (war.Attackers.Contains(second) && war.Defenders.Contains(first)))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Whether a country counts another as a friend: itself, and a member of its own
	/// faction. The original also counts an overlord or a puppet, an ally, a
	/// co-belligerent and a country it has military access to.
	/// </summary>
	public static bool IsFriendly(CGameState state, CCountry country, CCountryTag? other)
	{
		if (other is null)
		{
			return false;
		}
		if (other == country.Tag)
		{
			return true;
		}
		return country.Faction.Length > 0 && state.Country(other) is { } theirs
			&& theirs.Faction == country.Faction;
	}

	/// <summary>
	/// Whose ground a country may walk on: its own or a friend's, ground that belongs to
	/// nobody, and ground of anyone it is at war with. The sea is anyone's.
	/// </summary>
	public static bool MayEnter(CGameState state, CCountry country, CProvince province)
	{
		if (province.Id == 0)
		{
			return false;
		}
		if (province.IsSea || province.Owner is null)
		{
			return true;
		}
		if (IsEnemy(state, country.Tag, province.Owner) || IsEnemy(state, country.Tag, province.Controller))
		{
			return true;
		}
		if (IsFriendly(state, country, province.Controller))
		{
			return true;
		}
		return IsFriendly(state, country, province.Owner) && !IsEnemy(state, province.Owner, province.Controller);
	}

	/// <summary>
	/// How much of a building has been built in a province, by the building's name:
	/// damage does not take a port or an airfield away. What works of it now is
	/// <see cref="CProvince.BuildingLevel"/>.
	/// </summary>
	public static CFixedPoint BuildingLevel(CGameState state, CProvince province, string key)
	{
		foreach (CBuilding building in state.Buildings)
		{
			if (building.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
			{
				return province.BuildingMaxLevel(building);
			}
		}
		return CFixedPoint.Zero;
	}

	/// <summary>
	/// Whether a country's ships may lie in a land province: it has to belong to
	/// somebody, have a port with a naval base in it, and be held by a friend who is no
	/// enemy.
	/// </summary>
	public static bool IsOpenPort(CGameState state, CCountry country, CProvince province)
	{
		return !province.IsSea && province.Owner is not null && province.PortSea != 0
			&& BuildingLevel(state, province, NavalBase) > CFixedPoint.Zero
			&& !IsEnemy(state, province.Controller, country.Tag)
			&& IsFriendly(state, country, province.Controller);
	}

	/// <summary>
	/// Whether a sea province is one of the five a country can close, and the holder of
	/// the land that closes it is at war with this one.
	/// </summary>
	public static bool IsClosedWater(CGameState state, CCountry country, CProvince province)
	{
		if (!province.IsSea)
		{
			return false;
		}
		foreach ((string water, string blocker) in Canals)
		{
			if (!state.Defines.Has(MapSection, water) || !state.Defines.Has(MapSection, blocker)
				|| state.Defines[MapSection, water].Raw / CFixedPoint.Scale != province.Id)
			{
				continue;
			}
			long holder = state.Defines[MapSection, blocker].Raw / CFixedPoint.Scale;
			return holder > 0 && holder < state.Provinces.Count
				&& IsEnemy(state, state.Province((int)holder).Controller, country.Tag);
		}
		return false;
	}

	/// <summary>
	/// Whether a unit may stand in a province. A land unit stands in the sea only where a
	/// fleet of its own there may take it aboard (<c>transport.md</c>, section 3) - and,
	/// ours, not when it is aboard one already - and a fleet lies on land only in an open
	/// port, and not in water closed to it. Past that every unit stands where whoever holds
	/// the ground under it now holds the ground, or where its country may walk.
	/// </summary>
	public static bool MayStandIn(CGameState state, CCountry country, CUnit unit, CProvince province)
	{
		if (province.Id == 0)
		{
			return false;
		}
		if (unit.Kind == EUnitKind.Navy)
		{
			if (!province.IsSea && !IsOpenPort(state, country, province))
			{
				return false;
			}
			if (IsClosedWater(state, country, province))
			{
				return false;
			}
		}
		if (IsLandUnit(unit) && province.IsSea)
		{
			return unit.Transport is null && Transport.CTransport.FleetToBoard(state, unit, province) is not null;
		}
		CProvince here = state.Province(unit.Location);
		return province.Controller == here.Controller || MayEnter(state, country, province);
	}

	/// <summary>
	/// The sea a province is reached by: itself where it is sea, the sea its port opens
	/// onto where it is land with a port, and otherwise none.
	/// </summary>
	public static CProvince? SeaEnd(CGameState state, CProvince province)
	{
		if (province.IsSea)
		{
			return province;
		}
		return province.PortSea > 0 && province.PortSea < state.Provinces.Count
			? state.Province(province.PortSea)
			: null;
	}

	/// <summary>
	/// Whether a unit may be ordered to a province at all. It has to be able to stand
	/// there. An air unit is not sent under too much cloud; a fleet goes only where there
	/// is sea at both ends, and not while something it carries is on the move itself. A
	/// land unit too weak, or with nothing to eat or to burn, is not sent onto an enemy's
	/// ground in the original, which waits for an enemy to meet.
	/// </summary>
	public static bool MayMoveTo(CGameState state, CCountry country, CUnit unit, CProvince province)
	{
		if (unit.Kind == EUnitKind.Air && state.Defines.Has(CWeatherEffects.WeatherSection, AllowedToFlyThreshold)
			&& province.Weather.CloudCoverage >= state.Defines[CWeatherEffects.WeatherSection, AllowedToFlyThreshold])
		{
			return false;
		}
		if (!MayStandIn(state, country, unit, province))
		{
			return false;
		}
		// A land unit aboard an air unit goes only where the air unit takes it (transport.md, Ours).
		if (unit.Plane is not null)
		{
			return false;
		}
		if (unit.Kind != EUnitKind.Navy)
		{
			return true;
		}
		if (SeaEnd(state, province) is null || SeaEnd(state, state.Province(unit.Location)) is null)
		{
			return false;
		}
		foreach (CUnit passenger in unit.Passengers)
		{
			if (passenger.IsMoving)
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// Whether a unit may take one step of a route. In the original's order: not over
	/// an impassable edge; not over a crossing through a province an enemy holds; a fleet
	/// between land and sea only through a port and the one sea province it opens onto,
	/// and never from land to land; and then only into a province it may stand in.
	/// </summary>
	public static bool MayStep(CGameState state, CCountry country, CUnit unit, CProvince from, CProvince to,
		CProvinceEdge edge)
	{
		if (edge.Kind == EProvinceEdgeKind.Impassable || from.Id == to.Id)
		{
			return false;
		}
		if (IsCrossingBlocked(state, country, unit, edge))
		{
			return false;
		}
		// Nobody is routed onto an enemy's ground who was given no supplies, or no fuel, and uses some.
		if (IsEnemy(state, country.Tag, to.Controller) && state.Country(unit.Owner) is { } owner
			&& ((unit.SuppliesReceived == CFixedPoint.Zero && Units.CUnitSupply.SupplyConsumption(state, owner, unit) > CFixedPoint.Zero)
				|| (unit.FuelReceived == CFixedPoint.Zero && Units.CUnitSupply.FuelConsumption(state, owner, unit) > CFixedPoint.Zero)))
		{
			return false;
		}
		if (unit.Kind == EUnitKind.Navy)
		{
			if (!from.IsSea && !to.IsSea)
			{
				return false;
			}
			if (from.IsSea && !to.IsSea && to.PortSea != from.Id)
			{
				return false;
			}
			if (!from.IsSea && to.IsSea)
			{
				if (from.PortSea != to.Id)
				{
					return false;
				}
				// Out of a port it may no longer lie in, a fleet puts to sea whatever the sea is.
				if (!MayStandIn(state, country, unit, from))
				{
					return true;
				}
			}
		}
		return MayStandIn(state, country, unit, to);
	}

	/// <summary>
	/// Whether a crossing is closed to a unit: it goes through a land province an enemy
	/// holds. Through a sea province it is an enemy fleet standing there that closes it,
	/// which waits for fleets that fight. Nothing closes a crossing to an air unit.
	/// </summary>
	public static bool IsCrossingBlocked(CGameState state, CCountry country, CUnit unit, CProvinceEdge edge)
	{
		if (edge.Kind != EProvinceEdgeKind.Crossing || edge.Through == 0 || unit.Kind == EUnitKind.Air)
		{
			return false;
		}
		CProvince crossed = state.Province(edge.Through);
		return !crossed.IsSea && IsEnemy(state, country.Tag, crossed.Controller);
	}
}
