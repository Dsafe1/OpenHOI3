using OpenHOI3.Interface.Screens;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The rows of the technology panel's <c>effects</c> list, against <c>docs/mechanics/technologyscreen.md</c>, section 7:
/// every character one unit wide, in a box ten wide, so a line fits at nine with the spare <c>O</c>.
/// </summary>
public class TechDetailsRowsTests
{
	private static float Measure(string text) => text.Length;

	[Fact]
	public void A_line_breaks_at_the_last_space_that_leaves_room_for_an_O_and_an_empty_row_is_last()
	{
		Assert.Equal(new[] { "aaa bbb", "ccc", "" }, CTechDetailsRows.Rows("aaa bbb ccc\n", 10, Measure));
		Assert.Equal(new[] { "123456789", "" }, CTechDetailsRows.Rows("123456789\n", 10, Measure));
	}

	[Fact]
	public void A_word_too_wide_is_cut_after_the_most_characters_that_fit()
	{
		Assert.Equal(new[] { "abcdefghi", "jkl" }, CTechDetailsRows.Rows("abcdefghijkl", 10, Measure));
	}

	[Fact]
	public void Blank_and_one_character_lines_vanish()
	{
		Assert.Equal(new[] { "ab", "cd", "" }, CTechDetailsRows.Rows("ab\n\nx\ncd\n", 10, Measure));
	}

	[Fact]
	public void No_text_is_one_empty_row()
	{
		Assert.Equal(new[] { "" }, CTechDetailsRows.Rows("", 10, Measure));
	}

	[Fact]
	public void A_colour_escape_measures_nothing_and_is_not_carried_onto_the_next_row()
	{
		Assert.Equal(new[] { "§Yabcdefghi" }, CTechDetailsRows.Rows("§Yabcdefghi", 10, Measure));
		Assert.Equal(new[] { "§Yaaa bbb", "ccc" }, CTechDetailsRows.Rows("§Yaaa bbb ccc", 10, Measure));
	}

	[Fact]
	public void A_continuation_keeps_what_followed_the_dropped_space()
	{
		// Two spaces, only the first of them with room before it: it is dropped at the break, the other starts the next row.
		Assert.Equal(new[] { "aaa bbbbb", " c" }, CTechDetailsRows.Rows("aaa bbbbb  c", 10, Measure));
	}
}
