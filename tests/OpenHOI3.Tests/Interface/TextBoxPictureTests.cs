using System.Collections.Generic;
using OpenHOI3.Interface.Text;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The picture behind a text box's text, cut in nine and drawn as the original's font draws it, against
/// <c>docs/mechanics/interface.md</c>, section 6.
/// </summary>
public class TextBoxPictureTests
{
	[Fact]
	public void ThePictureIsWhatIsShownLessTwoEachWayItsBorderIsNotNought()
	{
		// game_name: 260 by 32, a border of 8 by 6, shows 252 by 26 and draws its picture 250 by 24 - the save dialog's
		// measured rim, 246 by 20, the picture having 2 clear all round.
		Assert.Equal((250f, 24f), CTextBoxPicture.Size(252f, 26f, 8, 6));
		// The lobby's chat box in a game has no border down: all the height.
		Assert.Equal((470f, 32f), CTextBoxPicture.Size(472f, 32f, 8, 0));
		Assert.Equal((100f, 40f), CTextBoxPicture.Size(100f, 40f, 0, 0));
	}

	[Fact]
	public void TheCornersKeepTheBorderAndTheEdgesAndTheMiddleStretch()
	{
		// The tooltip's tiles_dialog, 96 by 96, under a border of 16, drawn 200 by 100.
		List<CPicturePart> parts = CTextBoxPicture.Parts(200f, 100f, 16, 16, 96, 96);
		Assert.Equal(9, parts.Count);
		Assert.Equal(new CPicturePart(0f, 0f, 16f, 16f, 0, 0, 16, 16), parts[0]);
		Assert.Equal(new CPicturePart(16f, 0f, 168f, 16f, 16, 0, 64, 16), parts[1]);
		Assert.Equal(new CPicturePart(184f, 0f, 16f, 16f, 80, 0, 16, 16), parts[2]);
		Assert.Equal(new CPicturePart(0f, 16f, 16f, 68f, 0, 16, 16, 64), parts[3]);
		Assert.Equal(new CPicturePart(16f, 16f, 168f, 68f, 16, 16, 64, 64), parts[4]);
		Assert.Equal(new CPicturePart(184f, 84f, 16f, 16f, 80, 80, 16, 16), parts[8]);
	}

	[Fact]
	public void ABorderOfNoughtLeavesTheWholePictureStretchedOverTheWhole()
	{
		List<CPicturePart> parts = CTextBoxPicture.Parts(500f, 16f, 0, 0, 48, 48);
		Assert.Equal(new[] { new CPicturePart(0f, 0f, 500f, 16f, 0, 0, 48, 48) }, parts);
	}

	[Fact]
	public void ABorderOfNoughtDownKeepsOnlyTheMiddleRow()
	{
		// small_tiles_dialog, 32 by 32, under a border of 8 by 0: the three parts of the middle row, all the height.
		List<CPicturePart> parts = CTextBoxPicture.Parts(470f, 32f, 8, 0, 32, 32);
		Assert.Equal(3, parts.Count);
		Assert.Equal(new CPicturePart(8f, 0f, 454f, 32f, 8, 0, 16, 32), parts[1]);
	}
}
