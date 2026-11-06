using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenHOI3.Content.Shaders;

/// <summary>
/// The C preprocessor as the original's effect files use it: comments taken out, <c>#include</c> of another file - again
/// each time it is named, as <c>water.fx</c> names <c>water_include.h</c> six times with different flags - object-like
/// <c>#define</c> and <c>#undef</c>, and <c>#ifdef</c>, <c>#ifndef</c>, <c>#else</c> and <c>#endif</c>. A macro is
/// replaced word by word and again inside what replaced it, never inside itself. Lines keep their place, so a problem
/// reported later names the line it is on. See <c>docs/mechanics/shaders.md</c>, section 2.
/// </summary>
public static class CFxPreprocessor
{
	/// <summary>How deep includes may nest before a loop is assumed.</summary>
	private const int MaxDepth = 16;

	private static readonly Regex Word = new(@"[A-Za-z_]\w*", RegexOptions.Compiled);

	/// <param name="source">The file's text.</param>
	/// <param name="readInclude">The text of a file an <c>#include</c> names, or null where there is none.</param>
	/// <param name="problem">Told of each directive it cannot follow.</param>
	public static string Process(string source, Func<string, string?> readInclude, Action<string> problem)
	{
		var defines = new Dictionary<string, string>(StringComparer.Ordinal);
		var output = new StringBuilder();
		Run(source, defines, readInclude, problem, output, 0);
		return output.ToString();
	}

	private static void Run(string source, Dictionary<string, string> defines, Func<string, string?> readInclude,
		Action<string> problem, StringBuilder output, int depth)
	{
		string[] lines = StripComments(source).Split('\n');
		// Each open conditional: whether its condition held, and whether the lines outside it are kept.
		var open = new Stack<(bool Taken, bool Outer)>();
		bool active = true;
		for (int number = 0; number < lines.Length; number++)
		{
			string line = lines[number].TrimEnd('\r');
			string trimmed = line.TrimStart();
			if (!trimmed.StartsWith('#'))
			{
				output.Append(active ? Expand(line, defines) : "").Append('\n');
				continue;
			}
			string directive = trimmed[1..].TrimStart();
			string name = FirstWord(directive);
			string rest = directive[name.Length..].Trim();
			switch (name)
			{
				case "ifdef":
				case "ifndef":
					bool taken = defines.ContainsKey(FirstWord(rest)) == (name == "ifdef");
					open.Push((taken, active));
					active = active && taken;
					break;
				case "else":
					if (open.Count == 0)
					{
						problem($"line {number + 1}: #else with no #if");
						break;
					}
					(bool wasTaken, bool outer) = open.Pop();
					active = outer && !wasTaken;
					open.Push((true, outer));
					break;
				case "endif":
					if (open.Count == 0)
					{
						problem($"line {number + 1}: #endif with no #if");
						break;
					}
					active = open.Pop().Outer;
					break;
				case "define" when active:
					string macro = FirstWord(rest);
					if (macro.Length == 0 || rest.Length > macro.Length && rest[macro.Length] == '(')
					{
						problem($"line {number + 1}: #define {rest} is not an object-like macro");
						break;
					}
					defines[macro] = rest[macro.Length..].Trim();
					break;
				case "undef" when active:
					defines.Remove(FirstWord(rest));
					break;
				case "include" when active:
					string file = rest.Trim().Trim('"', '<', '>');
					if (depth >= MaxDepth)
					{
						problem($"line {number + 1}: #include \"{file}\" nests too deep");
					}
					else if (readInclude(file) is { } included)
					{
						Run(included, defines, readInclude, problem, output, depth + 1);
					}
					else
					{
						problem($"line {number + 1}: #include \"{file}\" was not found");
					}
					break;
				case "define":
				case "undef":
				case "include":
					break;
				default:
					if (active)
					{
						problem($"line {number + 1}: #{name} is not followed");
					}
					break;
			}
			output.Append('\n');
		}
		if (open.Count > 0)
		{
			problem("an #if is never closed");
		}
	}

	/// <summary>Every macro in a line replaced by what it stands for, and again inside that, a macro never inside itself.</summary>
	public static string Expand(string line, IReadOnlyDictionary<string, string> defines)
	{
		return Expand(line, defines, new HashSet<string>(StringComparer.Ordinal));
	}

	private static string Expand(string line, IReadOnlyDictionary<string, string> defines, HashSet<string> expanding)
	{
		if (defines.Count == 0)
		{
			return line;
		}
		return Word.Replace(line, match =>
		{
			string word = match.Value;
			if (!defines.TryGetValue(word, out string? body) || expanding.Contains(word) || InNumber(line, match.Index))
			{
				return word;
			}
			expanding.Add(word);
			string expanded = Expand(body, defines, expanding);
			expanding.Remove(word);
			return expanded;
		});
	}

	/// <summary>Whether a word starts inside a number, as the <c>f</c> of <c>0.5f</c> or the <c>e5</c> of <c>1e5</c>.</summary>
	private static bool InNumber(string line, int index)
	{
		return index > 0 && (char.IsDigit(line[index - 1]) || line[index - 1] == '.');
	}

	private static string FirstWord(string text)
	{
		Match match = Word.Match(text);
		return match.Success && match.Index == 0 ? match.Value : "";
	}

	/// <summary>The text with every comment replaced by spaces, its line breaks kept.</summary>
	public static string StripComments(string source)
	{
		var output = new StringBuilder(source.Length);
		int i = 0;
		while (i < source.Length)
		{
			char c = source[i];
			if (c == '"')
			{
				int end = source.IndexOf('"', i + 1);
				end = end < 0 ? source.Length - 1 : end;
				output.Append(source, i, end - i + 1);
				i = end + 1;
			}
			else if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
			{
				while (i < source.Length && source[i] != '\n')
				{
					i++;
				}
			}
			else if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
			{
				int end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
				end = end < 0 ? source.Length : end + 2;
				for (int k = i; k < end; k++)
				{
					output.Append(source[k] == '\n' ? '\n' : ' ');
				}
				i = end;
			}
			else
			{
				output.Append(c);
				i++;
			}
		}
		return output.ToString();
	}
}
