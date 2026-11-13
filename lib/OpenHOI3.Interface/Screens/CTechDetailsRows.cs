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

	/// <summary>The character whose width a line is given as room to spare before it breaks.</summary>
	private const string Spare = "O";

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
	/// The text laid out and split at its line breaks, every piece kept - an empty one between two breaks and the empty
	/// tail after a last one among them. A line breaks where it and one <c>O</c> are wider than the width: at the last
	/// space that leaves it narrow enough, the space dropped, or where no space does - its first word too wide - after
	/// the most characters that fit. A colour escape measures nothing, and no colour is carried onto the next line.
	/// </summary>
	public static List<string> Layout(string text, float width, Func<string, float> measure)
	{
		float spare = measure(Spare);
		bool Fits(string line) => measure(CColouredText.Strip(line)) + spare <= width;
		var pieces = new List<string>();
		foreach (string paragraph in text.Split('\n'))
		{
			string line = paragraph;
			while (!Fits(line))
			{
				int space = -1;
				for (int at = line.IndexOf(' '); at > 0; at = line.IndexOf(' ', at + 1))
				{
					if (!Fits(line[..at]))
					{
						break;
					}
					space = at;
				}
				if (space > 0)
				{
					pieces.Add(line[..space]);
					line = line[(space + 1)..];
					continue;
				}
				int kept = 1;
				while (kept < line.Length && Fits(line[..(kept + 1)]))
				{
					kept++;
				}
				pieces.Add(line[..kept]);
				line = line[kept..];
			}
			pieces.Add(line);
		}
		return pieces;
	}
}
