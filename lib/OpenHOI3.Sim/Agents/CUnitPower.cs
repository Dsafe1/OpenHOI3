using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// What a unit is worth to the AI: over its own brigades, ships or wings, each one's value by its strength and its
/// organisation, all in thousandths. The original's <c>CUnit::GetPower</c>. See <c>docs/mechanics/agents.md</c>, section
/// 7.3.
/// </summary>
public static class CUnitPower
{
	/// <summary>The key of <c>"soft_attack"</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey SoftAttackKey = CModifierKey.Of("soft_attack");

	/// <summary>The key of <c>"hard_attack"</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey HardAttackKey = CModifierKey.Of("hard_attack");

	/// <summary>The key of <c>"ap_attack"</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey ApAttackKey = CModifierKey.Of("ap_attack");

	/// <summary>The key of <c>"defensiveness"</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey DefensivenessKey = CModifierKey.Of("defensiveness");

	/// <summary>The key of <c>CSubUnitStats.Toughness</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey ToughnessKey = CModifierKey.Of(CSubUnitStats.Toughness);

	/// <summary>The key of <c>"armor_value"</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey ArmorValueKey = CModifierKey.Of("armor_value");

	/// <summary>The key of <c>"softness"</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey SoftnessKey = CModifierKey.Of("softness");

	/// <summary>The key of <c>"air_attack"</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey AirAttackKey = CModifierKey.Of("air_attack");

	/// <summary>The key of <c>"air_defence"</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey AirDefenceKey = CModifierKey.Of("air_defence");

	/// <summary>The key of <c>"sea_attack"</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey SeaAttackKey = CModifierKey.Of("sea_attack");

	/// <summary>The key of <c>"sea_defence"</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey SeaDefenceKey = CModifierKey.Of("sea_defence");

	/// <summary>The key of <c>"hull"</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey HullKey = CModifierKey.Of("hull");

	/// <summary>What an air wing's and a ship's value start from, in thousandths.</summary>
	private const long Base = 500;

	private const long Whole = 1000;

	public static long Of(CUnit unit)
	{
		long power = 0;
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			power += brigade.Strength.Raw / 100 * Value(brigade) / Whole * (brigade.Organisation.Raw / 100) / Whole;
		}
		return power;
	}

	/// <summary>
	/// A land brigade: its three attacks, a tenth of its defensiveness and toughness and its armour, by two less its
	/// softness; an air wing: its air attack and defence and 500; a ship: 500, its sea attack and defence, by its hull.
	/// </summary>
	public static long Value(CSubUnit brigade)
	{
		CSubUnitStats stats = brigade.Stats;
		switch (brigade.Type.Branch)
		{
			case EUnitBranch.Land:
				long attack = stats[SoftAttackKey].Raw + stats[HardAttackKey].Raw + stats[ApAttackKey].Raw;
				long defence = (stats[DefensivenessKey].Raw + stats[ToughnessKey].Raw) / 10;
				return (attack + defence + stats[ArmorValueKey].Raw) * (2 * Whole - stats[SoftnessKey].Raw) / Whole;
			case EUnitBranch.Air:
				return stats[AirAttackKey].Raw + stats[AirDefenceKey].Raw + Base;
			default:
				return (Base + stats[SeaAttackKey].Raw + stats[SeaDefenceKey].Raw) * stats[HullKey].Raw / Whole;
		}
	}
}
