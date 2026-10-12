using System.Collections.Generic;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Cuts event and decision script into words as the original does: <c>#</c>, <c>!</c> and
/// <c>;</c> start a comment, <c>=</c>, <c>{</c>, <c>}</c>, <c>(</c>, <c>)</c> and <c>,</c> are
/// words of their own, and a quoted word is kept apart from a bare one, since only a bare one
/// can be one of the language's words. <c>docs/mechanics/script.md</c>, section 2.
///
/// It differs from <see cref="Clausewitz.ClausewitzParser"/>, which the other files go through,
/// in the two comment characters and in keeping the quotes' mark.
/// </summary>
public static class CScriptLexer
{
	public static List<CScriptToken> Tokenize(string text)
	{
		var tokens = new List<CScriptToken>();
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
			if (c == '#' || c == '!' || c == ';')
			{
				while (i < text.Length && text[i] != '\n')
				{
					i++;
				}
				continue;
			}
			if (char.IsWhiteSpace(c))
			{
				i++;
				continue;
			}
			switch (c)
			{
				case '=':
					tokens.Add(new CScriptToken(EScriptToken.Equals, "=", line));
					i++;
					continue;
				case '{':
					tokens.Add(new CScriptToken(EScriptToken.Open, "{", line));
					i++;
					continue;
				case '}':
					tokens.Add(new CScriptToken(EScriptToken.Close, "}", line));
					i++;
					continue;
				case '(':
				case ')':
				case ',':
					tokens.Add(new CScriptToken(EScriptToken.Punctuation, c.ToString(), line));
					i++;
					continue;
				case '"':
				{
					int startLine = line;
					int start = ++i;
					while (i < text.Length && text[i] != '"')
					{
						if (text[i] == '\n')
						{
							line++;
						}
						i++;
					}
					tokens.Add(new CScriptToken(EScriptToken.Quoted, text[start..i], startLine));
					i++;                              // the closing quote
					continue;
				}
			}
			int from = i;
			while (i < text.Length && !EndsWord(text[i]))
			{
				i++;
			}
			tokens.Add(new CScriptToken(EScriptToken.Word, text[from..i], line));
		}
		return tokens;
	}

	private static bool EndsWord(char c)
	{
		return char.IsWhiteSpace(c) || c is '!' or '"' or '#' or '(' or ')' or ',' or ';' or '=' or '{' or '}';
	}
}
