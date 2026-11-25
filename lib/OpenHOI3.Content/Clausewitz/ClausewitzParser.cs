using System;
using System.Collections.Generic;
using System.Text;

namespace OpenHOI3.Content.Clausewitz;

/// <summary>
/// Reader for the Clausewitz script format shared by <c>.gui</c>, <c>.gfx</c> and the
/// game's <c>.txt</c> data files.
///
/// <para>
/// <b>A key with no <c>=</c></b> is read as the original reads it. Its key reader takes a key and one token, and takes a
/// value only when that token is <c>=</c>: otherwise the token is lost and the value is the one the pair before it
/// read (<c>docs/mechanics/clausewitz.md</c>, section 2). So in a block that holds <c>key = value</c> pairs - an object, where the
/// original reads pairs, and not a list of bare values - <c>manpower 0.66</c> is <c>manpower</c> with the value read
/// before it and 0.66 lost, and <c>theatre { ... }</c> is a <c>theatre</c> block, its <c>{</c> having been taken as the
/// <c>=</c>'s place. Each is reported where it is. A bare word in such a block followed by anything else is reported
/// and left as it is.
/// </para>
/// </summary>
public static class ClausewitzParser
{
	/// <summary>
	/// Game files predate UTF-8 and contain Windows-1252 bytes in localised strings.
	/// Latin1 round-trips every byte, so nothing is lost even where the two differ.
	/// </summary>
	public static ClausewitzNode ParseFile(string absolutePath)
	{
		return ParseFile(absolutePath, wordAfterKey: false);
	}

	/// <param name="wordAfterKey">
	/// An enhanced game's fix: a key whose <c>=</c> is missing takes the word after it as its value, and not the value of
	/// the pair before (<c>docs/mechanics/clausewitz.md</c>, section 2).
	/// </param>
	public static ClausewitzNode ParseFile(string absolutePath, bool wordAfterKey)
	{
		byte[] bytes = System.IO.File.ReadAllBytes(absolutePath);
		return Parse(Encoding.Latin1.GetString(bytes), absolutePath, wordAfterKey);
	}

	/// <param name="source">Where the text came from, for the reports of a key with no <c>=</c>.</param>
	public static ClausewitzNode Parse(string text, string source = "text", bool wordAfterKey = false)
	{
		var tokens = Tokenize(text);
		var root = new ClausewitzNode();
		int index = 0;
		ParseInto(root, tokens, ref index, topLevel: true, new CParse(source) { WordAfterKey = wordAfterKey });
		return root;
	}

	/// <summary>What a parse keeps across blocks: the value the last pair read, as the original's parse context does.</summary>
	private sealed class CParse(string source)
	{
		public string Source { get; } = source;
		public string LastValue { get; set; } = "";
		public bool WordAfterKey { get; init; }
	}

	/// <summary>A bare word met in a block: its node, the value read before it, its line and what came straight after.</summary>
	private readonly record struct CBare(ClausewitzNode Node, string Previous, int Line, TokenKind Next);

	private enum TokenKind
	{
		Word,
		Equals,
		OpenBrace,
		CloseBrace,
	}

	private readonly record struct Token(TokenKind Kind, string Text, bool Quoted = false, int Line = 0);

	private static List<Token> Tokenize(string text)
	{
		var tokens = new List<Token>();
		int i = 0;
		int line = 1;
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
				if (c == '\n')
				{
					line++;
				}
				i++;
				continue;
			}
			switch (c)
			{
				case '=':
					tokens.Add(new Token(TokenKind.Equals, "=", Line: line));
					i++;
					continue;
				case '{':
					tokens.Add(new Token(TokenKind.OpenBrace, "{", Line: line));
					i++;
					continue;
				case '}':
					tokens.Add(new Token(TokenKind.CloseBrace, "}", Line: line));
					i++;
					continue;
			}
			if (c == '"')
			{
				int quoteLine = line;
				i++;
				var sb = new StringBuilder();
				while (i < text.Length && text[i] != '"')
				{
					if (text[i] == '\n')
					{
						line++;
					}
					// The files escape path separators as "gfx\\interface\\x.dds" - and here
					// and there forget to, writing "gfx\mapitems\\x.tga". Both name a file
					// that is there, so a backslash on its own is a backslash too and only
					// a doubled one, or one before a quote, is an escape.
					if (text[i] == '\\' && i + 1 < text.Length && (text[i + 1] == '\\' || text[i + 1] == '"'))
					{
						i++;
					}
					sb.Append(text[i]);
					i++;
				}
				i++;                                  // closing quote
				tokens.Add(new Token(TokenKind.Word, sb.ToString(), Quoted: true, Line: quoteLine));
				continue;
			}
			int start = i;
			while (i < text.Length && !char.IsWhiteSpace(text[i]) &&
				text[i] != '=' && text[i] != '{' && text[i] != '}' && text[i] != '#')
			{
				i++;
			}
			tokens.Add(new Token(TokenKind.Word, text[start..i], Line: line));
		}
		return tokens;
	}

	private static void ParseInto(ClausewitzNode parent, List<Token> tokens, ref int i,
		bool topLevel, CParse parse)
	{
		var bare = new List<CBare>();
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
				Settle(parent, bare, parse);
				return;
			}
			// A block with no key - a save's list of finished battles is a run of them - holds its own entries,
			// and its closing brace closes it and not the block around it.
			if (token.Kind == TokenKind.OpenBrace)
			{
				i++;
				var anonymous = new ClausewitzNode { Line = token.Line };
				ParseInto(anonymous, tokens, ref i, topLevel: false, parse);
				parent.Children.Add(anonymous);
				continue;
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
					break;
				}
				if (tokens[i].Kind == TokenKind.OpenBrace)
				{
					// The original's parse context holds the block's opening brace as the pair's value.
					parse.LastValue = "{";
					i++;
					var block = new ClausewitzNode { Key = key, Line = token.Line };
					ParseInto(block, tokens, ref i, topLevel: false, parse);
					parent.Children.Add(block);
				}
				else
				{
					parse.LastValue = tokens[i].Text;
					parent.Children.Add(new ClausewitzNode { Key = key, Value = tokens[i].Text, IsQuoted = tokens[i].Quoted, Line = token.Line });
					i++;
				}
				continue;
			}

			// Bare value inside a list block - or, in a block of pairs, a key whose `=` is missing (Settle).
			var node = new ClausewitzNode { Value = token.Text, IsQuoted = token.Quoted, Line = token.Line };
			parent.Children.Add(node);
			TokenKind next = i + 1 < tokens.Count ? tokens[i + 1].Kind : TokenKind.CloseBrace;
			bare.Add(new CBare(node, parse.LastValue, token.Line, next));
			i++;
		}
		Settle(parent, bare, parse);
	}

	/// <summary>
	/// A block's bare words, once it is closed: in a list they are its values and nothing is said; in a block that holds
	/// <c>key = value</c> pairs each is a key whose <c>=</c> is missing, read as the original reads it (the class's
	/// summary) and reported.
	/// </summary>
	private static void Settle(ClausewitzNode parent, List<CBare> bare, CParse parse)
	{
		if (bare.Count == 0 || !parent.Children.Exists(child => child.Key is not null))
		{
			return;
		}
		var taken = new HashSet<ClausewitzNode>();
		foreach (CBare word in bare)
		{
			if (taken.Contains(word.Node))
			{
				continue;
			}
			int at = parent.Children.IndexOf(word.Node);
			ClausewitzNode? after = at + 1 < parent.Children.Count ? parent.Children[at + 1] : null;
			string key = word.Node.Value ?? "";
			if (word.Next == TokenKind.Word && after is { Key: null, IsBlock: false })
			{
				if (parse.WordAfterKey)
				{
					// The fix: the line is read as it plainly says.
					CDiagnostics.Problem(parse.Source, word.Line,
						$"'{key} {after.Value}' has no '='; read as '{key} = {after.Value}', where the original reads " +
						$"'{key} = {word.Previous}', the value the pair before it gave");
					parent.Children[at] = new ClausewitzNode { Key = key, Value = after.Value, IsQuoted = after.IsQuoted, Line = word.Line };
					parent.Children.RemoveAt(at + 1);
					taken.Add(after);
					continue;
				}
				// The value token is lost and the key takes the value the pair before it read.
				CDiagnostics.Problem(parse.Source, word.Line,
					$"'{key} {after.Value}' has no '='; read as the original reads it, '{key} = {word.Previous}' - the value " +
					$"the pair before it gave - and '{after.Value}' lost");
				parent.Children[at] = new ClausewitzNode { Key = key, Value = word.Previous, Line = word.Line };
				parent.Children.RemoveAt(at + 1);
				taken.Add(after);
			}
			else if (word.Next == TokenKind.OpenBrace && after is { Key: null, IsBlock: true })
			{
				// The brace is taken where the `=` should be, and the block is read as the key's.
				CDiagnostics.Problem(parse.Source, word.Line, $"'{key} {{' has no '='; read as '{key} = {{', as the original reads it");
				var block = new ClausewitzNode { Key = key, Line = word.Line };
				block.Children.AddRange(after.Children);
				parent.Children[at + 1] = block;
				parent.Children.RemoveAt(at);
			}
			else
			{
				CDiagnostics.Problem(parse.Source, word.Line,
					$"'{key}' stands alone among pairs, with no '=' and no value; passed over");
			}
		}
	}
}
