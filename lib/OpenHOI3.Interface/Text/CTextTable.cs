using System;
using System.Collections.Generic;
using System.Text;

namespace OpenHOI3.Interface.Text;

/// <summary>
/// Columns in a text drawn in a proportional font, made with spaces: a tooltip has no columns of its own. Each column
/// is as wide as its widest cell and starts a gap of two spaces after the one before; a cell is reached by the number
/// of spaces that brings the line nearest to where the cell should start - never fewer than one after the first
/// column. So a column lands within half a space of where it was aimed.
///
/// **Every width is measured on the line as it will be drawn**: cut into its runs of one colour, each run measured
/// whole and the widths added, which is how a text box draws a line (<see cref="CColouredText.Parse"/>). A run of
/// spaces is not as wide as so many single spaces in a font that measures a text whole - twelve came out a sixth
/// narrower in the tooltip's - so nothing here multiplies the width of one.
/// </summary>
public static class CTextTable
{
	/// <param name="rows">The cells of each row; a row with fewer cells ends early.</param>
	/// <param name="indent">Written before every row.</param>
	/// <param name="measure">How wide a text of one colour is in the font it will be drawn in.</param>
	/// <param name="rightAligned">The columns, counted from nought, whose cells end together.</param>
	/// <returns>The rows joined by line breaks, with none after the last.</returns>
	public static string Render(IReadOnlyList<IReadOnlyList<string>> rows, string indent, Func<string, float> measure,
		params int[] rightAligned)
	{
		if (measure(" ") <= 0f)
		{
			// A font that does not measure: every character counts one.
			measure = text => text.Length;
		}
		float Width(string text)
		{
			float width = 0f;
			foreach (CTextRun run in CColouredText.Parse(text))
			{
				width += measure(run.Text);
			}
			return width;
		}

		var widths = new List<float>();
		foreach (IReadOnlyList<string> row in rows)
		{
			for (int column = 0; column < row.Count; column++)
			{
				if (column == widths.Count)
				{
					widths.Add(0f);
				}
				widths[column] = Math.Max(widths[column], Width(row[column]));
			}
		}
		float space = measure(" ");
		float gap = measure("  ");
		var starts = new float[widths.Count];
		for (int column = 0; column < widths.Count; column++)
		{
			starts[column] = column == 0 ? Width(indent) : starts[column - 1] + widths[column - 1] + gap;
		}

		var text = new StringBuilder();
		for (int index = 0; index < rows.Count; index++)
		{
			if (index > 0)
			{
				text.Append('\n');
			}
			var line = new StringBuilder(indent);
			IReadOnlyList<string> row = rows[index];
			for (int column = 0; column < row.Count; column++)
			{
				float target = starts[column]
					+ (Array.IndexOf(rightAligned, column) >= 0 ? widths[column] - Width(row[column]) : 0f);
				int spaces = Nearest(line.ToString(), target, space, Width);
				if (column > 0 && spaces < 1)
				{
					spaces = 1;
				}
				line.Append(' ', spaces).Append(row[column]);
			}
			text.Append(line);
		}
		return text.ToString();
	}

	/// <summary>
	/// How many spaces after a line bring its end nearest a place: a first guess by one space's width, then a space
	/// more or fewer for as long as that comes nearer, each try measured on the line itself.
	/// </summary>
	private static int Nearest(string line, float target, float space, Func<string, float> width)
	{
		float Off(int count) => Math.Abs(width(line + new string(' ', count)) - target);

		float at = width(line);
		if (at >= target)
		{
			return 0;
		}
		int spaces = Math.Max(0, (int)((target - at) / space + 0.5f));
		float off = Off(spaces);
		while (spaces > 0 && Off(spaces - 1) < off)
		{
			spaces--;
			off = Off(spaces);
		}
		// A tie goes to the fewer, so a column is never pushed on for nothing.
		while (Off(spaces + 1) < off)
		{
			spaces++;
			off = Off(spaces);
		}
		return spaces;
	}
}
