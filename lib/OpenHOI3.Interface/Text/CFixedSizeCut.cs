using System;
using System.Text;

namespace OpenHOI3.Interface.Text;

/// <summary>
/// What the original's font makes of the text of a box that says <c>fixedsize = yes</c>, before the text is laid out and
/// broken (<c>docs/mechanics/interface.md</c>, section 6; <c>reversing/findings/FINDINGS-textdraw.md</c>, section 2.4):
/// the text cut where it runs past the room the box has, its last four characters before the cut made a space and three
/// dots.
///
/// It counts lines a character at a time, as no breaking does: a line is full at the first character that brings it to
/// the box's width less twice the border across, and that character and the one after it each start a line - the count
/// of the line's width is not set back at the first, only at the second. A newline starts one too. Where the lines then
/// counted are taller than the box's height less twice the border <b>across</b> - the original's slip, the height's border
/// unused - the text is cut before the character that started the line too many. A colour escape, a flag, a counter and
/// a strength bar are counted as the original counts them; the strength bar takes one character more with it.
/// </summary>
public static class CFixedSizeCut
{
	/// <summary>The width a box of no <c>maxWidth</c> is counted at.</summary>
	public const float DefaultWidth = 320f;

	/// <summary>What the last four characters before the cut become.</summary>
	public const string Mark = " ...";

	/// <param name="text">The text, colour escapes and all.</param>
	/// <param name="maxWidth">The box's <c>maxWidth</c>, its border in it; nought for none.</param>
	/// <param name="maxHeight">The box's <c>maxHeight</c>, its border in it.</param>
	/// <param name="borderX">The box's <c>borderSize</c> across.</param>
	/// <param name="lineHeight">The height of a line in the box's font.</param>
	/// <param name="measure">How wide a run of letters and pictures is drawn, with no colour escapes in it.</param>
	public static string Cut(string text, float maxWidth, float maxHeight, float borderX, float lineHeight,
		Func<string, float> measure)
	{
		float width = (maxWidth == 0f ? DefaultWidth : maxWidth) - 2f * borderX;
		float height = maxHeight - 2f * borderX;
		float counted = lineHeight;
		var line = new StringBuilder();
		var sinceFull = new StringBuilder();
		for (int at = 0; at < text.Length; at++)
		{
			char c = text[at];
			string? picture = null;
			if (c == CColouredText.Escape)
			{
				at++;
				continue;
			}
			if (c == CStrengthIcons.Mark)
			{
				// The bar's two digits, and one character more that the original passes over unread.
				picture = text.Substring(at, Math.Min(3, text.Length - at));
				at += 3;
			}
			else if (c == CFlagIcons.Mark)
			{
				picture = text.Substring(at, Math.Min(CFlagIcons.TagLength + 1, text.Length - at));
				at += CFlagIcons.TagLength;
			}
			else if (c == CCounterIcons.Mark)
			{
				int end = at + 1;
				while (end < text.Length && CCounterIcons.IsNameCharacter(text[end]))
				{
					end++;
				}
				picture = text[at..end];
				at = end - 1;
			}
			if (picture is not null)
			{
				line.Append(picture);
				sinceFull.Append(picture);
				continue;
			}

			if (c is '\n' or '\r')
			{
				line.Clear();
				sinceFull.Clear();
				counted += lineHeight;
				if (height < counted)
				{
					return at < Mark.Length + 1 ? text[..at] : Marked(text, at);
				}
				continue;
			}

			line.Append(c);
			sinceFull.Append(c);
			if (width <= measure(line.ToString()))
			{
				counted += lineHeight;
				line.Clear().Append(sinceFull);
				if (height < counted)
				{
					return at > Mark.Length - 1 ? Marked(text, at) : "...";
				}
				sinceFull.Clear();
			}
		}
		return text;
	}

	/// <summary>The text before <paramref name="at"/>, its last four characters made the mark.</summary>
	private static string Marked(string text, int at)
	{
		return text[..(at - Mark.Length)] + Mark;
	}
}
