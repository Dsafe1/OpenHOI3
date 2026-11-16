using System.Linq;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// The battles of a game: what starts one, who joins it on which side, its hour, and
/// what its ending leaves. The battles of fleets, of air units and the bombings have
/// classes of their own and are sent to from here. See
/// <c>docs/mechanics/landcombat.md</c>, sections 1, 4 and 7.
/// </summary>
public static class CCombats
{
	/// <summary>A battle is not decided before it has been fought for this many hours.</summary>
	public const int LeastHours = 4;

	/// <summary>A side with less strength than this in all has lost, whatever its front.</summary>
	private static readonly CFixedPoint LeastStrength = new(10);

	/// <summary>The battle of a kind fought in a province, or null. A province has at most one of each.</summary>
	public static CCombat? In(CGameState state, int province, ECombatKind kind = ECombatKind.Land)
	{
		foreach (CCombat combat in state.Combats)
		{
			if (combat.Province == province && combat.Kind == kind)
			{
				return combat;
			}
		}
		return null;
	}

	/// <summary>
	/// What the hour's circumstances make of both sides of a battle, by its kind: the attacker's side first and then the
	/// defender's, as each kind's own hour asks them and as entering a game asks them of every battle on the list
	/// (<c>docs/mechanics/savegame.md</c>, section 20). A battle at sea and a bombing of ships draw on the game's
	/// generator for a surprise while they have not fought a round past their first; the others draw nothing.
	/// </summary>
	public static void ApplyModifiers(CGameState state, CCombat combat)
	{
		switch (combat.Kind)
		{
			case ECombatKind.Land:
				CCombatModifiers.Apply(state, combat, combat.Attacker);
				CCombatModifiers.Apply(state, combat, combat.Defender);
				break;
			case ECombatKind.Naval:
				CNavalModifiers.Apply(state, combat, combat.Attacker);
				CNavalModifiers.Apply(state, combat, combat.Defender);
				break;
			case ECombatKind.Air:
				CAirModifiers.Apply(state, combat, combat.Attacker);
				CAirModifiers.Apply(state, combat, combat.Defender);
				break;
			default:
				CBomberModifiers.Apply(state, combat);
				CBombTargetModifiers.Apply(state, combat);
				break;
		}
	}

	public static bool HasCombatIn(CUnit unit, int province)
	{
		foreach (CCombat combat in unit.Combats)
		{
			if (combat.Province == province)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Whether one unit may fight another: two units of two countries at war, both on
	/// land, neither falling back.
	/// </summary>
	public static bool MayEngage(CGameState state, CUnit unit, CUnit other)
	{
		// A unit fights its own arm: a fleet a fleet, at sea, and an air unit an air unit.
		if (unit.Kind == EUnitKind.Navy || other.Kind == EUnitKind.Navy)
		{
			return CNavalBattle.MayEngage(state, unit, other);
		}
		if (unit.Kind == EUnitKind.Air || other.Kind == EUnitKind.Air)
		{
			return CAirBattle.MayEngage(state, unit, other);
		}
		return unit != other && unit.Owner != other.Owner
			&& !unit.IsRemoved && !other.IsRemoved
			&& !unit.IsRetreating && !other.IsRetreating
			// A passenger going nowhere fights no one and is fought by no one (transport.md, section 5).
			&& !Transport.CTransport.IsIdlePassenger(unit) && !Transport.CTransport.IsIdlePassenger(other)
			// Nor does one aboard an air unit (transport.md, section 13.3).
			&& unit.Plane is null && other.Plane is null
			&& CMovementRules.IsLandUnit(unit) && CMovementRules.IsLandUnit(other)
			&& unit.SubUnits.Count > 0 && other.SubUnits.Count > 0
			&& CMovementRules.IsEnemyIn(state, unit.Owner, other.Owner, unit.Location);
	}

	/// <summary>
	/// Whether a province holds an enemy land unit that stands its ground, which is
	/// what a unit walking towards it looks for.
	/// </summary>
	public static bool HasEnemyStanding(CGameState state, CUnit unit, int province)
	{
		return HasEnemyStanding(state, unit, province, unit.Owner);
	}

	/// <summary>The same, of the country a unit would take the province for (<c>rebels.md</c>, section 10).</summary>
	public static bool HasEnemyStanding(CGameState state, CUnit unit, int province, CCountryTag taker)
	{
		foreach (CUnit other in state.UnitsIn(province))
		{
			if (other != unit && other.Owner != unit.Owner && CMovementRules.IsLandUnit(other) && !other.IsRetreating
				&& !other.IsRemoved && !Transport.CTransport.IsIdlePassenger(other) && other.Plane is null
				&& CMovementRules.IsEnemy(state, other.Owner, taker))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// What a land unit does about a province: it joins the battle there if one will
	/// have it, starts one if an enemy is there, and - in no battle, standing in the
	/// province, with no enemy standing beside it - takes the province.
	/// </summary>
	public static void CheckForCombat(CGameState state, CUnit unit, int provinceId)
	{
		CProvince province = state.Province(provinceId);
		if (unit.Kind == EUnitKind.Navy)
		{
			CNavalBattle.Look(state, unit, province);
			return;
		}
		if (unit.Kind == EUnitKind.Air)
		{
			CAirBattle.Look(state, unit, province);
			// And where its mission bombs here, the bombing: the one there, or a new one.
			CBombing.Look(state, unit, province);
			return;
		}
		if (province.Id == 0 || unit.IsRemoved || !CMovementRules.IsLandUnit(unit) || unit.SubUnits.Count == 0)
		{
			return;
		}
		// A passenger starts nothing while its fleet sails, and going nowhere takes nothing (transport.md, section 5).
		if (unit.Transport is { } fleet && (fleet.IsMoving || !unit.IsMoving))
		{
			return;
		}
		// Nor while it is aboard an air unit (transport.md, section 13.3).
		if (unit.Plane is not null)
		{
			return;
		}
		if (!HasCombatIn(unit, provinceId) && In(state, provinceId) is { } there)
		{
			Offer(state, there, unit);
		}
		if (!HasCombatIn(unit, provinceId))
		{
			Start(state, province, unit);
		}

		if (unit.IsFighting || unit.IsRetreating || province.IsSea || province.Owner is null || unit.Location != provinceId)
		{
			return;
		}
		CCountryTag taker = Taker(state, unit, province);
		if (HasEnemyStanding(state, unit, provinceId, taker))
		{
			return;
		}
		CCountryTag? old = province.Controller;
		if (CProvinceControl.Take(state, province, taker))
		{
			TellLost(state, province, old);
		}
	}

	/// <summary>
	/// Who a unit takes a province for: its owner - and for a rebel army with a faction, the country the faction would
	/// free where its type frees nobody and that country owns land, then the province's holder where the faction counts
	/// it a friend, then its owner where it does - last, so it wins. See <c>docs/mechanics/rebels.md</c>, section 10.
	/// </summary>
	public static CCountryTag Taker(CGameState state, CUnit unit, CProvince province)
	{
		CCountryTag taker = unit.Owner;
		if (!Revolts.CRebelFactions.IsRebels(unit.Owner) || Revolts.CRebelFactions.OfArmy(state, unit) is not { } faction)
		{
			return taker;
		}
		if (!faction.Type.Independence && faction.Independence is { } freed && CCountryLists.OwnedCount(state, freed) > 0)
		{
			taker = freed;
		}
		if (province.Controller is { } holder && Revolts.CRebelFactions.IsFriendly(state, faction, holder))
		{
			taker = holder;
		}
		if (province.Owner is { } owner && Revolts.CRebelFactions.IsFriendly(state, faction, owner))
		{
			taker = owner;
		}
		return taker;
	}

	/// <summary>
	/// <c>PROVINCELOST</c> to the country that held a province a capture has just taken from it: the province, and the
	/// new holder's name - the faction's where the rebels hold it.
	/// </summary>
	private static void TellLost(CGameState state, CProvince province, CCountryTag? old)
	{
		if (old is not { } lost || province.Controller is not { } now || now == lost)
		{
			return;
		}
		string name = state.Country(now)?.Name ?? now.Value;
		if (Revolts.CRebelFactions.IsRebels(now) && Revolts.CRebelFactions.Holding(state, province.Id) is { } faction)
		{
			name = faction.Name;
		}
		state.RaisePactMessage(new Agreements.CPactMessage("PROVINCELOST", "", "", now, lost)
		{
			Told = lost,
			Province = province.Id,
			Messenger = "MESSAGE_HEAD_MARS",
			Values = new[] { ("PROV", province.Name), ("COUNTRY", name) },
		});
	}

	/// <summary>
	/// Starts a battle for a province, if the unit finds an enemy there to fight and
	/// the province has none already. The unit is the attacker, the first enemy it finds
	/// the defender, and everyone else in the province is offered a side.
	/// </summary>
	public static CCombat? Start(CGameState state, CProvince province, CUnit unit)
	{
		if (HasCombatIn(unit, province.Id) || province.IsSea)
		{
			return null;
		}
		CUnit? defender = null;
		foreach (CUnit other in state.UnitsIn(province.Id))
		{
			if (MayEngage(state, unit, other))
			{
				defender = other;
				break;
			}
		}
		if (defender is null || In(state, province.Id) is not null)
		{
			return null;
		}

		var combat = new CCombat(province.Id);
		state.Combats.Add(combat);
		Diplomacy.CThreat.BattleStarted(state, province, unit, Diplomacy.CThreat.LandCombat);
		combat.Attacker.Add(unit);
		combat.Defender.Add(defender);
		foreach (CUnit other in new List<CUnit>(state.UnitsIn(province.Id)))
		{
			if (other != unit && other != defender)
			{
				Offer(state, combat, other);
			}
		}
		return combat;
	}

	/// <summary>
	/// Offers a unit to a battle. It joins the side whose enemies are all its enemies:
	/// the attackers where every defender would fight it and not every attacker would,
	/// the defenders the other way about, and neither side otherwise.
	/// </summary>
	public static bool Offer(CGameState state, CCombat combat, CUnit unit)
	{
		bool isNaval = combat.Kind == ECombatKind.Naval;
		bool isAir = combat.Kind == ECombatKind.Air;
		bool isOfTheArm = isNaval ? unit.Kind == EUnitKind.Navy
			: isAir ? unit.Kind == EUnitKind.Air
			: CMovementRules.IsLandUnit(unit);
		if (unit.IsRemoved || !isOfTheArm)
		{
			return false;
		}
		bool attackers = IsHostileTo(state, combat.Attacker, unit);
		bool defenders = IsHostileTo(state, combat.Defender, unit);
		if (attackers == defenders)
		{
			return false;
		}
		CCombatSide side = defenders ? combat.Attacker : combat.Defender;
		if (isNaval)
		{
			CNavalBattle.Join(state, combat, side, unit);
		}
		else if (isAir)
		{
			CAirBattle.Join(side, unit);
		}
		else
		{
			side.Add(unit);
		}
		return true;
	}

	/// <summary>Whether every unit of a side would fight this one. A side with nobody on it would not.</summary>
	public static bool IsHostileTo(CGameState state, CCombatSide side, CUnit unit)
	{
		if (side.Units.Count == 0)
		{
			return false;
		}
		foreach (CUnit other in side.Units)
		{
			if (!MayEngage(state, other, unit))
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// Every battle's hour, in the order the battles began: a battle seen for the first
	/// time has its fronts set up, then it is fought, and one that is over is ended.
	/// </summary>
	public static void RunHourly(CGameState state)
	{
		// The list of finished battles is kept to a week before any battle is fought.
		state.CombatHistory.Prune(state.TotalHours);
		foreach (CCombat combat in new List<CCombat>(state.Combats))
		{
			if (!state.Combats.Contains(combat))
			{
				continue;
			}
			bool isNaval = combat.Kind == ECombatKind.Naval;
			if (CBombing.IsBombing(combat.Kind))
			{
				Trace(state, combat);
				bool bombingOver = CBombing.RunHour(state, combat);
				state.CombatTrace?.Invoke($"END draws={state.Random.Draws}");
				if (bombingOver)
				{
					Finish(state, combat);
				}
				continue;
			}
			if (combat.Kind == ECombatKind.Air)
			{
				Trace(state, combat);
				if (CAirBattle.RunHour(state, combat))
				{
					Finish(state, combat);
				}
				continue;
			}
			if (!combat.HasBegun)
			{
				if (isNaval)
				{
					CNavalBattle.Begin(state, combat);
				}
				else
				{
					Begin(state, combat);
				}
			}
			Trace(state, combat);
			if (isNaval ? CNavalBattle.RunHour(state, combat) : RunHour(state, combat))
			{
				Finish(state, combat);
			}
		}
	}

	private static void Trace(CGameState state, CCombat combat)
	{
		state.BeforeBattle?.Invoke(combat);
		if (state.CombatTrace is not { } trace)
		{
			return;
		}
		string fronts = combat.Kind != ECombatKind.Land ? "" :
			$" Af={string.Join(",", combat.Attacker.Front.ConvertAll(u => u.Id))} Ar={string.Join(",", combat.Attacker.Reserves.ConvertAll(u => u.Id))}" +
			$" Df={string.Join(",", combat.Defender.Front.ConvertAll(u => u.Id))} Dr={string.Join(",", combat.Defender.Reserves.ConvertAll(u => u.Id))}";
		trace($"COMBAT {combat.Kind} prov={combat.Province} day={combat.Hour} dur={combat.Duration} draws={state.Random.Draws}" +
			$" A={string.Join(",", combat.Attacker.Units.ConvertAll(u => u.Id))} D={string.Join(",", combat.Defender.Units.ConvertAll(u => u.Id))}{fronts}");
	}

	public static void Begin(CGameState state, CCombat combat)
	{
		combat.HasBegun = true;
		if (!combat.Attacker.IsFrontSetUp)
		{
			CFrontLine.SetUp(state, combat, combat.Attacker);
		}
		if (!combat.Defender.IsFrontSetUp)
		{
			CFrontLine.SetUp(state, combat, combat.Defender);
		}
		state.RaiseCombatBegan(combat);
	}

	/// <summary>
	/// One hour of a battle, in the original's order, attacker first throughout: the
	/// tactics where they are due, the two dice, what the hour's circumstances make of
	/// every unit, the two sides' shooting, the settling of what it did, and from the
	/// fourth hour whether a side has lost. Answers whether the battle is over.
	/// </summary>
	public static bool RunHour(CGameState state, CCombat combat)
	{
		if (CCombatTactics.IsDue(state, combat))
		{
			CCombatTactics.Pick(state, combat);
		}
		combat.Duration++;
		if (combat.Hour == CCombat.NotStarted)
		{
			RollDice(state, combat);
			combat.Hour = 0;
		}
		RollDice(state, combat);

		CCombatModifiers.Apply(state, combat, combat.Attacker);
		CCombatModifiers.Apply(state, combat, combat.Defender);
		if (state.CombatTrace is { } modifiers)
		{
			foreach (CUnit unit in combat.Attacker.Units.Concat(combat.Defender.Units))
			{
				modifiers($"MODS u={unit.Id} prod={unit.CombatAttack.Raw}/{unit.CombatDefence.Raw} " + string.Join(" ", unit.CombatModifiers.Select(m => $"{(int)m.Kind}:{m.Attack.Raw}")));
			}
		}
		CLandFire.Attack(state, combat, combat.Attacker);
		CLandFire.Attack(state, combat, combat.Defender);
		StrategicWarfare.CWarfareTally.LandHour(state, combat);
		combat.Attacker.NoteLosses();
		combat.Defender.NoteLosses();
		Settle(state, combat.Attacker);
		Settle(state, combat.Defender);

		if (combat.Duration >= LeastHours)
		{
			if (HasLost(combat.Attacker))
			{
				Conclude(state, combat, attackerLost: true);
				RetreatAll(state, combat.Attacker);
				return true;
			}
			if (HasLost(combat.Defender))
			{
				Conclude(state, combat, attackerLost: false);
				RetreatAll(state, combat.Defender);
				return true;
			}
		}
		combat.Hour++;
		return false;
	}

	private static void RollDice(CGameState state, CCombat combat)
	{
		combat.Attacker.Dice = state.Random.Next(10);
		combat.Defender.Dice = state.Random.Next(10);
	}

	/// <summary>The end of the hour for one side: every unit on it is settled, in the order they joined.</summary>
	private static void Settle(CGameState state, CCombatSide side)
	{
		long men = 0;
		foreach (CUnit unit in new List<CUnit>(side.Units))
		{
			men += CCombatDamage.SettleUnit(state, unit);
		}
		side.Losses += new CFixedPoint(men);
	}

	/// <summary>
	/// Whether a side has lost: it has next to no strength left, or nobody on its front,
	/// or a front with no organisation left to speak of. Reserves do not save it.
	/// </summary>
	public static bool HasLost(CCombatSide side)
	{
		long strength = 0;
		foreach (CUnit unit in side.Units)
		{
			strength += CUnitTotals.Strength(unit).Raw;
		}
		if (strength < LeastStrength.Raw || side.Front.Count == 0)
		{
			return true;
		}
		long organisation = 0;
		foreach (CUnit unit in side.Front)
		{
			organisation += CUnitTotals.AverageOrganisation(unit).Raw;
		}
		return organisation * CFixedPoint.Scale / (side.Front.Count * (long)CFixedPoint.Scale)
			<= CUnitTotals.LeastOrganisationToFight.Raw;
	}

	/// <summary>
	/// A battle is decided, once: the world's last-battle losses are set (<c>docs/mechanics/triggers.md</c>,
	/// section 5.2), and its on_actions fire - each side's countries' battles counted and their events picked, the
	/// winners' first (<c>docs/mechanics/events.md</c>, section 8). The original's message goes here too, not built.
	/// </summary>
	public static void Conclude(CGameState state, CCombat combat, bool attackerLost)
	{
		if (combat.IsConcluded)
		{
			return;
		}
		combat.IsConcluded = true;
		combat.AttackerLost = attackerLost;
		state.LastBattleLosses.Record(combat, attackerLost);
		Diplomacy.CRelations.LandBattleWon(state, combat, attackerLost);
		Events.COnActions.Battle(state, combat, attackerLost);
	}

	private static void RetreatAll(CGameState state, CCombatSide side)
	{
		foreach (CUnit unit in new List<CUnit>(side.Units))
		{
			CCombatRetreat.Retreat(state, unit, side);
		}
	}

	/// <summary>
	/// A battle that is over: its units are let go, it is gone, and every unit standing
	/// in its province looks at the province again - which is where a third party's
	/// battle starts, and where whoever is left takes the ground.
	/// </summary>
	public static void Finish(CGameState state, CCombat combat)
	{
		StrategicWarfare.CWarfareTally.BattleEnded(state, combat);
		// A fleet let out of a battle will start no other for some hours.
		if (combat.Kind == ECombatKind.Naval)
		{
			foreach (CCombatSide side in new[] { combat.Attacker, combat.Defender })
			{
				foreach (CUnit unit in side.Units)
				{
					CNavalDisengage.StartWait(state, unit);
				}
			}
		}
		// Over: each side's leaders have their banked hours counted, the attacker's first.
		combat.HasEnded = true;
		CTraitGain.Count(state, combat, combat.Attacker);
		Detach(combat.Attacker);
		CTraitGain.Count(state, combat, combat.Defender);
		Detach(combat.Defender);
		state.Combats.Remove(combat);
		state.CombatHistory.Record(combat, state.TotalHours);
		foreach (CUnit unit in new List<CUnit>(state.UnitsIn(combat.Province)))
		{
			if (unit.Location == combat.Province)
			{
				CheckForCombat(state, unit, combat.Province);
			}
		}
	}

	private static void Detach(CCombatSide side)
	{
		foreach (CUnit unit in side.Units)
		{
			unit.Combats.Remove(side.Combat);
			if (unit.Bombing == side.Combat)
			{
				unit.Bombing = null;
			}
		}
		side.Units.Clear();
		side.Front.Clear();
		side.Reserves.Clear();
	}

	/// <summary>
	/// One unit leaves a battle that goes on. Where it was the only unit of its side
	/// the battle is decided against that side - and is ended by its own hour, which
	/// finds the side with nobody on its front, and not before the fourth.
	/// </summary>
	public static void Leave(CGameState state, CCombat combat, CUnit unit)
	{
		if (combat.SideOf(unit) is { } side)
		{
			if (side.Units.Count == 1)
			{
				Conclude(state, combat, side.IsAttacker);
			}
			side.Remove(state, unit);
			if (combat.Kind == ECombatKind.Naval)
			{
				CNavalDisengage.StartWait(state, unit);
			}
		}
		unit.Combats.Remove(combat);
	}

	/// <summary>Takes a unit out of every battle it is in, and out of the bombing it is in.</summary>
	public static void LeaveAll(CGameState state, CUnit unit)
	{
		CBombing.Leave(state, unit);
		foreach (CCombat combat in new List<CCombat>(unit.Combats))
		{
			Leave(state, combat, unit);
		}
	}

	/// <summary>Takes a unit out of every battle it is attacking in, which is what halting it does.</summary>
	public static void LeaveAttacks(CGameState state, CUnit unit)
	{
		foreach (CCombat combat in new List<CCombat>(unit.Combats))
		{
			if (combat.Attacker.Units.Contains(unit))
			{
				Leave(state, combat, unit);
			}
		}
	}

	/// <summary>
	/// Whether a unit is holding ground somewhere: it is in a battle whose other side
	/// still has somebody on it. A unit ordered away from such a battle falls back.
	/// </summary>
	public static bool IsHoldingGround(CUnit unit)
	{
		foreach (CCombat combat in unit.Combats)
		{
			if (combat.SideOf(unit) is { } side && combat.Other(side).Units.Count > 0)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// A unit in a battle is given somewhere to go: it leaves every battle it is in, and
	/// where it was holding ground it is falling back from now on.
	/// </summary>
	public static void LeaveForMarch(CGameState state, CUnit unit)
	{
		if (!unit.IsFighting)
		{
			return;
		}
		bool holding = IsHoldingGround(unit);
		LeaveAll(state, unit);
		if (holding)
		{
			unit.IsRetreating = true;
		}
	}

	/// <summary>
	/// The battles a unit is no longer beside are left: one fought over a province that
	/// is neither where it stands nor next to it.
	/// </summary>
	public static void LeaveDistant(CGameState state, CUnit unit)
	{
		foreach (CCombat combat in new List<CCombat>(unit.Combats))
		{
			if (combat.Province != unit.Location && state.Province(combat.Province).EdgeTo(unit.Location) is null)
			{
				Leave(state, combat, unit);
			}
		}
	}
}
