using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// A battle between fleets: what a fleet does about the sea province it is in, the two
/// sides and where each ship is put, the positioning of a side, and the hour. See
/// <c>docs/mechanics/navalcombat.md</c>.
/// </summary>
public static class CNavalBattle
{
	public const string FiringDistance = "distance";
	public const string Positioning = "positioning";
	public const string Hull = "hull";
	public const string HullLimit = "NAVAL_STACK_POS_HULL_LIMIT";
	public const string HullPenalty = "NAVAL_STACK_POS_PENALTY_HULL_MULT";
	public const string MostPenalty = "NAVAL_STACK_POS_PENALTY_MAX";

	/// <summary>A side's positioning before its roll and its penalty.</summary>
	public const long BasePositioning = 400;

	/// <summary>What positioning creeps up by each hour, and the least it creeps towards.</summary>
	public const long PositioningStep = 20;
	public const long LeastPositioningCap = 100;

	/// <summary>
	/// How far out of its place a ship may be drawn up, as a share of half its reach, less
	/// the <c>spread_out</c> its admiral brings; and the least that share comes to.
	/// </summary>
	private const long Spread = 300;
	private const long LeastSpreadShare = 100;

	/// <summary>What that is multiplied by: five, less the skill of the fleet's own admiral.</summary>
	private const long SpreadWithoutLeader = 5;

	/// <summary>What a side's positioning gains for each point of its highest ranked leader's skill.</summary>
	public const long PositioningPerSkill = 100;

	/// <summary>The least the spread comes to, and the nearest a ship ever is.</summary>
	private const long LeastSpread = 2000;
	public const long NearestDistance = 1000;

	/// <summary>How much further out a fleet that joins a battle already begun starts.</summary>
	private const long LateDistance = 100000;

	/// <summary>A side with less strength than this in all has lost.</summary>
	private const long LeastStrength = 10;

	/// <summary>
	/// Whether one fleet may fight another: two fleets of two countries at war, with
	/// ships, neither falling back.
	/// </summary>
	public static bool MayEngage(CGameState state, CUnit unit, CUnit other)
	{
		return unit != other && unit.Owner != other.Owner
			&& unit.Kind == EUnitKind.Navy && other.Kind == EUnitKind.Navy
			&& !unit.IsRemoved && !other.IsRemoved
			&& !unit.IsRetreating && !other.IsRetreating
			&& unit.SubUnits.Count > 0 && other.SubUnits.Count > 0
			&& CMovementRules.IsEnemyIn(state, unit.Owner, other.Owner, unit.Location);
	}

	/// <summary>
	/// What a fleet does about the sea province it is in: it joins the battle there if
	/// one will have it, and otherwise looks for an enemy to start one with.
	/// </summary>
	public static void Look(CGameState state, CUnit fleet, CProvince province)
	{
		if (!province.IsSea || fleet.IsRemoved || fleet.SubUnits.Count == 0 || fleet.Location != province.Id)
		{
			return;
		}
		if (!CCombats.HasCombatIn(fleet, province.Id)
			&& CCombats.In(state, province.Id, ECombatKind.Naval) is { } there)
		{
			CCombats.Offer(state, there, fleet);
		}
		if (!CCombats.HasCombatIn(fleet, province.Id))
		{
			Start(state, province, fleet);
		}
	}

	/// <summary>
	/// Starts a battle, if the fleet sees the first enemy fleet in the province. Only
	/// that one is tried: a failed sighting of it ends the look.
	/// </summary>
	public static CCombat? Start(CGameState state, CProvince province, CUnit fleet)
	{
		if (CCombats.HasCombatIn(fleet, province.Id))
		{
			return null;
		}
		CUnit? found = null;
		foreach (CUnit other in state.UnitsIn(province.Id))
		{
			// The first that may be fought and whose order lets it be made the defender.
			if (MayEngage(state, fleet, other) && CNavalSpotting.DefenderAccepts(other, fleet))
			{
				found = other;
				break;
			}
		}
		if (found is null || !CNavalSpotting.ShouldStart(state, fleet, found)
			|| CCombats.In(state, province.Id, ECombatKind.Naval) is not null)
		{
			return null;
		}

		var combat = new CCombat(province.Id, ECombatKind.Naval);
		state.Combats.Add(combat);
		Diplomacy.CThreat.BattleStarted(state, province, fleet, Diplomacy.CThreat.NavalCombat);
		Join(state, combat, combat.Attacker, fleet);
		Join(state, combat, combat.Defender, found);
		foreach (CUnit other in new List<CUnit>(state.UnitsIn(province.Id)))
		{
			if (other != fleet && other != found)
			{
				CCombats.Offer(state, combat, other);
			}
		}
		return combat;
	}

	/// <summary>
	/// Takes a fleet onto a side: the side's positioning is worked out again, the fleet
	/// has no count towards breaking away, and each of its ships is given its distance.
	/// </summary>
	public static void Join(CGameState state, CCombat combat, CCombatSide side, CUnit fleet)
	{
		foreach (CSubUnit ship in fleet.SubUnits)
		{
			ship.TargetId = 0;
		}
		side.Add(fleet);
		fleet.Disengage = 0;
		fleet.DisengageTo = 0;
		UpdatePositioning(state, side);
		// A battle that has had a round is one a fleet joins late.
		DrawUp(state, side, fleet, late: combat.Hour != CCombat.NotStarted);
	}

	/// <summary>
	/// Puts a fleet's ships at their distances. A capital ship stands at half its reach
	/// or some way further out; the fleet's first sets a line, and every other ship is
	/// put before that line or behind it at even odds.
	/// </summary>
	public static void DrawUp(CGameState state, CCombatSide side, CUnit fleet, bool late)
	{
		long further = 0;
		long line = 0;
		if (late)
		{
			further = LateDistance;
			foreach (CUnit unit in side.Units)
			{
				foreach (CSubUnit ship in unit.SubUnits)
				{
					if (CShipCensus.IsCapital(ship))
					{
						line = ship.Distance.Raw;
						break;
					}
				}
				if (line > 0)
				{
					break;
				}
			}
		}

		long skill = Math.Max(0, (SpreadWithoutLeader - CCombatLeaders.UnitSkill(fleet)) * CFixedPoint.Scale);
		long share = Math.Max(Spread - CCommandReach.Effect(state, fleet, ETraitEffect.SpreadOut), LeastSpreadShare);
		foreach (CSubUnit ship in fleet.SubUnits)
		{
			long half = Reach(ship) * CFixedPoint.Scale / 2000;
			long spread = share * half / CFixedPoint.Scale;
			spread = spread * skill / CFixedPoint.Scale;
			spread = (CFixedPoint.Scale - ship.Stats[Positioning].Raw) * spread / CFixedPoint.Scale;
			if (spread < LeastSpread)
			{
				spread = LeastSpread;
			}
			long distance = half + state.Random.Next((int)(spread / CFixedPoint.Scale)) * (long)CFixedPoint.Scale;
			if (CShipCensus.IsCapital(ship))
			{
				if (line == 0)
				{
					line = distance;
				}
			}
			else
			{
				if (state.Random.Next(100) < 50)
				{
					distance = -distance;
				}
				distance += line;
			}
			ship.Distance = new CFixedPoint(Math.Max(distance + further, NearestDistance));
		}
	}

	/// <summary>A ship's reach in still weather: its file's <c>distance</c>, a hundred times over.</summary>
	public static long Reach(CSubUnit ship)
	{
		return ship.Stats[FiringDistance].Raw * 100;
	}

	/// <summary>The first hour a battle is fought: each side's positioning roll, the attacker's first.</summary>
	public static void Begin(CGameState state, CCombat combat)
	{
		combat.HasBegun = true;
		state.RaiseCombatBegan(combat);
		Politics.CStrategicWarfareImpacts.SeaBattleBegun(state, combat);
		combat.Attacker.PositioningRoll = new CFixedPoint(state.Random.Next(20) * 10L);
		combat.Defender.PositioningRoll = new CFixedPoint(state.Random.Next(20) * 10L);
		UpdatePositioning(state, combat.Attacker);
		UpdatePositioning(state, combat.Defender);
	}

	/// <summary>A side's positioning from its roll, its highest ranked leader's skill and its penalty.</summary>
	public static void UpdatePositioning(CGameState state, CCombatSide side)
	{
		long positioning = side.PositioningRoll.Raw + Base(side) - StackingPenalty(state, side);
		side.Positioning = new CFixedPoint(Math.Max(0, positioning));
	}

	/// <summary>A side's positioning before its roll and its penalty: a tenth more for each point of its leader's skill.</summary>
	public static long Base(CCombatSide side)
	{
		return BasePositioning + CCombatLeaders.Skill(side) * PositioningPerSkill;
	}

	/// <summary>
	/// What a crowded side pays in positioning, and what widens the dice thrown at it:
	/// so much for each hull over the limit, up to a most.
	/// </summary>
	public static long StackingPenalty(CGameState state, CCombatSide side)
	{
		return StackingPenalty(state, side.Units);
	}

	/// <summary>The same for any fleets: the reorganise window asks it of one (<c>reorganise.md</c>, section 2).</summary>
	public static long StackingPenalty(CGameState state, IEnumerable<CUnit> fleets)
	{
		long hull = 0;
		foreach (CUnit unit in fleets)
		{
			foreach (CSubUnit ship in unit.SubUnits)
			{
				hull += ship.Stats[Hull].Raw;
			}
		}
		CDefines defines = state.Defines;
		long over = Math.Max(0, hull - defines[CManpower.MilitarySection, HullLimit].Raw);
		long penalty = over * defines[CManpower.MilitarySection, HullPenalty].Raw / CFixedPoint.Scale;
		return Math.Min(penalty, defines[CManpower.MilitarySection, MostPenalty].Raw);
	}

	/// <summary>
	/// One hour of a battle at sea, attacker first at every step: what the hour makes of
	/// each ship, the shooting and the moving, the settling, from the fourth round
	/// whether a side has lost, and then the roll to break off. Answers whether it is over.
	/// </summary>
	public static bool RunHour(CGameState state, CCombat combat)
	{
		combat.Duration++;
		if (combat.Hour == CCombat.NotStarted)
		{
			RollDice(state, combat);
			combat.Hour = 0;
		}
		RollDice(state, combat);

		CNavalModifiers.Apply(state, combat, combat.Attacker);
		CNavalModifiers.Apply(state, combat, combat.Defender);
		CNavalFire.Attack(state, combat, combat.Attacker);
		CNavalFire.Attack(state, combat, combat.Defender);
		CNavalLosses.Settle(state, combat.Attacker);
		CNavalLosses.Settle(state, combat.Defender);
		CNavalLosses.RemoveSunk(state, combat.Attacker);
		CNavalLosses.RemoveSunk(state, combat.Defender);

		// Nobody is asked whether they have lost a raid on a convoy.
		if (combat.Duration >= CCombats.LeastHours && !combat.IsConvoyBattle)
		{
			if (HasLost(combat.Attacker))
			{
				CCombats.Conclude(state, combat, attackerLost: true);
				RetreatAll(state, combat.Attacker);
				return true;
			}
			if (HasLost(combat.Defender))
			{
				CCombats.Conclude(state, combat, attackerLost: false);
				RetreatAll(state, combat.Defender);
				return true;
			}
		}
		combat.Hour++;
		return CNavalDisengage.BreakOff(state, combat);
	}

	private static void RollDice(CGameState state, CCombat combat)
	{
		combat.Attacker.Dice = state.Random.Next(10);
		combat.Defender.Dice = state.Random.Next(10);
	}

	/// <summary>
	/// Whether a side has lost: next to no strength left in all, or its ships average
	/// under one organisation - each fleet's own average, weighted by its ships.
	/// </summary>
	public static bool HasLost(CCombatSide side)
	{
		long strength = 0;
		long organisation = 0;
		long ships = 0;
		foreach (CUnit unit in side.Units)
		{
			strength += CUnitTotals.Strength(unit).Raw;
			organisation += unit.SubUnits.Count * CUnitTotals.AverageOrganisation(unit).Raw;
			ships += unit.SubUnits.Count;
		}
		if (strength < LeastStrength)
		{
			return true;
		}
		long average = ships == 0 ? 0 : organisation * CFixedPoint.Scale / (ships * CFixedPoint.Scale);
		return average < CUnitTotals.LeastOrganisationToFight.Raw;
	}

	private static void RetreatAll(CGameState state, CCombatSide side)
	{
		foreach (CUnit unit in new List<CUnit>(side.Units))
		{
			CNavalRetreat.Retreat(state, unit, side);
		}
	}
}
