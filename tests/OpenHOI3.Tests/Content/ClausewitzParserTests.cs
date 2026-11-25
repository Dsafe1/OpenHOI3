using System.Linq;
using System.Numerics;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests.Content;

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
	public void ABackslashOnItsOwnIsStillASeparator()
	{
		// A few paths forget to double one or two of theirs, and still name a file that
		// is there: only a doubled backslash is an escape.
		ClausewitzNode root = ClausewitzParser.Parse(
			"""spriteType = { texturefile = "gfx\mapitems\resources\\a_small.tga" }""");

		Assert.Equal("gfx\\mapitems\\resources\\a_small.tga",
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

	[Fact]
	public void A_block_with_no_key_holds_its_entries_and_closes_only_itself()
	{
		// A save's finished battles, as the original writes them.
		ClausewitzNode root = ClausewitzParser.Parse("""
			combat = { history = { history = { { location = 5639 type = 5 } { location = 5650 type = 5 } } } land_combat = { day = 3 } }
			after = 1
			""");
		ClausewitzNode combat = root.Get("combat")!;
		ClausewitzNode[] entries = combat.Get("history")!.Get("history")!.Children.ToArray();
		Assert.Equal(2, entries.Length);
		Assert.All(entries, entry => Assert.True(entry.Key is null && entry.IsBlock));
		Assert.Equal(5650, entries[1].GetInt("location"));
		Assert.Equal(3, combat.Get("land_combat")!.GetInt("day"));
		Assert.Equal(1, root.GetInt("after"));
		Assert.Null(root.Get("location"));
	}

	[Fact]
	public void AKeyWithNoEqualsAmongPairsTakesTheValueThePairBeforeItRead()
	{
		// BlackICE's Kiskunhalas: the original's key reader loses 0.66 and keeps the value it read last, infra's 5.
		ClausewitzNode root = ClausewitzParser.Parse("""
			owner = HUN
			infra = 5
			manpower 0.66
			terrain = plains
			""");

		Assert.Equal("5", root.GetString("manpower"));
		Assert.Equal("plains", root.GetString("terrain"));
		Assert.Equal(4, root.Children.Count);
		Assert.DoesNotContain(root.Children, child => child.Value == "0.66");
	}

	[Fact]
	public void WithTheFixAKeyWithNoEqualsTakesTheWordAfterIt()
	{
		// An enhanced game's fix reads the line as it plainly says, a quoted value with it.
		ClausewitzNode root = ClausewitzParser.Parse("""
			owner = HUN
			infra = 5
			manpower 0.66
			terrain = plains
			regiment = { type = hq_brigade name "17th Mechanised Corps HQ" historical_model = 0 }
			theatre { name = x }
			""", wordAfterKey: true);

		Assert.Equal("0.66", root.GetString("manpower"));
		Assert.Equal("plains", root.GetString("terrain"));
		Assert.Equal("17th Mechanised Corps HQ", root.Get("regiment")!.GetString("name"));
		Assert.Equal("hq_brigade", root.Get("regiment")!.GetString("type"));
		Assert.Equal("0", root.Get("regiment")!.GetString("historical_model"));
		// A block whose `=` is missing is read as before.
		Assert.Equal("x", root.Get("theatre")!.GetString("name"));
		// A list of bare words is still a list.
		ClausewitzNode list = ClausewitzParser.Parse("values = { a b c }", wordAfterKey: true);
		Assert.Equal(3, list.Get("values")!.Children.Count);
	}

	[Fact]
	public void AKeyWithNoEqualsInsideABlockTakesItsLastPairsValueAndAfterABlockThatPairs()
	{
		// Their Finest Hour's SOV_1938: a brigade's name is its type, the value read before it.
		ClausewitzNode root = ClausewitzParser.Parse("""
			regiment = { type = hq_brigade name "17th Mechanised Corps HQ" historical_model = 0 }
			other = { a = 1 }
			stray "x"
			""");

		Assert.Equal("hq_brigade", root.Get("regiment")!.GetString("name"));
		Assert.Equal("0", root.Get("regiment")!.GetString("historical_model"));
		Assert.Equal("1", root.GetString("stray"));
	}

	[Fact]
	public void AKeyWithNoEqualsBeforeABraceIsTheBlocksKey()
	{
		// Their Finest Hour's JAP_DayOfInfamy: the brace taken where the `=` should be, and the theatre read whole.
		ClausewitzNode root = ClausewitzParser.Parse("""
			last_election = 1936.9.15
			theatre {
				name = "Southern Expeditionary Theater"
				location = 5809
			}
			""");

		ClausewitzNode theatre = root.Get("theatre")!;
		Assert.Equal("Southern Expeditionary Theater", theatre.GetString("name"));
		Assert.Equal(2, root.Children.Count);
	}

	[Fact]
	public void BareWordsInAListAreItsValues()
	{
		ClausewitzNode root = ClausewitzParser.Parse("color = { 1 2 3 } add = { GER ITA }");

		Assert.Equal(new[] { "1", "2", "3" }, root.Get("color")!.Children.Select(child => child.Value));
		Assert.Equal(new[] { "GER", "ITA" }, root.Get("add")!.Children.Select(child => child.Value));
	}
}
