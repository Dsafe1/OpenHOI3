using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// A fleet's missions: which may be given, what giving one does, the hour each is asked
/// whether it is still going - which is where a mission does everything it does - and
/// the frame they share. See <c>docs/mechanics/navalmissions.md</c>.
/// </summary>
public static class CMissions
{
	/// <summary>
	/// How much of its strength a ship may have lost before it counts as worn, by the
	/// order's stance. The original has these as plain numbers, not the defines.
	/// </summary>
	private const long WornWhenAggressive = 500;
	private const long WornWhenDefensive = 250;
	private const long WornWhenPassive = 100;

	/// <summary>The hours between a patrol's or a raider's tries at a convoy.</summary>
	public const int HoursBetweenRaids = 12;

	public static bool IsMission(EUnitOrder order)
	{
		return order is EUnitOrder.Reserve or EUnitOrder.Patrol or EUnitOrder.Intercept or EUnitOrder.Sortie
			or EUnitOrder.ConvoyEscort or EUnitOrder.ConvoyRaid or EUnitOrder.Transport or EUnitOrder.Invasion;
	}

	/// <summary>
	/// Whether a mission may be given to a fleet for a province. Patrol, intercept,
	/// sortie and raid share one test: the fleet is in no battle, the province is sea,
	/// and it is in range.
	/// </summary>
	public static bool MayGive(CGameState state, CCountry country, CUnit unit, EUnitOrder order, CProvince province,
		int convoy = 0)
	{
		if (unit.Kind != EUnitKind.Navy || unit.IsFighting)
		{
			return false;
		}
		switch (order)
		{
			case EUnitOrder.Patrol:
			case EUnitOrder.Intercept:
			case EUnitOrder.Sortie:
			case EUnitOrder.ConvoyRaid:
				return province.IsSea && CUnitOrders.IsInRange(state, unit, order, province);
			case EUnitOrder.Reserve:
				return CMovementRules.BuildingLevel(state, province, CMovementRules.NavalBase) > CFixedPoint.Zero
					&& province.Controller == unit.Owner
					&& CUnitOrders.IsInRange(state, unit, order, province);
			case EUnitOrder.ConvoyEscort:
				return province.IsSea && CConvoyEscort.Find(state, convoy) is { } run
					&& CConvoyEscort.FindBeat(state, unit, run).First != 0;
			case EUnitOrder.Transport:
				return CTransportMission.MayGive(state, country, unit, province);
			case EUnitOrder.Invasion:
				return CInvasionMission.MayGive(state, country, unit, province);
			default:
				return false;
		}
	}

	/// <summary>Sets a fleet off on the mission it has just been given.</summary>
	public static void Start(CGameState state, CCountry country, CUnit unit)
	{
		switch (unit.Order)
		{
			case EUnitOrder.Reserve:
				CReserve.Start(state, country, unit);
				break;
			case EUnitOrder.Patrol:
				CPatrol.Move(state, country, unit);
				break;
			case EUnitOrder.ConvoyRaid:
				CConvoyRaiding.Move(state, country, unit);
				break;
			case EUnitOrder.Intercept:
				CIntercept.Start(state, country, unit);
				break;
			case EUnitOrder.Sortie:
				CSortie.Plot(state, country, unit);
				break;
			case EUnitOrder.ConvoyEscort:
				CConvoyEscort.Start(state, country, unit);
				break;
			case EUnitOrder.Invasion:
				CInvasionMission.Start(state, country, unit);
				break;
		}
	}

	/// <summary>
	/// A mission's hour, before the fleet moves. Answers whether the order is still
	/// going; one that is not is dropped.
	/// </summary>
	public static bool RunHour(CGameState state, CCountry country, CUnit unit)
	{
		return unit.Order switch
		{
			EUnitOrder.Reserve => CReserve.RunHour(state, country, unit),
			EUnitOrder.Patrol => CPatrol.RunHour(state, country, unit),
			EUnitOrder.ConvoyRaid => CConvoyRaiding.RunHour(state, country, unit),
			EUnitOrder.Intercept => CIntercept.RunHour(state, country, unit),
			EUnitOrder.Sortie => CSortie.RunHour(state, country, unit),
			EUnitOrder.ConvoyEscort => CConvoyEscort.RunHour(state, country, unit),
			EUnitOrder.Transport => CTransportMission.RunHour(state, country, unit),
			EUnitOrder.Invasion => CInvasionMission.RunHour(state, country, unit),
			_ => true,
		};
	}

	/// <summary>
	/// Whether a fleet is too worn to go on: at least as many of its ships have lost
	/// more of their strength than its stance allows as have not. Asked only of a fleet
	/// whose country a person plays; nobody else's breaks off here.
	/// </summary>
	public static bool IsTooWorn(CGameState state, CCountry country, CUnit unit)
	{
		if (!state.Played.Contains(unit.Owner))
		{
			return false;
		}
		long limit = unit.Mission.Stance switch
		{
			CMission.Aggressive => WornWhenAggressive,
			CMission.Passive => WornWhenPassive,
			_ => WornWhenDefensive,
		};
		int worn = 0;
		int fit = 0;
		foreach (CSubUnit ship in unit.SubUnits)
		{
			long most = CUnits.MaxStrength(state, country, ship, false, false).Raw;
			long lost = most == 0 ? -1 : (most - ship.Strength.Raw) * CFixedPoint.Scale / most;
			if (lost > limit)
			{
				worn++;
			}
			else
			{
				fit++;
			}
		}
		return worn >= fit;
	}

	/// <summary>
	/// A worn fleet at sea with nowhere to go makes for the nearest harbour by sea: one
	/// of its country's, or of anybody's in its faction. Its base and its order stay.
	/// </summary>
	public static void HeadForNearestBase(CGameState state, CCountry country, CUnit unit)
	{
		CProvince here = state.Province(unit.Location);
		if (unit.IsFighting || unit.IsMoving || !here.IsSea)
		{
			return;
		}
		int nearest = 0;
		long least = long.MaxValue;
		foreach (CProvince province in state.Provinces)
		{
			if (province.IsSea || province.PortSea == 0 || province.Controller is not { } holder
				|| !(holder == country.Tag || IsOfOneFaction(state, country, holder))
				|| CMovementRules.BuildingLevel(state, province, CMovementRules.NavalBase) <= CFixedPoint.Zero)
			{
				continue;
			}
			long distance = state.NavalDistances.Between(province, here);
			if (distance < least)
			{
				least = distance;
				nearest = province.Id;
			}
		}
		if (nearest != 0 && CRouteFinder.Find(state, country, unit, unit.Location, nearest, safe: true) is { } route)
		{
			SetPath(state, unit, route);
		}
	}

	private static bool IsOfOneFaction(CGameState state, CCountry country, CCountryTag other)
	{
		return country.Faction.Length > 0 && state.Country(other) is { } theirs && theirs.Faction == country.Faction;
	}

	/// <summary>
	/// Sends a fleet to a province by safe water. Nothing where it is there already or
	/// may not be sent there; otherwise its path is replaced - by nothing, where there
	/// is no way.
	/// </summary>
	public static void GoTo(CGameState state, CCountry country, CUnit unit, int province)
	{
		if (unit.Location != province && CMovementRules.MayMoveTo(state, country, unit, state.Province(province)))
		{
			SetPathFrom(state, country, unit, unit.Location, province, safe: true);
		}
	}

	/// <summary>
	/// Replaces a fleet's path with the way from one province to another - which need not
	/// be the one the fleet is in. A sortie hands this each of its waypoints in turn.
	/// </summary>
	public static void SetPathFrom(CGameState state, CCountry country, CUnit unit, int from, int goal, bool safe)
	{
		if (unit.Location == goal || !CMovementRules.MayMoveTo(state, country, unit, state.Province(goal)))
		{
			return;
		}
		SetPath(state, unit, CRouteFinder.Find(state, country, unit, from, goal, safe) ?? new List<int>());
	}

	/// <summary>
	/// Gives a fleet a path in place of the one it has. One in a battle that will not
	/// let it go yet keeps what it had.
	/// </summary>
	public static void SetPath(CGameState state, CUnit unit, IReadOnlyList<int> route)
	{
		if (unit.IsFighting)
		{
			if (!CNavalDisengage.MayLeave(unit))
			{
				return;
			}
			CCombats.LeaveForMarch(state, unit);
		}
		CMovement.SetPath(unit, route, clear: true);
	}

	/// <summary>
	/// Tells a fleet to return to base: the order remembers it for good, and the fleet
	/// is given the plain way home, where it is not there.
	/// </summary>
	public static void ReturnToBase(CGameState state, CCountry country, CUnit unit)
	{
		unit.Mission.ReturnToBase = true;
		if (unit.Base != 0 && unit.Location != unit.Base)
		{
			SetPath(state, unit, CRouteFinder.Find(state, country, unit, unit.Location, unit.Base) ?? new List<int>());
		}
	}

	/// <summary>Whether a fleet is in its order's province or in one that province has an edge to.</summary>
	public static bool IsNear(CGameState state, CUnit unit)
	{
		if (unit.Location == unit.OrderProvince)
		{
			return true;
		}
		foreach (CProvinceEdge edge in state.Province(unit.OrderProvince).Edges)
		{
			if (edge.To == unit.Location)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// One step of the round a patrol and a raider make: the sea provinces beside the
	/// order's province, in the order of its edges, and then the province itself again.
	/// </summary>
	public static void StepRound(CGameState state, CCountry country, CUnit unit, bool withinRange)
	{
		CMission mission = unit.Mission;
		CProvince centre = state.Province(unit.OrderProvince);
		int count = centre.Edges.Count;
		if (mission.Round == count)
		{
			if (unit.Location != centre.Id)
			{
				GoTo(state, country, unit, centre.Id);
			}
			mission.Round = 0;
		}
		else if (mission.Round < count)
		{
			int to = centre.Edges[mission.Round].To;
			mission.Round++;
			if (to <= 0 || to >= state.Provinces.Count)
			{
				return;
			}
			CProvince beside = state.Province(to);
			if (beside.IsSea && (!withinRange || CUnitOrders.IsInRange(state, unit, unit.Order, beside)))
			{
				GoTo(state, country, unit, beside.Id);
			}
		}
	}

	/// <summary>
	/// A patrol's and a raider's try at the convoys of the water they are in, every
	/// twelve hours - the first in the first hour.
	/// </summary>
	public static void TryConvoys(CGameState state, CCountry country, CUnit unit)
	{
		CMission mission = unit.Mission;
		if (unit.IsFighting || mission.ReturnToBase || unit.IsRetreating)
		{
			return;
		}
		mission.RaidCountdown--;
		if (mission.RaidCountdown < 1)
		{
			CConvoyRaid.Resolve(state, country, unit);
			mission.RaidCountdown = HoursBetweenRaids;
		}
	}

	/// <summary>
	/// The once-a-day part of every mission. Only a reserve has one: its sound ships
	/// change places with the worn ones of the fleets beside it.
	/// </summary>
	public static void RunDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in new List<CUnit>(country.AllUnits()))
			{
				if (unit.Order == EUnitOrder.Reserve && !unit.IsRemoved)
				{
					CReserve.RunDaily(state, country, unit);
				}
			}
		}
	}
}
