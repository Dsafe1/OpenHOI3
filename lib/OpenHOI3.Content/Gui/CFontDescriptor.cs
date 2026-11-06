using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace OpenHOI3.Content.Gui;

/// <summary>
/// What one of the original's font descriptors says, <c>gfx/fonts/&lt;name&gt;.fnt</c>:
/// the typeface its glyphs were drawn from, and how big they came out. The glyph
/// texture beside it is not used. The typeface is drawn afresh at whatever size the
/// screen needs, and this is the measure of the size the text was meant to be. See
/// <c>docs/mechanics/interface.md</c>, section 6.
/// </summary>
public sealed class CFontDescriptor
{
	/// <summary>The characters a font's size is taken from: what the interface mostly writes.</summary>
	public const string Reference = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

	private readonly Dictionary<int, int> _advances = new();
	private readonly Dictionary<int, int> _widths = new();
	private readonly Dictionary<int, CFontGlyph> _glyphs = new();

	/// <summary>A character's glyph - its box, offsets and advance - or nothing for one the font does not have.</summary>
	public CFontGlyph? Glyph(char character)
	{
		return _glyphs.TryGetValue(character, out CFontGlyph glyph) ? glyph : null;
	}

	/// <summary>The typeface's name, as the tool that made the glyphs was given it: <c>Arial</c>.</summary>
	public string Face { get; private set; } = "";

	public bool Bold { get; private set; }

	public bool Italic { get; private set; }

	/// <summary>From the top of one line of text to the top of the next, in the file's pixels.</summary>
	public int LineHeight { get; private set; }

	/// <summary>From the top of a line down to the line its letters stand on.</summary>
	public int Base { get; private set; }

	/// <summary>How many characters it gives a width for.</summary>
	public int Count => _advances.Count;

	public static CFontDescriptor? Load(string absolutePath, Action<string>? warn = null)
	{
		try
		{
			return Parse(Encoding.Latin1.GetString(File.ReadAllBytes(absolutePath)));
		}
		catch (IOException e)
		{
			(warn ?? CDiagnostics.Warn)($"Could not read '{absolutePath}': {e.Message}");
			return null;
		}
	}

	public static CFontDescriptor Parse(string text)
	{
		var descriptor = new CFontDescriptor();
		foreach (string raw in text.Split('\n'))
		{
			string line = raw.Trim();
			if (line.StartsWith("info ", StringComparison.Ordinal))
			{
				descriptor.Face = Value(line, "face");
				descriptor.Bold = Number(line, "bold") != 0;
				descriptor.Italic = Number(line, "italic") != 0;
			}
			else if (line.StartsWith("common ", StringComparison.Ordinal))
			{
				descriptor.LineHeight = Number(line, "lineHeight");
				descriptor.Base = Number(line, "base");
			}
			else if (line.StartsWith("char ", StringComparison.Ordinal))
			{
				// The original takes these numbers by their place in the line and never
				// reads the names: the id first, and how far the pen moves eighth.
				List<int> numbers = Numbers(line);
				if (numbers.Count >= 8)
				{
					descriptor._advances[numbers[0]] = numbers[7];
					descriptor._widths[numbers[0]] = numbers[3];
					descriptor._glyphs[numbers[0]] = new CFontGlyph(numbers[3], numbers[4], numbers[5], numbers[6], numbers[7]);
				}
			}
		}
		return descriptor;
	}

	/// <summary>How far the pen moves for a character, or nothing for one the font does not have.</summary>
	public int Advance(char character)
	{
		return _advances.GetValueOrDefault(character);
	}

	/// <summary>
	/// How wide the original's glyph of a character is drawn, or nothing for one the font does not have: what a letter of
	/// a font of fixed width is narrowed to, so that it keeps to its cell (<c>docs/mechanics/interface.md</c>, section 6).
	/// </summary>
	public int GlyphWidth(char character)
	{
		return _widths.GetValueOrDefault(character);
	}

	/// <summary>
	/// The advance every character of the font shares, or nought where they differ: a font of fixed width, such as the
	/// date widget's <c>timefont</c>, whose characters each stand in a cell of the picture behind them
	/// (<c>docs/mechanics/interface.md</c>, section 6).
	/// </summary>
	public int FixedAdvance
	{
		get
		{
			int shared = 0;
			foreach (int advance in _advances.Values)
			{
				if (shared == 0)
				{
					shared = advance;
				}
				else if (advance != shared)
				{
					return 0;
				}
			}
			return shared;
		}
	}

	/// <summary>
	/// How wide a text comes out in the original's glyphs, in the file's pixels: the sum
	/// of how far the pen moves. The adjustments between particular pairs of letters are
	/// left out.
	/// </summary>
	public int Width(string text)
	{
		int width = 0;
		foreach (char character in text)
		{
			width += Advance(character);
		}
		return width;
	}

	/// <summary>The characters of <see cref="Reference"/> the font has, which is what to measure it by.</summary>
	public string Measurable()
	{
		var present = new StringBuilder();
		foreach (char character in Reference)
		{
			if (_advances.ContainsKey(character))
			{
				present.Append(character);
			}
		}
		return present.ToString();
	}

	/// <summary>The value after <c>key=</c> on a line, without its quotes; empty when the key is not there.</summary>
	private static string Value(string line, string key)
	{
		int at = line.IndexOf(" " + key + "=", StringComparison.Ordinal);
		if (at < 0)
		{
			return "";
		}
		int start = at + key.Length + 2;
		if (start < line.Length && line[start] == '"')
		{
			int close = line.IndexOf('"', start + 1);
			return close < 0 ? line[(start + 1)..] : line[(start + 1)..close];
		}
		int end = line.IndexOf(' ', start);
		return end < 0 ? line[start..] : line[start..end];
	}

	private static int Number(string line, string key)
	{
		return int.TryParse(Value(line, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
			? number : 0;
	}

	/// <summary>Every whole number that follows an equals sign on a line, in order.</summary>
	private static List<int> Numbers(string line)
	{
		var numbers = new List<int>();
		int index = 0;
		while ((index = line.IndexOf('=', index)) >= 0)
		{
			int start = ++index;
			while (index < line.Length && (char.IsDigit(line[index]) || (index == start && line[index] == '-')))
			{
				index++;
			}
			if (index > start && int.TryParse(line.AsSpan(start, index - start), NumberStyles.Integer,
				CultureInfo.InvariantCulture, out int number))
			{
				numbers.Add(number);
			}
		}
		return numbers;
	}
}
