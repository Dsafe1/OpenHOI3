using System.Linq;
using OpenHOI3.Interface.Text;

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
	public void KnowsTheLettersAndSaysWhenItDoesNot()
	{
		Assert.True(CTextColours.TryGet('R', out _));
		Assert.True(CTextColours.TryGet('G', out _));
		Assert.True(CTextColours.TryGet('Y', out _));
		Assert.False(CTextColours.TryGet(CColouredText.NoColour, out _));
		Assert.False(CTextColours.TryGet('?', out _));

		// Red is redder than it is green, and green the other way about.
		CTextColours.TryGet('R', out var red);
		CTextColours.TryGet('G', out var green);
		Assert.True(red.R > red.G && green.G > green.R);
		Assert.Equal(2, new[] { red, green }.Distinct().Count());
	}
}
