using OpenHOI3.Interface.Text;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the original's font makes of a box of fixed size's text before laying it out, against
/// <c>docs/mechanics/interface.md</c>, section 6. Every character is 10 wide and a line 10 high.
/// </summary>
public class FixedSizeCutTests
{
	private static float Measure(string text) => CTextIcons.Measure(text, run => run.Length * 10f);

	private static string Cut(string text, float width, float height, float border = 0f) =>
		CFixedSizeCut.Cut(text, width, height, border, 10f, Measure);

	[Fact]
	public void ATextThatFitsIsLeftAlone()
	{
		Assert.Equal("abcdefghi", Cut("abcdefghi", 100f, 10f));
	}

	[Fact]
	public void TheLastFourCharactersBeforeTheOneThatReachesTheBoxBecomeASpaceAndThreeDots()
	{
		// The tenth character brings the line to 100: the nine before it are kept, their last four made " ...".
		Assert.Equal("abcde ...", Cut("abcdefghijkl", 100f, 10f));
	}

	[Fact]
	public void TheHeightIsCountedLessTwiceTheBorderAcross()
	{
		// 30 high less twice 5 across is 20, two lines: the ninth character fills the first line and starts the second,
		// and the tenth, the line's count not yet set back, starts a third.
		Assert.Equal("aaaaa ...", Cut(new string('a', 20), 100f, 30f, 5f));
	}

	[Fact]
	public void ANewlineStartsALineAndAShortCutHasNoDots()
	{
		Assert.Equal("abc", Cut("abc\ndefgh\nij", 100f, 10f));
		Assert.Equal("ab ...", Cut("abcdef\ngh", 100f, 10f));
	}

	[Fact]
	public void AColourEscapeIsNotCountedButIsKeptInTheText()
	{
		Assert.Equal("§Rabcde ...", Cut("§Rabcdefghijkl", 100f, 10f));
	}
}
