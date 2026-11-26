using OpenHOI3.Interface.Tooltips;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// Where the tooltip box goes, against <c>docs/mechanics/tooltips.md</c>, sections 5, 7
/// and 8.
/// </summary>
public class TooltipPlacementTests
{
	[Fact]
	public void A_text_that_fits_is_shown_whole_and_one_that_does_not_is_moved_through_three_lines_a_notch()
	{
		var scroll = new CTooltipScroll();
		Assert.Equal(CTooltipWindow.Whole(10), scroll.Window("short", 10, 10));
		Assert.False(scroll.Scrollable);

		// Forty lines in room for ten: nine and "more below".
		Assert.Equal(new CTooltipWindow(0, 9, 0, 31), scroll.Window("long", 40, 10));
		Assert.True(scroll.Scrollable);
		// A notch down: a line each for what is above and below, eight of the text.
		scroll.Wheel(1);
		Assert.Equal(new CTooltipWindow(3, 8, 3, 29), scroll.Window("long", 40, 10));
		// Far past the end stops where the last line shows: "more above" and nine lines.
		scroll.Wheel(40);
		Assert.Equal(new CTooltipWindow(31, 9, 31, 0), scroll.Window("long", 40, 10));
		// And back past the top.
		scroll.Wheel(-40);
		Assert.Equal(new CTooltipWindow(0, 9, 0, 31), scroll.Window("long", 40, 10));
	}

	[Fact]
	public void The_place_is_kept_while_the_text_is_the_same_and_starts_again_at_the_top_when_it_changes()
	{
		var scroll = new CTooltipScroll();
		scroll.Window("long", 40, 10);
		scroll.Wheel(2);
		Assert.Equal(6, scroll.Window("long", 40, 10).First);
		Assert.Equal(6, scroll.Window("long", 40, 10).First);
		// Another text, and the steps taken before it was shown, count for nothing.
		scroll.Wheel(1);
		Assert.Equal(0, scroll.Window("another", 40, 10).First);
		// A box with room for fewer than three lines is not scrolled at all.
		Assert.Equal(CTooltipWindow.Whole(40), scroll.Window("another", 40, 2));
	}

	[Fact]
	public void An_arrow_key_moves_a_line_at_its_press_and_another_each_130_ms_it_is_held_down_winning()
	{
		var scroll = new CTooltipScroll();
		scroll.Window("long", 40, 10);
		scroll.Keys(up: false, down: true, 0.016);
		Assert.Equal(1, scroll.Window("long", 40, 10).First);
		// Held 0.1 s more: nothing yet; 0.05 more, another line.
		scroll.Keys(false, true, 0.1);
		Assert.Equal(1, scroll.Window("long", 40, 10).First);
		scroll.Keys(false, true, 0.05);
		Assert.Equal(2, scroll.Window("long", 40, 10).First);
		// Let go and pressed again, at once; both held is down.
		scroll.Keys(false, false, 0.016);
		scroll.Keys(true, true, 0.016);
		Assert.Equal(3, scroll.Window("long", 40, 10).First);
		scroll.Keys(false, false, 0.016);
		scroll.Keys(true, false, 0.016);
		Assert.Equal(2, scroll.Window("long", 40, 10).First);
	}

	private const int Width = 1920;
	private const int Height = 1080;

	[Fact]
	public void AnElementsTooltipHangs64BelowItsCorner()
	{
		CTooltip placed = CTooltipPlacement.AtElement(new CTooltip { Text = "a" }, 420, 24, Height);

		Assert.Equal(420, placed.X);
		Assert.Equal(88, placed.Y);
		Assert.True(placed.Below);
	}

	[Fact]
	public void ItsOwnOffsetIsAddedToTheCorner()
	{
		CTooltip placed = CTooltipPlacement.AtElement(new CTooltip { X = 5, Y = -4 }, 100, 100, Height);

		Assert.Equal(105, placed.X);
		Assert.Equal(160, placed.Y);
	}

	[Fact]
	public void InTheLowestFifthItStandsOnTheCornerInstead()
	{
		// 0.8 of 1080 is 864; a corner at 865 is below it.
		CTooltip placed = CTooltipPlacement.AtElement(CTooltip.Empty, 10, 865, Height);

		Assert.Equal(865, placed.Y);
		Assert.False(placed.Below);
	}

	[Fact]
	public void AtTheFifthItselfItStillHangsBelow()
	{
		// 0.8 as a single-precision number is a hair over 0.8, so 864 is not past it.
		CTooltip placed = CTooltipPlacement.AtElement(CTooltip.Empty, 10, 864, Height);

		Assert.Equal(928, placed.Y);
		Assert.True(placed.Below);
	}

	[Fact]
	public void ATooltipThatStandsAboveStillMoves64Down()
	{
		// Only one that hangs below is turned round; one that stands above already is moved
		// down like any other, and then stands on that point.
		CTooltip placed = CTooltipPlacement.AtElement(new CTooltip { Below = false }, 10, 100, Height);

		Assert.Equal(164, placed.Y);
		Assert.False(placed.Below);
	}

	[Fact]
	public void AProvincesTooltipStandsAboveTheMinimapsCornerAndToItsLeft()
	{
		// The minimap's corner, wherever the mouse is: the base game's at 1557, 945 on a 1920 x 1080 canvas.
		CTooltip placed = CTooltipPlacement.AtMinimap(new CTooltip { Text = "a" }, 1557, 945);

		Assert.Equal(1507, placed.X);
		Assert.Equal(925, placed.Y);
		Assert.False(placed.Below);
	}

	[Fact]
	public void TheBoxHangsFromThePointOrStandsOnIt()
	{
		Assert.Equal((100, 200), CTooltipPlacement.Place(new CTooltip { X = 100, Y = 200 }, 300, 50, Width, Height));
		Assert.Equal((100, 150), CTooltipPlacement.Place(new CTooltip { X = 100, Y = 200, Below = false }, 300, 50, Width, Height));
	}

	[Fact]
	public void ItIsPulledBackInsideTheRightAndTheBottom()
	{
		Assert.Equal((1620, 1030), CTooltipPlacement.Place(new CTooltip { X = 1800, Y = 1060 }, 300, 50, Width, Height));
	}

	[Fact]
	public void NothingKeepsItOffTheLeftOrTheTop()
	{
		Assert.Equal((-40, -30), CTooltipPlacement.Place(new CTooltip { X = -40, Y = 20, Below = false }, 300, 50, Width, Height));
		// An enhanced game's fix keeps it on the screen at those two edges too, and leaves the other two as they were.
		Assert.Equal((0, 0), CTooltipPlacement.Place(new CTooltip { X = -40, Y = 20, Below = false }, 300, 50, Width, Height, onScreen: true));
		Assert.Equal((1620, 1030), CTooltipPlacement.Place(new CTooltip { X = 1800, Y = 1060 }, 300, 50, Width, Height, onScreen: true));
		Assert.Equal((100, 200), CTooltipPlacement.Place(new CTooltip { X = 100, Y = 200 }, 300, 50, Width, Height, onScreen: true));
	}
}
