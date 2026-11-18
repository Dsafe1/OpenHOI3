using OpenHOI3.Interface.Text;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// Where an edit box's line and caret stand, against <c>docs/mechanics/interface.md</c>, section 8, and the two screenshots
/// of the original it was measured on.
/// </summary>
public class EditLinePlacementTests
{
	[Fact]
	public void TheLineStartsTheBorderInTwice()
	{
		// game_name and unitname, both a border of 8 across: the original's capitals start 17 in, the drawing's own unit
		// right on top of 16.
		Assert.Equal(16f, CEditLinePlacement.Left(8));
		Assert.Equal(20f, CEditLinePlacement.Left(10));
	}

	[Fact]
	public void WithAPictureTheLineIsCentredInTheBoxsHeight()
	{
		// game_name: 260 by 32, a border of 6 down, Arial12_bold's line 14 - 6 + (32 - 14) / 2 = 15, so its capitals,
		// 3 down the line and drawn a unit up, stand 17 below the box's top, as on a screenshot of the original.
		Assert.Equal(15f, CEditLinePlacement.Top(260, 32, 6, 14f, true));
	}

	[Fact]
	public void TheCentringIsCutTowardsNought()
	{
		// The battle plans' text_editbox, 32 high with a border of 4, in a font of line 15: 4 + 17 / 2 = 4 + 8.
		Assert.Equal(12f, CEditLinePlacement.Top(200, 32, 4, 15f, true));
	}

	[Fact]
	public void WithNoPictureTheLineIsTheBorderInTwiceAndAUnitLower()
	{
		// unitname: 235 by 24, a border of 4 down and no picture - 4 + 4, and one more against the drawing's unit up, so
		// its figures stand 11 below the box's top, as on a screenshot of the original's unit panel.
		Assert.Equal(9f, CEditLinePlacement.Top(235, 24, 4, 14f, false));
	}

	[Fact]
	public void AFontEntrysLineIsTheBorderInTwiceDownAndNeverCentred()
	{
		// The console's console_edit: 500 by 40, a border of 10 and a picture, in FPS_Font, a font = { } entry of height
		// 14. The bitmap font's routine would centre it, 10 + (40 - 14) / 2 = 23; the entry's own sets it at the
		// rectangle's top, the border into an object the border in.
		Assert.Equal(20f, CEditLinePlacement.TrueTypeTop(10));
		Assert.Equal(23f, CEditLinePlacement.Top(500, 40, 10, 14f, true));
		Assert.Equal(20f, CEditLinePlacement.Left(10));
	}

	[Fact]
	public void TheCaretStandsAUnitHigherWithAPicture()
	{
		Assert.Equal(5f, CEditLinePlacement.CaretTop(6, 14f, 0, true));
		Assert.Equal(19f, CEditLinePlacement.CaretTop(6, 14f, 1, true));
		Assert.Equal(4f, CEditLinePlacement.CaretTop(4, 14f, 0, false));
	}
}
