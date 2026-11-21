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
		CCountryTag? killer = combat.Other(side).Countries.Count > 0 ? combat.Other(side).Countries[0] : null;
		if (state.Country(unit.Owner) is not { } country || to == 0)
		{
			StrategicWarfare.CWarfareTally.UnitLost(state, killer, unit);
			Units.CPendingUnitRemovals.Remove(state, unit);
			return;
		}

		List<int>? route = to == unit.Location
			? new List<int>()
			: CRouteFinder.Find(state, country, unit, unit.Location, to);
		if (route is null)
		{
			StrategicWarfare.CWarfareTally.UnitLost(state, killer, unit);
			Units.CPendingUnitRemovals.Remove(state, unit);
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
				// Its owner's, or on its side or fighting beside it and not its enemy (FINDINGS-liveread.md, section 41).
				if (Agents.CApproaches.IsFriendly(state, country, neighbour.Controller))
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
	/// Whether a unit may fall back into a province, the original's <c>CUnit::CanRetreatToProvince</c> whole: the province
	/// it stands in or one an edge of its province leads to, of any kind, and then <see cref="MayRetreatTo"/>. An air unit
	/// asks only that no enemy air unit's wings there add up to a whole point of strength (<see cref="CAirRetreat"/>).
	/// It is the test a unit in a battle is sent by, and what the map's tooltip says of one
	/// (<c>reversing/findings/FINDINGS-movement.md</c>, section 12.2).
	/// </summary>
	public static bool CanRetreatTo(CGameState state, CCountry country, CUnit unit, CProvince province)
	{
		if (unit.Kind == EUnitKind.Air)
		{
			return CAirRetreat.MayRetreatTo(state, unit, province);
		}
		if (province.Id != unit.Location && state.Province(unit.Location).EdgeTo(province.Id) is null)
		{
			return false;
		}
		return MayRetreatTo(state, country, unit, province);
	}

	/// <summary>
	/// Whether a unit may fall back into a province beside it: the enemy land units standing there add up to less than a
	/// whole point of strength, and either the province is not held by a country at war with the unit's - a war nobody
	/// declared counting where it covers the province - or the unit still has an organisation of one.
	/// </summary>
	public static bool MayRetreatTo(CGameState state, CCountry country, CUnit unit, CProvince province)
	{
		if (CMovementRules.IsEnemyIn(state, province.Controller, country.Tag, province.Id)
			&& CUnitTotals.AverageOrganisation(unit) < CUnitTotals.LeastOrganisationToFight)
		{
			return false;
		}
		return EnemyStrengthIn(state, country.Tag, province.Id, air: false) < 1;
	}

	/// <summary>
	/// The strength of the land units - or of the air units - in a province whose countries are at war with the asking
	/// one there, rebels counted and a war nobody declared where it covers the province: their brigades' strength added up
	/// in thousandths and cut to whole points (the original's <c>EnemyLandStrengthIn</c> and <c>EnemyAirStrengthIn</c>).
	/// </summary>
	public static long EnemyStrengthIn(CGameState state, CCountryTag asking, int province, bool air)
	{
		long sum = 0;
		foreach (CUnit other in state.UnitsIn(province))
		{
			bool kind = air ? other.Kind == EUnitKind.Air : CMovementRules.IsLandUnit(other);
			if (kind && CMovementRules.IsEnemyIn(state, other.Owner, asking, other.Location))
			{
				sum += CUnitTotals.Strength(other).Raw;
			}
		}
		return sum / CFixedPoint.Scale;
	}

	/// <summary>The share of its strength a shattered unit is re-formed with, in thousandths.</summary>
	public const long ShatteredStrength = 100;

	/// <summary>
	/// Where a shattered unit is re-formed: its country's acting capital - but for a theatre's headquarters the depot
	/// its province draws on, where no enemy holds that and it stands elsewhere, and for any other unit the province of
	/// the theatre's headquarters at the top of its chain, where that stands elsewhere. 0 for none.
	/// </summary>
	public static int ShatterHome(CGameState state, CCountry owner, CUnit unit)
	{
		int home = owner.ActingCapital;
		if (unit.Kind == EUnitKind.Theatre)
		{
			int depot = Supply.CSupplyNetworks.Refresh(state).DepotOf(unit.Location);
			if (depot > 0 && depot != unit.Location && !CMovementRules.IsEnemy(state, state.Province(depot).Controller, owner.Tag))
			{
				home = depot;
			}
			return home;
		}
		CUnit? top = unit.Parent;
		while (top is not null && top.Kind != EUnitKind.Theatre)
		{
			top = top.Parent;
		}
		if (top is not null && top.Location != unit.Location)
		{
			home = top.Location;
		}
		return home;
	}

	/// <summary>
	/// A unit worn too thin is taken out of every battle and re-formed at its home (<see cref="ShatterHome"/>) with a
	/// tenth of each brigade's strength, its place in the order of battle as it was, its player told. With no home, or
	/// standing there already, it is lost instead: it leaves its commander, what it commanded is put under that
	/// commander, and it goes with its officers. See <c>docs/mechanics/landcombat.md</c>, section 6.
	/// </summary>
	public static void Shatter(CGameState state, CCountry owner, CUnit unit)
	{
		int home = ShatterHome(state, owner, unit);
		if (home <= 0 || home == unit.Location)
		{
			CCombats.LeaveAll(state, unit);
			// The one removal that hands what the unit commanded to whatever commanded it; with nobody above it they
			// are left as they are. Its officers go with it.
			Units.CPendingUnitRemovals.LoseOfficers(state, unit);
			RemoveFromGame(state, unit, childrenToTop: unit.Parent is null);
			return;
		}
		if (state.Played.Contains(owner.Tag))
		{
			state.RaisePactMessage(new Agreements.CPactMessage("SHATTERED", "", "", owner.Tag, owner.Tag)
			{
				Told = owner.Tag,
				Values = new[]
				{
					("UNIT", unit.Name), ("LOCATION", state.Province(unit.Location).Name),
					("NEWHOME", state.Province(home).Name),
				},
			});
		}
		CCombats.LeaveAll(state, unit);
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			long most = brigade.Stats[CSubUnitType.MaxStrength].Raw;
			long left = System.Math.Max(0, System.Math.Min(most, brigade.Strength.Raw * ShatteredStrength / CFixedPoint.Scale));
			brigade.Strength = new CFixedPoint(left);
			if (brigade.Highest.Raw < left)
			{
				brigade.Highest = new CFixedPoint(left);
			}
		}
		CMovement.Cancel(unit);
		CUnitOrders.Clear(unit);
		unit.IsRetreating = false;
		state.Positions.Move(unit, unit.Location, home);
		unit.Previous = 0;
		unit.Location = home;
	}

	/// <summary>
	/// Takes a unit out of the game: out of its battles, off the map and out of its
	/// country's order of battle. What it commanded is left, as the original's <c>CGameState::RemoveUnit</c> leaves it, at
	/// the top of the order of battle with nobody above it, what a theatre-level unit commanded marked orphans - or, with
	/// <paramref name="childrenToTop"/> off, handed to whatever commanded the unit, which the original does for a
	/// shattered unit with no home alone. No officers are charged here: a caller the original charges them for goes
	/// through <see cref="Units.CPendingUnitRemovals.Remove"/> (<c>docs/mechanics/units.md</c>, section 9).
	/// </summary>
	public static void RemoveFromGame(CGameState state, CUnit unit, bool childrenToTop = true)
	{
		if (unit.IsRemoved)
		{
			return;
		}
		unit.IsRemoved = true;
		state.UnitRemovedTrace?.Invoke(unit);
		// A unit on loan leaves its lender's list of them, which reinforces and upgrades it (expeditions.md, section 4).
		if (unit.Lender is { } lender)
		{
			state.Country(lender)?.LentUnits.Remove(unit);
		}
		Missions.CNukeMission.GiveBack(unit.Mission);
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
			// Every ship still in it leaves the game with it, and goes on the list of sunk ships (savegame.md, section 7).
			foreach (CSubUnit sunk in unit.SubUnits)
			{
				CSunkShips.Add(state, unit.Owner, sunk);
			}
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
			// A fleet's units are its carriers' air groups, and the original leaves each with none above it - one still
			// aboard a ship, which a merge hands to another fleet, met through that ship (transport.md, section 13.1).
			if (unit.Kind == EUnitKind.Navy)
			{
				child.Parent = null;
				if (child.CarriedBy is null)
				{
					country?.Units.Add(child);
				}
				continue;
			}
			if (childrenToTop)
			{
				child.Parent = null;
				country?.Units.Add(child);
				if (unit.Kind == EUnitKind.Theatre)
				{
					child.IsOrphaned = true;
				}
				continue;
			}
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
