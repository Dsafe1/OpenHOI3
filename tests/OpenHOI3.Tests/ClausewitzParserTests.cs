using System.Linq;
using System.Numerics;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests;

/// <summary>
/// The parser underpins every interface file today and will underpin some twenty
/// thousand data files next, where a misreading produces a wrong game state rather
/// than an error. These cases are drawn from shapes that actually occur in the
/// shipped files.
/// </summary>
public class ClausewitzParserTests
{
	[Fact]
	public void ReadsScalarsAndBlocks()
	{
		ClausewitzNode root = ClausewitzParser.Parse("""
			guiTypes = {
				windowType = {
					name = "mainmenu_panel"
					size = { x = 420 y = 500 }
				}
			}
			""");

		ClausewitzNode window = root.Get("guiTypes")!.Get("windowType")!;
		Assert.Equal("mainmenu_panel", window.GetString("name"));
		Assert.Equal(new Vector2(420f, 500f), window.GetVector2("size"));
		Assert.True(window.IsBlock);
	}

	[Fact]
	public void KeyLookupIsCaseInsensitive()
	{
		// The files spell the same key both ways, sometimes within one block.
		ClausewitzNode root = ClausewitzParser.Parse("""
			a = { Orientation = "CENTER" }
			b = { orientation = "CENTER" }
			""");

		Assert.Equal("CENTER", root.Get("a")!.GetString("orientation"));
		Assert.Equal("CENTER", root.Get("b")!.GetString("Orientation"));
	}

	[Fact]
	public void KeepsEveryDuplicateKeyInOrder()
	{
		// A window declares many guiButtonType entries; losing any of them silently
		// drops controls from the screen.
		ClausewitzNode root = ClausewitzParser.Parse("""
			windowType = {
				guiButtonType = { name = "first" }
				guiButtonType = { name = "second" }
				guiButtonType = { name = "third" }
			}
			""");

		string[] names = root.Get("windowType")!.All("guiButtonType")
			.Select(n => n.GetString("name")).ToArray();
		Assert.Equal(new[] { "first", "second", "third" }, names);
	}

	[Fact]
	public void GetReturnsTheFirstOfSeveral()
	{
		ClausewitzNode root = ClausewitzParser.Parse("x = { a = 1 a = 2 }");
		Assert.Equal(1, root.Get("x")!.GetInt("a"));
	}

	[Fact]
	public void UnescapesPathSeparatorsInStrings()
	{
		// Texture paths are written "gfx\\interface\\frontend_button.dds"; leaving the
		// escaping in place makes every texture lookup miss.
		ClausewitzNode root = ClausewitzParser.Parse(
			"""spriteType = { texturefile = "gfx\\interface\\frontend_button.dds" }""");

		Assert.Equal("gfx\\interface\\frontend_button.dds",
			root.Get("spriteType")!.GetString("texturefile"));
	}

	[Fact]
	public void SkipsCommentsAnywhere()
	{
		ClausewitzNode root = ClausewitzParser.Parse("""
			# leading comment
			windowType = {   # trailing comment
				name = "panel"
			#	guiButtonType = { name = "commented_out" }
			}
			""");

		ClausewitzNode window = root.Get("windowType")!;
		Assert.Equal("panel", window.GetString("name"));
		Assert.Empty(window.All("guiButtonType"));
	}

	[Fact]
	public void ReadsNegativeAndFractionalNumbers()
	{
		// Positions are offsets from an anchor and are routinely negative.
		ClausewitzNode root = ClausewitzParser.Parse(
			"a = { position = { x = -430 y = 0 } scale = 0.7 }");

		Assert.Equal(new Vector2(-430f, 0f), root.Get("a")!.GetVector2("position"));
		Assert.Equal(0.7f, root.Get("a")!.GetFloat("scale"), 5);
	}

	[Fact]
	public void ReadsYesNoAsBool()
	{
		ClausewitzNode root = ClausewitzParser.Parse("a = { fullscreen = yes moveable = 0 }");
		Assert.True(root.Get("a")!.GetBool("fullscreen"));
		Assert.False(root.Get("a")!.GetBool("moveable"));
	}

	[Fact]
	public void ReadsArgbColourLiterals()
	{
		// bitmapfont entries use 0xAARRGGBB.
		ClausewitzNode root = ClausewitzParser.Parse("f = { color = 0xff000000 }");
		CColor black = root.Get("f")!.GetColor("color", CColor.White);

		Assert.Equal(0f, black.R);
		Assert.Equal(0f, black.G);
		Assert.Equal(0f, black.B);
		Assert.Equal(1f, black.A);
	}

	[Fact]
	public void MissingValuesFallBack()
	{
		ClausewitzNode root = ClausewitzParser.Parse("a = { }");
		ClausewitzNode a = root.Get("a")!;

		Assert.Null(root.Get("nope"));
		Assert.Equal("fallback", a.GetString("nope", "fallback"));
		Assert.Equal(7, a.GetInt("nope", 7));
		Assert.Equal(new Vector2(1f, 2f), a.GetVector2("nope", new Vector2(1f, 2f)));
	}

	[Fact]
	public void HandlesUnclosedBlockWithoutHanging()
	{
		// Truncated or malformed files should degrade, not throw or loop forever.
		ClausewitzNode root = ClausewitzParser.Parse("a = { b = { c = 1 ");
		Assert.NotNull(root.Get("a"));
	}

	[Fact]
	public void IgnoresStrayClosingBraceAtTopLevel()
	{
		ClausewitzNode root = ClausewitzParser.Parse("} a = 1");
		Assert.Equal(1, root.GetInt("a"));
	}

	[Fact]
	public void ParsesBareListValues()
	{
		ClausewitzNode root = ClausewitzParser.Parse("colour = { 255 128 0 }");
		string[] values = root.Get("colour")!.Children.Select(c => c.Value!).ToArray();
		Assert.Equal(new[] { "255", "128", "0" }, values);
	}

	[Fact]
	public void TolerantOfMissingWhitespaceAroundEquals()
	{
		ClausewitzNode root = ClausewitzParser.Parse("size= {x=32 y=32 }");
		Assert.Equal(new Vector2(32f, 32f), root.GetVector2("size"));
	}
}
