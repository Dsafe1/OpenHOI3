using System.Linq;
using OpenHOI3.Interface.Text;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// Where the original's font breaks a <c>textBoxType</c>'s text, against <c>docs/mechanics/interface.md</c>, section 6.
/// Every character here is 10 wide, so a box of 100 holds ten - but a line that reaches the box, as ten do, breaks.
/// </summary>
public class TextBoxLineBreakTests
{
	private static float Measure(string text) => CTextIcons.Measure(text, run => run.Length * 10f);

	[Fact]
	public void ALineThatReachesTheBoxBreaksBeforeTheWordWhereAnInstantBoxWouldKeepIt()
	{
		// "aaaa bbbb " is 100 at its space: it reaches the box, so "bbbb " goes down; "dd" brings the next line to 100.
		Assert.Equal(new[] { "aaaa ", "bbbb cc ", "dd" }, CTextBoxLineBreak.Break("aaaa bbbb cc dd", 100f, 0f, Measure));
		Assert.Equal(new[] { "aaaa bbbb ", "cc dd" }, CFontLineBreak.Break("aaaa bbbb cc dd", 100f, 0f, Measure));
	}

	[Fact]
	public void TheBorderIsCountedTwice()
	{
		// 90 and twice 5 reach 100, which breaks; twice 4 does not.
		Assert.Equal(new[] { "aaaa ", "bbbb" }, CTextBoxLineBreak.Break("aaaa bbbb", 100f, 5f, Measure));
		Assert.Equal(new[] { "aaaa bbbb" }, CTextBoxLineBreak.Break("aaaa bbbb", 100f, 4f, Measure));
	}

	[Fact]
	public void AWordIsMovedDownAtTheCharacterBeforeWhichTheLineHasReachedTheBox()
	{
		// The line is 100 before the eighth b: the seven and it go down, and the count of the line is one short from
		// there, the original's own slip - so the line reaches 100 again before the twelfth b, and the word's count, never
		// set back, puts what is left on a line of its own.
		Assert.Equal(new[] { "aa ", "bbbbbbbb", "bbbb", "" }, CTextBoxLineBreak.Break("aa bbbbbbbbbbbb", 100f, 0f, Measure));
	}

	[Fact]
	public void AFirstWordThatFillsTheBoxLeavesTheFirstLineEmpty()
	{
		// Nothing is laid out, so its own space does not break it; the line has reached the box before that space, and
		// that break has no such test.
		Assert.Equal(new[] { "", "aaaaaaaaaa ", "", "b" }, CTextBoxLineBreak.Break("aaaaaaaaaa b", 100f, 0f, Measure));
		Assert.Equal(new[] { "aaaaaaaaa b" }, CTextBoxLineBreak.Break("aaaaaaaaa b", 120f, 0f, Measure));
	}

	[Fact]
	public void ANewlineEndsALineAndAColourTakesNoRoom()
	{
		Assert.Equal(new[] { "aa", "bb" }, CTextBoxLineBreak.Break("aa\nbb", 100f, 0f, Measure));
		Assert.Equal(new[] { "§Gaaaa ", "bbbb" }, CTextBoxLineBreak.Break("§Gaaaa bbbb", 90f, 0f, Measure));
		Assert.Equal(new[] { "§Gaaaa bbbb" }, CTextBoxLineBreak.Break("§Gaaaa bbbb", 100f, 0f, Measure));
	}

	[Fact]
	public void AFlagIsAsWideAsItsPicture()
	{
		Assert.Equal(new[] { "@GERaaaaaaa" }, CTextBoxLineBreak.Break("@GERaaaaaaa", 100f, 0f, Measure));
		// 24 and eight letters reach the box at the end, and the word - the flag with it - goes down.
		Assert.Equal(new[] { "", "@GERaaaaaaaa" }, CTextBoxLineBreak.Break("@GERaaaaaaaa", 100f, 0f, Measure));
	}

	[Fact]
	public void ABoxOfNoWidthBreaksAt320()
	{
		string text = new string('a', 20) + " " + new string('b', 20);
		Assert.Equal(new[] { new string('a', 20) + " ", new string('b', 20) }, CTextBoxLineBreak.Break(text, 0f, 0f, Measure));
	}

	[Fact]
	public void The55thLineIsTheLastRead()
	{
		string text = string.Concat(Enumerable.Repeat("a\n", 60));
		var lines = CTextBoxLineBreak.Break(text, 100f, 0f, Measure);
		Assert.Equal(56, lines.Count);
		Assert.All(lines.Take(55), line => Assert.Equal("a", line));
		Assert.Equal("", lines[55]);
	}
}
