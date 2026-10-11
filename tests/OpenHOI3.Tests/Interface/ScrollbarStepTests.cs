using OpenHOI3.Interface.Screens;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// A scrollbar's end buttons, against <c>docs/mechanics/interface.md</c>, section 2: what a
/// press moves, what Ctrl makes of it, and how holding repeats.
/// </summary>
public class ScrollbarStepTests
{
	[Fact]
	public void A_step_of_a_thousandth_counts_as_ten_and_any_other_as_itself()
	{
		// The sliders' files give 0.001, the lend-lease slider's 0.01.
		Assert.Equal(10, CScrollbarSteps.PressStep(1));
		Assert.Equal(10, CScrollbarSteps.PressStep(10));
		Assert.Equal(1000, CScrollbarSteps.PressStep(1000));
	}

	[Theory]
	[InlineData(false, false, 10)]
	[InlineData(false, true, 100)]
	[InlineData(true, false, 1000)]
	[InlineData(true, true, 10000)]
	public void Shift_makes_a_step_ten_times_as_long_and_ctrl_a_hundred(bool ctrl, bool shift, long step)
	{
		Assert.Equal(step, CScrollbarSteps.Step(10, ctrl, shift));
	}

	[Theory]
	[InlineData(0.0, 0)]
	[InlineData(0.15, 0)]
	[InlineData(0.16, 1)]
	[InlineData(0.31, 2)]
	[InlineData(1.0, 6)]
	[InlineData(3.0, 19)]
	public void Holding_presses_again_each_frame_by_the_whole_time_held(double held, int repeats)
	{
		Assert.Equal(repeats, CScrollbarSteps.Repeats(held));
	}
}
