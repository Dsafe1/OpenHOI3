using System.Collections.Generic;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// A condition out of a tactic's <c>trigger</c> block: one test of the side that is
/// picking, or a group of them. It is the part of the script's condition language that
/// belongs to a battle. See <c>docs/mechanics/landcombat.md</c>, section 11.
/// </summary>
public sealed class CTacticCondition
{
	/// <summary>The flag of a unit type that makes its brigades armour.</summary>
	public const string ArmourFlag = "is_armor";

	public CTacticCondition(ETacticCondition kind, bool wanted = true, int amount = 0, string name = "")
	{
		Kind = kind;
		Wanted = wanted;
		Amount = amount;
		Name = name;
	}

	public ETacticCondition Kind { get; }

	/// <summary>What a yes-or-no test was written with: <c>is_attacker = no</c> wants false.</summary>
	public bool Wanted { get; }

	/// <summary>What a test of skill asks for, in whole levels.</summary>
	public int Amount { get; }

	/// <summary>The trait asked about.</summary>
	public string Name { get; }

	/// <summary>What a group holds.</summary>
	public List<CTacticCondition> Children { get; } = new();

	public bool Holds(CGameState state, CCombat combat, CCombatSide side)
	{
		switch (Kind)
		{
			case ETacticCondition.IsAttacker:
				return side.IsAttacker == Wanted;
			case ETacticCondition.FrontageFull:
				return (CFrontLine.Room(state, combat, side) < 0) == Wanted;
			case ETacticCondition.Reserves:
				return (side.Reserves.Count > 0) == Wanted;
			case ETacticCondition.SkillAdvantage:
				return CCombatTactics.LeaderSkill(side) - CCombatTactics.LeaderSkill(combat.Other(side)) >= Amount;
			case ETacticCondition.Skill:
				return CCombatTactics.LeaderSkill(side) >= Amount;
			case ETacticCondition.HasArmourUnit:
				return HasArmourOnFront(side) == Wanted;
			case ETacticCondition.Trait:
				return CCombatLeaders.HasTrait(side, Name);
			case ETacticCondition.Never:
				return false;
			case ETacticCondition.Any:
				foreach (CTacticCondition child in Children)
				{
					if (child.Holds(state, combat, side))
					{
						return true;
					}
				}
				return false;
			case ETacticCondition.None:
				foreach (CTacticCondition child in Children)
				{
					if (child.Holds(state, combat, side))
					{
						return false;
					}
				}
				return true;
			default:
				foreach (CTacticCondition child in Children)
				{
					if (!child.Holds(state, combat, side))
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
}
