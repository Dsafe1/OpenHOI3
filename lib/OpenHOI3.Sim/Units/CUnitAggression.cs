namespace OpenHOI3.Sim.Units;

/// <summary>
/// How aggressive a unit is, in thousandths from -1000 to 1000: the first unit from it upward with an active plan by its stance, or,
/// with none, the first with an aggression of its own, or else nought. A battle's tactic is picked by it, and the panel's slider shows
/// it. See <c>docs/mechanics/combatview.md</c>.
/// </summary>
public static class CUnitAggression
{
	private const long Half = 500;

	public static long Of(CUnit? unit)
	{
		for (CUnit? above = unit; above is not null; above = above.Parent)
		{
			if (above.Plan.Active && above.Plan.Stance < 5)
			{
				return above.Plan.Stance switch
				{
					CUnitPlan.Withdraw => -CFixedPoint.Scale,
					CUnitPlan.Prepare => -Half,
					CUnitPlan.Attacking => Half,
					CUnitPlan.Blitzing => CFixedPoint.Scale,
					_ => 0,
				};
			}
		}
		for (CUnit? above = unit; above is not null; above = above.Parent)
		{
			if (above.HasOwnAggression)
			{
				return above.Aggression.Raw;
			}
		}
		return 0;
	}
}
