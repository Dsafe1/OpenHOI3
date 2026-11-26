using OpenHOI3.Interface.Text;

namespace OpenHOI3.Tests.Interface;

/// <summary>Columns made of spaces in a proportional font (<see cref="CTextTable"/>), each character here one wide.</summary>
public class TextTableTests
{
	private static float Chars(string text) => text.Length;

	[Fact]
	public void A_column_starts_a_gap_of_two_spaces_after_the_widest_cell_before_it()
	{
		var rows = new[] { new[] { "a:", "1" }, new[] { "long:", "22" } };

		Assert.Equal("  a:     1\n  long:  22", CTextTable.Render(rows, "  ", Chars));
		// Ending together.
		Assert.Equal("  a:      1\n  long:  22", CTextTable.Render(rows, "  ", Chars, 1));
	}

	[Fact]
	public void Colours_are_not_measured_a_short_row_ends_early_and_a_later_column_always_has_a_space()
	{
		var rows = new[] { new[] { "\u00A7Yab\u00A7W", "c" }, new[] { "abcd" } };

		Assert.Equal("\u00A7Yab\u00A7W    c\nabcd", CTextTable.Render(rows, "", Chars));
		// A font that measures a space as nothing falls back on counting characters.
		Assert.Equal("\u00A7Yab\u00A7W    c\nabcd", CTextTable.Render(rows, "", _ => 0f));
	}

	[Fact]
	public void The_spaces_are_those_that_bring_the_line_nearest_measured_on_the_line_itself()
	{
		// A font that measures a text whole: a letter 5, and a run of spaces 4.4 each and 0.6 once - so one space is 5
		// and twelve are 53.4, not 60. The second column aims at 50 + 9.4: twelve spaces after "a" end at 58.4 and
		// thirteen at 62.8, where counting by one space's width would stop at eleven, 5.4 short.
		static float Whole(string text)
		{
			int spaces = text.Length - text.Replace(" ", "").Length;
			return (text.Length - spaces) * 5f + (spaces == 0 ? 0f : spaces * 4.4f + 0.6f);
		}
		var rows = new[] { new[] { "a", "b" }, new[] { "aaaaaaaaaa", "b" } };

		Assert.Equal("a" + new string(' ', 12) + "b\naaaaaaaaaa  b", CTextTable.Render(rows, "", Whole));
	}

	[Fact]
	public void A_line_is_measured_as_it_is_drawn_run_by_run_of_one_colour()
	{
		// Every run costs 3 on top of its letters, as a font that rounds each text it measures would: the cell in two
		// colours is 2 + 3 and 2 + 3 wide, not 4 + 3, so the column after it starts that much further on.
		static float Runs(string text) => text.Length + 3f;
		var rows = new[] { new[] { "§Yab§Wcd", "x" }, new[] { "abcd", "x" } };

		// The first column is 10 wide. Each row's spaces are whatever brings that row's own line nearest the second
		// column's start, measured on the line with its runs as they are.
		string text = CTextTable.Render(rows, "", Runs);
		string[] lines = text.Split('\n');
		Assert.StartsWith("§Yab§Wcd", lines[0]);
		Assert.EndsWith("x", lines[1]);
		// Both rows' second cells start at the same measured place, within the half space the rounding allows.
		float First(string line) => Measured(line[..line.LastIndexOf('x')], Runs);
		Assert.InRange(System.Math.Abs(First(lines[0]) - First(lines[1])), 0f, 0.5f);
	}

	private static float Measured(string line, System.Func<string, float> measure)
	{
		float width = 0f;
		foreach (CTextRun run in CColouredText.Parse(line))
		{
			width += measure(run.Text);
		}
		return width;
	}
}
