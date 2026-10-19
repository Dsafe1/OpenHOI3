using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// What each side of a battle is worth, and the attacker's share of the two: the original's
/// <c>CCombat::GetAttackerStrengthShare</c>, the figure a battle's window shows as its bar and the AI weighs a battle by.
/// See <c>docs/mechanics/combatview.md</c>, section 6, and <c>docs/mechanics/moving.md</c>, section 8.
/// </summary>
public static class CCombatShares
{
	private const long Scale = CFixedPoint.Scale;

	/// <summary>
	/// A side sub unit by sub unit of every unit on it, front or reserve. A brigade counts by its organisation by its
	/// strength, by both of what the hour's circumstances make of its unit, and by a tenth of its two attacks and its
	/// toughness - its defensiveness for a defender - where that is more than one. A ship and a wing count by their
	/// organisation by their strength and by their own circumstances, and by nothing of their type.
	/// </summary>
	public static long Worth(CCombatSide side)
	{
		long sum = 0;
		foreach (CUnit unit in side.Units)
		{
			long circumstances = unit.CombatAttack.Raw * unit.CombatDefence.Raw / Scale;
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				long body = subUnit.Organisation.Raw * subUnit.Strength.Raw / Scale;
				if (subUnit.Type.Branch != EUnitBranch.Land)
				{
					sum += body * (subUnit.CombatDefence.Raw * subUnit.CombatAttack.Raw / Scale) / Scale;
					continue;
				}
				long arms = (subUnit.Stats[CUnitTotals.SoftAttack].Raw + subUnit.Stats[CUnitTotals.HardAttack].Raw
					+ subUnit.Stats[side.IsAttacker ? CSubUnitStats.Toughness : CUnitTotals.Defensiveness].Raw)
					* Scale / (10 * Scale);
				long factor = arms > Scale ? circumstances * arms / Scale : circumstances;
				sum += body * factor / Scale;
			}
		}
		return sum;
	}

	/// <summary>The attacker's share of the two sides' worth, in thousandths. Neither side counts for less than one.</summary>
	public static long AttackerShare(CCombat combat)
	{
		long attacker = System.Math.Max(Worth(combat.Attacker), 1);
		long defender = System.Math.Max(Worth(combat.Defender), 1);
		return attacker * Scale / (attacker + defender);
	}
}
