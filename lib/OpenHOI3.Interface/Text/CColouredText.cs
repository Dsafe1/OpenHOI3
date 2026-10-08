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
