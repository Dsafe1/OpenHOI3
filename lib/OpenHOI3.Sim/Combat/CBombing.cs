using System.Collections.Generic;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// A bombing: what starts one, which of its three kinds it is, who is taken into it on
/// which side, and its four hours. A province has one at most, and a unit is in one at
/// most - kept apart from its battles, so a unit under bombs is not "fighting" and may
/// march and attack as before. See <c>docs/mechanics/bombing.md</c>, sections 5 and 9.
/// </summary>
public static class CBombing
{
	/// <summary>A bombing is over at its fourth hour.</summary>
	public const int Hours = 4;

	/// <summary>The hours a unit found in a bombing starts no other, that hour counted.</summary>
	public const int Grace = 2;

	public static bool IsBombing(ECombatKind kind)
	{
		return kind is ECombatKind.GroundBombing or ECombatKind.LandBombing or ECombatKind.NavalBombing;
	}

	/// <summary>The bombing a province has, or null.</summary>
	public static CCombat? In(CGameState state, int province)
	{
		foreach (CCombat combat in state.Combats)
		{
			if (combat.Province == province && IsBombing(combat.Kind))
			{
				return combat;
			}
		}
		return null;
	}

	/// <summary>
	/// The units standing in a province, in the order they came - the fleets in its port
	/// among them. An air group aboard a carrier is left out: carriers are not built.
	/// </summary>
	public static IReadOnlyList<CUnit> Standing(CGameState state, CProvince province)
	{
		var standing = new List<CUnit>();
		foreach (CUnit unit in state.UnitsIn(province.Id))
		{
			if (!unit.IsRemoved && unit.CarriedBy is null)
			{
				standing.Add(unit);
			}
		}
		return standing;
	}

	/// <summary>
	/// Whether a unit is holding ground: it is in a battle or under a bombing whose other
	/// side still has somebody on it.
	/// </summary>
	public static bool IsHoldingGround(CUnit unit)
	{
		return CCombats.IsHoldingGround(unit)
			|| (unit.Bombing is { } bombing && bombing.SideOf(unit) is { } side && bombing.Other(side).Units.Count > 0);
	}

	/// <summary>
	/// Whether one unit would be on the other side of a bombing from another: two units
	/// of two countries at war, neither falling back; of two air units, the other at its
	/// own base and the asker under a mission that goes for aircraft there and bombs
	/// where it is; a fleet or a land unit only against an air unit; and a fleet in a
	/// port only against a mission that goes for ships.
	/// </summary>
	public static bool MayBomb(CGameState state, CUnit asker, CUnit other)
	{
		if (asker == other || asker.Owner == other.Owner || asker.IsRemoved || other.IsRemoved
			|| asker.IsRetreating || other.IsRetreating || !CMovementRules.IsEnemy(state, asker.Owner, other.Owner))
		{
			return false;
		}
		bool otherIsAir = other.Kind == EUnitKind.Air;
		if (asker.Kind == EUnitKind.Air)
		{
			return !otherIsAir || (CAirMissions.IsAtOwnBase(other)
				&& CBombingOrders.GoesForBasedAircraft(asker.Order)
				&& CBombingOrders.ShouldBomb(state, asker, state.Province(asker.Location)));
		}
		if (!otherIsAir)
		{
			return false;
		}
		return asker.Kind != EUnitKind.Navy || state.Province(asker.Location).IsSea
			|| CBombingOrders.GoesForShips(other.Order);
	}

	/// <summary>Whether every unit of a side would be on the other side from this one. A side with nobody on it would not.</summary>
	public static bool IsHostileTo(CGameState state, CCombatSide side, CUnit unit)
	{
		if (side.Units.Count == 0)
		{
			return false;
		}
		foreach (CUnit other in side.Units)
		{
			if (!MayBomb(state, other, unit))
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// What an air unit under a bombing mission does about a province it bombs in: it
	/// joins the bombing there if that will have it, and otherwise starts one.
	/// </summary>
	public static void Look(CGameState state, CUnit unit, CProvince province)
	{
		if (province.Id == 0 || unit.Kind != EUnitKind.Air || unit.IsRemoved || unit.SubUnits.Count == 0
			|| unit.Location != province.Id || !CBombingOrders.ShouldBomb(state, unit, province))
		{
			return;
		}
		if (In(state, province.Id) is { } there)
		{
			JoinExisting(state, there, unit);
		}
		if (unit.Bombing is null)
		{
			Start(state, province, unit);
		}
	}

	/// <summary>Offers a unit to a bombing that is going on: not in its hours of grace, and only a bombing of its arm.</summary>
	public static bool JoinExisting(CGameState state, CCombat bombing, CUnit unit)
	{
		if (unit.NoBombingHours > 0 || unit.Bombing is not null
			|| (CMovementRules.IsLandUnit(unit) && bombing.Kind != ECombatKind.LandBombing)
			|| (unit.Kind == EUnitKind.Navy && bombing.Kind != ECombatKind.NavalBombing))
		{
			return false;
		}
		return Offer(state, bombing, unit);
	}

	/// <summary>
	/// Offers a unit to a bombing. It joins the side the other side is against: the
	/// bombers where the targets are against it and the bombers are not, the targets the
	/// other way about - an air unit only where it is at its own base - and neither side
	/// otherwise.
	/// </summary>
	public static bool Offer(CGameState state, CCombat bombing, CUnit unit)
	{
		if (unit.IsRemoved || unit.Bombing is not null)
		{
			return false;
		}
		bool attackers = IsHostileTo(state, bombing.Attacker, unit);
		bool defenders = IsHostileTo(state, bombing.Defender, unit);
		if (defenders)
		{
			if (attackers)
			{
				return false;
			}
			Join(bombing.Attacker, unit);
			return true;
		}
		if (!attackers || (unit.Kind == EUnitKind.Air && !CAirMissions.IsAtOwnBase(unit)))
		{
			return false;
		}
		Join(bombing.Defender, unit);
		return true;
	}

	/// <summary>Takes a unit onto a side of a bombing. Its sub units forget whatever they were firing at.</summary>
	public static void Join(CCombatSide side, CUnit unit)
	{
		if (!side.Units.Contains(unit))
		{
			side.Units.Add(unit);
		}
		side.AddCountry(unit.Owner);
		unit.Bombing = side.Combat;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			subUnit.TargetId = 0;
		}
	}

	/// <summary>
	/// Takes a unit out of the bombing it is in. Its side's countries still in the fight are
	/// made again from the units left, which drops a ground bombing's holder.
	/// </summary>
	public static void Leave(CGameState state, CUnit unit)
	{
		if (unit.Bombing is not { } bombing)
		{
			return;
		}
		bombing.SideOf(unit)?.Remove(state, unit);
		unit.Bombing = null;
	}

	/// <summary>
	/// Starts a bombing where the province has none: of the ground itself, for a mission
	/// that bombs ground over land an enemy holds; otherwise of the first unit there the
	/// mission goes for and may bomb - a fleet at sea only where the bombers sight it.
	/// </summary>
	public static CCombat? Start(CGameState state, CProvince province, CUnit unit)
	{
		if (unit.Bombing is not null || unit.NoBombingHours > 0)
		{
			return null;
		}
		EUnitOrder order = unit.Order;
		ECombatKind kind;
		CUnit? target = null;
		if (!province.IsSea && CBombingOrders.BombsGround(order)
			&& CMovementRules.IsEnemy(state, province.Controller, unit.Owner))
		{
			kind = ECombatKind.GroundBombing;
		}
		else
		{
			foreach (CUnit other in Standing(state, province))
			{
				if (IsTarget(state, province, unit, other))
				{
					target = other;
					break;
				}
			}
			if (target is null)
			{
				return null;
			}
			kind = target.Kind == EUnitKind.Navy ? ECombatKind.NavalBombing : ECombatKind.LandBombing;
		}
		if (In(state, province.Id) is not null)
		{
			return null;
		}

		var bombing = new CCombat(province.Id, kind);
		state.Combats.Add(bombing);
		// A strategic bombing spreads its bomber's owner's threat (diplomacy.md, 5.3).
		if (order == EUnitOrder.StrategicBombing && state.Country(unit.Owner) is { } bomberOwner)
		{
			Diplomacy.CThreat.SpreadToAll(state, state.Defines[Diplomacy.CThreat.MilitarySection, Diplomacy.CThreat.Bombing].Raw,
				Diplomacy.CThreat.Adds, bomberOwner);
		}
		// And costs the bombed some national unity (politics.md, section 6).
		if (order == EUnitOrder.StrategicBombing)
		{
			Politics.CStrategicWarfareImpacts.StrategicBombingStarted(state, province, target);
		}
		if (kind == ECombatKind.GroundBombing && province.Controller is { } holder)
		{
			bombing.Defender.AddCountry(holder);
		}
		Join(bombing.Attacker, unit);
		if (target is not null)
		{
			Join(bombing.Defender, target);
		}
		// Everybody else in the province is offered a side - a land unit only to a
		// bombing of land units, a fleet only to one of ships.
		foreach (CUnit other in Standing(state, province))
		{
			if (other == unit || other == target
				|| (CMovementRules.IsLandUnit(other) && kind != ECombatKind.LandBombing)
				|| (other.Kind == EUnitKind.Navy && kind != ECombatKind.NavalBombing))
			{
				continue;
			}
			Offer(state, bombing, other);
		}
		// And for a mission that goes for ships, every fleet in the port, whatever the kind.
		if (CBombingOrders.GoesForShips(order) && HasPort(state, province))
		{
			foreach (CUnit fleet in Standing(state, province))
			{
				if (fleet.Kind == EUnitKind.Navy && fleet != unit && fleet != target)
				{
					Offer(state, bombing, fleet);
				}
			}
		}
		return bombing;
	}

	/// <summary>Whether a land province belongs to somebody and has a port with a naval base built in it.</summary>
	public static bool HasPort(CGameState state, CProvince province)
	{
		return !province.IsSea && province.Owner is not null && province.PortSea != 0
			&& CBombingOrders.MaxLevel(state, province, CBombingOrders.NavalBaseLevel) > CFixedPoint.Zero;
	}

	private static bool IsTarget(CGameState state, CProvince province, CUnit unit, CUnit other)
	{
		EUnitOrder order = unit.Order;
		switch (other.Kind)
		{
			case EUnitKind.Air:
				if (!CAirMissions.IsAtOwnBase(other) || !CBombingOrders.GoesForBasedAircraft(order)
					|| !CBombingOrders.ShouldBomb(state, unit, state.Province(unit.Location)))
				{
					return false;
				}
				break;
			case EUnitKind.Navy:
				if (!CBombingOrders.GoesForShips(order))
				{
					return false;
				}
				break;
			default:
				if (!CBombingOrders.GoesForLandUnits(order))
				{
					return false;
				}
				break;
		}
		// A fleet at sea has to be sighted by the bombers as by a fleet.
		if (province.IsSea && other.Kind == EUnitKind.Navy
			&& (unit.SubUnits.Count < 1 || !CNavalSpotting.ShouldStart(state, unit, other)))
		{
			return false;
		}
		return MayBomb(state, unit, other) && CNavalSpotting.DefenderAccepts(other, unit);
	}

	/// <summary>
	/// One hour of a bombing, the bombers first at every step: the two dice, what the
	/// hour makes of each side, the bombers' fire and their bombs, the fire that comes
	/// back, the settling - and at the fourth hour the end, with bombers worn to nothing
	/// falling back. The other side never loses. Answers whether it is over.
	/// </summary>
	public static bool RunHour(CGameState state, CCombat bombing)
	{
		bombing.HasBegun = true;
		bombing.Duration++;
		if (bombing.Hour == CCombat.NotStarted)
		{
			RollDice(state, bombing);
			bombing.Hour = 0;
		}
		RollDice(state, bombing);

		CBomberModifiers.Apply(state, bombing);
		CBombTargetModifiers.Apply(state, bombing);
		CBomberFire.Attack(state, bombing);
		CAntiAircraft.Attack(state, bombing);
		Settle(state, bombing.Attacker);
		Settle(state, bombing.Defender);

		if (bombing.Duration >= Hours && CNavalBattle.HasLost(bombing.Attacker))
		{
			CCombats.Conclude(state, bombing, attackerLost: true);
			foreach (CUnit unit in new List<CUnit>(bombing.Attacker.Units))
			{
				unit.Bombing = null;
				CAirRetreat.Retreat(state, unit, bombing.Attacker);
			}
			return true;
		}
		bombing.Hour++;
		return bombing.Duration >= Hours;
	}

	private static void RollDice(CGameState state, CCombat bombing)
	{
		bombing.Attacker.Dice = state.Random.Next(10);
		bombing.Defender.Dice = state.Random.Next(10);
	}

	/// <summary>
	/// The end of the hour for one side: a wing loses a hundredth of what was rolled
	/// against it, a land unit is settled as in a battle, a ship loses all of it - and a
	/// ship left with under a tenth of a point of strength is sunk.
	/// </summary>
	private static void Settle(CGameState state, CCombatSide side)
	{
		foreach (CUnit unit in new List<CUnit>(side.Units))
		{
			switch (unit.Kind)
			{
				case EUnitKind.Air:
					foreach (CSubUnit wing in unit.SubUnits)
					{
						CCombatDamage.Settle(wing);
					}
					break;
				case EUnitKind.Navy:
					SettleFleet(state, side, unit);
					break;
				default:
					CCombatDamage.SettleUnit(state, unit);
					break;
			}
		}
	}

	private static void SettleFleet(CGameState state, CCombatSide side, CUnit fleet)
	{
		for (int i = fleet.SubUnits.Count - 1; i >= 0; i--)
		{
			CSubUnit ship = fleet.SubUnits[i];
			long strength = System.Math.Min(ship.PendingStrengthDamage, ship.Strength.Raw);
			long organisation = System.Math.Min(ship.PendingOrganisationDamage, ship.Organisation.Raw);
			ship.Strength = new CFixedPoint(ship.Strength.Raw - strength);
			ship.Organisation = new CFixedPoint(ship.Organisation.Raw - organisation);
			ship.PendingStrengthDamage = 0;
			ship.PendingOrganisationDamage = 0;
			if (ship.Strength < CNavalLosses.LeastStrengthAfloat)
			{
				fleet.SubUnits.RemoveAt(i);
				ship.Unit = null;
				side.Sunk.Add(ship);
			}
		}
		if (fleet.SubUnits.Count == 0)
		{
			CCombatRetreat.RemoveFromGame(state, fleet);
		}
	}
}
