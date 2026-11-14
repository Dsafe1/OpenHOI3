using System;
using System.Collections.Generic;
using System.Text;

namespace OpenHOI3.Interface.Text;

/// <summary>
/// Where the original's font breaks a text box's text into lines as it draws it (<c>docs/mechanics/interface.md</c>,
/// section 6; <c>reversing/findings/FINDINGS-textdraw.md</c>): at the box's <c>maxWidth</c> - 320 where it gives none -
/// whatever its <c>maxHeight</c>, which plays no part, so the lines run on down past the box.
///
/// A word is taken up to and with the space after it. At each space, where the line with that word and its space and twice
/// the border is wider than the box, the line breaks before the word and the word begins the next; the line broken off
/// keeps the space after its last word, which is measured with it. A word over 50 wide that runs past the box, once
/// something has been laid out, is broken where it reaches the edge. The last word is tested without a space, and not at
/// all where it alone is wider than the box. A newline ends a line, and a colour escape takes no room.
/// </summary>
public static class CFontLineBreak
{
	/// <summary>The width a box of no <c>maxWidth</c> breaks at.</summary>
	public const float DefaultWidth = 320f;

	/// <summary>How wide a word that runs past the box must be before it is broken where it reaches the edge.</summary>
	public const float LongWord = 50f;

	/// <param name="text">The text, colour escapes and all.</param>
	/// <param name="maxWidth">The box's <c>maxWidth</c>, its border in it; nought for none.</param>
	/// <param name="borderX">The box's <c>borderSize</c> across.</param>
	/// <param name="measure">How wide a run of text is drawn, without its colour escapes.</param>
	public static List<string> Break(string text, float maxWidth, float borderX, Func<string, float> measure)
	{
		float limit = maxWidth == 0f ? DefaultWidth : maxWidth;
		float border = 2f * borderX;
		var lines = new List<string>();
		var line = new StringBuilder();
		var word = new StringBuilder();
		bool laidOut = false;

		float Width(StringBuilder first, StringBuilder second) => measure(CColouredText.Strip(first.ToString() + second));

		void BreakBeforeWord()
		{
			lines.Add(line.ToString());
			line.Clear().Append(word);
			word.Clear();
			laidOut = true;
		}

		void TakeWord()
		{
			line.Append(word);
			laidOut |= word.Length > 0;
			word.Clear();
		}

		for (int at = 0; at < text.Length; at++)
		{
			char c = text[at];
			if (c == CColouredText.Escape && at + 1 < text.Length)
			{
				word.Append(c).Append(text[at + 1]);
				at++;
				continue;
			}
			if (c is '\n' or '\r')
			{
				TakeWord();
				lines.Add(line.ToString());
				line.Clear();
				laidOut = true;
				continue;
			}
			word.Append(c);
			if (c == ' ')
			{
				if (limit < Width(line, word) + border)
				{
					BreakBeforeWord();
				}
				else
				{
					TakeWord();
				}
			}
			else if (laidOut && limit <= border + Width(line, word) && measure(CColouredText.Strip(word.ToString())) > LongWord)
			{
				BreakBeforeWord();
			}
		}
		if (limit < measure(CColouredText.Strip(word.ToString())))
		{
			TakeWord();
		}
		else if (limit < Width(line, word) + border)
		{
			BreakBeforeWord();
		}
		else
		{
			TakeWord();
		}
		lines.Add(line.ToString());
		return lines;
	}
}
