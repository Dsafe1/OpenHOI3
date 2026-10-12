using System.Collections.Generic;
using System.IO;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Reads the decisions of every file in <c>decisions/</c>, layered: <c>docs/mechanics/script.md</c>,
/// section 8.
/// </summary>
public static class CDecisionReader
{
	public static List<CDecision> ReadAll(CDirectorySettings directories, CScriptContext context)
	{
		var decisions = new List<CDecision>();
		foreach (string path in directories.ListFiles("decisions", "*.txt"))
		{
			context.Problems.File = Path.GetRelativePath(directories.GameRoot, path).Replace('\\', '/');
			ReadFile(CScriptParser.ParseFile(path, context.Problems), decisions, context);
		}
		return decisions;
	}

	/// <summary>One file's decisions, appended. A group the original does not know abandons the rest.</summary>
	public static void ReadFile(CScriptNode root, List<CDecision> decisions, CScriptContext context)
	{
		foreach (CScriptNode group in root.Children)
		{
			EDecisionGroup kind;
			if (group.KeyIs("diplomatic_decisions"))
			{
				kind = EDecisionGroup.Diplomatic;
			}
			else if (group.KeyIs("political_decisions"))
			{
				kind = EDecisionGroup.Political;
			}
			else
			{
				context.Problems.Add(EScriptProblem.UnknownKey, group.Line,
					$"'{group.Key}' is no group of decisions; the original reads nothing more of this file");
				return;
			}
			if (!CEventReader.Block(group, context))
			{
				continue;
			}
			foreach (CScriptNode node in group.Children)
			{
				if (CEventReader.Block(node, context))
				{
					decisions.Add(Read(node, kind, context));
				}
			}
		}
	}

	private static CDecision Read(CScriptNode node, EDecisionGroup group, CScriptContext context)
	{
		var read = new CDecision(node.Key, group, context.Problems.File, node.Line);
		foreach (CScriptNode entry in node.Children)
		{
			switch (entry.KeyQuoted ? "" : entry.Key.ToLowerInvariant())
			{
				case "potential":
					if (CEventReader.Block(entry, context))
					{
						CTriggerReader.ReadInto(read.Potential, entry, context);
					}
					break;
				case "allow":
					if (CEventReader.Block(entry, context))
					{
						CTriggerReader.ReadInto(read.Allow, entry, context);
					}
					break;
				case "effect":
					if (CEventReader.Block(entry, context))
					{
						CEffectReader.ReadInto(read.Effects, entry, context);
					}
					break;
				case "ai_will_do":
					CMeanTimeToHappenReader.Read(entry, read.AiWillDo, context, aiChance: false);
					break;
				case "major":
					read.Major = CEventReader.Yes(entry, context);
					break;
				default:
					context.Problems.Add(EScriptProblem.UnknownKey, entry.Line,
						$"'{entry.Key}' is not a key of a decision; the original drops it");
					break;
			}
		}
		return read;
	}
}
