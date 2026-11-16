using OpenHOI3.Interface.Text;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// Where a picture given to an icon by code is drawn and which of its pixels: the sprite's own box and texture
/// coordinates laid on the picture, against <c>docs/mechanics/interface.md</c>, section 5.
/// </summary>
public class PictureSpriteTests
{
	[Fact]
	public void APictureTheSizeOfItsSpritesFileFillsTheBoxWhole()
	{
		// The base game's skins: GFX_country_bg and every country_bg_TAG are 1024 by 680.
		Assert.Equal(new CPicturePart(0f, 0f, 1024f, 680f, 0, 0, 1024, 680), CPictureSprite.Place(1024, 680, 1, 1f, 1024, 680));
	}

	[Fact]
	public void BlackIcesSkinSpriteStretchesTheGermanSkinsFirst640ColumnsAcrossItsWidth()
	{
		// GFX_country_bg in BlackICE is 1280 by 736: its coordinates run to 1280/2048 across and 736/1024 down. On the base
		// game's 1024 by 680 skin that is its first 640 columns, stretched to 1280, and 736 rows of which the last 56 are
		// the padding's nothing - so the picture covers the top 680 of the box.
		Assert.Equal(new CPicturePart(0f, 0f, 1280f, 680f, 0, 0, 640, 680), CPictureSprite.Place(1280, 736, 1, 1f, 1024, 680));
	}

	[Fact]
	public void ThePictureIsPaddedToItsOwnPowerOfTwo()
	{
		// A sprite of 50 by 50 (to 64) shows 50/64 of a picture padded to 64 - of a 40 by 40 picture, all of it, in the
		// part 40/50 of the box.
		CPicturePart? part = CPictureSprite.Place(50, 50, 1, 1f, 40, 40);
		Assert.NotNull(part);
		Assert.Equal(40, part!.Value.SourceWidth);
		Assert.Equal(40, part.Value.SourceHeight);
		Assert.Equal(40f, part.Value.Width, 3);
		Assert.Equal(40f, part.Value.Height, 3);
	}

	[Fact]
	public void TheBoxIsAFrameOfTheSpriteTimesTheScale()
	{
		// Two frames of 64 by 32: a box of 32 by 32, its coordinates half of 64/64; a picture of 64 by 32 shows its left half.
		Assert.Equal(new CPicturePart(0f, 0f, 64f, 64f, 0, 0, 32, 32), CPictureSprite.Place(64, 32, 2, 2f, 64, 32));
	}

	[Fact]
	public void NothingIsDrawnForAnEmptySpriteOrPicture()
	{
		Assert.Null(CPictureSprite.Place(0, 680, 1, 1f, 1024, 680));
		Assert.Null(CPictureSprite.Place(1024, 680, 1, 1f, 0, 0));
	}

	[Fact]
	public void APowerOfTwoIsTheLeastNotBelow()
	{
		Assert.Equal(2048, CPictureSprite.PowerOfTwo(1280));
		Assert.Equal(1024, CPictureSprite.PowerOfTwo(1024));
		Assert.Equal(1024, CPictureSprite.PowerOfTwo(680));
		Assert.Equal(1, CPictureSprite.PowerOfTwo(1));
	}
}
