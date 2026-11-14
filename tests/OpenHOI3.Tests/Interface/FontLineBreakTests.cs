using OpenHOI3.Interface.Text;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// Where the original's font breaks a text box's text, against <c>docs/mechanics/interface.md</c>, section 6. Every
/// character here is 10 wide, so a box of 100 holds ten.
/// </summary>
public class FontLineBreakTests
{
	private static float Measure(string text) => text.Length * 10f;

	[Fact]
	public void ALineBreaksBeforeTheWordWhoseSpaceRunsPastTheBoxAndKeepsItsOwnSpace()
	{
		// "aaaa " fits, "bbbb " makes 100 - still in - and "cc " would make 130.
		Assert.Equal(new[] { "aaaa bbbb ", "cc dd" }, CFontLineBreak.Break("aaaa bbbb cc dd", 100f, 0f, Measure));
	}

	[Fact]
	public void TheBorderIsCountedTwiceAndTheHeightNotAtAll()
	{
		// 90 and twice 6 is 102; twice 5 would be exactly 100, which still fits.
		Assert.Equal(new[] { "aaaa ", "bbbb" }, CFontLineBreak.Break("aaaa bbbb", 100f, 6f, Measure));
		Assert.Equal(new[] { "aaaa bbbb" }, CFontLineBreak.Break("aaaa bbbb", 100f, 5f, Measure));
	}

	[Fact]
	public void TheLastWordIsTestedWithoutASpaceAndNotWhereItAloneIsTooWide()
	{
		Assert.Equal(new[] { "aaaaa ", "bbbbbb" }, CFontLineBreak.Break("aaaaa bbbbbb", 100f, 0f, Measure));
		Assert.Equal(new[] { "bbbbbbbbbbbb" }, CFontLineBreak.Break("bbbbbbbbbbbb", 100f, 0f, Measure));
	}

	[Fact]
	public void ALongWordIsBrokenAtTheEdgeOnceSomethingIsLaidOut()
	{
		// "aa " is laid out; the word reaches the edge at its seventh letter, 70 wide, and again at its thirteenth, the
		// six since 60 wide.
		Assert.Equal(new[] { "aa ", "bbbbbbb", "bbbbbbbb" }, CFontLineBreak.Break("aa bbbbbbbbbbbbbbb", 100f, 0f, Measure));
		// Its tail is tested at the end as a word of its own.
		Assert.Equal(new[] { "aa ", "bbbbbbb", "bbbbb" }, CFontLineBreak.Break("aa bbbbbbbbbbbb", 100f, 0f, Measure));
		// The first word is never broken so; with a space after it, the line before it is empty.
		Assert.Equal(new[] { "", "bbbbbbbbbbbb ", "cc" }, CFontLineBreak.Break("bbbbbbbbbbbb cc", 100f, 0f, Measure));
	}

	[Fact]
	public void ANewlineEndsALineAndAColourTakesNoRoom()
	{
		Assert.Equal(new[] { "aa", "bb" }, CFontLineBreak.Break("aa\nbb", 100f, 0f, Measure));
		Assert.Equal(new[] { "§Gaaaa bbbb" }, CFontLineBreak.Break("§Gaaaa bbbb", 90f, 0f, Measure));
	}

	[Fact]
	public void TheBreakAtASpaceThatMakes55LinesIsTheLastRead()
	{
		// Every word goes down in a box of 15, the first leaving an empty line. At the 55th the text is no longer read;
		// the end then puts the last word down, and the line after it is empty.
		var lines = CFontLineBreak.Break(string.Concat(System.Linq.Enumerable.Repeat("a ", 100)), 15f, 0f, Measure);
		Assert.Equal(57, lines.Count);
		Assert.Equal("a ", lines[55]);
		Assert.Equal("", lines[56]);
	}

	[Fact]
	public void ABoxOfNoWidthBreaksAt320()
	{
		string text = new string('a', 20) + " " + new string('b', 20);
		Assert.Equal(new[] { new string('a', 20) + " ", new string('b', 20) }, CFontLineBreak.Break(text, 0f, 0f, Measure));
	}
}
