using OpenHOI3.Interface.Tooltips;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// Where the tooltip box goes, against <c>docs/mechanics/tooltips.md</c>, sections 5, 7
/// and 8.
/// </summary>
public class TooltipPlacementTests
{
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
	public void AProvincesTooltipStandsAboveTheMouseAndToItsLeft()
	{
		CTooltip placed = CTooltipPlacement.AtMouse(new CTooltip { Text = "a" }, 900, 500);

		Assert.Equal(850, placed.X);
		Assert.Equal(480, placed.Y);
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
	}
}
