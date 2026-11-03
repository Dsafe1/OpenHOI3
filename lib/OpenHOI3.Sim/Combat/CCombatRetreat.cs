using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// The ways a unit leaves a battle it did not win: it falls back to a province next
/// door, it stays where it is because it was attacking from there, it shatters and is
/// seen again at its capital, or - with nowhere to go - it is gone. See
/// <c>docs/mechanics/landcombat.md</c>, section 7.
/// </summary>
public static class CCombatRetreat
{
	public const string RetreatProgress = "RETREAT_PROGRESS";

	/// <summary>What a neighbour adds to a province's worth as somewhere to fall back to.</summary>
	private const long FriendlyNeighbour = 1000;
	private const long NeutralNeighbour = 500;

	/// <summary>A province has to be worth more than this to be fallen back to at all.</summary>
	private const long LeastWorth = 10;

	/// <summary>
	/// A unit falls back out of a battle. A defender goes to the best province next to
	/// it; an attacker, which fought from where it stands, simply stops there. A unit
	/// with nowhere to go, or no way to get there, is removed from the game.
	/// </summary>
	public static void Retreat(CGameState state, CUnit unit, CCombatSide side)
	{
		CCombat combat = side.Combat;
		int to = combat.Province != unit.Location ? unit.Location : FindRetreatProvince(state, unit);

		unit.Combats.Remove(combat);
		side.Remove(state, unit);
		if (state.Country(unit.Owner) is not { } country || to == 0)
		{
			RemoveFromGame(state, unit);
			return;
		}

		List<int>? route = to == unit.Location
			? new List<int>()
			: CRouteFinder.Find(state, country, unit, unit.Location, to);
		if (route is null)
		{
			RemoveFromGame(state, unit);
			return;
		}

		CUnitOrders.Clear(unit);
		if (to == unit.Location)
		{
			CMovement.Cancel(unit);
			return;
		}

		CCombats.LeaveAll(state, unit);
		CMovement.SetPath(unit, route, clear: true);

		// It sets off as far along as the best of the defenders had already walked.
		long share = 0;
		foreach (CUnit other in combat.Defender.Units)
		{
			long distance = CMovement.Distance(state, other).Raw;
			if (other.IsMoving && distance != 0)
			{
				share = System.Math.Max(share, other.MovementProgress.Raw * CFixedPoint.Scale / distance);
			}
		}
		share = state.Defines[CManpower.MilitarySection, RetreatProgress].Raw * share / CFixedPoint.Scale;
		unit.MovementProgress = new CFixedPoint(CMovement.Distance(state, unit).Raw * share / CFixedPoint.Scale);
		unit.IsRetreating = true;
	}

	/// <summary>
	/// Where a land unit falls back to: of the provinces next to it that it may stand
	/// in and that hold no enemy, the one worth most - a thousand for each of its own
	/// neighbours held by the unit's country or a friend, five hundred for one held by
	/// a country that is neither friend nor enemy, and the whole halved if any is held by
	/// an enemy. Of two worth the same the first found is kept. An enemy's own ground
	/// will do, undefended, for a unit that still has an organisation of one. 0 for
	/// nowhere.
	/// </summary>
	public static int FindRetreatProvince(CGameState state, CUnit unit)
	{
		if (state.Country(unit.Owner) is not { } country)
		{
			return 0;
		}
		CProvince here = state.Province(unit.Location);
		int best = 0;
		long bestWorth = FriendlyNeighbour;
		foreach (CProvinceEdge edge in here.Edges)
		{
			CProvince province = state.Province(edge.To);
			if (province.Id == 0 || province.IsSea || here.IsSea
				|| !CMovementRules.MayStandIn(state, country, unit, province)
				|| CMovementRules.IsCrossingBlocked(state, country, unit, edge)
				|| !MayRetreatTo(state, country, unit, province))
			{
				continue;
			}

			long worth = 0;
			bool enemyBeside = false;
			foreach (CProvinceEdge beyond in province.Edges)
			{
				CProvince neighbour = state.Province(beyond.To);
				if (beyond.Kind == EProvinceEdgeKind.Impassable || neighbour.Id == 0 || neighbour.IsSea
					|| neighbour.Id == province.Id)
				{
					continue;
				}
				if (CMovementRules.IsFriendly(state, country, neighbour.Controller))
				{
					worth += FriendlyNeighbour;
				}
				else if (CMovementRules.IsEnemy(state, country.Tag, neighbour.Controller))
				{
					enemyBeside = true;
				}
				else
				{
					worth += NeutralNeighbour;
				}
			}
			if (enemyBeside)
			{
				worth = worth * CFixedPoint.Scale / (2 * CFixedPoint.Scale);
			}
			if (worth > LeastWorth && (best == 0 || worth > bestWorth))
			{
				best = province.Id;
				bestWorth = worth;
			}
		}
		return best;
	}

	/// <summary>
	/// Whether a unit may fall back into a province: no enemy land unit is there, and
	/// either an enemy does not hold it or the unit still has an organisation of one.
	/// </summary>
	public static bool MayRetreatTo(CGameState state, CCountry country, CUnit unit, CProvince province)
	{
		if (CMovementRules.IsEnemy(state, province.Controller, country.Tag)
			&& CUnitTotals.AverageOrganisation(unit) < CUnitTotals.LeastOrganisationToFight)
		{
			return false;
		}
		foreach (CUnit other in state.UnitsIn(province.Id))
		{
			if (CMovementRules.IsLandUnit(other) && CMovementRules.IsEnemy(state, other.Owner, country.Tag)
				&& CUnitTotals.Strength(other) > CFixedPoint.Zero)
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// A unit worn too thin is taken out of every battle and off the map, and stands at
	/// its country's capital, under its theatre. A country with no capital loses it.
	/// </summary>
	public static void Shatter(CGameState state, CCountry owner, CUnit unit)
	{
		CCombats.LeaveAll(state, unit);
		if (owner.ActingCapital == 0)
		{
			RemoveFromGame(state, unit);
			return;
		}
		CMovement.Cancel(unit);
		CUnitOrders.Clear(unit);
		unit.IsRetreating = false;
		state.Positions.Move(unit, unit.Location, owner.ActingCapital);
		unit.Previous = 0;
		unit.Location = owner.ActingCapital;

		CUnit? top = unit.Parent;
		while (top?.Parent is not null)
		{
			top = top.Parent;
		}
		if (top is { Kind: EUnitKind.Theatre } && top != unit.Parent)
		{
			unit.Parent!.Children.Remove(unit);
			unit.Parent = top;
			top.Children.Add(unit);
		}
	}

	/// <summary>
	/// Takes a unit out of the game: out of its battles, off the map and out of its
	/// country's order of battle. What it commanded goes to whatever commanded it.
	/// </summary>
	public static void RemoveFromGame(CGameState state, CUnit unit)
	{
		if (unit.IsRemoved)
		{
			return;
		}
		unit.IsRemoved = true;
		state.UnitRemovedTrace?.Invoke(unit);
		// A fleet or an air unit takes its passengers with it; a passenger leaves its fleet's or its air unit's list
		// (transport.md, sections 6 and 13.3); and a fleet lets its carriers' air groups go (section 13.1).
		Transport.CTransport.RemovePassengers(state, unit);
		if (unit.Transport is { } fleet)
		{
			fleet.Passengers.Remove(unit);
			unit.Transport = null;
		}
		if (unit.Plane is { } plane)
		{
			plane.Passengers.Remove(unit);
			unit.Plane = null;
		}
		if (unit.Kind == EUnitKind.Navy)
		{
			Transport.CCarriers.LetGoAll(state, unit);
		}
		// An air group aboard a carrier leaves the ship's list.
		if (unit.CarriedBy is { } ship)
		{
			ship.Carried.Remove(unit);
			unit.CarriedBy = null;
		}
		CCombats.LeaveAll(state, unit);
		Leaders.CLeaders.SetLeader(state, unit, null, false, false);   // its leader is free again
		unit.Path.Clear();
		state.Positions.Remove(unit, unit.Location);

		CCountry? country = state.Country(unit.Owner);
		foreach (CUnit child in unit.Children)
		{
			child.Parent = unit.Parent;
			if (unit.Parent is not null)
			{
				unit.Parent.Children.Add(child);
			}
			else
			{
				country?.Units.Add(child);
			}
		}
		unit.Children.Clear();
		if (unit.Parent is not null)
		{
			unit.Parent.Children.Remove(unit);
			unit.Parent = null;
		}
		else
		{
			country?.Units.Remove(unit);
		}
	}
}
