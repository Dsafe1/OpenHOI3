using System.Collections.Generic;

namespace OpenHOI3.Interface.Text;

/// <summary>
/// A typed character as the byte the original's window would have been handed, Windows-1252 being the code page of a
/// Western system: held as the character of that byte, as every text read from the game files is held (Latin-1).
/// </summary>
public static class CWindows1252
{
	/// <summary>The 27 characters Windows-1252 puts in 0x80 to 0x9F, where Latin-1 has controls.</summary>
	private static readonly Dictionary<char, byte> Specials = new()
	{
		['€'] = 0x80, ['‚'] = 0x82, ['ƒ'] = 0x83, ['„'] = 0x84, ['…'] = 0x85, ['†'] = 0x86,
		['‡'] = 0x87, ['ˆ'] = 0x88, ['‰'] = 0x89, ['Š'] = 0x8A, ['‹'] = 0x8B, ['Œ'] = 0x8C,
		['Ž'] = 0x8E, ['‘'] = 0x91, ['’'] = 0x92, ['“'] = 0x93, ['”'] = 0x94, ['•'] = 0x95,
		['–'] = 0x96, ['—'] = 0x97, ['˜'] = 0x98, ['™'] = 0x99, ['š'] = 0x9A, ['›'] = 0x9B,
		['œ'] = 0x9C, ['ž'] = 0x9E, ['Ÿ'] = 0x9F,
	};

	private static readonly Dictionary<char, char> Shown = Invert();

	private static Dictionary<char, char> Invert()
	{
		var shown = new Dictionary<char, char>();
		foreach ((char unicode, byte code) in Specials)
		{
			shown[(char)code] = unicode;
		}
		return shown;
	}

	/// <summary>
	/// A text held as its bytes, as it is drawn: each of 0x80 to 0x9F the character Windows-1252 gives it - the curly
	/// quotes and apostrophe, the dashes - where a typeface would find only a control. Everything else as it is.
	/// </summary>
	public static string ToDisplay(string text)
	{
		int at = 0;
		while (at < text.Length && (text[at] < '\u0080' || text[at] > '\u009F'))
		{
			at++;
		}
		if (at == text.Length)
		{
			return text;
		}
		char[] shown = text.ToCharArray();
		for (int i = at; i < shown.Length; i++)
		{
			if (Shown.TryGetValue(shown[i], out char unicode))
			{
				shown[i] = unicode;
			}
		}
		return new string(shown);
	}

	/// <summary>The byte's character, or none for a character Windows-1252 has no byte for.</summary>
	public static char? FromUnicode(char typed)
	{
		if (Specials.TryGetValue(typed, out byte special))
		{
			return (char)special;
		}
		return typed < 0x80 || (typed >= 0xA0 && typed <= 0xFF) ? typed : null;
	}
}
