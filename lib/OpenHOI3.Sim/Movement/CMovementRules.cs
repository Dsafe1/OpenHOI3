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

	/// <summary>Enemies in this province: at war, or in a war nobody declared whose regions take it in (<c>war.md</c>, section 15).</summary>
	public static bool IsEnemyIn(CGameState state, CCountryTag? a, CCountryTag? b, int province)
	{
		return IsEnemy(state, a, b) || (a is { } one && b is { } other && Wars.CUndeclaredWars.Covers(state, one, other, province));
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
	/// Whose ground a country may walk on: its own or a friend's, ground that belongs to
	/// nobody, and ground of anyone it is at war with. The sea is anyone's. A friend is the
	/// original's <c>IsFriend</c> with military access counting - an overlord or a puppet, a
	/// faction or alliance partner, a co-belligerent, a country it has access through
	/// (<c>reversing/findings/FINDINGS-movement.md</c>, section 3.1).
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
		if (province.Controller is { } controller && CCountryLists.IsFriend(state, country, controller, withAccess: true))
		{
			return true;
		}
		return province.Owner is { } owner && CCountryLists.IsFriend(state, country, owner, withAccess: true)
			&& !IsEnemy(state, province.Owner, province.Controller);
	}

	/// <summary>
	/// How much of a building has been built in a province, by the building's name:
	/// damage does not take a port or an airfield away. What works of it now is
	/// <see cref="CProvince.BuildingLevel"/>.
	/// </summary>
	public static CFixedPoint BuildingLevel(CGameState state, CProvince province, string key)
	{
		// An air base, a naval base and the other roles are the buildings that carry the role's modifier (CBuildingRoles).
		return CBuildingRoles.Find(state, key) is { } building ? province.BuildingMaxLevel(building) : CFixedPoint.Zero;
	}

	/// <summary>What works now of a building in a province, by the building's name: what bombing lowers and the day's repair raises.</summary>
	public static CFixedPoint WorkingLevel(CGameState state, CProvince province, string key)
	{
		return CBuildingRoles.Find(state, key) is { } building ? province.BuildingLevel(building) : CFixedPoint.Zero;
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
			&& province.Controller is { } controller && CCountryLists.IsFriend(state, country, controller, withAccess: true);
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
		// A land unit stands only in land that is in an area: held by somebody and with more than one level of
		// infrastructure built (supply.md, section 2). The original asks the province's owner area, and the null one says no.
		if (IsLandUnit(unit) && Supply.CSupplyNetworks.Refresh(state).AreaOf(province.Id) is null)
		{
			return false;
		}
		CProvince here = state.Province(unit.Location);
		// A land unit standing where its country may not go may walk on through its host's land (war.md, section 16).
		if (IsLandUnit(unit) && province.Owner == here.Owner && CTrespassing.Is(state, country, unit))
		{
			return true;
		}
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
	/// Whether a unit may be sent to a province at all, the original's <c>CUnit::CanMoveTo</c> whole. It has to be able
	/// to stand there. An air unit is not sent under too much cloud. A land unit at sea is sent to land only where an edge
	/// leads there from its sea. A unit in a battle is sent only where it may fall back
	/// (<see cref="Combat.CCombatRetreat.CanRetreatTo"/>). A land unit is not sent onto ground held by a country at war
	/// with it there - a war nobody declared counting where it covers the province - when it is too weak, or was given no
	/// supplies and uses some (not asked of one aboard a fleet), or no fuel and burns some. No unit is sent anywhere while
	/// it has a way, is in a battle and has a battle in the next province of its way. A fleet goes only where the sea
	/// joins its own (<see cref="SeaConnected"/>), and not while something it carries has a way of its own
	/// (<c>docs/mechanics/movement.md</c>, section 1). It is the click's test and every test the original makes with it;
	/// <c>CMoveUnitCommand</c> asks <see cref="MayCommandMoveTo"/>.
	/// </summary>
	public static bool MayMoveTo(CGameState state, CCountry country, CUnit unit, CProvince province)
	{
		return MayMoveTo(state, country, unit, province, whole: true);
	}

	/// <summary>
	/// What a move command asks before it runs: <see cref="MayMoveTo"/> less its three tests of where the unit is and what
	/// it is fighting - the edge from a sea, a battle's way out and a battle ahead. Ours: the original's command asks
	/// nothing; the whole test is asked before a command is posted - by the click, and by the AI's <c>CEU3AI::MoveUnit</c>
	/// as its second gate (<c>reversing/findings/FINDINGS-liveread.md</c>, section 88), which is the AI's own to ask
	/// (<c>docs/mechanics/movement.md</c>, section 1).
	/// </summary>
	public static bool MayCommandMoveTo(CGameState state, CCountry country, CUnit unit, CProvince province)
	{
		return MayMoveTo(state, country, unit, province, whole: false);
	}

	private static bool MayMoveTo(CGameState state, CCountry country, CUnit unit, CProvince province, bool whole)
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
		CProvince here = state.Province(unit.Location);
		if (whole && IsLandUnit(unit) && here.IsSea && !province.IsSea && province.EdgeTo(here.Id) is null)
		{
			return false;
		}
		if (whole && unit.IsFighting && !Combat.CCombatRetreat.CanRetreatTo(state, country, unit, province))
		{
			return false;
		}
		// A land unit aboard an air unit is not held back: it walks, still aboard (FINDINGS-liveread.md, section 127) -
		// and under an enhanced game's fix plane_passenger_march it is sent only where it may get off, which the giving
		// of the way then does for it: not while its air unit is in the air.
		if (IsLandUnit(unit) && unit.Plane is not null && state.Rules.Has(Rules.EFix.PlanePassengerMarch)
			&& !Transport.CAirTransport.MayGetOff(state, unit))
		{
			return false;
		}
		if (IsLandUnit(unit) && state.Country(unit.Owner) is { } owner
			&& IsEnemyIn(state, province.Controller, unit.Owner, province.Id))
		{
			if (IsTooWeak(state, unit))
			{
				return false;
			}
			if (unit.Transport is null && unit.SuppliesReceived == CFixedPoint.Zero
				&& Units.CUnitSupply.SupplyConsumption(state, owner, unit) > CFixedPoint.Zero)
			{
				return false;
			}
			if (Units.CUnitSupply.FuelConsumption(state, owner, unit) > CFixedPoint.Zero && unit.FuelReceived == CFixedPoint.Zero)
			{
				return false;
			}
		}
		if (whole && unit.IsMoving && unit.IsFighting && HasCombatIn(unit, unit.Path[0]))
		{
			return false;
		}
		if (unit.Kind != EUnitKind.Navy)
		{
			return true;
		}
		if (!SeaConnected(state, country, province, OwnSea(state, unit)))
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

	/// <summary>Whether one of the unit's battles is in the province.</summary>
	private static bool HasCombatIn(CUnit unit, int province)
	{
		foreach (Combat.CCombat combat in unit.Combats)
		{
			if (combat.Province == province)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// A fleet's own sea: the province it is in where that is sea, otherwise the sea its port opens onto - the province
	/// itself where it has none.
	/// </summary>
	public static CProvince OwnSea(CGameState state, CUnit unit)
	{
		CProvince here = state.Province(unit.Location);
		return here.IsSea || here.PortSea <= 0 || here.PortSea >= state.Provinces.Count ? here : state.Province(here.PortSea);
	}

	/// <summary>
	/// Whether a country's fleet can get from one province to the other at all, the original's <c>SeaConnected</c>: each
	/// end that is land with a port stands for the sea the port opens onto, both have then to be sea, and
	/// <see cref="SeaRegionsConnected"/> has to agree both ways (<c>docs/mechanics/fleetsandair.md</c>, section 1).
	/// </summary>
	public static bool SeaConnected(CGameState state, CCountry country, CProvince a, CProvince b)
	{
		if (SeaEnd(state, a) is not { } first || SeaEnd(state, b) is not { } second)
		{
			return false;
		}
		return SeaRegionsConnected(state, country, second, first) && SeaRegionsConnected(state, country, first, second);
	}

	/// <summary>
	/// Whether closed waters cut one sea off from another for a country, by the first region of each in
	/// <c>map/region.txt</c> and the three it names <c>black_sea_region</c>, <c>baltic_sea_region</c> and
	/// <c>mediterranean_region</c> (<c>docs/mechanics/fleetsandair.md</c>, section 1). Two in one region are joined. Out
	/// of the Black Sea: not with <c>BLACKSEA</c> closed, and to anywhere but the Mediterranean not with both
	/// <c>GIBRALTAR</c> and <c>SUEZ</c> closed. Out of the Baltic: not with <c>BALTIC</c> closed. Out of the
	/// Mediterranean: into the Black Sea not with <c>BLACKSEA</c> closed, elsewhere not with both <c>GIBRALTAR</c> and
	/// <c>SUEZ</c> closed. Anything else is joined. One way only: <see cref="SeaConnected"/> asks it both ways.
	/// </summary>
	public static bool SeaRegionsConnected(CGameState state, CCountry country, CProvince from, CProvince to)
	{
		int start = FirstRegion(from);
		int end = FirstRegion(to);
		if (start == end)
		{
			return true;
		}
		int blackSea = RegionNamed(state, "black_sea_region");
		int mediterranean = RegionNamed(state, "mediterranean_region");
		if (start == blackSea)
		{
			if (IsWaterClosed(state, country, "BLACKSEA"))
			{
				return false;
			}
			return end == mediterranean || end == blackSea
				|| !IsWaterClosed(state, country, "GIBRALTAR") || !IsWaterClosed(state, country, "SUEZ");
		}
		if (start == RegionNamed(state, "baltic_sea_region") && IsWaterClosed(state, country, "BALTIC"))
		{
			return false;
		}
		if (start != mediterranean)
		{
			return true;
		}
		if (end == blackSea)
		{
			return !IsWaterClosed(state, country, "BLACKSEA");
		}
		return end == mediterranean || !IsWaterClosed(state, country, "GIBRALTAR") || !IsWaterClosed(state, country, "SUEZ");
	}

	/// <summary>
	/// Whether a port opens onto the Baltic while the Baltic is closed to its owner - which the original's ranking of an
	/// area's harbours counts as a naval base of level 0 (<c>CMapProvince::UsableNavalBaseLevel</c>;
	/// reversing/findings/FINDINGS-supplyconvoys.md, section 4).
	/// </summary>
	public static bool IsShutInBaltic(CGameState state, CProvince port)
	{
		if (port.PortSea <= 0 || port.PortSea >= state.Provinces.Count || state.Country(port.Owner) is not { } owner)
		{
			return false;
		}
		int baltic = RegionNamed(state, "baltic_sea_region");
		return FirstRegion(state.Province(port.PortSea)) == baltic && IsWaterClosed(state, owner, "BALTIC");
	}

	/// <summary>The number of a province's first region, or -1 where it is in none.</summary>
	private static int FirstRegion(CProvince province)
	{
		return province.Regions.Count > 0 ? province.Regions[0] : -1;
	}

	/// <summary>The number of the region of that name, or -1 where the map has none - which is what a province in no region has.</summary>
	private static int RegionNamed(CGameState state, string name)
	{
		for (int index = 0; index < state.Regions.Count; index++)
		{
			if (state.Regions[index].Name == name)
			{
				return index;
			}
		}
		return -1;
	}

	/// <summary>Whether one of the five waters, by its define's name, is closed to a country.</summary>
	private static bool IsWaterClosed(CGameState state, CCountry country, string water)
	{
		if (!state.Defines.Has(MapSection, water))
		{
			return false;
		}
		long id = state.Defines[MapSection, water].Raw / CFixedPoint.Scale;
		return id > 0 && id < state.Provinces.Count && IsClosedWater(state, country, state.Province((int)id));
	}

	/// <summary>
	/// Whether a unit may take one step of a route. In the original's order: not over
	/// an impassable edge; not over a crossing through a province an enemy holds; a fleet
	/// between land and sea only through a port and the one sea province it opens onto,
	/// and never from land to land; only into a province it may stand in; and a land unit,
	/// into ground its own country does not hold, past <see cref="MayLandUnitStep"/>.
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
		if (!MayStandIn(state, country, unit, to))
		{
			return false;
		}
		return !IsLandUnit(unit) || to.Controller == unit.Owner || MayLandUnitStep(state, unit, from, to);
	}

	/// <summary>
	/// The original's last four tests of a land unit's step into ground its own country does not hold
	/// (<c>CPathFind::MayStep</c>; <c>docs/mechanics/movement.md</c>, section 3), all by the unit's owner. Onto ground
	/// held by a country at war with it, rebels counted: not a unit under its shatter threshold - too weak, its strength
	/// over the most its brigades have had - nor one given no supplies on its last day that uses some, nor one given no
	/// fuel that burns some. And not into a country it is at war with out of a province held by a country it has
	/// military access through that is not itself at war with that country: nobody attacks out of a neutral that merely
	/// lets it through.
	/// </summary>
	public static bool MayLandUnitStep(CGameState state, CUnit unit, CProvince from, CProvince to)
	{
		if (state.Country(unit.Owner) is not { } owner)
		{
			return true;
		}
		bool enemy = IsEnemy(state, to.Controller, unit.Owner);
		if (enemy && IsTooWeak(state, unit))
		{
			return false;
		}
		if (enemy && unit.SuppliesReceived == CFixedPoint.Zero
			&& Units.CUnitSupply.SupplyConsumption(state, owner, unit) > CFixedPoint.Zero)
		{
			return false;
		}
		if (enemy && Units.CUnitSupply.FuelConsumption(state, owner, unit) > CFixedPoint.Zero
			&& unit.FuelReceived == CFixedPoint.Zero)
		{
			return false;
		}
		// The owner's own record of the country holding the province it leaves, and a declared war with the one holding
		// the province it enters - the rebels not counted there, as they are in the tests before.
		return from.Controller is not { } through || state.Country(through) is not { } passage
			|| !CGameState.Status(owner, passage).HasMilitaryAccess
			|| IsEnemy(state, through, to.Controller)
			|| to.Controller is not { } holder || !CCountryLists.AtWar(state, unit.Owner, holder);
	}

	/// <summary>
	/// Whether a unit is under its shatter threshold: its strength over the most its brigades have had together, in
	/// thousandths - -1 where that is nothing - below <see cref="Combat.CCombatDamage.ShatterThreshold"/>. Such a land
	/// unit is neither sent nor routed onto ground held by a country at war with it. The threshold is by the officers of
	/// the country whose figures it has, a lender's for a unit lent.
	/// </summary>
	public static bool IsTooWeak(CGameState state, CUnit unit)
	{
		return state.Country(unit.FiguresOwner) is { } figures
			&& Combat.CCombatDamage.StrengthShare(unit) < Combat.CCombatDamage.ShatterThreshold(state, figures, unit);
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
