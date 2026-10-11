using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// A condition out of a battle's script: a tactic's <c>trigger</c> block, or a gainable
/// trait's. One test of a side of a battle - and of a leader on it, where one is asked
/// about - or a group of them. It is the part of the script's condition language that
/// belongs to a battle. See <c>docs/mechanics/landcombat.md</c>, section 11, and
/// <c>docs/mechanics/leaders.md</c>, section 11.
/// </summary>
public sealed class CCombatCondition
{
	/// <summary>The flag of a unit type that makes its brigades armour.</summary>
	public const string ArmourFlag = "is_armor";

	public CCombatCondition(ECombatCondition kind, bool wanted = true, int amount = 0, string name = "")
	{
		Kind = kind;
		Wanted = wanted;
		Amount = amount;
		Name = name;
	}

	public ECombatCondition Kind { get; }

	/// <summary>What a yes-or-no test was written with: <c>is_attacker = no</c> wants false.</summary>
	public bool Wanted { get; }

	/// <summary>What a test of skill asks for, in whole levels; and a temperature, in whole degrees.</summary>
	public int Amount { get; }

	/// <summary>The trait, the terrain or the modifier asked about.</summary>
	public string Name { get; }

	/// <summary>What a group holds.</summary>
	public List<CCombatCondition> Children { get; } = new();

	/// <summary>
	/// Whether it holds for a side of a battle. <paramref name="leader"/> is the leader a
	/// gainable trait is asked for; a tactic asks with none.
	/// </summary>
	public bool Holds(CGameState state, CCombat combat, CCombatSide side, CLeader? leader = null)
	{
		switch (Kind)
		{
			case ECombatCondition.IsAttacker:
				return side.IsAttacker == Wanted;
			case ECombatCondition.FrontageFull:
				return (CFrontLine.Room(state, combat, side) < 0) == Wanted;
			case ECombatCondition.Reserves:
				return (side.Reserves.Count > 0) == Wanted;
			case ECombatCondition.SkillAdvantage:
				return CCombatTactics.LeaderSkill(side) - CCombatTactics.LeaderSkill(combat.Other(side)) >= Amount;
			case ECombatCondition.Skill:
				return CCombatTactics.LeaderSkill(side) >= Amount;
			case ECombatCondition.HasArmourUnit:
				return HasArmourOnFront(side) == Wanted;
			case ECombatCondition.Trait:
				return CCombatLeaders.HasTrait(side, Name);
			case ECombatCondition.Terrain:
				return string.Equals(state.Province(combat.Province).Terrain?.Name, Name, StringComparison.Ordinal);
			case ECombatCondition.ProvinceTemperature:
				return state.Province(combat.Province).Weather.Temperature.Raw < Amount * (long)CFixedPoint.Scale;
			case ECombatCondition.HasCombinedArms:
				return HasCombinedArms(state, combat, side, leader);
			case ECombatCondition.HasCombatModifier:
				return HadModifier(side, Name);
			case ECombatCondition.IsConvoy:
				return combat.IsConvoyBattle == Wanted;
			case ECombatCondition.IsWinner:
				return combat.HasEnded && !CTraitGain.HasLost(combat, side) == Wanted;
			case ECombatCondition.Never:
				return false;
			case ECombatCondition.Any:
				foreach (CCombatCondition child in Children)
				{
					if (child.Holds(state, combat, side, leader))
					{
						return true;
					}
				}
				return false;
			case ECombatCondition.None:
				foreach (CCombatCondition child in Children)
				{
					if (child.Holds(state, combat, side, leader))
					{
						return false;
					}
				}
				return true;
			default:
				foreach (CCombatCondition child in Children)
				{
					if (!child.Holds(state, combat, side, leader))
					{
						return false;
					}
				}
				return true;
		}
	}

	/// <summary>Whether a unit on the side's front - not one in its reserves - has an armoured brigade.</summary>
	private static bool HasArmourOnFront(CCombatSide side)
	{
		foreach (CUnit unit in side.Front)
		{
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				if (subUnit.Type.Flag(ArmourFlag))
				{
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>
	/// In a land battle, whether a unit of the side - of the leader asked about, where
	/// there is one - is made up for combined arms. What is written after the key is not read.
	/// </summary>
	private static bool HasCombinedArms(CGameState state, CCombat combat, CCombatSide side, CLeader? leader)
	{
		if (combat.Kind != ECombatKind.Land)
		{
			return false;
		}
		foreach (CUnit unit in side.Units)
		{
			if ((leader is null || unit.Leader == leader) && CCombatModifiers.HasCombinedArms(state, unit))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Whether the side had the modifier of that name in its last hour: on one of its
	/// units, or on a ship, a wing or a brigade of one.
	/// </summary>
	private static bool HadModifier(CCombatSide side, string key)
	{
		if (CCombatModifierKeys.Find(key) is not { } kind)
		{
			return false;
		}
		foreach (CUnit unit in side.Units)
		{
			if (Has(unit.CombatModifiers, kind))
			{
				return true;
			}
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				if (Has(subUnit.CombatModifiers, kind))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool Has(List<CCombatModifier> modifiers, ECombatModifier kind)
	{
		foreach (CCombatModifier modifier in modifiers)
		{
			if (modifier.Kind == kind)
			{
				return true;
			}
		}
		return false;
	}
}
