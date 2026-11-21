using System;
using System.Text;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// How a text on the map is laid out, the original's font's draw in the 3D world: cut to 99 characters, measured as
/// lines broken at 320 font pixels, and then **drawn in one row** starting half its widest line left of its point - the
/// letters loop has no case for a line's end, so a long text runs off to the right. See
/// <c>docs/mechanics/messages.md</c>, section 6.
/// </summary>
public static class CMapTextLayout
{
	/// <summary>A text of this many characters or more is cut to one fewer.</summary>
	public const int MostCharacters = 100;

	/// <summary>The width a line is measured against, in font pixels.</summary>
	public const float LineWidth = 320f;

	/// <summary>How far above the text's point a line's top is, in font pixels.</summary>
	public const float LineTop = 15f;

	/// <summary>A font pixel in the map's world units, at a scale of one.</summary>
	public const float FontPixel = 0.05f;

	/// <summary>The text as it is taken: cut to 99 characters where it has 100 or more.</summary>
	public static string Cut(string text) => text.Length >= MostCharacters ? text[..(MostCharacters - 1)] : text;

	/// <summary>What is drawn, in one row: the text with its ends of line gone, which have no glyph.</summary>
	public static string Row(string text)
	{
		var row = new StringBuilder(text.Length);
		foreach (char character in text)
		{
			if (character != '\n' && character != '\r')
			{
				row.Append(character);
			}
		}
		return row.ToString();
	}

	/// <summary>
	/// The widest line as the original measures them: at each space, where the line with the word before it and that
	/// space is strictly wider than 320, the line ends before the word; an end of line ends one; the last word is never
	/// tested.
	/// </summary>
	/// <param name="measure">How wide a run is in the font, colour escapes taking no room.</param>
	public static float WidestLine(string text, Func<string, float> measure)
	{
		float widest = 0f;
		var line = new StringBuilder();
		var word = new StringBuilder();
		void EndLine()
		{
			widest = Math.Max(widest, measure(line.ToString()));
			line.Clear();
		}
		foreach (char character in text)
		{
			if (character == '\n' || character == '\r')
			{
				line.Append(word);
				word.Clear();
				EndLine();
				continue;
			}
			word.Append(character);
			if (character != ' ')
			{
				continue;
			}
			if (measure(line.ToString() + word) > LineWidth)
			{
				EndLine();
			}
			line.Append(word);
			word.Clear();
		}
		line.Append(word);
		EndLine();
		return widest;
	}

	/// <summary>Where the row's first letter starts, from the text's point: half the widest line to the left.</summary>
	public static float Start(float widestLine) => -(widestLine / 2f);
}
