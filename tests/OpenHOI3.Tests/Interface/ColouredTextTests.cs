using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Gui;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>The colour escape, against <c>docs/mechanics/interface.md</c>, section 6.</summary>
public class ColouredTextTests
{
	[Fact]
	public void TextBeforeAnyEscapeHasNoColourOfItsOwn()
	{
		CTextRun run = Assert.Single(CColouredText.Parse("115"));
		Assert.Equal(new CTextRun("115", CColouredText.NoColour), run);
	}

	[Fact]
	public void AnEscapeColoursWhatFollowsUntilTheNext()
	{
		// What the original's top bar held for a country wasting 10 of its 21 IC.
		Assert.Equal(
			new[] { new CTextRun("10/", 'R'), new CTextRun("21/21", 'G') },
			CColouredText.Parse("§R10/§G21/21"));
	}

	[Fact]
	public void AnEscapeDrawsNothing()
	{
		Assert.Equal("10/21/21", CColouredText.Strip("§R10/§G21/21"));
		Assert.Empty(CColouredText.Parse("§G"));
		Assert.Empty(CColouredText.Parse("§"));
	}

	[Fact]
	public void PutsAnEscapeInFrontOfAFigure()
	{
		Assert.Equal("§Y80%", CColouredText.In(CColouredText.Yellow) + "80%");
	}

	[Fact]
	public void AColourRunsOnOverALineBreakUntilTheNextEscape()
	{
		Assert.Equal(new[] { "§Raa", "§Rbb", "§Gcc", "§Gdd" }, CColouredText.CarryColour(new[] { "§Raa", "bb", "§Gcc", "dd" }));
		Assert.Equal(new[] { "aa §Yb", "§Ycc§!", "§!dd" }, CColouredText.CarryColour(new[] { "aa §Yb", "cc§!", "dd" }));
		Assert.Equal(new[] { "aa", "bb" }, CColouredText.CarryColour(new[] { "aa", "bb" }));
	}

	[Fact]
	public void ALetterThatChangesNothingIsNotCarriedOnToTheNextLine()
	{
		static bool Known(char letter) => letter is 'R' or '!';

		Assert.Equal(new[] { "§Raa§X", "§Rbb" }, CColouredText.CarryColour(new[] { "§Raa§X", "bb" }, Known));
		Assert.Equal(new[] { "§Raa", "§R§Xbb" }, CColouredText.CarryColour(new[] { "§Raa", "§Xbb" }, Known));
	}

	[Fact]
	public void ALetterIsTheFontsOwnColourThenTheSharedTablesAndOtherwiseNothing()
	{
		CFontColourTable own = Table("G = { 0 255 0 }");
		CFontColourTable shared = Table("G = { 0 159 3 } R = { 255 50 50 }");

		Assert.True(CTextColours.TryGet('G', own, shared, out CColor green));
		Assert.Equal(new CColor(0f, 1f, 0f), green);
		Assert.True(CTextColours.TryGet('R', own, shared, out CColor red));
		Assert.Equal(new CColor(1f, 50f / 255f, 50f / 255f), red);
		Assert.True(CTextColours.TryGet('G', null, shared, out CColor sharedGreen));
		Assert.Equal(new CColor(0f, 159f / 255f, 3f / 255f), sharedGreen);
		// A letter neither has changes nothing; the original swallows it.
		Assert.False(CTextColours.TryGet('X', own, shared, out _));
		Assert.False(CTextColours.TryGet('R', null, null, out _));
		Assert.True(CTextColours.IsPlain('!'));
		Assert.True(CTextColours.IsPlain(CColouredText.NoColour));
		Assert.False(CTextColours.IsPlain('W'));
	}

	private static CFontColourTable Table(string lines)
	{
		var table = new CFontColourTable();
		table.Add(ClausewitzParser.Parse(lines));
		return table;
	}
}
