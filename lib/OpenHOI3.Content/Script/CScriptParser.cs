using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Builds the <c>key = value</c> tree of an event or decision file. Braces decide where a block
/// ends; the original reads a block by its keywords instead, and goes astray where a file gives
/// a keyword the wrong shape - here that is a problem the readers report.
/// <c>docs/mechanics/script.md</c>, section 2.
/// </summary>
public static class CScriptParser
{
	/// <summary>Reads a file as Windows-1252, whose bytes Latin-1 keeps, and parses it.</summary>
	public static CScriptNode ParseFile(string absolutePath, CScriptProblems problems)
	{
		return Parse(Encoding.Latin1.GetString(File.ReadAllBytes(absolutePath)), problems);
	}

	/// <summary>The file's entries, as the children of a nameless block.</summary>
	public static CScriptNode Parse(string text, CScriptProblems problems)
	{
		List<CScriptToken> tokens = CScriptLexer.Tokenize(text);
		var root = new CScriptNode("", false, 1);
		int index = 0;
		ReadBlock(root, tokens, ref index, problems, topLevel: true);
		return root;
	}

	private static void ReadBlock(CScriptNode parent, List<CScriptToken> tokens, ref int i,
		CScriptProblems problems, bool topLevel)
	{
		while (i < tokens.Count)
		{
			CScriptToken token = tokens[i];
			switch (token.Kind)
			{
				case EScriptToken.Close:
					i++;
					if (topLevel)
					{
						problems.Add(EScriptProblem.Structure, token.Line, "a '}' closes no block");
						continue;
					}
					return;
				case EScriptToken.Open:
					problems.Add(EScriptProblem.Structure, token.Line, "a block with no key before it is skipped");
					i++;
					ReadBlock(new CScriptNode("", false, token.Line), tokens, ref i, problems, topLevel: false);
					continue;
				case EScriptToken.Equals:
				case EScriptToken.Punctuation:
					problems.Add(EScriptProblem.Structure, token.Line, $"'{token.Text}' where a key goes is skipped");
					i++;
					continue;
			}

			if (i + 1 >= tokens.Count || tokens[i + 1].Kind != EScriptToken.Equals)
			{
				problems.Add(EScriptProblem.Structure, token.Line,
					$"'{token.Text}' has no '=' after it; the original loses the word after it and keeps the value before");
				// The word after it goes with it, as it does in the original.
				i += i + 1 < tokens.Count && tokens[i + 1].Kind is EScriptToken.Word or EScriptToken.Quoted ? 2 : 1;
				continue;
			}
			i += 2;
			if (i >= tokens.Count)
			{
				problems.Add(EScriptProblem.Structure, token.Line, $"'{token.Text} =' ends the file");
				return;
			}

			CScriptToken value = tokens[i];
			bool quoted = token.Kind == EScriptToken.Quoted;
			if (value.Kind == EScriptToken.Open)
			{
				i++;
				var block = new CScriptNode(token.Text, quoted, token.Line);
				ReadBlock(block, tokens, ref i, problems, topLevel: false);
				parent.Children.Add(block);
				continue;
			}
			if (value.Kind is EScriptToken.Word or EScriptToken.Quoted)
			{
				parent.Children.Add(new CScriptNode(token.Text, quoted, token.Line)
				{
					Value = value.Text,
					ValueQuoted = value.Kind == EScriptToken.Quoted,
				});
				i++;
				continue;
			}
			problems.Add(EScriptProblem.Structure, value.Line, $"'{token.Text} =' is followed by '{value.Text}'");
		}
		if (!topLevel)
		{
			problems.Add(EScriptProblem.Structure, parent.Line, $"the block of '{parent.Key}' is not closed");
		}
	}
}
