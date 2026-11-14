using System;
using System.Collections.Generic;
using System.Text;

namespace OpenHOI3.Interface.Text;

/// <summary>
/// Where the original's font breaks a <c>textBoxType</c>'s text, as it draws it into the picture the box shows
/// (<c>docs/mechanics/interface.md</c>, section 6; <c>reversing/findings/FINDINGS-textdraw.md</c>, section 2.2): at the
/// box's <c>maxWidth</c>, 320 where it gives none. Not the rule of an <c>instantTextBoxType</c>, which its font draws
/// straight to the screen (<see cref="CFontLineBreak"/>): the two are near alike and differ at their edges.
///
/// The text is read a character at a time. Before each one - a space among them - a line that has reached the box, twice
/// the border in, breaks before the word being gathered, the word so far beginning the next line; with no room left, a
/// word wider than the box goes on a character to a line. At a space, a line that the word and its space bring to the
/// box breaks before the word - but never while nothing has yet been laid out. At the end, the last word goes onto the
/// next line where the line has reached the box. A newline ends a line. Past 55 lines the rest of the text is dropped.
/// A colour escape takes no room; a flag, a counter and a strength bar are as wide as their pictures and are no part of
/// the word's width.
/// </summary>
public static class CTextBoxLineBreak
{
	/// <summary>The width a box of no <c>maxWidth</c> breaks at.</summary>
	public const float DefaultWidth = 320f;

	/// <summary>How many lines the text is read for; it stops at the break after which there are more.</summary>
	public const int MostLines = 55;

	/// <param name="text">The text, colour escapes and all.</param>
	/// <param name="maxWidth">The box's <c>maxWidth</c>, its border in it; nought for none.</param>
	/// <param name="borderX">The box's <c>borderSize</c> across.</param>
	/// <param name="measure">
	/// How wide a run of text is drawn: letters, and the pictures of flags, counters and strength bars, with no colour
	/// escapes in it.
	/// </param>
	public static List<string> Break(string text, float maxWidth, float borderX, Func<string, float> measure)
	{
		float limit = maxWidth == 0f ? DefaultWidth : maxWidth;
		float border = 2f * borderX;
		var result = new StringBuilder();
		var word = new StringBuilder();

		// What the line's width is measured over since it was last set, and how far the original's own count of it is
		// off that: it sets the width one short after a break in the middle of a word.
		var lineDrawn = new StringBuilder();
		float lineOff = 0f;

		// What the word's width is measured over. The original does not clear it at a break in the middle of a word, nor
		// at a newline that breaks before the word, so it can run on over more than one line.
		var wordDrawn = new StringBuilder();
		int breaks = 0;

		float Line() => measure(lineDrawn.ToString()) + lineOff;

		void BreakBeforeWord()
		{
			result.Append('\n').Append(word);
			word.Clear();
			breaks++;
		}

		for (int at = 0; at < text.Length; at++)
		{
			char c = text[at];
			word.Append(c);
			string? picture = null;
			if (c == CColouredText.Escape)
			{
				if (at + 1 < text.Length)
				{
					word.Append(text[++at]);
				}
				picture = "";
			}
			else if (c == CFlagIcons.Mark || c == CStrengthIcons.Mark)
			{
				// A flag is the three characters after it, a strength bar the two.
				int length = Math.Min(c == CFlagIcons.Mark ? CFlagIcons.TagLength : 2, text.Length - at - 1);
				picture = text.Substring(at, length + 1);
				word.Append(text, at + 1, length);
				at += length;
			}
			else if (c == CCounterIcons.Mark)
			{
				int end = at + 1;
				while (end < text.Length && CCounterIcons.IsNameCharacter(text[end]))
				{
					end++;
				}
				picture = text[at..end];
				word.Append(text, at + 1, end - at - 1);
				at = end - 1;
			}
			lineDrawn.Append(picture);

			if (limit <= Line() + border)
			{
				BreakBeforeWord();
				lineDrawn.Clear().Append(wordDrawn).Append(picture);
				lineOff = picture is null ? -1f : 0f;
			}
			if (picture is not null)
			{
				continue;
			}

			if (c is '\n' or '\r')
			{
				if (Line() + border < limit)
				{
					result.Append(word);
					word.Clear();
					wordDrawn.Clear();
					breaks++;
				}
				else
				{
					BreakBeforeWord();
					breaks++;
				}
				lineDrawn.Clear();
				lineOff = 0f;
				if (breaks >= MostLines)
				{
					break;
				}
				continue;
			}

			lineDrawn.Append(c);
			wordDrawn.Append(c);
			if (c == ' ')
			{
				if (result.Length == 0 || Line() + border < limit)
				{
					result.Append(word);
					word.Clear();
				}
				else
				{
					BreakBeforeWord();
					lineDrawn.Clear().Append(wordDrawn);
					lineOff = 0f;
					if (breaks >= MostLines)
					{
						wordDrawn.Clear();
						break;
					}
				}
				wordDrawn.Clear();
			}
		}
		if (Line() + border < limit)
		{
			result.Append(word);
		}
		else
		{
			BreakBeforeWord();
		}
		// A carriage return, kept in the text like a newline, ends a line as one does.
		return new List<string>(result.ToString().Split('\n', '\r'));
	}
}
