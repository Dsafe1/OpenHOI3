using OpenHOI3.Content.Gui;
using OpenHOI3.Interface;

namespace OpenHOI3.Tests;

/// <summary>Where an element goes, against <c>docs/mechanics/interface.md</c>, section 3.</summary>
public class GuiLayoutTests
{
	private static CGuiIconType At(int x, int y, EGuiOrientation orientation)
	{
		return new CGuiIconType { Name = "a", Position = new CGuiPoint(x, y), Orientation = orientation };
	}

	[Theory]
	[InlineData(EGuiOrientation.UpperLeft, 0f, 0f)]
	[InlineData(EGuiOrientation.UpperRight, 1f, 0f)]
	[InlineData(EGuiOrientation.LowerLeft, 0f, 1f)]
	[InlineData(EGuiOrientation.LowerRight, 1f, 1f)]
	[InlineData(EGuiOrientation.Center, 0.5f, 0.5f)]
	[InlineData(EGuiOrientation.CenterUp, 0.5f, 0f)]
	[InlineData(EGuiOrientation.CenterDown, 0.5f, 1f)]
	public void AnOrientationNamesAPointOfTheParent(EGuiOrientation orientation, float x, float y)
	{
		Assert.Equal((x, y), CGuiLayout.Anchor(orientation));
	}

	[Fact]
	public void ThePositionIsMeasuredFromThatPoint()
	{
		// A button 25 in from the right edge stays there on a wider screen.
		Assert.Equal((1895f, 58f), CGuiLayout.UpperLeft(At(-25, 58, EGuiOrientation.UpperRight), 1920, 1080));
		Assert.Equal((2535f, 58f), CGuiLayout.UpperLeft(At(-25, 58, EGuiOrientation.UpperRight), 2560, 1080));

		// A window of 1120 by 768 put at -555, -330 from the centre sits about the middle.
		Assert.Equal((405f, 210f), CGuiLayout.UpperLeft(At(-555, -330, EGuiOrientation.Center), 1920, 1080));
	}

	[Fact]
	public void AFrameIsAFractionOfTheTextureSoItsResolutionDoesNotMatter()
	{
		// Six frames: the first starts at the left edge and each is a sixth wide.
		Assert.Equal((0f, 1f / 6f), CGuiLayout.FrameRegion(1, 6));
		Assert.Equal((5f / 6f, 1f / 6f), CGuiLayout.FrameRegion(6, 6));
		Assert.Equal((0.5f, 0.5f), CGuiLayout.FrameRegion(2, 2));
	}

	[Fact]
	public void CentredTextIsCentredInTheWholeBoxFromItsBorder()
	{
		// A technology's difficulty is a box 24 wide with a border of 4. Its figure, a "1"
		// that is 6 wide with 4 of ink, starts 13 in: the border, half the box, less half
		// the line.
		float start = CGuiLayout.TextStart(EGuiTextFormat.Centre, EGuiOrientation.UpperLeft, border: 4, boxWidth: 24,
			textWidth: 6);
		Assert.Equal(13f, start);

		// With the shift every glyph is drawn at, that is where the original's own screen
		// has it: the box is at 811 and the ink of the "1" runs from 825, which is in the
		// middle of the circle painted from 819 to 835.
		Assert.Equal(825f, 811 + start + CGuiLayout.GlyphShiftX);

		// And the year beside it, "1938", 32 wide in a box of 60 at 823: its ink starts
		// one in from the pen, at 843.
		float year = CGuiLayout.TextStart(EGuiTextFormat.Centre, EGuiOrientation.UpperLeft, border: 4, boxWidth: 60,
			textWidth: 32);
		Assert.Equal(843f, 823 + year + CGuiLayout.GlyphShiftX + 1);
	}

	[Fact]
	public void TheHalvesAreCutToWholeNumbersBeforeTheyAreTakenApart()
	{
		// A line 31 wide in a box of 60: thirty less fifteen, not fourteen and a half.
		Assert.Equal(15f, CGuiLayout.TextStart(EGuiTextFormat.Centre, EGuiOrientation.UpperLeft, 0, 60, 31f));
	}

	[Fact]
	public void TextSetLeftStartsAtTheBorderAndSetRightEndsAtTheBoxsEdge()
	{
		Assert.Equal(4f, CGuiLayout.TextStart(EGuiTextFormat.Left, EGuiOrientation.UpperLeft, 4, 100, 30f));
		Assert.Equal(70f, CGuiLayout.TextStart(EGuiTextFormat.Right, EGuiOrientation.UpperLeft, 4, 100, 30f));
	}

	[Fact]
	public void ABoxMeasuredFromTheUpperRightHasTheBorderAddedOnceMore()
	{
		Assert.Equal(8f, CGuiLayout.TextStart(EGuiTextFormat.Left, EGuiOrientation.UpperRight, 4, 100, 30f));
		Assert.Equal(74f, CGuiLayout.TextStart(EGuiTextFormat.Right, EGuiOrientation.UpperRight, 4, 100, 30f));
		Assert.Equal(43f, CGuiLayout.TextStart(EGuiTextFormat.Centre, EGuiOrientation.UpperRight, 4, 100, 30f));
	}

	[Fact]
	public void AFrameNumberOffTheSheetGetsTheNearestThatIsOnIt()
	{
		Assert.Equal(CGuiLayout.FrameRegion(1, 6), CGuiLayout.FrameRegion(0, 6));
		Assert.Equal(CGuiLayout.FrameRegion(6, 6), CGuiLayout.FrameRegion(9, 6));
		Assert.Equal((0f, 1f), CGuiLayout.FrameRegion(3, 0));
	}
}
