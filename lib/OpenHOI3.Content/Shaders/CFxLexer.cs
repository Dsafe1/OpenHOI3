using System.Collections.Generic;

namespace OpenHOI3.Content.Shaders;

/// <summary>
/// Cuts preprocessed effect text into tokens: identifiers, numbers as written (<c>0.5f</c>, <c>.5</c>, <c>1e-3</c>,
/// <c>0x10</c>), strings, and symbols, the two-character operators kept whole.
/// </summary>
public static class CFxLexer
{
	private static readonly string[] TwoCharacter =
	{
		"==", "!=", "<=", ">=", "&&", "||", "++", "--", "+=", "-=", "*=", "/=", "%=", "<<", ">>",
	};

	public static List<CFxToken> Tokenise(string text)
	{
		var tokens = new List<CFxToken>();
		int line = 1;
		int i = 0;
		while (i < text.Length)
		{
			char c = text[i];
			if (c == '\n')
			{
				line++;
				i++;
				continue;
			}
			if (char.IsWhiteSpace(c))
			{
				i++;
				continue;
			}
			int start = i;
			if (char.IsLetter(c) || c == '_')
			{
				while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
				{
					i++;
				}
				tokens.Add(new CFxToken(EFxTokenKind.Identifier, text[start..i], line));
				continue;
			}
			if (char.IsDigit(c) || c == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1]))
			{
				i = Number(text, i);
				tokens.Add(new CFxToken(EFxTokenKind.Number, text[start..i], line));
				continue;
			}
			if (c == '"')
			{
				int end = text.IndexOf('"', i + 1);
				end = end < 0 ? text.Length : end;
				tokens.Add(new CFxToken(EFxTokenKind.String, text[(i + 1)..end], line));
				i = end + 1;
				continue;
			}
			string pair = i + 1 < text.Length ? text.Substring(i, 2) : "";
			if (System.Array.IndexOf(TwoCharacter, pair) >= 0)
			{
				tokens.Add(new CFxToken(EFxTokenKind.Symbol, pair, line));
				i += 2;
				continue;
			}
			tokens.Add(new CFxToken(EFxTokenKind.Symbol, c.ToString(), line));
			i++;
		}
		tokens.Add(new CFxToken(EFxTokenKind.End, "", line));
		return tokens;
	}

	private static int Number(string text, int i)
	{
		if (text[i] == '0' && i + 1 < text.Length && (text[i + 1] is 'x' or 'X'))
		{
			i += 2;
			while (i < text.Length && System.Uri.IsHexDigit(text[i]))
			{
				i++;
			}
			return i;
		}
		while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.'))
		{
			i++;
		}
		if (i < text.Length && (text[i] is 'e' or 'E'))
		{
			int exponent = i + 1;
			if (exponent < text.Length && (text[exponent] is '+' or '-'))
			{
				exponent++;
			}
			if (exponent < text.Length && char.IsDigit(text[exponent]))
			{
				i = exponent;
				while (i < text.Length && char.IsDigit(text[i]))
				{
					i++;
				}
			}
		}
		while (i < text.Length && (text[i] is 'f' or 'F' or 'h' or 'H' or 'u' or 'U' or 'l' or 'L'))
		{
			i++;
		}
		return i;
	}
}
