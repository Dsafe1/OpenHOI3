using System;
using System.Collections.Generic;
using System.Text;

namespace OpenHOI3.Interface.Text;

/// <summary>
/// Columns in a text drawn in a proportional font, made with spaces: a tooltip has no columns of its own. Each column
/// is as wide as its widest cell and starts a gap after the one before; a cell is reached by the whole number of
/// spaces nearest to where it should start - never fewer than one after the first column - and the place the spaces
/// really end is carried on, so that the rounding does not gather along a row. So a column lands within half a
/// space of where it was aimed. Cells carry their own colours, which are not measured.
/// </summary>
public static class CTextTable
{
	/// <param name="rows">The cells of each row; a row with fewer cells ends early.</param>
	/// <param name="indent">Written before every row.</param>
	/// <param name="measure">How wide a text is in the font it will be drawn in.</param>
	/// <param name="rightAligned">The columns, counted from nought, whose cells end together.</param>
	/// <returns>The rows joined by line breaks, with none after the last.</returns>
	public static string Render(IReadOnlyList<IReadOnlyList<string>> rows, string indent, Func<string, float> measure,
		params int[] rightAligned)
	{
		float space = measure(" ");
		if (space <= 0f)
		{
			// A font that does not measure: every character counts one.
			measure = text => text.Length;
			space = 1f;
		}
		float Width(string cell) => measure(CColouredText.Strip(cell));

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
		float gap = 2 * space;
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
			text.Append(indent);
			float at = Width(indent);
			IReadOnlyList<string> row = rows[index];
			for (int column = 0; column < row.Count; column++)
			{
				float width = Width(row[column]);
				float target = starts[column] + (Array.IndexOf(rightAligned, column) >= 0 ? widths[column] - width : 0f);
				int spaces = (int)((target - at + space / 2) / space);
				if (column > 0 && spaces < 1)
				{
					spaces = 1;
				}
				if (spaces > 0)
				{
					text.Append(' ', spaces);
					at += spaces * space;
				}
				text.Append(row[column]);
				at += width;
			}
		}
		return text.ToString();
	}
}
