using System.Collections.Generic;
using OpenHOI3.Content.Gui;
using OpenHOI3.Interface.Text;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The box a button's caption is laid out in, and what the textBoxType rules then make of a long caption on a sprite,
/// against <c>docs/mechanics/interface.md</c>, section 6. Every character is ten wide, the line 14 high.
/// </summary>
public class ButtonCaptionTests
{
	private static float Measure(string text) => text.Length * 10f;

	[Fact]
	public void ASpriteButtonsCaptionIsLaidOutInItsPictureLessTheBorderOnceAndCut()
	{
		Assert.Equal((120, 24, true), CButtonCaption.SpriteBox(120, 24, 1, 0, 0));
		Assert.Equal((112, 20, true), CButtonCaption.SpriteBox(120, 24, 1, 8, 4));
	}

	[Fact]
	public void ASpritesPictureOfSeveralFramesGivesAQuarterOfItsWidthUncut()
	{
		// A quarter, whatever the number of frames: three frames of 40 lay out in 30.
		Assert.Equal((30, 24, false), CButtonCaption.SpriteBox(120, 24, 3, 8, 4));
	}

	[Fact]
	public void AButtonWithNoSpriteHasItsSizeAndTwoHundredWhereItGivesNone()
	{
		Assert.Equal(430, CButtonCaption.TextWidth(430));
		Assert.Equal(200, CButtonCaption.TextWidth(0));
	}

	[Fact]
	public void ALongCaptionOnASpriteIsCutToTheLinesItsPictureHoldsAndCentred()
	{
		// A picture of 120 by 24 holds one line of 14: a caption of two lines' width is cut to one, ending " ...".
		(int width, int height, bool cut) = CButtonCaption.SpriteBox(120, 24, 1, 0, 0);
		Assert.True(cut);
		string text = CFixedSizeCut.Cut("Declare war on them", width, height, 0, 14f, Measure);
		Assert.EndsWith(CFixedSizeCut.Mark, text);
		List<string> lines = CTextBoxLineBreak.Break(text, width, 0, Measure);
		Assert.Single(lines);
		// Centred with no border: half the picture less half the line.
		float lineWidth = Measure(lines[0]);
		Assert.Equal(60 - (int)(lineWidth / 2f), CTextBoxPlacement.LineStart(EGuiTextFormat.Centre, true, width, 0, lineWidth));
		// And the line stands in the middle of the height, cut to a whole number: (24 - 14) / 2.
		Assert.Equal(5f, CTextBoxPlacement.Top(true, true, height, 0, 1, 14f));
	}
}
