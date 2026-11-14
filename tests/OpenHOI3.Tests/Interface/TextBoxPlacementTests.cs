using OpenHOI3.Content.Gui;
using OpenHOI3.Interface;
using OpenHOI3.Interface.Text;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// Where the original's font places a <c>textBoxType</c>'s lines and how much of them is shown, against
/// <c>docs/mechanics/interface.md</c>, section 6.
/// </summary>
public class TextBoxPlacementTests
{
	[Fact]
	public void ABoxFitsWithBothAWidthAndAHeightOrWhenOfFixedSize()
	{
		Assert.True(CTextBoxPlacement.Fits(240, 32, false));
		Assert.False(CTextBoxPlacement.Fits(315, 0, false));
		Assert.False(CTextBoxPlacement.Fits(0, 32, false));
		Assert.True(CTextBoxPlacement.Fits(350, 0, true));
		Assert.True(CTextBoxPlacement.Fits(0, 0, true));
	}

	[Fact]
	public void ACentredLineLeavesTheBorderOutWhereAnInstantBoxCountsIt()
	{
		// current_research_text: 240 wide, a border of 4, a line 31 wide - 120 - 15 = 105, where slot 8 has 109.
		Assert.Equal(105f, CTextBoxPlacement.LineStart(EGuiTextFormat.Centre, true, 240, 4, 31f));
		Assert.Equal(109f, CGuiLayout.TextStart(EGuiTextFormat.Centre, EGuiOrientation.UpperLeft, 4, 240, 31f));
	}

	[Fact]
	public void ALineSetRightEndsOneShortOfTheBorder()
	{
		// label_relation_value: 100 wide, a border of 4, a line 20 wide - 100 - 4 - 1 - 20.
		Assert.Equal(75f, CTextBoxPlacement.LineStart(EGuiTextFormat.Right, true, 100, 4, 20f));
		Assert.Equal(79f, CTextBoxPlacement.LineStart(EGuiTextFormat.Right, true, 100, 0, 20f));
	}

	[Fact]
	public void ALineSetLeftStartsAtTheBorder()
	{
		Assert.Equal(4f, CTextBoxPlacement.LineStart(EGuiTextFormat.Left, true, 100, 4, 20f));
	}

	[Fact]
	public void ABoxThatDoesNotFitStartsEveryLineAtTheBorderWhateverItsFormat()
	{
		// MustRestartTextBox: 315 wide, no height, a border of 6, centred.
		Assert.Equal(6f, CTextBoxPlacement.LineStart(EGuiTextFormat.Centre, false, 315, 6, 100f));
		Assert.Equal(6f, CTextBoxPlacement.LineStart(EGuiTextFormat.Right, false, 315, 6, 100f));
	}

	[Fact]
	public void ABoxOfFixedSizeAndNoWidthIsLaidOutAt320()
	{
		Assert.Equal(160f - 25f, CTextBoxPlacement.LineStart(EGuiTextFormat.Centre, true, 0, 4, 50f));
	}

	[Fact]
	public void EachHalfIsCutToAWholeNumber()
	{
		// 61 / 2 is 30 and 31 / 2 is 15.
		Assert.Equal(15f, CTextBoxPlacement.LineStart(EGuiTextFormat.Centre, true, 61, 0, 31f));
		Assert.Equal(16f, CTextBoxPlacement.LineStart(EGuiTextFormat.Centre, true, 61, 0, 28.5f));
	}

	[Fact]
	public void TheLinesStartAtTheBorderWithNoPictureBehindThem()
	{
		Assert.Equal(2f, CTextBoxPlacement.Top(true, false, 32, 2, 1, 14f));
		Assert.Equal(2f, CTextBoxPlacement.Top(false, true, 0, 2, 1, 14f));
	}

	[Fact]
	public void WithAPictureBehindThemTheLinesAreCentredUpAndDownInABoxThatFits()
	{
		// console_text: 16 high, a border of 1, one line of garamond_16's 16 - at the top, not the border's 1 down.
		Assert.Equal(0f, CTextBoxPlacement.Top(true, true, 16, 1, 1, 16f));
		// 22 high and a line of 14: 4 down, where the border would put it 1 down; a line of 13 leaves 9, and 4.
		Assert.Equal(4f, CTextBoxPlacement.Top(true, true, 22, 1, 1, 14f));
		Assert.Equal(4f, CTextBoxPlacement.Top(true, true, 22, 1, 1, 13f));
		// Lines taller than the box start above it, the half cut towards nought: (16 - 45) / 2 = -14.
		Assert.Equal(-14f, CTextBoxPlacement.Top(true, true, 16, 1, 3, 15f));
		// A box of fixed size and no height is as tall as its lines and border, which leaves the border.
		Assert.Equal(3f, CTextBoxPlacement.Top(true, true, 0, 3, 2, 14f));
	}

	[Fact]
	public void ABoxThatFitsShowsItsSizeLessTheBorderOnTheRightAndAtTheBottom()
	{
		Assert.Equal((236f, 30f), CTextBoxPlacement.Shown(true, 240, 32, 4, 2, 500f, 5, 14f));
		// Of fixed size and no height: as tall as the lines and the border above and below.
		Assert.Equal((346f, 2 * 14f + 8f), CTextBoxPlacement.Shown(true, 350, 0, 4, 4, 100f, 2, 14f));
	}

	[Fact]
	public void ABoxThatDoesNotFitShowsItsWidestLineAndItsLines()
	{
		Assert.Equal((112f, 3 * 14f + 12f), CTextBoxPlacement.Shown(false, 315, 0, 6, 6, 100f, 3, 14f));
	}

	[Fact]
	public void AFixedBoxThatGivesNoughtTakesItsPictureLessTheBorderOnce()
	{
		Assert.Equal((246, 26), CTextBoxPlacement.Size(0, 0, true, 10, 6, 256, 32));
		Assert.Equal((100, 26), CTextBoxPlacement.Size(100, 0, true, 10, 6, 256, 32));
		// Not of fixed size, or with no picture: as the file gives it.
		Assert.Equal((0, 0), CTextBoxPlacement.Size(0, 0, false, 10, 6, 256, 32));
		Assert.Equal((0, 0), CTextBoxPlacement.Size(0, 0, true, 10, 6, null, null));
	}
}
