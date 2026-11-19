using OpenHOI3.Sim.Agents;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A held-back division's test for company, against <c>docs/mechanics/moving.md</c>, section 7.5: it stays with none of the
/// country's idle divisions beside it or fewer than the enemy's units halved in whole numbers.
/// </summary>
public class ReservePlacingTests
{
	[Theory]
	[InlineData(0, 1, true)]
	[InlineData(1, 1, false)]
	[InlineData(1, 3, false)]
	[InlineData(1, 4, true)]
	[InlineData(2, 4, false)]
	[InlineData(2, 5, false)]
	[InlineData(2, 6, true)]
	public void A_held_back_division_stays_only_below_half_the_enemys_units_in_whole_numbers(int idle, int enemies, bool stays)
	{
		Assert.Equal(stays, CReservePlacing.IsOutnumbered(idle, enemies));
	}
}
