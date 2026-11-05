using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OpenHOI3.Sim;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// The original's text writer for a save, its layout kept to the byte: lines end in CR LF; a key is indented a tab for
/// each level it is in, the top level at column 0; a block's brace stands on a line of its own at its key's
/// indentation; and the root opens no brace and still closes one, so a save ends with one brace too many. A list of
/// numbers stands at column 0, each number followed by a space, with its closing brace on the same line after the
/// indentation. See <c>docs/mechanics/savegame.md</c>, section 3.
/// </summary>
public sealed class CSaveTextWriter
{
	private readonly StringBuilder _text = new();

	/// <summary>The level the next key is written at, less one: -1 before the root is opened.</summary>
	private int _depth = -1;

	/// <summary>The text written so far.</summary>
	public string Text => _text.ToString();

	/// <summary>
	/// Writes the text out byte for byte as it was read: the readers take a file's Windows-1252 bytes as Latin-1, which
	/// keeps every byte, so Latin-1 gives the same bytes back.
	/// </summary>
	public void SaveTo(string path)
	{
		File.WriteAllBytes(path, Encoding.Latin1.GetBytes(_text.ToString()));
	}

	/// <summary>Opens the root: no brace, and the keys after it at column 0.</summary>
	public void OpenRoot()
	{
		_depth = 1;
	}

	/// <summary>Closes the root with the brace it never opened.</summary>
	public void CloseRoot()
	{
		End();
	}

	/// <summary>A key and a block opened under it.</summary>
	public void Begin(string key)
	{
		Key(key);
		OpenBrace();
	}

	/// <summary>A block with no key: a list's anonymous entries.</summary>
	public void BeginAnonymous()
	{
		Indent();
		_text.Append('{').Append("\r\n");
		_depth++;
	}

	/// <summary>
	/// A line holding only the indentation: the empty key the original writes before the first of the finished battles,
	/// whose brace then follows on a line of its own.
	/// </summary>
	public void IndentationLine()
	{
		Indent();
		_text.Append("\r\n");
	}

	/// <summary>Closes the block last opened: its brace at its key's indentation.</summary>
	public void End()
	{
		_depth--;
		Indent();
		_text.Append('}').Append("\r\n");
	}

	public void Int(string key, long value)
	{
		Key(key);
		Line(value.ToString(CultureInfo.InvariantCulture));
	}

	/// <summary>A thousandths figure with three places: <c>55.000</c>, <c>-0.002</c>.</summary>
	public void Fixed(string key, CFixedPoint value)
	{
		Key(key);
		Line(FixedText(value.Raw));
	}

	public void Bool(string key, bool value)
	{
		Key(key);
		Line(value ? "yes" : "no");
	}

	/// <summary>A quoted text: a tag, a name, a date.</summary>
	public void Quoted(string key, string value)
	{
		Key(key);
		Line("\"" + value + "\"");
	}

	/// <summary>A bare word: a law's name, a government.</summary>
	public void Word(string key, string value)
	{
		Key(key);
		Line(value);
	}

	/// <summary>A date with its hour, quoted: <c>"1936.1.7.22"</c>.</summary>
	public void Date(string key, long hours)
	{
		Quoted(key, CSaveValues.HoursText(hours));
	}

	/// <summary>A date with its hour, or the original's null date, <c>"1.1.1.0"</c>, where there is none.</summary>
	public void DateOrNull(string key, long? hours)
	{
		Quoted(key, hours is { } at ? CSaveValues.HoursText(at) : "1.1.1.0");
	}

	/// <summary>An id pair, the serial first: <c>id = { id = N type = T }</c>.</summary>
	public void IdPair(string key, int serial, int type)
	{
		Begin(key);
		Int("id", serial);
		Int("type", type);
		End();
	}

	/// <summary>A list of whole numbers at column 0, its brace closing on the same line.</summary>
	public void Numbers(string key, IEnumerable<long> values)
	{
		Begin(key);
		foreach (long value in values)
		{
			_text.Append(value.ToString(CultureInfo.InvariantCulture)).Append(' ');
		}
		CloseList();
	}

	/// <summary>A list of bare words at column 0, its brace closing on the same line: a leader's traits, a template's types.</summary>
	public void Words(string key, IEnumerable<string> values)
	{
		Begin(key);
		foreach (string value in values)
		{
			_text.Append(value).Append(' ');
		}
		CloseList();
	}

	/// <summary>The serial and the type of an id pair: <c>key = { id = N type = T }</c>.</summary>
	public void IdPair(string key, (int Serial, int Type) pair)
	{
		IdPair(key, pair.Serial, pair.Type);
	}

	/// <summary>An empty line, as the original leaves after a patrol's fleet.</summary>
	public void BlankLine()
	{
		_text.Append("\r\n");
	}

	/// <summary>A list of thousandths figures at column 0, its brace closing on the same line.</summary>
	public void Figures(string key, IEnumerable<CFixedPoint> values)
	{
		Begin(key);
		foreach (CFixedPoint value in values)
		{
			_text.Append(FixedText(value.Raw)).Append(' ');
		}
		CloseList();
	}

	/// <summary>A list indented at its block's depth, each value followed by a space: how a country's shares are written.</summary>
	public void IndentedList(string key, IEnumerable<string> values)
	{
		Begin(key);
		Indent();
		foreach (string value in values)
		{
			_text.Append(value).Append(' ');
		}
		CloseList();
	}

	/// <summary>A building's pair, the level in working order first: a space before each figure.</summary>
	public void Pair(string key, CFixedPoint first, CFixedPoint second)
	{
		Begin(key);
		_text.Append(' ').Append(FixedText(first.Raw)).Append(' ').Append(FixedText(second.Raw)).Append(' ');
		CloseList();
	}

	private void CloseList()
	{
		_depth--;
		Indent();
		_text.Append('}').Append("\r\n");
	}

	/// <summary>The key, indented, and its <c>=</c>.</summary>
	public void Key(string key)
	{
		Indent();
		_text.Append(key).Append('=');
	}

	/// <summary>The key at column 0 whatever the level: how the original writes a country's <c>organization</c> and <c>popularity</c>.</summary>
	public void KeyAtColumnZero(string key)
	{
		_text.Append(key).Append('=');
	}

	/// <summary>A brace opened after a key already written.</summary>
	public void OpenBrace()
	{
		_text.Append("\r\n");
		Indent();
		_text.Append('{').Append("\r\n");
		_depth++;
	}

	/// <summary>A value after a key already written, and the end of its line.</summary>
	public void Line(string value)
	{
		_text.Append(value).Append("\r\n");
	}

	/// <summary>Text written as it stands, with no indentation and no line's end.</summary>
	public void Raw(string text)
	{
		_text.Append(text);
	}

	private void Indent()
	{
		for (int i = 0; i < _depth - 1; i++)
		{
			_text.Append('\t');
		}
	}

	/// <summary>The original's thousandths: the whole part, a point and three places, the sign kept below one.</summary>
	public static string FixedText(long raw)
	{
		string sign = raw < 0 ? "-" : "";
		long magnitude = Math.Abs(raw);
		return sign + (magnitude / 1000).ToString(CultureInfo.InvariantCulture) + "."
			+ (magnitude % 1000).ToString("000", CultureInfo.InvariantCulture);
	}
}
