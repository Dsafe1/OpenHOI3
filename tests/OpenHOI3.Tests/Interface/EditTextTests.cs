using OpenHOI3.Interface.Text;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// An edit box's typing as <c>docs/mechanics/interface.md</c>, section 8, has it, in a font of ten units a character
/// and a box of a hundred inside: one <c>O</c> less leaves nine characters a line.
/// </summary>
public class EditTextTests
{
	private static CEditText Box(string text = "", bool effect = true)
	{
		return new CEditText(s => s.Length * 10f, 100f, effect) { Text = text };
	}

	[Fact]
	public void Pasted_text_goes_in_at_the_caret_by_the_rules_of_typing()
	{
		CEditText box = Box("ab");
		box.Key(EEditKey.Left, shift: false, ctrl: false);

		// A line break and a character the font refuses are left out; the apostrophe does not move the caret.
		Assert.Equal(9, box.Paste("127.0.0.1\r\n@"));
		Assert.Equal("a127.0.0.1b", box.Text);
		Assert.Equal(10, box.Caret);
		box.Paste("'x");
		Assert.Equal("a127.0.0.1'xb", box.Text);
	}

	[Fact]
	public void Pasting_stops_at_the_cap()
	{
		CEditText box = Box(new string('a', CEditText.MaxLength - 2));
		Assert.Equal(2, box.Paste("12345"));
		Assert.Equal(CEditText.MaxLength, box.Text.Length);
	}

	[Fact]
	public void A_box_of_one_line_erases_at_once_a_character_that_would_wrap()
	{
		CEditText box = Box("Panzerkor");
		box.MaxLines = 1;
		Assert.True(box.Type('p', shift: false, ctrl: false));
		Assert.Equal("Panzerkor", box.Text);
		Assert.Equal(9, box.Caret);
	}

	[Fact]
	public void A_name_too_wide_for_its_one_line_is_eaten_from_the_front_as_typing_begins_at_its_start()
	{
		// Filled by code past the one line, the caret at its start: the character typed goes, and then the one after the
		// caret, until the name fits (FINDINGS-unitrename.md, section 5).
		CEditText box = Box("1st Panzer Army");
		box.MaxLines = 1;
		box.CaretToStart();
		box.Type('X', shift: true, ctrl: false);
		// Nine characters to the line: "X" goes, then "1st Pa" from the front, leaving the last nine.
		Assert.Single(box.Lines());
		Assert.Equal("nzer Army", box.Text);
		Assert.Equal(0, box.Caret);
	}

	[Fact]
	public void A_character_goes_in_at_the_caret_and_the_caret_after_it()
	{
		CEditText box = Box("Germany");
		Assert.True(box.Key(EEditKey.Home, shift: false, ctrl: false));
		Assert.True(box.Type('X', shift: true, ctrl: false));
		Assert.Equal("XGermany", box.Text);
		Assert.Equal(1, box.Caret);
	}

	[Fact]
	public void Tab_and_with_the_fonts_effect_seven_characters_are_refused_and_go_on_and_with_ctrl_none_reaches_it()
	{
		CEditText box = Box("a");
		Assert.False(box.Type('\t', false, false));
		foreach (char refused in "\"§¤£@{}")
		{
			Assert.False(box.Type(refused, false, false));
		}
		Assert.False(box.Type('b', false, ctrl: true));
		Assert.Equal("a", box.Text);
		// A font that says effect = no takes them.
		CEditText plain = Box("a", effect: false);
		Assert.True(plain.Type('@', true, false));
		Assert.Equal("a@", plain.Text);
	}

	[Fact]
	public void A_character_past_250_is_taken_and_not_inserted_and_text_set_by_code_is_not_cut()
	{
		CEditText box = Box(new string('a', 249));
		Assert.True(box.Type('b', false, false));
		Assert.True(box.Type('c', false, false));
		Assert.Equal(250, box.Text.Length);
		Assert.EndsWith("b", box.Text);
		box.Text = new string('x', 300);
		Assert.Equal(300, box.Text.Length);
	}

	[Fact]
	public void Backspace_erases_before_the_caret_and_at_the_very_start_after_it()
	{
		CEditText box = Box("abc");
		Assert.True(box.Type(CEditText.Backspace, false, false));
		Assert.Equal("ab", box.Text);
		box.Key(EEditKey.Home, false, false);
		box.Type(CEditText.Backspace, false, false);
		Assert.Equal("b", box.Text);
		Assert.Equal(0, box.Caret);
	}

	[Fact]
	public void Delete_erases_after_the_caret_and_held_with_shift_goes_on_and_enter_is_taken_and_does_nothing()
	{
		CEditText box = Box("abc");
		box.Key(EEditKey.Left, false, false);
		Assert.True(box.Key(EEditKey.Delete, false, false));
		Assert.Equal("ab", box.Text);
		Assert.Equal(2, box.Caret);
		Assert.True(box.Key(EEditKey.Delete, false, false));
		Assert.Equal("ab", box.Text);
		Assert.False(box.Key(EEditKey.Delete, shift: true, false));
		Assert.False(box.Key(EEditKey.Left, shift: true, false));
		Assert.False(box.Key(EEditKey.Right, false, ctrl: true));
		Assert.True(box.Type(CEditText.Enter, false, false));
		Assert.Equal("ab", box.Text);
	}

	[Fact]
	public void An_apostrophe_also_moves_the_caret_right_and_a_percent_with_no_shift_left()
	{
		CEditText box = Box("ab");
		box.Key(EEditKey.Home, false, false);
		box.Type('\'', false, false);
		Assert.Equal("'ab", box.Text);
		Assert.Equal(2, box.Caret);
		box.Type('%', shift: true, false);
		Assert.Equal(3, box.Caret);
		box.Type('%', shift: false, false);
		Assert.Equal("'a%%b", box.Text);
		Assert.Equal(3, box.Caret);
	}

	[Fact]
	public void A_long_text_wraps_by_the_text_layouts_rule_and_the_box_shows_the_carets_line_alone()
	{
		CEditText box = Box("Deutsches Reich 1936");
		// The first nine characters reach the box less an O; "Deutsches" and an O just fit, so the line is cut at the last
		// space before the ninth character's end - the space after "Deutsches" - and the space belongs to neither line.
		Assert.Equal(new[] { (0, 9), (10, 5), (16, 4) }, box.Lines());
		Assert.Equal("1936", box.ShownLine);
		Assert.Equal(2, box.CaretRow);
		box.Key(EEditKey.Home, false, false);
		box.Key(EEditKey.Left, false, false);
		Assert.Equal("Reich", box.ShownLine);
		Assert.Equal(5, box.CaretColumn);
		// No space to break at: after the first characters to reach the box less an O.
		Assert.Equal(new[] { (0, 9), (9, 3) }, Box("abcdefghijkl").Lines());
	}

	[Fact]
	public void A_line_kept_whole_may_be_the_box_less_an_O_where_one_cut_off_is_narrower()
	{
		// Nine characters and an O are the hundred: the line stands. Ten are not: cut at the space, the word on its own.
		Assert.Single(Box("abcd efgh").Lines());
		Assert.Equal(new[] { (0, 4), (5, 5) }, Box("abcd efghi").Lines());
	}

	[Fact]
	public void End_goes_by_the_lines_length_an_edit_last_stored()
	{
		CEditText box = Box("Deutsches Reich 1936");
		// The caret's line was "1936", four long; on the first line End takes the caret four in.
		box.Key(EEditKey.Home, false, false);
		for (int i = 0; i < 8; i++)
		{
			box.Key(EEditKey.Left, false, false);
		}
		Assert.Equal(0, box.CaretRow);
		box.Key(EEditKey.Home, false, false);
		box.Key(EEditKey.End, false, false);
		Assert.Equal(4, box.Caret);
	}

	[Fact]
	public void The_caret_blinks_half_a_second_on_and_off_and_hides_after_a_key_until_it_next_comes_on()
	{
		CEditText box = Box("a");
		Assert.True(box.CaretShown);
		box.Tick(0.3);
		Assert.True(box.CaretShown);
		box.Tick(0.3);
		Assert.False(box.CaretShown);
		box.Tick(0.5);
		Assert.True(box.CaretShown);
		box.Type('b', false, false);
		Assert.False(box.CaretShown);
		box.Tick(0.2);
		Assert.False(box.CaretShown);
		box.Tick(0.4);
		Assert.False(box.CaretShown);
		box.Tick(0.5);
		Assert.True(box.CaretShown);
	}

	[Fact]
	public void A_typed_character_is_the_byte_windows_1252_gives_it()
	{
		Assert.Equal('a', CWindows1252.FromUnicode('a'));
		Assert.Equal('é', CWindows1252.FromUnicode('é'));
		Assert.Equal((char)0x80, CWindows1252.FromUnicode('€'));
		Assert.Null(CWindows1252.FromUnicode('Ж'));
	}

	[Fact]
	public void A_text_held_as_its_bytes_is_drawn_with_windows_1252s_quotes_and_dashes()
	{
		// "Foch’s remark" and “ ” as the localisation holds them, bytes 0x92, 0x93 and 0x94; 0x81 has no character and stays.
		Assert.Equal("Foch’s “peace” – \u0081", CWindows1252.ToDisplay("Foch\u0092s \u0093peace\u0094 \u0096 \u0081"));
		Assert.Same("plain", CWindows1252.ToDisplay("plain"));
	}
}
