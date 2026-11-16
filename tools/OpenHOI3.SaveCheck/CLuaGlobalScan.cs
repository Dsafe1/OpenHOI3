using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// The free names a Lua 5.1 file uses and defines, read from its text: every name that is not a field, not a keyword
/// and not a local, parameter or loop variable of a block it stands in - a global, to Lua. A name assigned at statement
/// level or declared with <c>function name</c> is a definition; every other a use, with the fields after it
/// (<c>BiceLib.Leaders.activate</c>) and whether it is called. Methods called with <c>:</c>, and the ones a file defines,
/// are kept as well. A reading of the text and not of the bytecode, so a name made by <c>_G[...]</c> or
/// <c>setfenv</c> is not seen; <c>docs/mechanics/mods.md</c>, section 5.
/// </summary>
internal sealed class CLuaGlobalScan
{
	/// <summary>A use of a free name: its path of fields, the line, and whether it is called.</summary>
	public readonly record struct Use(string Path, int Line, bool Called);

	private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
	{
		"and", "break", "do", "else", "elseif", "end", "false", "for", "function", "if", "in", "local", "nil", "not", "or",
		"repeat", "return", "then", "true", "until", "while",
	};

	private enum TokenKind
	{
		Name,
		Number,
		String,
		Symbol,
	}

	private readonly record struct Token(TokenKind Kind, string Text, int Line);

	public List<Use> Uses { get; } = new();

	/// <summary>The globals the file assigns or declares as functions.</summary>
	public HashSet<string> Defined { get; } = new(StringComparer.Ordinal);

	/// <summary>Every name called after a <c>:</c>, with the line of each call.</summary>
	public List<(string Name, int Line)> MethodCalls { get; } = new();

	/// <summary>Every name a file defines as a method or a field holding a function: <c>function X:name</c>, <c>name = function</c>.</summary>
	public HashSet<string> DefinedMembers { get; } = new(StringComparer.Ordinal);

	/// <summary>Every name used, local or global, with its fields - <c>BiceLib.Leaders.activate</c>, <c>lfs.dir</c>.</summary>
	public List<Use> Paths { get; } = new();

	/// <summary>Every module a literal <c>require</c> names, with its line.</summary>
	public List<(string Module, int Line)> Requires { get; } = new();

	/// <summary>
	/// The requires of <see cref="Requires"/> that this engine never comes to: inside the block of an
	/// <c>if OpenHOI3 == nil then</c>, or the <c>else</c> of an <c>if OpenHOI3 ~= nil then</c> - the global this engine sets
	/// and the original does not (<c>docs/mechanics/mods.md</c>, section 6).
	/// </summary>
	public List<(string Module, int Line)> GatedRequires { get; } = new();

	public static CLuaGlobalScan Read(string text)
	{
		var scan = new CLuaGlobalScan();
		scan.Walk(Tokenise(text));
		return scan;
	}

	private void Walk(List<Token> tokens)
	{
		// The blocks open, each with its locals; and the brackets open, to tell a table's key from an assignment.
		var scopes = new List<HashSet<string>> { new(StringComparer.Ordinal) };
		// Beside each block: whether this engine never comes into it, and whether it would not come into the block's else.
		var gates = new List<(bool Gated, bool ElseGated)> { (false, false) };
		(bool Then, bool Else) pendingGate = (false, false);
		var brackets = new Stack<char>();
		bool Local(string name)
		{
			for (int s = scopes.Count - 1; s >= 0; s--)
			{
				if (scopes[s].Contains(name))
				{
					return true;
				}
			}
			return false;
		}
		void Push()
		{
			scopes.Add(new HashSet<string>(StringComparer.Ordinal));
			gates.Add((gates[^1].Gated, false));
		}
		void Pop()
		{
			if (scopes.Count > 1)
			{
				scopes.RemoveAt(scopes.Count - 1);
				gates.RemoveAt(gates.Count - 1);
			}
		}
		// An if's condition up to its then: OpenHOI3 == nil gates the then, OpenHOI3 ~= nil the else.
		(bool Then, bool Else) Gate(int from)
		{
			for (int at = from; at + 2 < tokens.Count && !Is(at, "then"); at++)
			{
				if (tokens[at].Kind == TokenKind.Name && tokens[at].Text == "OpenHOI3" && Is(at + 2, "nil"))
				{
					if (Is(at + 1, "=="))
					{
						return (true, false);
					}
					if (Is(at + 1, "~="))
					{
						return (false, true);
					}
				}
			}
			return (false, false);
		}
		bool Is(int at, string symbol) => at >= 0 && at < tokens.Count && tokens[at].Kind is TokenKind.Symbol or TokenKind.Name
			&& tokens[at].Text == symbol;
		// "for" and "while" open their block at their "do", which then opens none of its own.
		int pendingLoops = 0;
		var pendingLocals = new List<string>();

		for (int i = 0; i < tokens.Count; i++)
		{
			Token token = tokens[i];
			if (token.Kind == TokenKind.Symbol)
			{
				switch (token.Text)
				{
					case "(":
					case "[":
					case "{":
						brackets.Push(token.Text[0]);
						break;
					case ")":
					case "]":
					case "}":
						if (brackets.Count > 0)
						{
							brackets.Pop();
						}
						break;
				}
				continue;
			}
			if (token.Kind != TokenKind.Name)
			{
				continue;
			}
			switch (token.Text)
			{
				case "function":
				{
					// function a.b:c(params) / local function f(params) / function(params)
					int at = i + 1;
					if (at < tokens.Count && tokens[at].Kind == TokenKind.Name && !Keywords.Contains(tokens[at].Text))
					{
						bool isLocal = Is(i - 1, "local");
						string first = tokens[at].Text;
						var path = new StringBuilder(first);
						int last = at;
						string? member = null;
						while (Is(last + 1, ".") || Is(last + 1, ":"))
						{
							if (last + 2 < tokens.Count && tokens[last + 2].Kind == TokenKind.Name)
							{
								member = tokens[last + 2].Text;
								path.Append(tokens[last + 1].Text).Append(member);
								last += 2;
							}
							else
							{
								break;
							}
						}
						if (isLocal)
						{
							scopes[^1].Add(first);
						}
						else if (member is null)
						{
							if (!Local(first))
							{
								Defined.Add(first);
							}
						}
						else
						{
							DefinedMembers.Add(member);
							if (!Local(first))
							{
								Uses.Add(new Use(first, tokens[at].Line, false));
							}
						}
						Push();
						if (path.ToString().Contains(':'))
						{
							scopes[^1].Add("self");
						}
						i = last;
					}
					else
					{
						Push();
					}
					// The parameters, up to the closing bracket.
					if (Is(i + 1, "("))
					{
						int p = i + 2;
						while (p < tokens.Count && !Is(p, ")"))
						{
							if (tokens[p].Kind == TokenKind.Name)
							{
								scopes[^1].Add(tokens[p].Text);
							}
							p++;
						}
						i = p;          // the ")"; the bracket was never pushed
					}
					continue;
				}
				case "local":
				{
					if (Is(i + 1, "function"))
					{
						continue;
					}
					// local a, b = ... : the names are locals from the next statement on; they are added after the
					// expression, which is near enough for a list of globals.
					int p = i + 1;
					while (p < tokens.Count && tokens[p].Kind == TokenKind.Name && !Keywords.Contains(tokens[p].Text))
					{
						pendingLocals.Add(tokens[p].Text);
						if (!Is(p + 1, ","))
						{
							break;
						}
						p += 2;
					}
					// With no "=", they are locals at once; with one, after the line's expression. Taken as at once:
					// "local x = x" reading the global x is rare in these scripts.
					foreach (string name in pendingLocals)
					{
						scopes[^1].Add(name);
					}
					pendingLocals.Clear();
					i = p;
					continue;
				}
				case "for":
				{
					Push();
					pendingLoops++;
					int p = i + 1;
					while (p < tokens.Count && tokens[p].Kind == TokenKind.Name && !Keywords.Contains(tokens[p].Text))
					{
						scopes[^1].Add(tokens[p].Text);
						if (!Is(p + 1, ","))
						{
							break;
						}
						p += 2;
					}
					i = p;
					continue;
				}
				case "while":
					Push();
					pendingLoops++;
					continue;
				case "do":
					if (pendingLoops > 0)
					{
						pendingLoops--;
					}
					else
					{
						Push();
					}
					continue;
				case "if":
					pendingGate = Gate(i + 1);
					continue;
				case "repeat":
					Push();
					continue;
				case "then":
					Push();
					gates[^1] = (gates[^1].Gated || pendingGate.Then, pendingGate.Else);
					pendingGate = (false, false);
					continue;
				case "elseif":
				case "else":
				{
					bool elseGated = gates[^1].ElseGated;
					Pop();
					if (token.Text == "else")
					{
						Push();
						gates[^1] = (gates[^1].Gated || elseGated, false);
					}
					else
					{
						pendingGate = Gate(i + 1);
					}
					continue;
				}
				case "end":
				case "until":
					Pop();
					continue;
			}
			if (Keywords.Contains(token.Text))
			{
				continue;
			}
			// A field or a method's name: after "." or ":".
			if (Is(i - 1, ".") || Is(i - 1, ":"))
			{
				if (Is(i - 1, ":") && (Is(i + 1, "(") || (i + 1 < tokens.Count && tokens[i + 1].Kind == TokenKind.String) || Is(i + 1, "{")))
				{
					MethodCalls.Add((token.Text, token.Line));
				}
				if (Is(i + 1, "=") && Is(i + 2, "function"))
				{
					DefinedMembers.Add(token.Text);
				}
				continue;
			}
			bool inTable = brackets.Count > 0 && brackets.Peek() == '{';
			// A key of a table: { name = ... }.
			if (inTable && Is(i + 1, "=") && (Is(i - 1, "{") || Is(i - 1, ",") || Is(i - 1, ";")))
			{
				if (Is(i + 2, "function"))
				{
					DefinedMembers.Add(token.Text);
				}
				continue;
			}
			// The path of fields after the name, local or not.
			var used = new StringBuilder(token.Text);
			int end = i;
			while (Is(end + 1, ".") && end + 2 < tokens.Count && tokens[end + 2].Kind == TokenKind.Name)
			{
				used.Append('.').Append(tokens[end + 2].Text);
				end += 2;
			}
			bool called = Is(end + 1, "(") || Is(end + 1, "{") || (end + 1 < tokens.Count && tokens[end + 1].Kind == TokenKind.String)
				|| Is(end + 1, ":");
			if (Is(end + 1, ":") && end + 2 < tokens.Count && tokens[end + 2].Kind == TokenKind.Name)
			{
				used.Append(':').Append(tokens[end + 2].Text);
			}
			if (!Is(end + 1, "=") || end != i)
			{
				Paths.Add(new Use(used.ToString(), token.Line, called));
			}
			// require("name"), require "name"
			if (token.Text == "require")
			{
				// pcall(require, "name") as well.
				int at = Is(i + 1, "(") ? i + 2 : Is(i + 1, ",") && Is(i - 1, "(") ? i + 2 : i + 1;
				if (at < tokens.Count && tokens[at].Kind == TokenKind.String && tokens[at].Text.Length > 0)
				{
					Requires.Add((tokens[at].Text, token.Line));
					if (gates[^1].Gated)
					{
						GatedRequires.Add((tokens[at].Text, token.Line));
					}
				}
			}
			if (Local(token.Text))
			{
				continue;
			}
			// An assignment at statement level defines it.
			if (Is(i + 1, "=") && !inTable)
			{
				Defined.Add(token.Text);
				continue;
			}
			Uses.Add(new Use(used.ToString(), token.Line, called));
		}
	}

	private static List<Token> Tokenise(string text)
	{
		var tokens = new List<Token>();
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
			if (c == '-' && i + 1 < text.Length && text[i + 1] == '-')
			{
				i += 2;
				if (LongBracket(text, i) is int level)
				{
					i = SkipLong(text, i, level, ref line);
				}
				else
				{
					while (i < text.Length && text[i] != '\n')
					{
						i++;
					}
				}
				continue;
			}
			if (c == '[' && LongBracket(text, i) is int stringLevel)
			{
				int startLine = line;
				i = SkipLong(text, i, stringLevel, ref line);
				tokens.Add(new Token(TokenKind.String, "", startLine));
				continue;
			}
			if (c == '"' || c == '\'')
			{
				int startLine = line;
				i++;
				int contentStart = i;
				while (i < text.Length && text[i] != c)
				{
					if (text[i] == '\\' && i + 1 < text.Length)
					{
						if (text[i + 1] == '\n')
						{
							line++;
						}
						i += 2;
						continue;
					}
					if (text[i] == '\n')
					{
						line++;
						break;              // an unfinished string ends at its line
					}
					i++;
				}
				// The text as written, escapes and all: enough for a module's name.
				string content = text[contentStart..Math.Min(i, text.Length)];
				i++;
				tokens.Add(new Token(TokenKind.String, content, startLine));
				continue;
			}
			if (char.IsLetter(c) || c == '_')
			{
				int start = i;
				while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
				{
					i++;
				}
				tokens.Add(new Token(TokenKind.Name, text[start..i], line));
				continue;
			}
			if (char.IsDigit(c) || (c == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
			{
				int start = i;
				while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '.'
					|| ((text[i] == '+' || text[i] == '-') && (text[i - 1] == 'e' || text[i - 1] == 'E'))))
				{
					i++;
				}
				tokens.Add(new Token(TokenKind.Number, text[start..i], line));
				continue;
			}
			// Symbols: the longest of "...", "..", "==", "~=", "<=", ">=", else one character.
			string symbol = i + 2 < text.Length && text.Substring(i, 3) == "..." ? "..."
				: i + 1 < text.Length && Array.IndexOf(TwoCharacterSymbols, text.Substring(i, 2)) >= 0 ? text.Substring(i, 2)
				: c.ToString();
			tokens.Add(new Token(TokenKind.Symbol, symbol, line));
			i += symbol.Length;
		}
		return tokens;
	}

	private static readonly string[] TwoCharacterSymbols = { "..", "==", "~=", "<=", ">=" };

	/// <summary>The level of a long bracket opening at a place - <c>[[</c> is 0, <c>[==[</c> 2 - or null.</summary>
	private static int? LongBracket(string text, int at)
	{
		if (at >= text.Length || text[at] != '[')
		{
			return null;
		}
		int level = 0;
		int i = at + 1;
		while (i < text.Length && text[i] == '=')
		{
			level++;
			i++;
		}
		return i < text.Length && text[i] == '[' ? level : null;
	}

	/// <summary>Past a long bracket's closing, counting its lines.</summary>
	private static int SkipLong(string text, int at, int level, ref int line)
	{
		string close = "]" + new string('=', level) + "]";
		int i = at + level + 2;
		int end = text.IndexOf(close, i, StringComparison.Ordinal);
		int stop = end < 0 ? text.Length : end + close.Length;
		for (int k = i; k < stop && k < text.Length; k++)
		{
			if (text[k] == '\n')
			{
				line++;
			}
		}
		return stop;
	}
}
