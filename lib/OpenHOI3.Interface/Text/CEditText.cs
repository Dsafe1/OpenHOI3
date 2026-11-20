using System;
using System.Collections.Generic;

namespace OpenHOI3.Interface.Text;

/// <summary>
/// What an edit box holds and does with what is typed, as the original's: its text, the caret and the line the box
/// shows, the characters it refuses, its cap of 250, the six keys and the caret's blink. The engine draws it and hands
/// it keys; nothing here knows of either. See <c>docs/mechanics/interface.md</c>, section 8, and
/// <c>reversing/findings/FINDINGS-editbox.md</c>.
/// </summary>
public sealed class CEditText
{
	/// <summary>The most a box holds [exe]: a character is refused once the text already holds this many.</summary>
	public const int MaxLength = 250;

	/// <summary>What a box refuses where its font's <c>effect</c> is on, the default [exe]: <c>" § ¤ £ @ { }</c>.</summary>
	public const string EffectRefused = "\"§¤£@{}";

	/// <summary>Backspace and Enter as the characters the box is handed.</summary>
	public const char Backspace = '\b';

	/// <summary>What this box refuses besides, as a name's box refuses a quotation mark.</summary>
	public string AlsoRefused { get; set; } = "";

	/// <summary>
	/// The most lines the box holds as it wraps, or none: a character typed that makes the text wrap past it is erased again
	/// at once, as the original's layout backspaces after every insert while its lines are more than its most - the unit
	/// panel's name holds one (<c>reversing/findings/FINDINGS-unitrename.md</c>, section 5). Text set by code is not fitted.
	/// </summary>
	public int? MaxLines { get; set; }

	/// <summary>The caret to the text's start, as the unit panel's name box puts it when it is filled.</summary>
	public void CaretToStart()
	{
		_caret = 0;
		StoreLineLength();
	}
	public const char Enter = '\r';
	private const char Tab = '\t';

	/// <summary>Half a second on and half off [exe].</summary>
	private const double HalfPeriod = 0.5;

	private readonly Func<string, float> _width;
	private readonly float _innerWidth;
	private readonly bool _fontEffect;
	private string _text = "";
	private int _caret;
	private int _storedLineLength;
	private double _clock;
	private int _phase;

	/// <param name="width">The width of a text in the box's font.</param>
	/// <param name="innerWidth">The box's <c>size</c> across less twice its <c>borderSize</c>: what a line is wrapped to.</param>
	/// <param name="fontEffect">The box's font's <c>effect</c>, on unless the font says <c>effect = no</c>.</param>
	public CEditText(Func<string, float> width, float innerWidth, bool fontEffect)
	{
		_width = width;
		_innerWidth = innerWidth;
		_fontEffect = fontEffect;
	}

	/// <summary>
	/// The text. Set by code it is never cut at the cap, and the caret goes to its end (*not established*: where the
	/// original's leaves it was not read).
	/// </summary>
	public string Text
	{
		get => _text;
		set
		{
			_text = value;
			_caret = value.Length;
			StoreLineLength();
		}
	}

	/// <summary>The caret, as a place in the text: before the character of that index.</summary>
	public int Caret => _caret;

	/// <summary>Whether the caret is drawn now: by its blink, and hidden from a key taken until it next comes on.</summary>
	public bool CaretShown { get; private set; } = true;

	/// <summary>
	/// The text's lines as the box's layout breaks them, the original's text layout's rule
	/// (<see cref="CTextLayoutLineBreak"/>): at each newline, and where a line with one <c>O</c> more is wider than the
	/// box, at the last space before the first character at which it reaches the box less the <c>O</c> - the space
	/// belonging to neither line - else after that character. Each line by its start and its length.
	/// </summary>
	public IReadOnlyList<(int Start, int Length)> Lines()
	{
		return CTextLayoutLineBreak.Ranges(_text, _innerWidth, _width);
	}

	/// <summary>The line the caret is on, by its place among the lines: the last that begins at or before it.</summary>
	public int CaretRow
	{
		get
		{
			IReadOnlyList<(int Start, int Length)> lines = Lines();
			int row = 0;
			for (int i = 1; i < lines.Count; i++)
			{
				if (lines[i].Start <= _caret)
				{
					row = i;
				}
			}
			return row;
		}
	}

	/// <summary>What the box shows: the line the caret is on, and only that line [exe].</summary>
	public string ShownLine
	{
		get
		{
			(int start, int length) = Lines()[CaretRow];
			return _text.Substring(start, length);
		}
	}

	/// <summary>The caret's place across its line, in characters.</summary>
	public int CaretColumn => _caret - Lines()[CaretRow].Start;

	/// <summary>
	/// A character typed: inserted at the caret, the caret after it - or Backspace, or Enter, which the box takes and
	/// does nothing with. Answers whether the box took it; one it refuses - Tab, the font's seven where its
	/// <c>effect</c> is on - goes on to whatever else takes keys. Past the cap a character is taken and not inserted.
	/// With Ctrl held no character reaches a box. A typed <c>'</c> also moves the caret right, and a <c>%</c> with no
	/// Shift left, the original's key test taking their codes for the arrows'.
	/// </summary>
	public bool Type(char character, bool shift, bool ctrl)
	{
		if (ctrl || character == Tab || AlsoRefused.Contains(character) || (_fontEffect && EffectRefused.Contains(character)))
		{
			return false;
		}
		Typed();
		switch (character)
		{
			case Backspace:
				EraseBack();
				return true;
			case Enter:
				return true;
		}
		if (_text.Length < MaxLength)
		{
			_text = _text.Insert(_caret, character.ToString());
			_caret++;
			StoreLineLength();
			// Backspace while the text wraps past the most lines it may have: the character just typed goes again, and at
			// the very start - a name set too wide for the box - the one after the caret, eating it from the front [exe].
			while (MaxLines is { } most && _text.Length > 0 && Lines().Count > most)
			{
				EraseBack();
			}
		}
		if (!shift)
		{
			if (character == '\'')
			{
				Key(EEditKey.Right, shift, ctrl);
			}
			else if (character == '%')
			{
				Key(EEditKey.Left, shift, ctrl);
			}
		}
		return true;
	}

	/// <summary>
	/// Text pasted at the caret, a character at a time by the rules of typing: one the box refuses is left out, a line
	/// break and any other control character with it, and past the cap nothing more goes in. The two characters that
	/// move the caret when typed do not when pasted. Answers how many characters went in. *Ours* - the original's box
	/// has no clipboard (<c>docs/mechanics/interface.md</c>, section 8).
	/// </summary>
	public int Paste(string text)
	{
		int before = _text.Length;
		foreach (char character in text)
		{
			if (char.IsControl(character))
			{
				continue;
			}
			// As typed with Shift held: the apostrophe's and the per cent's steps of the caret are a typed key's alone.
			Type(character, shift: true, ctrl: false);
		}
		return _text.Length - before;
	}

	/// <summary>
	/// A key: Left and Right with neither Ctrl nor Shift a character either way, Home to its line's start, End to the
	/// line's length as last stored by an edit, Delete without Shift erasing the character after the caret. Answers
	/// whether the box took it; any other key, or one of these held otherwise, goes on.
	/// </summary>
	public bool Key(EEditKey key, bool shift, bool ctrl)
	{
		switch (key)
		{
			case EEditKey.Left when !shift && !ctrl:
				_caret = Math.Max(0, _caret - 1);
				break;
			case EEditKey.Right when !shift && !ctrl:
				_caret = Math.Min(_text.Length, _caret + 1);
				break;
			case EEditKey.Home:
				_caret = Lines()[CaretRow].Start;
				break;
			case EEditKey.End:
				_caret = Math.Min(_text.Length, Lines()[CaretRow].Start + _storedLineLength);
				break;
			case EEditKey.Delete when !shift:
				if (_caret < _text.Length)
				{
					_text = _text.Remove(_caret, 1);
					StoreLineLength();
				}
				break;
			default:
				return false;
		}
		Typed();
		return true;
	}

	/// <summary>The byte before the caret, the caret back one - and at the very start the byte after it.</summary>
	private void EraseBack()
	{
		if (_caret > 0)
		{
			_caret--;
			_text = _text.Remove(_caret, 1);
		}
		else if (_text.Length > 0)
		{
			_text = _text.Remove(0, 1);
		}
		StoreLineLength();
	}

	/// <summary>The caret's line's length, kept as an edit leaves it: what End goes by, though the caret moves on.</summary>
	private void StoreLineLength()
	{
		_storedLineLength = Lines()[CaretRow].Length;
	}

	/// <summary>A key taken hides the caret until its blink next brings it back.</summary>
	private void Typed()
	{
		CaretShown = false;
	}

	/// <summary>
	/// A frame's time for the blink, while the box has the keyboard: under half a second the first phase, between half
	/// a second and one the second, and at exactly a half or at one or more the clock starts again. Into the second
	/// phase the caret hides, back into the first it shows.
	/// </summary>
	public void Tick(double seconds)
	{
		_clock += seconds;
		int phase;
		if (_clock < HalfPeriod)
		{
			phase = 0;
		}
		else if (_clock > HalfPeriod && _clock < 2 * HalfPeriod)
		{
			phase = 1;
		}
		else
		{
			_clock = 0;
			phase = 0;
		}
		if (phase != _phase)
		{
			CaretShown = phase == 0;
			_phase = phase;
		}
	}
}
