using System;
using System.Collections.Generic;
using OpenHOI3.Interface.Text;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The rows of the technology panel's <c>effects</c> list, as the original cuts them: the text of what the levels do laid
/// out at the row's text box width and then split at its line breaks, a piece under two characters dropped, each other
/// piece a row as it is, and a row made after it only where another piece follows - so the list always ends in an empty
/// row. See <c>docs/mechanics/technologyscreen.md</c>, section 7.
/// </summary>
public static class CTechDetailsRows
{
	/// <summary>A piece shorter than this is no row. The original counts bytes; Windows-1252 has one a character.</summary>
	public const int ShortestRow = 2;

	/// <summary>How wide the original makes a row it has put a piece in, whatever its window's file says.</summary>
	public const float FilledRowWidth = 180f;

	/// <summary>
	/// A row's size once the list is made: a row a piece was put in is <see cref="FilledRowWidth"/> wide and as tall as
	/// its text box says its text is - its lines times its font's line height, and its border above and below; a row
	/// left empty keeps its window's own size. Every piece is two characters or more, so a row is empty only where
	/// nothing was put in it.
	/// </summary>
	public static (float Width, float Height) RowSize(string row, float windowWidth, float windowHeight, float textHeight)
	{
		return row.Length == 0 ? (windowWidth, windowHeight) : (FilledRowWidth, textHeight);
	}

	/// <summary>The rows, the first made before any piece is looked at.</summary>
	public static List<string> Rows(string text, float width, Func<string, float> measure)
	{
		List<string> pieces = Layout(text, width, measure);
		var rows = new List<string> { "" };
		for (int index = 0; index < pieces.Count; index++)
		{
			if (pieces[index].Length < ShortestRow)
			{
				continue;
			}
			rows[^1] = pieces[index];
			if (index + 1 < pieces.Count)
			{
				rows.Add("");
			}
		}
		return rows;
	}

	/// <summary>
	/// The text laid out by the original's text layout at the width and split at its line breaks, every piece kept - an
	/// empty one between two breaks and the empty tail after a last one among them (<see cref="CTextLayoutLineBreak"/>).
	/// </summary>
	public static List<string> Layout(string text, float width, Func<string, float> measure)
	{
		return CTextLayoutLineBreak.Break(text, width, measure);
	}
}
