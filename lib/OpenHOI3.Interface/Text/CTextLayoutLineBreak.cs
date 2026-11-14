using System;
using System.Collections.Generic;

namespace OpenHOI3.Interface.Text;

/// <summary>
/// How the original's text layout breaks a text before a screen hands it on, which is how a tooltip, a row of the message
/// log, a line of the diplomacy screen's status list, a line of the lobby's chat and a row of the technology panel's
/// effects are cut (<c>docs/mechanics/interface.md</c>, section 6; <c>reversing/findings/FINDINGS-editbox.md</c>,
/// section 6, and <c>reversing/findings/FINDINGS-textdraw.md</c>, section 2.3). Not a text box's own rule: what it gives
/// is set into a box whose font draws each line as it is.
///
/// A line is cut where it and one <c>O</c> are wider than the room. It is cut at the last space before the first
/// character at which it reaches the room less the <c>O</c> - so the line kept is narrower than that by a hair - the
/// space dropped; or, where the first word and an <c>O</c> do not fit, after that first character, the word split. A
/// newline ends a line. A colour escape takes no room, and nothing is carried from one line to the next. The lines are
/// the layout's text split at its breaks, so an empty text, or one ending in a newline, has an empty line last.
/// </summary>
public static class CTextLayoutLineBreak
{
	/// <summary>The letter whose width a line is given as room to spare before it breaks.</summary>
	public const string Spare = "O";

	/// <param name="text">The text, colour escapes and all.</param>
	/// <param name="room">How wide a line may be, in the original's terms the layout's area.</param>
	/// <param name="measure">How wide a text is drawn, its colour escapes taken out.</param>
	public static List<string> Break(string text, float room, Func<string, float> measure)
	{
		var lines = new List<string>();
		foreach ((int start, int length) in Ranges(text, room, run => measure(CColouredText.Strip(run))))
		{
			lines.Add(text.Substring(start, length));
		}
		return lines;
	}

	/// <summary>
	/// The same lines as <see cref="Break"/>, each by where it starts in the text and how long it is: a newline that ends
	/// a line and a space a line was cut at belong to neither line, the next starting after them. What an edit box keeps
	/// its caret against (<c>docs/mechanics/interface.md</c>, section 8).
	/// </summary>
	/// <param name="width">How wide a run of the text is drawn, as it stands.</param>
	public static List<(int Start, int Length)> Ranges(string text, float room, Func<string, float> width)
	{
		float spare = width(Spare);
		var lines = new List<(int, int)>();
		int at = 0;
		while (at < text.Length)
		{
			string rest = text[at..];
			int newline = rest.IndexOf('\n');
			string first = newline < 0 ? rest : rest[..newline];
			if (width(first) + spare <= room)
			{
				lines.Add((at, first.Length));
				at = newline < 0 ? text.Length : at + newline + 1;
				continue;
			}
			int reaching = FirstReaching(rest, room - spare, width);
			int space = rest.IndexOf(' ');
			if (space >= 0 && width(rest[..space]) + spare <= room)
			{
				int cut = LastSpaceBefore(rest, space, reaching);
				lines.Add((at, cut));
				at += cut + 1;
			}
			else
			{
				lines.Add((at, reaching));
				at += reaching;
			}
		}

		// The layout's text, split at its line breaks as every screen splits it: an empty text, or one that ends in a
		// newline, has an empty piece last.
		if (text.Length == 0 || text[^1] == '\n')
		{
			lines.Add((text.Length, 0));
		}
		return lines;
	}

	/// <summary>
	/// The fewest characters from the start that are at least <paramref name="limit"/> wide, and never none, so that a
	/// line always takes something.
	/// </summary>
	private static int FirstReaching(string text, float limit, Func<string, float> width)
	{
		int count = 1;
		while (count < text.Length && width(text[..count]) < limit)
		{
			count++;
		}
		return count;
	}

	/// <summary>The last space before <paramref name="before"/>, or the first space where none is.</summary>
	private static int LastSpaceBefore(string text, int first, int before)
	{
		int last = first;
		for (int next = text.IndexOf(' ', last + 1); next >= 0 && next < before; next = text.IndexOf(' ', last + 1))
		{
			last = next;
		}
		return last;
	}
}
