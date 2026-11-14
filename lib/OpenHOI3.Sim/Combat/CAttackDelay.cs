using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// The delay an attack leaves behind. A land unit that attacks is given a week or so
/// before it may set off or attack again; an hour of fighting takes a quarter of an
/// hour off it; and once the battle is over a short attack has its delay cut to match,
/// after which it runs down an hour an hour. See <c>docs/mechanics/landcombat.md</c>,
/// section 9.
/// </summary>
public static class CAttackDelay
{
	public const string Delay = "UNIT_ATTACK_DELAY";
	public const string DelayPeriod = "UNIT_ATTACK_DELAY_PERIOD";
	public const string DelayModify = "UNIT_ATTACK_DELAY_MODIFY";

	/// <summary>What a dropped unit's delay starts from in place of <see cref="Delay"/> (<c>transport.md</c>, section 13.4).</summary>
	public const string ParadropDelay = "PARATROOP_MISSION_DELAY";

	/// <summary>What each level of a doctrine takes off the delay, in hours.</summary>
	public const string TechnologyAttackDelay = "attack_delay";

	/// <summary>The country modifier that makes a tactic's speed count for more.</summary>
	public const string CombatMovementSpeed = "combat_movement_speed";

	/// <summary>The technologies' figure that does the same, the technology status's own field.</summary>
	public const string TechnologyAttackMovementSpeed = "attack_movement_speed";

	/// <summary>
	/// How much quicker a unit attacks: one, the country's combat movement speed, its technologies' attack movement
	/// speed and its commanders' combat_move_speed trait - which the attack delay's hour and the walk in a battle are both
	/// multiplied by (<c>reversing/findings/FINDINGS-landbattle.md</c>; <c>FINDINGS-movement.md</c>, section 2).
	/// </summary>
	public static long Quicker(CGameState state, CCountry country, CUnit unit)
	{
		return CFixedPoint.Scale + country.Modifier[CombatMovementSpeed].Raw + country.Technology[TechnologyAttackMovementSpeed].Raw
			+ Leaders.CCommandReach.Effect(state, unit, Leaders.ETraitEffect.CombatMoveSpeed);
	}

	/// <summary>How many thousandths of an hour an hour of attacking takes off the delay.</summary>
	private const long PerHourFought = 250;

	/// <summary>The hours a country with no officers at all adds to every delay.</summary>
	private const long HoursForNoOfficers = 72;

	private static readonly CFixedPoint Least = CFixedPoint.FromInt(12);

	/// <summary>
	/// Whether a unit counts as attacking this hour: on land, in a battle, with
	/// somewhere to go and at least a step towards it.
	/// </summary>
	public static bool IsAttacking(CUnit unit)
	{
		return CMovementRules.IsLandUnit(unit) && unit.IsFighting && unit.IsMoving
			&& unit.MovementProgress >= new CFixedPoint(1);
	}

	/// <summary>A land unit's hour as far as its delay goes. It comes before the unit moves.</summary>
	public static void RunHour(CGameState state, CCountry country, CUnit unit)
	{
		if (!IsAttacking(unit))
		{
			unit.AttackDelay = CFixedPoint.Max(unit.AttackDelay - CFixedPoint.One, CFixedPoint.Zero);
			if (!unit.IsFighting && unit.AttackDelay > CFixedPoint.Zero && unit.HoursAttacking > 0)
			{
				long period = state.Defines[CManpower.MilitarySection, DelayPeriod].Raw / CFixedPoint.Scale;
				if (unit.HoursAttacking <= period && period > 0)
				{
					unit.AttackDelay = new CFixedPoint(unit.AttackDelay.Raw * unit.HoursAttacking / period);
				}
				unit.AttackDelay = CFixedPoint.Max(unit.AttackDelay, CFixedPoint.One);
				unit.HoursAttacking = 0;
			}
			if (unit.AttackDelay < new CFixedPoint(1))
			{
				unit.HoursAttacking = 0;
			}
			return;
		}

		unit.HoursAttacking++;
		if (unit.AttackDelay == CFixedPoint.Zero)
		{
			Start(state, country, unit);
		}
		if (unit.AttackDelay > CFixedPoint.One)
		{
			long speed = 0;
			foreach (CCombat combat in unit.Combats)
			{
				if (combat.Attacker.Units.Contains(unit))
				{
					speed = combat.TacticSpeed.Raw * 2;
					break;
				}
			}
			long quicker = Quicker(state, country, unit);
			long off = PerHourFought + quicker * speed / CFixedPoint.Scale;
			unit.AttackDelay = CFixedPoint.Max(new CFixedPoint(unit.AttackDelay.Raw - off), CFixedPoint.One);
		}
	}

	/// <summary>
	/// The delay an attack starts: the base - a dropped unit's own - a roll of some hours, more for a country
	/// short of officers, less for its doctrine, and never under twelve hours. One draw
	/// of the game's generator.
	/// </summary>
	public static void Start(CGameState state, CCountry country, CUnit unit)
	{
		country = state.Country(unit.FiguresOwner) ?? country;
		long hours = state.Defines[CManpower.MilitarySection, unit.WasParadropped ? ParadropDelay : Delay].Raw / CFixedPoint.Scale;
		long modify = state.Defines[CManpower.MilitarySection, DelayModify].Raw / CFixedPoint.Scale;
		hours += state.Random.Next((int)modify);

		long delay = hours * CFixedPoint.Scale;
		delay += (CFixedPoint.Scale - country.OfficerRatio.Raw) * (HoursForNoOfficers * CFixedPoint.Scale) / CFixedPoint.Scale;
		delay -= country.Technology[TechnologyAttackDelay].Raw / CFixedPoint.Scale * CFixedPoint.Scale;
		unit.AttackDelay = CFixedPoint.Max(new CFixedPoint(delay), Least);
	}
}
