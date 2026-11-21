using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHOI3.Interface.Screens;

namespace OpenHOI3.Tests.Interface;

/// <summary>The credits' lines and their roll, against <c>docs/mechanics/frontend.md</c>, section 10.</summary>
public class CreditsTests
{
	private static List<CCreditsLine> Lines(string file)
	{
		var lines = new List<CCreditsLine>();
		CCreditsText.Append(lines, Encoding.Latin1.GetBytes(file));
		return lines;
	}

	[Fact]
	public void ALineEndsAtALineFeedAndASectionSignFirstMakesItBold()
	{
		List<CCreditsLine> lines = Lines("§Producer\r\nJohan\r\n\r\nA § inside\r\n");

		Assert.Equal(new[] { "Producer", "Johan", "", "A § inside" }, lines.Select(line => line.Text));
		Assert.Equal(new[] { true, false, false, false }, lines.Select(line => line.Bold));
	}

	[Fact]
	public void AFileNotEndingInALineFeedLosesItsLastCharacterAndASecondVisitAddsTheFileAgain()
	{
		var lines = new List<CCreditsLine>();
		CCreditsText.Append(lines, Encoding.Latin1.GetBytes("One\nTwo"));
		Assert.Equal(new[] { "One", "Tw" }, lines.Select(line => line.Text));

		CCreditsText.Append(lines, Encoding.Latin1.GetBytes("One\nTwo"));
		Assert.Equal(4, lines.Count);
	}

	[Fact]
	public void TheBoxesStartFortyBelowTheBandTwentyApartAndRiseTwentyASecond()
	{
		var scroll = new CCreditsScroll(100);
		Assert.Equal(35, scroll.Slots.Count);
		Assert.Equal(260f, scroll.Slots[0].Y);
		Assert.Equal(280f, scroll.Slots[1].Y);
		Assert.Equal(35, scroll.NextLine);
		Assert.All(scroll.Slots, slot => Assert.False(slot.Shown));

		// Two seconds on the first is at the edge and not yet below it; a moment later it shows.
		scroll.Step(2f);
		Assert.Equal(220f, scroll.Slots[0].Y);
		Assert.False(scroll.Slots[0].Shown);
		scroll.Step(0.05f);
		Assert.True(scroll.Slots[0].Shown);
		Assert.False(scroll.Slots[1].Shown);
	}

	[Fact]
	public void ABoxLeavingAtTheTopTakesTheNextLineSevenHundredLowerAndIsHiddenUntilItsNextFrame()
	{
		var scroll = new CCreditsScroll(100);
		// 346 up from its start the first box is at the upper edge; just past it, it leaves.
		scroll.Step(17.3f);
		scroll.Step(0.1f);
		CCreditsSlot first = scroll.Slots[0];
		Assert.Equal(35, first.Line);
		Assert.Equal(36, scroll.NextLine);
		Assert.False(first.Shown);
		Assert.Equal(-88f + 700f, first.Y, 2);
		// One spacing under the lowest box.
		Assert.Equal(scroll.Slots[34].Y + 20f, first.Y, 2);
	}

	[Fact]
	public void WithNoLineLeftABoxStopsAHundredAboveTheBandHidden()
	{
		var scroll = new CCreditsScroll(3);
		Assert.Equal(new[] { 0, 1, 2, -1 }, scroll.Slots.Take(4).Select(slot => slot.Line));
		scroll.Step(60f);
		Assert.Equal(-186f, scroll.Slots[0].Y);
		Assert.False(scroll.Slots[0].Shown);
		Assert.Equal(0, scroll.Slots[0].Line);
		Assert.Equal(3, scroll.NextLine);
	}

	[Fact]
	public void ABoxIsPutAtItsPlaceCutTowardsNought()
	{
		var scroll = new CCreditsScroll(100);
		scroll.Step(13.02f);                    // the first box at -0.4
		Assert.Equal(0, scroll.Slots[0].BoxY);
		scroll.Step(0.06f);                     // -1.6
		Assert.Equal(-1, scroll.Slots[0].BoxY);
	}
}
