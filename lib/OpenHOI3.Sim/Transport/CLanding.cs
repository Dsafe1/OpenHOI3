using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Transport;

/// <summary>
/// A land unit landing on a coast from the sea: the leg's distance and speed, the progress, and what the progress makes
/// of its attack and its defence in a battle. See <c>docs/mechanics/transport.md</c>, sections 8 and 9.
/// </summary>
public static class CLanding
{
	public const string SpeedBase = "AMPHIBIOUS_INVADE_SPEED_BASE";
	public const string MovementCost = "AMPHIBIOUS_INVADE_MOVEMENT_COST";
	public const string AttackLow = "AMPHIBIOUS_INVADE_ATTACK_LOW";
	public const string AttackHigh = "AMPHIBIOUS_INVADE_ATTACK_HIGH";
	public const string DefendLow = "AMPHIBIOUS_INVADE_DEFEND_LOW";
	public const string DefendHigh = "AMPHIBIOUS_INVADE_DEFEND_HIGH";
	public const string PenaltyDecrease = "AMPHIBIOUS_INVADE_LANDING_PENALTY_DECREASE";

	public const string InvasionSpeed = "amphibious_invasion_speed";
	public const string InvasionDefence = "amphibious_invasion_defence";

	private const long Scale = CFixedPoint.Scale;

	/// <summary>A land unit in the sea whose next province is land: on its leg ashore, aboard a fleet or not.</summary>
	public static bool IsLanding(CGameState state, CUnit unit)
	{
		return CMovementRules.IsLandUnit(unit) && unit.IsMoving && state.Province(unit.Location).IsSea
			&& !state.Province(unit.Path[0]).IsSea;
	}

	/// <summary>The same, onto ground an enemy of its owner holds - the landing a battle's scales are set for.</summary>
	public static bool IsLandingOnEnemy(CGameState state, CUnit unit)
	{
		return IsLanding(state, unit)
			&& CMovementRules.IsEnemy(state, state.Province(unit.Path[0]).Controller, unit.Owner);
	}

	/// <summary>
	/// Whether another land unit of the country that can fight has a path ending in a province - landing or not. A
	/// unit that cannot fight waits on its landing for as long as one has.
	/// </summary>
	public static bool OtherFighterBoundFor(CGameState state, CCountry country, CUnit unit, int province)
	{
		foreach (CUnit other in country.AllUnits())
		{
			if (other != unit && !other.IsRemoved && CMovementRules.IsLandUnit(other) && other.IsMoving
				&& other.Path[^1] == province && CUnitTotals.CanFight(other))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The leg's distance: <c>AMPHIBIOUS_INVADE_MOVEMENT_COST</c>, whoever's coast it is.</summary>
	public static CFixedPoint Distance(CGameState state)
	{
		return state.Defines[CManpower.MilitarySection, MovementCost];
	}

	/// <summary>
	/// The leg's speed: <c>AMPHIBIOUS_INVADE_SPEED_BASE</c> and the carrying fleet's invasion speed, and nothing else.
	/// </summary>
	public static CFixedPoint Speed(CGameState state, CUnit unit)
	{
		long speed = state.Defines[CManpower.MilitarySection, SpeedBase].Raw;
		if (unit.Transport is { } fleet)
		{
			speed += FleetSpeed(fleet);
		}
		return new CFixedPoint(speed);
	}

	/// <summary>A fleet's invasion speed: the mean over the ships that have one, so escorts do not lower it.</summary>
	public static long FleetSpeed(CUnit fleet)
	{
		long sum = 0;
		long count = 0;
		foreach (CSubUnit ship in fleet.SubUnits)
		{
			long speed = ship.Stats[InvasionSpeed].Raw;
			if (speed != 0)
			{
				sum += speed;
				count++;
			}
		}
		return count == 0 ? 0 : sum / count;
	}

	/// <summary>A fleet's invasion defence: the plain sum over its ships.</summary>
	public static long FleetDefence(CUnit fleet)
	{
		long sum = 0;
		foreach (CSubUnit ship in fleet.SubUnits)
		{
			sum += ship.Stats[InvasionDefence].Raw;
		}
		return sum;
	}

	/// <summary>
	/// How far along its landing a unit is, in thousandths: its progress over the leg's distance, cut. Nought with no
	/// path, -1 at a distance of nought.
	/// </summary>
	public static long Progress(CGameState state, CUnit unit)
	{
		if (!unit.IsMoving)
		{
			return 0;
		}
		long distance = CMovement.Distance(state, unit).Raw;
		return distance == 0 ? -1 : unit.MovementProgress.Raw * Scale / distance;
	}

	/// <summary>
	/// Sets a landing unit's two scales for this hour of its battle, by its progress, each product cut toward nought:
	/// the attack from <c>ATTACK_LOW</c> to <c>ATTACK_HIGH</c>, the defence from <c>DEFEND_LOW</c> and the carrying
	/// fleet's invasion defence to <c>DEFEND_HIGH</c>. Asked of attacker and defender alike; a unit that is not landing
	/// on an enemy is left at one.
	/// </summary>
	public static void SetScales(CGameState state, CUnit unit)
	{
		unit.LandingAttack = CFixedPoint.One;
		unit.LandingDefence = CFixedPoint.One;
		if (!IsLandingOnEnemy(state, unit))
		{
			return;
		}
		long progress = Progress(state, unit);
		long attackLow = state.Defines[CManpower.MilitarySection, AttackLow].Raw;
		long attackHigh = state.Defines[CManpower.MilitarySection, AttackHigh].Raw;
		long low = state.Defines[CManpower.MilitarySection, DefendLow].Raw
			+ (unit.Transport is { } fleet ? FleetDefence(fleet) : 0);
		long defendHigh = state.Defines[CManpower.MilitarySection, DefendHigh].Raw;
		unit.LandingAttack = new CFixedPoint(attackLow + (attackHigh - attackLow) * progress / Scale);
		unit.LandingDefence = new CFixedPoint(low + (defendHigh - low) * progress / Scale);
	}

	/// <summary>
	/// What a landing does to the amphibious penalty where it is aboard a fleet: the fleet's invasion defence, weighed by
	/// <see cref="FleetExtraDefence"/> for the unit's brigades and up to <c>AMPHIBIOUS_INVADE_LANDING_PENALTY_DECREASE</c>,
	/// cuts it - and with none it is a hundredth worse.
	/// </summary>
	public static long CutPenalty(CGameState state, CUnit unit, long penalty)
	{
		if (unit.Transport is not { } fleet || !IsLanding(state, unit))
		{
			return penalty;
		}
		long cap = state.Defines[CManpower.MilitarySection, PenaltyDecrease].Raw;
		// An enhanced game's fix amphibious_defence: the ships' own tables counted, and the penalty never made worse.
		bool mended = state.Rules.Has(Rules.EFix.AmphibiousDefence);
		// The landing unit's share of the fleet's invasion defence: one, and for each of its brigades what the fleet's table
		// gives that kind.
		IReadOnlyDictionary<string, CFixedPoint> extra = FleetExtraDefence(fleet, mended);
		long weight = Scale;
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			weight += extra.TryGetValue(brigade.Type.Key, out CFixedPoint more) ? more.Raw : 0;
		}
		long cut = cap == 0 ? -1 : System.Math.Min(weight * FleetDefence(fleet) / Scale, cap);
		long share = cap == 0 ? 0 : cut * Scale / cap;
		long factor = Scale + Worse - share;
		if (mended && factor > Scale)
		{
			factor = Scale;
		}
		return penalty * factor / Scale;
	}

	/// <summary>
	/// The fleet's table of <c>extra_amphibious_defence</c>, by kind of brigade, as the original asks it: of the one
	/// definition a fleet's ships are summed into, which is made with an empty table and to which the summing copies no
	/// ship's - so it is always empty, and an assault ship's table does nothing. A fault of the original, kept
	/// (<c>docs/mechanics/transport.md</c>, section 9; <c>docs/enhanced.md</c>).
	/// </summary>
	/// <param name="summed">
	/// An enhanced game's fix <c>amphibious_defence</c>: the fleet's table is its ships' own added up, kind by kind, as
	/// its invasion defence is its ships' added up.
	/// </param>
	public static IReadOnlyDictionary<string, CFixedPoint> FleetExtraDefence(CUnit fleet, bool summed = false)
	{
		if (!summed)
		{
			return NoExtraDefence;
		}
		var sum = new Dictionary<string, CFixedPoint>(System.StringComparer.OrdinalIgnoreCase);
		foreach (CSubUnit ship in fleet.SubUnits)
		{
			foreach ((string kind, CFixedPoint more) in ship.Type.ExtraAmphibiousDefence)
			{
				sum[kind] = sum.TryGetValue(kind, out CFixedPoint held) ? held + more : more;
			}
		}
		return sum;
	}

	private static readonly IReadOnlyDictionary<string, CFixedPoint> NoExtraDefence = new Dictionary<string, CFixedPoint>();

	/// <summary>The original's <c>floor(1010.5)</c>: a penalty with nothing to cut it is a hundredth worse.</summary>
	private const long Worse = 10;
}
