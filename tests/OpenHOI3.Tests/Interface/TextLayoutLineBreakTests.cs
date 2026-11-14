using OpenHOI3.Interface.Text;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// How the original's text layout breaks a text a screen hands on - a tooltip, a row of the message log, a line of the
/// diplomacy screen's status list - against <c>docs/mechanics/interface.md</c>, section 6. Every character is one wide,
/// the <c>O</c> among them.
/// </summary>
public class TextLayoutLineBreakTests
{
	private static float Measure(string text) => text.Length;

	[Fact]
	public void ALineIsLeftWholeWhereItAndAnOFit()
	{
		Assert.Equal(new[] { "123456789" }, CTextLayoutLineBreak.Break("123456789", 10f, Measure));
		Assert.Equal(new[] { "aaa bbb", "ccc" }, CTextLayoutLineBreak.Break("aaa bbb ccc", 10f, Measure));
	}

	[Fact]
	public void ALineIsCutAtTheLastSpaceBeforeTheCharacterThatReachesTheRoomLessTheO()
	{
		// "aaa bbbbb" and an O would fit as a whole line, but the ninth character is the first to reach nine.
		Assert.Equal(new[] { "aaa", "bbbbb c" }, CTextLayoutLineBreak.Break("aaa bbbbb c", 10f, Measure));
	}

	[Fact]
	public void AFirstWordTooWideIsSplitAfterTheCharacterThatReachesTheRoomLessTheO()
	{
		Assert.Equal(new[] { "abcdefghi", "jkl" }, CTextLayoutLineBreak.Break("abcdefghijkl", 10f, Measure));
		Assert.Equal(new[] { "abcdefghi", "jkl b" }, CTextLayoutLineBreak.Break("abcdefghijkl b", 10f, Measure));
	}

	[Fact]
	public void ANewlineEndsALineAndOneAtTheEndLeavesAnEmptyLine()
	{
		Assert.Equal(new[] { "ab", "", "cd" }, CTextLayoutLineBreak.Break("ab\n\ncd", 10f, Measure));
		Assert.Equal(new[] { "ab", "" }, CTextLayoutLineBreak.Break("ab\n", 10f, Measure));
		Assert.Equal(new[] { "" }, CTextLayoutLineBreak.Break("", 10f, Measure));
	}

	[Fact]
	public void AColourTakesNoRoomAndIsNotCarried()
	{
		Assert.Equal(new[] { "§Yaaa bbb", "ccc" }, CTextLayoutLineBreak.Break("§Yaaa bbb ccc", 10f, Measure));
	}

	[Fact]
	public void ATooltipIsLaidOutAtItsBoxLessThreeBorders()
	{
		// core.gui's box of 400 and border 16 lays out at 352: a line of 345 and an O of 8 is cut, one of 340 is not.
		string longer = new string('a', 300) + " " + new string('b', 44);
		Assert.Equal(new[] { new string('a', 300), new string('b', 44) },
			CTextLayoutLineBreak.Break(longer, 352f, text => text == CTextLayoutLineBreak.Spare ? 8f : text.Length));
		string shorter = new string('a', 300) + " " + new string('b', 39);
		Assert.Single(CTextLayoutLineBreak.Break(shorter, 352f, text => text == CTextLayoutLineBreak.Spare ? 8f : text.Length));
	}

	[Fact]
	public void TheLinesByPlaceLeaveOutTheSpaceAndTheNewlineALineEndsAt()
	{
		Assert.Equal(new[] { (0, 3), (4, 7) }, CTextLayoutLineBreak.Ranges("aaa bbbbb c", 10f, Measure));
		Assert.Equal(new[] { (0, 2), (3, 2), (6, 0) }, CTextLayoutLineBreak.Ranges("ab\ncd\n", 10f, Measure));
		Assert.Equal(new[] { (0, 0) }, CTextLayoutLineBreak.Ranges("", 10f, Measure));
		Assert.Equal(new[] { (0, 9), (9, 3) }, CTextLayoutLineBreak.Ranges("abcdefghijkl", 10f, Measure));
	}

	[Fact]
	public void TheLobbysChatIsLaidOutAtItsRowsTextBoxLessTen()
	{
		// temp_frontend.gui's messagelogtext is 750 wide, so the chat is laid out at 740: a line of 735 and an O of 8 is
		// cut, one of 730 is not.
		float Wide(string text) => text == CTextLayoutLineBreak.Spare ? 8f : text.Length;
		Assert.Equal(2, CTextLayoutLineBreak.Break(new string('a', 700) + " " + new string('b', 34), 740f, Wide).Count);
		Assert.Single(CTextLayoutLineBreak.Break(new string('a', 700) + " " + new string('b', 29), 740f, Wide));
	}
}
