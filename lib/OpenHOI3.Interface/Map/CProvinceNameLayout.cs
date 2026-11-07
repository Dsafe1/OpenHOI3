using System;
using System.Collections.Generic;
using OpenHOI3.Content.Gui;
using OpenHOI3.Content.Map;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// A province's name laid out on the near map as the original lays it out (<c>docs/mechanics/mapdraw.md</c>, section 4;
/// <c>reversing/findings/FINDINGS-farmap.md</c>, B.7.3): cut to 49 characters where it has more than 50; broken into lines
/// only at a glyph with no height - the space - before the word that takes its line past 320 font pixels; each line
/// centred on the text position, the lines stacked upwards so that the block stands on the point; turned by
/// text_rotation radians; and a font pixel 0.05 × text_scale × 0.5 map pixels. The name keeps its case.
/// </summary>
public static class CProvinceNameLayout
{
	public const int WrapWidth = 320;
	public const int LongestName = 50;
	private const float FontPixel = 0.05f;
	private const float ScaleOfLabel = 0.5f;

	/// <summary>Each drawn letter of the name, its glyph's box's middle in map units (x the column, z the row from the south).</summary>
	public static List<CMapNameLetter> Lay(string name, CFontDescriptor font, CProvinceLabel label)
	{
		if (name.Length > LongestName)
		{
			name = name[..(LongestName - 1)];
		}
		float k = FontPixel * label.Scale * ScaleOfLabel;
		float alongX = MathF.Cos(label.Rotation), alongZ = MathF.Sin(label.Rotation);
		float upX = -alongZ, upZ = alongX;

		// The breaks: a line's width is tested only at a space, its word and the space taken in.
		var widths = new List<int>();
		var starts = new HashSet<int>();
		int line = 0, word = 0, wordStart = 0;
		for (int i = 0; i < name.Length; i++)
		{
			char c = name[i];
			if (c is '\n' or '\r')
			{
				widths.Add(line);
				starts.Add(i + 1);
				line = word = 0;
				wordStart = i + 1;
				continue;
			}
			if (font.Glyph(c) is not { } glyph)
			{
				continue;
			}
			// Each glyph its advance and its kerning with the character after it [exe].
			int step = glyph.Advance + (i + 1 < name.Length ? font.Kerning(c, name[i + 1]) : 0);
			line += step;
			word += step;
			if (glyph.Height == 0)
			{
				if (line > WrapWidth)
				{
					widths.Add(line - word);
					starts.Add(wordStart);
					line = word;
				}
				word = 0;
				wordStart = i + 1;
			}
		}
		widths.Add(line);

		// The placing: the first line's top as high as the lines, each line's pen half its width to the left.
		var letters = new List<CMapNameLetter>();
		int at = 0;
		int top = widths.Count * font.LineHeight;
		int pen = -(widths[0] / 2);
		for (int i = 0; i < name.Length; i++)
		{
			if (starts.Contains(i) && at + 1 < widths.Count)
			{
				at++;
				top -= font.LineHeight;
				pen = -(widths[at] / 2);
			}
			char c = name[i];
			if (c is '\n' or '\r' || font.Glyph(c) is not { } glyph)
			{
				continue;
			}
			if (glyph.Height < 1)
			{
				pen += glyph.Advance;
				continue;
			}
			float middleX = pen + glyph.XOffset + glyph.Width / 2f;
			float middleY = top - glyph.YOffset - glyph.Height / 2f;
			letters.Add(new CMapNameLetter(c, label.X + (alongX * middleX + upX * middleY) * k,
				label.Y + (alongZ * middleX + upZ * middleY) * k, alongX, alongZ, k));
			pen += glyph.Advance + (i + 1 < name.Length ? font.Kerning(c, name[i + 1]) : 0);
		}
		return letters;
	}
}
