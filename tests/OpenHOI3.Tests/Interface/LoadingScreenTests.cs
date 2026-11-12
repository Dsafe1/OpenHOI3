using System.Collections.Generic;
using System.Numerics;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Gui;
using OpenHOI3.Interface.Loading;

namespace OpenHOI3.Tests.Interface;

/// <summary>The loading screen's picture, layout and font: <c>docs/mechanics/loadingscreen.md</c>.</summary>
public class LoadingScreenTests
{
	[Fact]
	public void PicturesStepFromTheFirstAndHoldAtTheLast()
	{
		var pictures = new CLoadingPictures(3, firstOnly: false);
		Assert.Equal(0, pictures.Shown);
		pictures.Step();
		Assert.Equal(1, pictures.Shown);
		pictures.Step();
		pictures.Step();
		pictures.Step();
		Assert.Equal(2, pictures.Shown);
	}

	[Fact]
	public void AfterTheFirstMenuOnlyTheFirstPictureShows()
	{
		var pictures = new CLoadingPictures(6, firstOnly: true);
		pictures.Step();
		pictures.Step();
		Assert.Equal(0, pictures.Shown);
		Assert.Equal(-1, new CLoadingPictures(0, firstOnly: false).Shown);
	}

	[Fact]
	public void ThePictureIsASquareAsWideAsTheScreenCentred()
	{
		(Vector2 at, Vector2 size) = CLoadingLayout.Picture(1920f, 1080f);
		Assert.Equal(new Vector2(1920f, 1920f), size);
		// 420 cut from the top and as much from the bottom.
		Assert.Equal(new Vector2(0f, -420f), at);
	}

	[Fact]
	public void TheStripStandsFourAboveTheBottomAndTheTextBoxEightAbove()
	{
		(Vector2 stripAt, Vector2 stripSize) = CLoadingLayout.Strip(1920f, 1080f);
		Assert.Equal(new Vector2(660f, 972f), stripAt);
		Assert.Equal(1080f - 4f, stripAt.Y + stripSize.Y);
		(Vector2 textAt, Vector2 textSize) = CLoadingLayout.Text(1920f, 1080f);
		Assert.Equal(new Vector2(0f, 972f), textAt);
		Assert.Equal(1080f - 8f, textAt.Y + textSize.Y);
		Assert.Equal(1920f, textSize.X);
	}

	[Fact]
	public void ATrueTypeFontIsReadByItsNameTheLaterWinning()
	{
		var fonts = new Dictionary<string, CTrueTypeFont>();
		CTrueTypeFontReader.Read(ClausewitzParser.Parse(
			"bitmapfonts = { bitmapfont = { name = \"vic_18\" } } fonts = { font = { name = \"Loading_Font\" fontname = \"Arial\" height = 32 charset = \"ANSI\" color = 0xffffffff } }"),
			fonts);
		CTrueTypeFontReader.Read(ClausewitzParser.Parse("fonts = { font = { name = \"Loading_Font\" fontname = \"Tahoma\" height = 24 color = 0xff00ff00 } }"),
			fonts);

		CTrueTypeFont font = Assert.Single(fonts).Value;
		Assert.Equal("Tahoma", font.FontName);
		Assert.Equal(24, font.Height);
		Assert.Equal(1f, font.Color.G);
		Assert.Equal(0f, font.Color.R);
	}
}
