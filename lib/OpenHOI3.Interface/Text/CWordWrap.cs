using System;
using System.Collections.Generic;

namespace OpenHOI3.Interface.Text;

/// <summary>
/// Breaks a line of text that is too wide at its spaces. A colour that is on where a line
/// breaks is on again at the start of the next. How the original breaks lines was not read.
/// </summary>
public static class CWordWrap
{
	/// <summary>
	/// The paragraph as lines no wider than <paramref name="width"/>, as
	/// <paramref name="measure"/> measures text without its colour escapes. A word wider than
	/// the width on its own is a line of its own.
	/// </summary>
	public static IEnumerable<string> Wrap(string paragraph, float width, Func<string, float> measure)
	{
		if (measure(CColouredText.Strip(paragraph)) <= width)
		{
			yield return paragraph;
			yield break;
		}

		// A line's indent stays at the front of its first piece.
		int indent = 0;
		while (indent < paragraph.Length && paragraph[indent] == ' ')
		{
			indent++;
		}
		string line = paragraph[..indent];
		bool started = false;
		string colour = "";
		foreach (string word in paragraph[indent..].Split(' '))
		{
			string longer = started ? line + " " + word : line + word;
			if (started && measure(CColouredText.Strip(longer)) > width)
			{
				yield return line;
				longer = colour + word;
			}
			line = longer;
			started = true;

			// The last escape in the word is the colour the next line starts in.
			int escape = word.LastIndexOf(CColouredText.Escape);
			if (escape >= 0 && escape + 1 < word.Length)
			{
				colour = word.Substring(escape, 2);
			}
		}
		if (line.Length > 0)
		{
			yield return line;
		}
	}
}
