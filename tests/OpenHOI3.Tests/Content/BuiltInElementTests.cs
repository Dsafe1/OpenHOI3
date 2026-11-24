using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Gui;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// Built-in elements, against <c>docs/mechanics/builtin.md</c>: the key an icon and a button take, the name it stands
/// in their sprite as, the styles and a mod's file over them, and the picture painted.
/// </summary>
public class BuiltInElementTests
{
	private static CGuiTypes Read(string elements, List<string> problems)
	{
		return CGuiTypes.Parse("guiTypes = { windowType = { name = \"w\" " + elements + " } }", problems.Add);
	}

	[Fact]
	public void AnIconAndAButtonTakeABuiltInPictureInPlaceOfASprite()
	{
		var problems = new List<string>();
		CGuiTypes types = Read(
			"iconType = { name = \"bg\" built_in = { style = panel size = { x = 300 y = 120 } } } "
			+ "guiButtonType = { name = \"go\" spriteType = \"GFX_other\" built_in = { style = BUTTON size = { x = 90 y = 24 } color = { r = 0 g = 255 b = 0 } } }",
			problems);

		var window = (CGuiWindowType)types.Find("w")!;
		Assert.Equal("openhoi_builtin:panel:300:120:-", window.Children.OfType<CGuiIconType>().Single().Sprite);
		// It stands before a spriteType beside it, and its style is taken whatever its case.
		Assert.Equal("openhoi_builtin:button:90:24:00ff00ff", window.Children.OfType<CGuiButtonType>().Single().Sprite);
		Assert.Empty(problems);
	}

	[Fact]
	public void ANameSaysAllOfAPictureAndIsReadBack()
	{
		var sprite = new CBuiltInSprite("row", 396, 26, new CColor(200 / 255f, 40 / 255f, 40 / 255f, 110 / 255f));
		Assert.Equal("openhoi_builtin:row:396:26:c828286e", sprite.Name);
		Assert.Equal(sprite, CBuiltInSprite.FromName(sprite.Name));
		Assert.Equal(new CBuiltInSprite("flat", 1, 2, null), CBuiltInSprite.FromName("openhoi_builtin:flat:1:2:-"));

		Assert.True(CBuiltInSprite.Is("OPENHOI_BUILTIN:flat:1:1:-"));
		Assert.Null(CBuiltInSprite.FromName("GFX_button"));
		Assert.Null(CBuiltInSprite.FromName("openhoi_builtin:flat:0:10:-"));
		Assert.Null(CBuiltInSprite.FromName("openhoi_builtin:flat:10:5000:-"));
		Assert.Null(CBuiltInSprite.FromName("openhoi_builtin:flat:10:10:zz"));
		Assert.Null(CBuiltInSprite.FromName("openhoi_builtin:flat:10:10"));
	}

	[Fact]
	public void AStyleNothingHasAMissingSizeAndAKeyOfNoneAreReported()
	{
		var problems = new List<string>();
		CGuiTypes types = Read(
			"iconType = { name = \"a\" built_in = { style = marble size = { x = 10 y = 10 } } } "
			+ "iconType = { name = \"b\" built_in = { style = panel } } "
			+ "iconType = { name = \"c\" built_in = { size = { x = 4 y = 6 } shine = yes } }",
			problems);

		var window = (CGuiWindowType)types.Find("w")!;
		string[] sprites = window.Children.OfType<CGuiIconType>().Select(icon => icon.Sprite).ToArray();
		// Painted flat; held to one each way; flat where no style is named.
		Assert.Equal(new[] { "openhoi_builtin:flat:10:10:-", "openhoi_builtin:panel:1:1:-", "openhoi_builtin:flat:4:6:-" }, sprites);
		Assert.Contains(problems, problem => problem.Contains("'marble'") && problem.Contains("panel"));
		Assert.Contains(problems, problem => problem.Contains("needs a size"));
		Assert.Contains(problems, problem => problem.Contains("does not take 'shine'"));
		Assert.Equal(3, problems.Count);
	}

	[Fact]
	public void AnEditBoxOfTheEnhancedFolderHasTheCaretTheEngineDrawsAndAnyOtherTheGames()
	{
		const string Box = "guiTypes = { windowType = { name = \"w\" editBoxType = { name = \"box\" size = { x = 64 y = 20 } } } }";
		static bool Drawn(string file) => ((CGuiWindowType)CGuiTypes.Parse(Box, _ => { }, file).Find("w")!)
			.Children.OfType<CGuiTextBoxType>().Single().BuiltInCaret;

		Assert.True(Drawn(@"C:\Game\tfh\mod\Mod\interface\openhoi\sliders.gui"));
		Assert.True(Drawn("Interface/OpenHOI/sliders.gui"));
		Assert.False(Drawn(@"C:\Game\tfh\interface\frontend.gui"));
		Assert.False(Drawn("interface/openhoi_sliders.gui"));
	}

	[Fact]
	public void AModsFileChangesAStyleKeyByKeyAndAddsItsOwn()
	{
		var problems = new List<string>();
		CBuiltInStyles styles = CBuiltInStyles.Parse(
			"panel = { fill = { r = 10 g = 20 b = 30 } border_width = 3 } "
			+ "steel = { fill = { 100 110 120 } bevel = 0.5 hollow = no glow = 1 }",
			problems.Add);

		CBuiltInStyle engine = new CBuiltInStyles().Find("panel")!;
		CBuiltInStyle panel = styles.Find("PANEL")!;
		Assert.Equal(new CColor(10 / 255f, 20 / 255f, 30 / 255f), panel.Fill);
		Assert.Equal(3, panel.BorderWidth);
		// What the file did not give is the engine's still.
		Assert.Equal((engine.Border, engine.Bevel, engine.Gradient), (panel.Border, panel.Bevel, panel.Gradient));

		CBuiltInStyle steel = styles.Find("steel")!;
		Assert.Equal(new CColor(100 / 255f, 110 / 255f, 120 / 255f), steel.Fill);
		Assert.Equal((0.5f, 0), (steel.Bevel, steel.BorderWidth));
		Assert.Single(problems, problem => problem.Contains("'glow'"));
		Assert.Contains("steel", styles.Names);
		Assert.Null(styles.Find("marble"));
	}

	private static (byte R, byte G, byte B, byte A) At(byte[] pixels, int width, int x, int y)
	{
		int at = (y * width + x) * 4;
		return (pixels[at], pixels[at + 1], pixels[at + 2], pixels[at + 3]);
	}

	[Fact]
	public void APictureIsItsBorderItsBevelAndItsFillShadedDownItsHeight()
	{
		var style = new CBuiltInStyle
		{
			Name = "test", Fill = new CColor(0.5f, 0.5f, 0.5f), Border = new CColor(0f, 0f, 0f), BorderWidth = 1, Bevel = 0.2f, Gradient = 0.2f,
		};
		byte[] pixels = CBuiltInPainter.Paint(style, 8, 9);
		Assert.Equal(8 * 9 * 4, pixels.Length);

		Assert.Equal(((byte)0, (byte)0, (byte)0, (byte)255), At(pixels, 8, 0, 0));
		Assert.Equal(((byte)0, (byte)0, (byte)0, (byte)255), At(pixels, 8, 7, 8));
		// The middle row is the fill itself; the rows above are lighter and those below darker.
		Assert.Equal((byte)128, At(pixels, 8, 4, 4).R);
		Assert.True(At(pixels, 8, 4, 2).R > 128 && At(pixels, 8, 4, 6).R < 128);
		// The bevel: the first row and column inside the border lit, the last ones shaded.
		Assert.True(At(pixels, 8, 4, 1).R > At(pixels, 8, 4, 2).R);
		Assert.True(At(pixels, 8, 1, 4).R > At(pixels, 8, 4, 4).R);
		Assert.True(At(pixels, 8, 4, 7).R < At(pixels, 8, 4, 6).R);
		Assert.True(At(pixels, 8, 6, 4).R < At(pixels, 8, 4, 4).R);
	}

	[Fact]
	public void AnElementsOwnColourIsTheFillAndADarkerBorderAndAHollowStyleIsItsBorderAlone()
	{
		var plain = new CBuiltInStyle { Name = "plain", Fill = CColor.White, Border = new CColor(1f, 0f, 0f), BorderWidth = 1 };
		byte[] tinted = CBuiltInPainter.Paint(plain, 5, 5, new CColor(0f, 1f, 0f, 0.5f));
		Assert.Equal(((byte)0, (byte)255, (byte)0, (byte)128), At(tinted, 5, 2, 2));
		Assert.Equal(((byte)0, (byte)115, (byte)0, (byte)128), At(tinted, 5, 0, 0));

		var frame = new CBuiltInStyle { Name = "frame", Border = new CColor(0f, 0f, 0f), BorderWidth = 2, Hollow = true };
		byte[] hollow = CBuiltInPainter.Paint(frame, 7, 7, new CColor(0f, 0f, 1f));
		Assert.Equal(((byte)0, (byte)0, (byte)255, (byte)255), At(hollow, 7, 1, 3));
		Assert.Equal(((byte)0, (byte)0, (byte)0, (byte)0), At(hollow, 7, 3, 3));

		// A line thinner than twice its border is all border.
		byte[] line = CBuiltInPainter.Paint(frame, 6, 2);
		Assert.All(Enumerable.Range(0, 12), index => Assert.Equal((byte)255, line[index * 4 + 3]));
	}
}
