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
	public void The_spaces_are_the_whole_number_nearest_and_where_they_end_is_carried_on()
	{
		// A space 4 wide, a letter 5: the second column aims at 5 + 8 = 13, three spaces from 5 is 17, so the third
		// column's two spaces are counted from there.
		float Wide(string text) => text.Replace(" ", "").Length * 5f + (text.Length - text.Replace(" ", "").Length) * 4f;
		var rows = new[] { new[] { "a", "b", "c" } };

		Assert.Equal("a  b  c", CTextTable.Render(rows, "", Wide));
	}
}
