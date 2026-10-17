using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// What a unit is worth to the AI: over its own brigades, ships or wings, each one's value by its strength and its
/// organisation, all in thousandths. The original's <c>CUnit::GetPower</c>. See <c>docs/mechanics/agents.md</c>, section
/// 7.3.
/// </summary>
public static class CUnitPower
{
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
				long attack = stats["soft_attack"].Raw + stats["hard_attack"].Raw + stats["ap_attack"].Raw;
				long defence = (stats["defensiveness"].Raw + stats[CSubUnitStats.Toughness].Raw) / 10;
				return (attack + defence + stats["armor_value"].Raw) * (2 * Whole - stats["softness"].Raw) / Whole;
			case EUnitBranch.Air:
				return stats["air_attack"].Raw + stats["air_defence"].Raw + Base;
			default:
				return (Base + stats["sea_attack"].Raw + stats["sea_defence"].Raw) * stats["hull"].Raw / Whole;
		}
	}
}
