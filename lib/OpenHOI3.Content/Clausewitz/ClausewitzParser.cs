using System;
using System.Collections.Generic;
using System.Text;

namespace OpenHOI3.Content.Clausewitz;

/// <summary>
/// Reader for the Clausewitz script format shared by <c>.gui</c>, <c>.gfx</c> and the
/// game's <c>.txt</c> data files.
/// </summary>
public static class ClausewitzParser
{
	/// <summary>
	/// Game files predate UTF-8 and contain Windows-1252 bytes in localised strings.
	/// Latin1 round-trips every byte, so nothing is lost even where the two differ.
	/// </summary>
	public static ClausewitzNode ParseFile(string absolutePath)
	{
		byte[] bytes = System.IO.File.ReadAllBytes(absolutePath);
		return Parse(Encoding.Latin1.GetString(bytes));
	}

	public static ClausewitzNode Parse(string text)
	{
		var tokens = Tokenize(text);
		var root = new ClausewitzNode();
		int index = 0;
		ParseInto(root, tokens, ref index, topLevel: true);
		return root;
	}

	private enum TokenKind
	{
		Word,
		Equals,
		OpenBrace,
		CloseBrace,
	}

	private readonly record struct Token(TokenKind Kind, string Text);

	private static List<Token> Tokenize(string text)
	{
		var tokens = new List<Token>();
		int i = 0;
		while (i < text.Length)
		{
			char c = text[i];
			if (c == '#')
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
					tokens.Add(new Token(TokenKind.Equals, "="));
					i++;
					continue;
				case '{':
					tokens.Add(new Token(TokenKind.OpenBrace, "{"));
					i++;
					continue;
				case '}':
					tokens.Add(new Token(TokenKind.CloseBrace, "}"));
					i++;
					continue;
			}
			if (c == '"')
			{
				i++;
				var sb = new StringBuilder();
				while (i < text.Length && text[i] != '"')
				{
					// The files escape path separators as "gfx\\interface\\x.dds".
					if (text[i] == '\\' && i + 1 < text.Length)
					{
						i++;
					}
					sb.Append(text[i]);
					i++;
				}
				i++;                                  // closing quote
				tokens.Add(new Token(TokenKind.Word, sb.ToString()));
				continue;
			}
			int start = i;
			while (i < text.Length && !char.IsWhiteSpace(text[i]) &&
				text[i] != '=' && text[i] != '{' && text[i] != '}' && text[i] != '#')
			{
				i++;
			}
			tokens.Add(new Token(TokenKind.Word, text[start..i]));
		}
		return tokens;
	}

	private static void ParseInto(ClausewitzNode parent, List<Token> tokens, ref int i,
		bool topLevel)
	{
		while (i < tokens.Count)
		{
			Token token = tokens[i];
			if (token.Kind == TokenKind.CloseBrace)
			{
				if (topLevel)
				{
					i++;                              // stray brace; skip rather than abort
					continue;
				}
				i++;
				return;
			}
			if (token.Kind != TokenKind.Word)
			{
				i++;
				continue;
			}

			// key = value | key = { ... }
			if (i + 1 < tokens.Count && tokens[i + 1].Kind == TokenKind.Equals)
			{
				string key = token.Text;
				i += 2;
				if (i >= tokens.Count)
				{
					return;
				}
				if (tokens[i].Kind == TokenKind.OpenBrace)
				{
					i++;
					var block = new ClausewitzNode { Key = key };
					ParseInto(block, tokens, ref i, topLevel: false);
					parent.Children.Add(block);
				}
				else
				{
					parent.Children.Add(new ClausewitzNode { Key = key, Value = tokens[i].Text });
					i++;
				}
				continue;
			}

			// Bare value inside a list block.
			parent.Children.Add(new ClausewitzNode { Value = token.Text });
			i++;
		}
	}
}
