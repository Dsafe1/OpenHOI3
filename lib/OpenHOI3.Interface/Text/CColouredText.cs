using System.Collections.Generic;
using System.Text;

namespace OpenHOI3.Interface.Text;

/// <summary>
/// The original's colour escape: a section sign and a letter, which draw nothing and
/// change the colour of what follows. See <c>docs/mechanics/interface.md</c>, section 6.
/// </summary>
public static class CColouredText
{
	/// <summary>What starts an escape. Byte 0xA7 in the game's own encoding.</summary>
	public const char Escape = '§';

	public const char NoColour = '\0';

	public const char Red = 'R';
	public const char Green = 'G';
	public const char Yellow = 'Y';

	/// <summary>An escape for a colour, to put in front of what it colours.</summary>
	public static string In(char colour)
	{
		return new string(new[] { Escape, colour });
	}

	/// <summary>The text cut into its runs of one colour. Runs with no text in them are left out.</summary>
	public static List<CTextRun> Parse(string text)
	{
		var runs = new List<CTextRun>();
		var current = new StringBuilder();
		char colour = NoColour;
		for (int index = 0; index < text.Length; index++)
		{
			if (text[index] != Escape)
			{
				current.Append(text[index]);
				continue;
			}
			if (current.Length > 0)
			{
				runs.Add(new CTextRun(current.ToString(), colour));
				current.Clear();
			}
			// An escape is two characters. One at the very end has nothing to name.
			if (index + 1 < text.Length)
			{
				colour = text[++index];
			}
		}
		if (current.Length > 0)
		{
			runs.Add(new CTextRun(current.ToString(), colour));
		}
		return runs;
	}

	/// <summary>
	/// Lines drawn one after another as one text, each after the first given the colour in force where the one before it
	/// ended: the original's font keeps the colour an escape set until the next escape, across a line break and a newline
	/// alike. A line that begins with an escape of its own is left as it is.
	/// </summary>
	public static List<string> CarryColour(IEnumerable<string> lines)
	{
		var carried = new List<string>();
		char colour = NoColour;
		foreach (string line in lines)
		{
			bool own = line.Length > 1 && line[0] == Escape;
			carried.Add(colour == NoColour || own ? line : In(colour) + line);
			for (int index = 0; index + 1 < line.Length; index++)
			{
				if (line[index] == Escape)
				{
					colour = line[++index];
				}
			}
		}
		return carried;
	}

	/// <summary>The text with its escapes taken out.</summary>
	public static string Strip(string text)
	{
		var plain = new StringBuilder(text.Length);
		foreach (CTextRun run in Parse(text))
		{
			plain.Append(run.Text);
		}
		return plain.ToString();
	}
}
