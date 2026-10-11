using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Leaders;

/// <summary>
/// Who commands what: a leader's skill as his experience gives it, his putting in
/// service, which units he may command, and the choice a country makes of a leader for
/// a unit that has none. See <c>docs/mechanics/leaders.md</c>.
/// </summary>
public static class CLeaders
{
	public const string NewLeaderOrgHit = "NEW_LEADER_ORG_HIT";

	private static readonly string[] NavalRanks = { "NAVAL_RANK_1", "NAVAL_RANK_2", "NAVAL_RANK_3", "NAVAL_RANK_4" };
	private static readonly string[] AirRanks = { "AIR_RANK_1", "AIR_RANK_2", "AIR_RANK_3", "AIR_RANK_4" };

	/// <summary>
	/// The experience each skill starts at, in whole points: skill 1 at 1000, skill 10 at
	/// 60000. The last is out of reach, so nobody learns his way to 11.
	/// </summary>
	public static readonly long[] SkillFloors = { 0, 1000, 2000, 4000, 8000, 12000, 18000, 25000, 33000, 43000, 60000, 100000000 };

	/// <summary>The skill nothing raises a leader past.</summary>
	public const int MostSkill = 12;

	/// <summary>What a leader's skill counts for in the choice of him, before his traits.</summary>
	private const long SkillWeight = 1000;

	/// <summary>An admiral or an air marshal given more than his rank commands counts for a hundredth.</summary>
	private const long OverCommandDivisor = 100;

	/// <summary>The combined arms a unit must have for its leader's attack to count, from a float of 50.5.</summary>
	private static readonly CFixedPoint MixedArmsForAttack = new(50);

	private static readonly CFixedPoint SlowForDefence = new(3000);
	private static readonly CFixedPoint FastForAttack = new(4000);

	/// <summary>The floor of a skill's experience in the original's 15 binary places.</summary>
	public static CFixed15 Floor(int skill)
	{
		return CFixed15.FromInt(SkillFloors[Math.Clamp(skill, 0, SkillFloors.Length - 1)]);
	}

	/// <summary>
	/// What a leader's skill and experience are once he is read: his experience raised to
	/// the floor of the skill his file gives, or to his file's experience where that left
	/// it at nothing; then his skill raised while his experience is above the next
	/// floor. Neither is ever lowered, so the file's skill is the least he has.
	/// </summary>
	public static void SettleSkill(CLeader leader)
	{
		int skill = leader.Skill;
		CFixed15 floor = Floor(skill);
		if (leader.Experience < floor)
		{
			leader.Experience = floor;
		}
		if (skill >= MostSkill)
		{
			return;
		}
		if (leader.Experience == CFixed15.Zero)
		{
			leader.Experience = CFixed15.FromThousandths(new CFixedPoint(leader.StartingExperience));
		}
		while (skill < MostSkill && skill + 1 < SkillFloors.Length && leader.Experience > Floor(skill + 1))
		{
			skill++;
		}
		leader.Skill = Math.Max(skill, 0);
	}

	/// <summary>
	/// A leader gains experience from a battle: the amount times one and a fifth of the
	/// square of how far his skill is below his most - a third of that once he is at it
	/// or past it - added to his experience; and where that is now above his next floor,
	/// a level of skill. One level at a time, and none at all from 12.
	/// <c>docs/mechanics/leaders.md</c>, section 11.
	/// </summary>
	public static void AddExperience(CLeader leader, CFixed15 amount)
	{
		if (leader.Skill >= MostSkill)
		{
			return;
		}
		long below = leader.MaxSkill - leader.Skill;
		long factor = (below * below << CFixed15.Places) / 5 + CFixed15.Scale;
		if (leader.Skill >= leader.MaxSkill)
		{
			factor = (factor << CFixed15.Places) / (3 * CFixed15.Scale);
		}
		// Every rank's factor is one.
		leader.Experience = new CFixed15(leader.Experience.Raw + ((factor * amount.Raw) >> CFixed15.Places));
		if (leader.Experience > Floor(leader.Skill + 1))
		{
			leader.Skill++;
		}
	}

	/// <summary>
	/// What a game being made does with leaders once the units are read, before anything
	/// is chosen: puts in service every leader of every country who has a rank, in the
	/// order the country keeps them, and puts on each unit the leader its file named, of
	/// whatever country, rank or kind. <see cref="AssignAtStart"/> gives the rest theirs,
	/// after a country's starting stockpile is worked out - which so sees only the
	/// leaders the files named.
	/// </summary>
	public static void SetAtStart(CGameState state)
	{
		foreach (CLeader leader in state.Leaders.Values)
		{
			leader.Unit = null;
		}
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				unit.Leader = null;
			}
		}
		foreach (CCountry country in state.Countries)
		{
			country.ActiveLeaders.Clear();
			foreach (CLeader leader in country.Leaders)
			{
				if (leader.Rank != 0)
				{
					country.ActiveLeaders.Add(leader);
				}
			}
		}
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.UnitsInOrder())
			{
				if (unit.LeaderId != 0 && state.Leaders.TryGetValue(unit.LeaderId, out CLeader? named))
				{
					SetLeader(state, unit, named, false, false);
				}
			}
		}
	}

	/// <summary>
	/// The rest of a game being made, a country at a time: every unit with no leader is
	/// given the best of its country's free leaders, in the order the units were made.
	/// Nothing else is put to a leader then.
	/// </summary>
	public static void AssignAtStart(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.UnitsInOrder())
			{
				if (unit.Leader is null)
				{
					AssignBest(state, country, unit);
				}
			}
		}
	}

	/// <summary>
	/// The day's putting in service: a leader whose history first gives him a rank today,
	/// and who is not in service already, is put in service at rank 1, whatever rank the
	/// history gives. Nothing later in his history is ever applied.
	/// </summary>
	public static void ActivateDaily(CGameState state)
	{
		if (state.Hour != 0)
		{
			return;
		}
		foreach (CCountry country in state.Countries)
		{
			foreach (CLeader leader in country.Leaders)
			{
				if (leader.FirstRankDate == state.Date && !country.ActiveLeaders.Contains(leader))
				{
					leader.Rank = 1;
					country.ActiveLeaders.Add(leader);
				}
			}
		}
	}

	/// <summary>
	/// Whether a leader may take a unit: he is in service, he is of its kind, and - when
	/// <paramref name="onlyFree"/> - he commands nothing else. A land unit asks for one
	/// rank exactly: a division 1, and only one of more than one brigade; a corps 2; an
	/// army 3; an army group or a theatre 4. A fleet or an air unit asks for none.
	/// </summary>
	public static bool MayCommand(CLeader leader, CUnit unit, bool onlyFree)
	{
		if (leader.Rank == 0 || leader.Type != TypeFor(unit))
		{
			return false;
		}
		if (onlyFree && leader.Unit is { } other && other != unit)
		{
			return false;
		}
		return unit.Kind switch
		{
			EUnitKind.Division => leader.Rank == 1 && unit.SubUnits.Count > 1,
			EUnitKind.Corps => leader.Rank == 2,
			EUnitKind.Army => leader.Rank == 3,
			EUnitKind.ArmyGroup or EUnitKind.Theatre => leader.Rank == 4,
			_ => true,
		};
	}

	/// <summary>
	/// Whether a unit's leader has the rank for it - which is looser than what choosing
	/// him asks: a theatre or an army group wants at least 3, an army 2, a corps 1, and a
	/// division nothing; a fleet or an air unit, no more ships or wings than his rank
	/// commands. A unit with none has not. What the panel writes his name in red for,
	/// and what a battle asks before his skill counts.
	/// </summary>
	public static bool HasRankFor(CGameState state, CUnit unit)
	{
		if (unit.Leader is not { } leader)
		{
			return false;
		}
		return unit.Kind switch
		{
			EUnitKind.Theatre or EUnitKind.ArmyGroup => leader.Rank > 2,
			EUnitKind.Army => leader.Rank > 1,
			EUnitKind.Corps => leader.Rank > 0,
			EUnitKind.Division => true,
			_ => CommandSize(state, leader, unit) >= unit.SubUnits.Count,
		};
	}

	/// <summary>The kind of leader a unit takes.</summary>
	public static ELeaderType TypeFor(CUnit unit)
	{
		return unit.Kind switch
		{
			EUnitKind.Navy => ELeaderType.Sea,
			EUnitKind.Air => ELeaderType.Air,
			_ => ELeaderType.Land,
		};
	}

	/// <summary>
	/// How many ships or wings an admiral or an air marshal of his rank commands, out of
	/// <c>NAVAL_RANK_1</c> to <c>4</c> and <c>AIR_RANK_1</c> to <c>4</c> - a rank that is
	/// none of 2, 3 or 4 counting as 1. Nothing for a land unit.
	/// </summary>
	public static int CommandSize(CGameState state, CLeader leader, CUnit unit)
	{
		string[]? keys = unit.Kind switch
		{
			EUnitKind.Navy => NavalRanks,
			EUnitKind.Air => AirRanks,
			_ => null,
		};
		if (keys is null)
		{
			return 0;
		}
		int index = leader.Rank is >= 2 and <= 4 ? leader.Rank - 1 : 0;
		return (int)(state.Defines[CManpower.MilitarySection, keys[index]].Raw / CFixedPoint.Scale);
	}

	/// <summary>
	/// What a leader is worth to a unit when one is chosen for it: his skill and one more,
	/// in thousands - a hundredth of that where he is an admiral or an air marshal given
	/// more than his rank commands - and for each of his traits' effects that suits the
	/// unit, a set amount, whatever the effect's own figure.
	/// </summary>
	public static long Score(CGameState state, CLeader leader, CUnit unit)
	{
		long score = (leader.Skill + 1) * SkillWeight;
		if (TypeFor(unit) != ELeaderType.Land && CommandSize(state, leader, unit) < unit.SubUnits.Count)
		{
			score = score * CFixedPoint.Scale / (OverCommandDivisor * CFixedPoint.Scale);
		}
		foreach (CTrait trait in leader.Traits)
		{
			foreach (CTraitEffect effect in trait.Effects)
			{
				score += Suits(state, unit, effect.Kind);
			}
		}
		return score;
	}

	/// <summary>What one effect of a leader's adds to his worth to a unit.</summary>
	private static long Suits(CGameState state, CUnit unit, ETraitEffect kind)
	{
		switch (kind)
		{
			case ETraitEffect.DefenceModifier:
				return CMovementSpeed.SlowestBrigade(unit) < SlowForDefence ? 10000 : 0;
			case ETraitEffect.OffenceModifier:
			case ETraitEffect.CombatMoveSpeed:
				return CMovementSpeed.SlowestBrigade(unit) > FastForAttack
					|| BaseCombinedArms(state, unit) > MixedArmsForAttack ? 10000 : 0;
			case ETraitEffect.RiverAttack:
				return CUnitTotals.Adjuster(unit, RiverAdjuster, AttackKind) > 0 ? 20000 : 0;
			case ETraitEffect.FortAttack:
				return CUnitTotals.Adjuster(unit, FortAdjuster, AttackKind) > 0 ? 20000 : 0;
			case ETraitEffect.CombinedArmsBonus:
				return BaseCombinedArms(state, unit) > CFixedPoint.Zero ? 50000 : 0;
			case ETraitEffect.SubmarineAttack:
				return unit.SubUnits.Count > 0 && unit.SubUnits[0].Type.Flag(SubmarineFlag) ? 20000 : 0;
			case ETraitEffect.DefenderSoftness:
				return CUnitTotals.Sum(unit, CUnitTotals.SoftAttack) > CUnitTotals.Sum(unit, CUnitTotals.HardAttack) ? 5000 : 0;
			case ETraitEffect.StrategicAttack:
				return CUnitTotals.Sum(unit, StrategicAttack) > CUnitTotals.Sum(unit, AirAttack) ? 10000 : 0;
			case ETraitEffect.NavalAttack:
				return CUnitTotals.Sum(unit, SeaAttack) > CUnitTotals.Sum(unit, AirAttack) ? 10000 : 0;
			case ETraitEffect.TacticalAttack:
				return CUnitTotals.Sum(unit, CUnitTotals.SoftAttack) + CUnitTotals.Sum(unit, CUnitTotals.HardAttack)
					> CUnitTotals.Sum(unit, AirAttack) ? 10000 : 0;
			case ETraitEffect.AmphAttack:
				return CUnitTotals.Adjuster(unit, AmphibiousAdjuster, AttackKind) > 0 ? 20000 : 0;
			case ETraitEffect.ParadropMission:
				// The original asks the figures its unit is made of, which never carry the
				// flag a brigade's type has: this trait counts for nothing.
				return 0;
			default:
				return 0;
		}
	}

	private const string RiverAdjuster = "river";
	private const string FortAdjuster = "fort";
	private const string AmphibiousAdjuster = "amphibious";
	private const string AttackKind = "attack";
	private const string SubmarineFlag = "is_sub";
	private const string StrategicAttack = "strategic_attack";
	private const string AirAttack = "air_attack";
	private const string SeaAttack = "sea_attack";

	/// <summary>What the unit's mix of brigades is worth before its country's technology adds to it.</summary>
	private static CFixedPoint BaseCombinedArms(CGameState state, CUnit unit)
	{
		var types = new List<CSubUnitType>();
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (CUnits.CountsTowardsItsUnit(unit, subUnit))
			{
				types.Add(subUnit.Type);
			}
		}
		return CCombinedArms.BaseBonus(state, types);
	}

	/// <summary>
	/// Gives a unit the best of its country's leaders in service who command nothing and
	/// may command it, by <see cref="Score"/>; of two who are worth the same, the one the
	/// country put in service first. A unit nobody may command keeps none.
	/// </summary>
	public static void AssignBest(CGameState state, CCountry country, CUnit unit)
	{
		CLeader? best = null;
		long bestScore = 0;
		foreach (CLeader leader in country.ActiveLeaders)
		{
			if (leader.Unit is not null || !MayCommand(leader, unit, true))
			{
				continue;
			}
			long score = Score(state, leader, unit);
			if (score > bestScore)
			{
				bestScore = score;
				best = leader;
			}
		}
		if (best is not null)
		{
			SetLeader(state, unit, best, false, false);
		}
	}

	/// <summary>
	/// Puts a leader on a unit, or takes its leader off with null. Whatever either
	/// commanded before is left with nobody. Two things come only with a leader and only
	/// when asked for, as the original's two flags give them: <paramref name="resetTraitHours"/>
	/// throws away his hours of battle banked towards a trait, and <paramref name="organisationHit"/>
	/// leaves every brigade of the unit the share of its organisation
	/// <c>NEW_LEADER_ORG_HIT</c> gives. The choosing passes neither; a player's change of
	/// leader passes both (<c>docs/mechanics/leaders.md</c>, section 5).
	/// </summary>
	public static void SetLeader(CGameState state, CUnit unit, CLeader? leader, bool resetTraitHours, bool organisationHit)
	{
		if (leader == unit.Leader)
		{
			return;
		}
		if (unit.Leader is { } old)
		{
			if (old.Unit is { } oldUnit && oldUnit != unit)
			{
				oldUnit.Leader = null;
			}
			old.Unit = null;
		}
		if (leader is not null)
		{
			if (leader.Unit is { } previous && previous != unit)
			{
				previous.Leader = null;
			}
			leader.Unit = unit;
			if (resetTraitHours)
			{
				leader.TraitProgress.Duration = 0;
				leader.TraitProgress.CurrentHour = -1;
			}
			if (organisationHit)
			{
				long hit = state.Defines[CManpower.MilitarySection, NewLeaderOrgHit].Raw;
				foreach (CSubUnit subUnit in unit.SubUnits)
				{
					long left = subUnit.Organisation.Raw * hit / CFixedPoint.Scale;
					subUnit.Organisation = new CFixedPoint(Math.Max(left, 0));
				}
			}
		}
		unit.Leader = leader;
	}
}
