using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// Who a ship shoots at this hour. Its side's positioning decides whether it keeps the
/// target it had, whether it finds one at all, and - failing that - whether it takes a
/// friend for the enemy. The rolls are made in the original's order, which is what a
/// battle's course hangs on. See <c>docs/mechanics/navalcombat.md</c>, section 4.
/// </summary>
public static class CNavalTargets
{
	public const string Priority = "priority";
	public const string SubAttack = "sub_attack";
	public const string FiringRangeModifier = "FIRINGRANGEMODIFIER";

	/// <summary>The technology figures that scale a ship's positioning when it looks for a target, and that help it pick the best.</summary>
	public const string TargetingChance = "targeting_chance";
	public const string TargetingChoice = "targeting_choice";

	/// <summary>What is added to a hundredth of a ship's damage before its priority weighs it.</summary>
	private const long BestTargetBase = 250;

	/// <summary>The chances in a hundred that a ship which has lost the enemy shoots at a friend, less its side's leader's skill.</summary>
	private const int FriendlyFire = 7;

	/// <summary>
	/// The ship's target for this hour, or null. What it picks it remembers for the next.
	/// </summary>
	public static CSubUnit? Pick(CGameState state, CCombat combat, CCombatSide side, CCombatSide other, CSubUnit ship)
	{
		state.CombatTrace?.Invoke($"PICK ship={ship.Id} draws={state.Random.Draws}");
		CCountry? owner = ship.Unit is { } fleet0 ? state.Country(fleet0.FiguresOwner) : null;
		long placed = (ship.Stats[CNavalBattle.Positioning].Raw + side.Positioning.Raw) * 100000 / CFixedPoint.Scale;
		placed = placed * (CFixedPoint.Scale + (owner?.Technology[TargetingChance].Raw ?? 0)) / CFixedPoint.Scale;
		int p = (int)(placed / CFixedPoint.Scale);
		bool isNight = CDaylight.IsNight(state, state.Province(combat.Province));
		int pNight = isNight ? p / 2 : p;

		CSubUnit? target = Remembered(side, other, ship);
		// A raid's convoy fleet is made for the raid and never registered, so a ship of it is never found again: whoever
		// shot at one picks afresh every round (FINDINGS-liveread.md, section 119).
		if (target?.Unit is { } held && combat.IsConvoyBattle && combat.Defender.Units.Contains(held))
		{
			target = null;
		}
		bool keep = false;
		if (target is not null)
		{
			if (state.Random.Next(50) > p + 10)
			{
				target = null;
			}
			if (isNight && state.Random.Next(100) > 80)
			{
				target = null;
			}
			else if (target is not null)
			{
				if (target.Unit is { } fleet && fleet.Owner != ship.Unit?.Owner)
				{
					// Another country's ship is held while its fleet is still across the way.
					keep = other.Units.Contains(fleet);
				}
				else
				{
					keep = state.Random.Next(100) >= 75;
				}
				if (!keep)
				{
					target = null;
				}
			}
		}

		if (!keep)
		{
			target = Fresh(state, side, other, ship, pNight);
		}
		ship.TargetId = target?.Id ?? 0;
		return target;
	}

	/// <summary>The ship it shot at before, wherever on either side that ship now is.</summary>
	private static CSubUnit? Remembered(CCombatSide side, CCombatSide other, CSubUnit ship)
	{
		if (ship.TargetId == 0)
		{
			return null;
		}
		foreach (CCombatSide where in new[] { other, side })
		{
			foreach (CUnit unit in where.Units)
			{
				foreach (CSubUnit candidate in unit.SubUnits)
				{
					if (candidate.Id == ship.TargetId)
					{
						return candidate;
					}
				}
			}
		}
		return null;
	}

	/// <summary>
	/// Which ship of its own side a ship takes for the enemy, by a roll over the side's ships: the walk passes the shooter
	/// over and counts the roll down. The original's does not stop at the ship it drew - once the count is spent every ship
	/// after overwrites the pick, so it ends on the last ship of the side's last fleet - and a roll past every ship is no
	/// target. An enhanced game's fix <c>friendly_fire_target</c> stops at the ship drawn.
	/// </summary>
	public static CSubUnit? Friend(CCombatSide side, CSubUnit ship, int countdown, bool stopsAtItsRoll)
	{
		CSubUnit? friend = null;
		foreach (CUnit unit in side.Units)
		{
			foreach (CSubUnit candidate in unit.SubUnits)
			{
				if (candidate != ship)
				{
					if (countdown < 1)
					{
						if (stopsAtItsRoll)
						{
							return candidate;
						}
						friend = candidate;
					}
					countdown--;
				}
			}
		}
		return friend;
	}

	/// <summary>A target for a ship that has none: an enemy in reach, nothing, or a friend.</summary>
	private static CSubUnit? Fresh(CGameState state, CCombatSide side, CCombatSide other, CSubUnit ship, int pNight)
	{
		if (state.Random.Next(80) > pNight)
		{
			// It has lost the enemy; a side's leader makes it less likely to take a friend for him.
			if (state.Random.Next(100) >= FriendlyFire - CCombatLeaders.Skill(side))
			{
				return null;
			}
			int ships = 0;
			foreach (CUnit unit in side.Units)
			{
				ships += unit.SubUnits.Count;
			}
			int countdown = state.Random.Next(ships);
			return Friend(side, ship, countdown, state.Rules.Has(Rules.EFix.FriendlyFireTarget));
		}

		Collect(state, other, side, ship);
		int roll = state.Random.Next(100);
		if (side.Targets.Count < 1)
		{
			return null;
		}
		return roll > TargetingSkill(state, ship) / 10 ? side.Targets[state.Random.Next(side.Targets.Count)] : Best(state, side);
	}

	/// <summary>
	/// A ship's skill at picking its target: ten for each point of its fleet's own admiral's
	/// skill, whatever his rank, and its country's <c>targeting_choice</c> in thousandths.
	/// A tenth of it is the chance in a hundred of the best target.
	/// </summary>
	public static long TargetingSkill(CGameState state, CSubUnit ship)
	{
		if (ship.Unit is not { } fleet)
		{
			return 0;
		}
		long skill = CCombatLeaders.UnitSkill(fleet) * CAirFire.SkillTargeting;
		return state.Country(fleet.FiguresOwner) is { } owner ? skill + owner.Technology[TargetingChoice].Raw : skill;
	}

	/// <summary>
	/// Fills a side's list with the ships of the other side that one of its ships can
	/// reach: near enough, not already sunk on this hour's notes, and - for a submarine -
	/// seen, by a ship that can attack one.
	/// </summary>
	public static void Collect(CGameState state, CCombatSide looked, CCombatSide side, CSubUnit ship)
	{
		side.Targets.Clear();
		long reach = ReachNow(state, ship);
		foreach (CUnit unit in looked.Units)
		{
			foreach (CSubUnit candidate in unit.SubUnits)
			{
				if (candidate.Distance.Raw + ship.Distance.Raw > reach)
				{
					continue;
				}
				bool isSubmarine = CShipCensus.IsSubmarine(candidate);
				if (isSubmarine && candidate.TargetId == 0)
				{
					long seen = ship.Stats[CNavalSpotting.SubDetection].Raw * 5000 / CFixedPoint.Scale;
					if (seen / CFixedPoint.Scale < state.Random.Next(100))
					{
						continue;
					}
				}
				if (candidate.Strength.Raw - candidate.PendingStrengthDamage >= 0
					&& (!isSubmarine || ship.Stats[SubAttack].Raw != 0)
					&& side.Targets.Count < 1024)
				{
					side.Targets.Add(candidate);
				}
			}
		}
	}

	/// <summary>How far a ship reaches in the weather of the province its fleet is in.</summary>
	public static long ReachNow(CGameState state, CSubUnit ship)
	{
		long reach = CNavalBattle.Reach(ship);
		if (ship.Unit is { } fleet)
		{
			CProvince province = state.Province(fleet.Location);
			long rain = province.Weather.Precipitation.Raw
				* state.Defines[CWeatherEffects.WeatherSection, FiringRangeModifier].Raw / CFixedPoint.Scale;
			reach = (rain + CFixedPoint.Scale) * reach / CFixedPoint.Scale;
		}
		return reach;
	}

	/// <summary>
	/// The most valuable of the ships a side has in its list: by the unit file's
	/// priority, weighed up by the damage the ship has taken; the first of equals.
	/// </summary>
	public static CSubUnit? Best(CGameState state, CCombatSide side)
	{
		CSubUnit? best = null;
		long most = 0;
		foreach (CSubUnit candidate in side.Targets)
		{
			CCountry? owner = candidate.Unit is { } fleet ? state.Country(fleet.FiguresOwner) : null;
			long full = owner is null
				? candidate.Stats[CSubUnitType.MaxStrength].Raw
				: CUnits.MaxStrength(state, owner, candidate, false, false).Raw;
			long damage = 10 * (full - candidate.Strength.Raw) / CFixedPoint.Scale;
			long worth = candidate.Stats[Priority].Raw * (damage + BestTargetBase) / CFixedPoint.Scale;
			if (worth > most)
			{
				most = worth;
				best = candidate;
			}
		}
		return best;
	}
}
