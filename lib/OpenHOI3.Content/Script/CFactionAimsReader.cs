using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Reads <c>common/faction_aims.txt</c>, the victory conditions: a block a faction, named as its group names it, and in
/// it a block a condition, each with its <c>trigger</c> and maybe <c>primary = yes</c>. A faction no group has, and any
/// other key, is reported. See <c>docs/mechanics/factions.md</c>, section 8.
/// </summary>
public static class CFactionAimsReader
{
	public const string File = "common/faction_aims.txt";

	public static List<(string Faction, List<CVictoryCondition> Conditions)> Read(CDirectorySettings directories, CGameState state,
		CScriptContext context)
	{
		var read = new List<(string, List<CVictoryCondition>)>();
		string path = directories.Resolve(File);
		if (!System.IO.File.Exists(path))
		{
			return read;
		}
		context.Problems.File = Path.GetRelativePath(directories.GameRoot, path).Replace('\\', '/');
		ReadText(Encoding.Latin1.GetString(System.IO.File.ReadAllBytes(path)), state, read, context);
		return read;
	}

	public static void ReadText(string text, CGameState state, List<(string Faction, List<CVictoryCondition> Conditions)> read,
		CScriptContext context)
	{
		CScriptNode root = CScriptParser.Parse(text, context.Problems);
		foreach (CScriptNode node in root.Children)
		{
			if (!CEventReader.Block(node, context))
			{
				continue;
			}
			string faction = CFactions.Named(state, node.Key);
			if (faction.Length == 0)
			{
				context.Problems.Add(EScriptProblem.UnknownName, node.Line, $"'{node.Key}' is no faction");
				continue;
			}
			int at = read.FindIndex(entry => entry.Faction == faction);
			if (at < 0)
			{
				read.Add((faction, new List<CVictoryCondition>()));
				at = read.Count - 1;
			}
			foreach (CScriptNode aim in node.Children)
			{
				if (CEventReader.Block(aim, context))
				{
					read[at].Conditions.Add(ReadCondition(aim, context));
				}
			}
		}
	}

	private static CVictoryCondition ReadCondition(CScriptNode node, CScriptContext context)
	{
		CTrigger trigger = CTrigger.All();
		bool primary = false;
		foreach (CScriptNode entry in node.Children)
		{
			switch (entry.KeyQuoted ? "" : entry.Key.ToLowerInvariant())
			{
				case "trigger":
					if (CEventReader.Block(entry, context))
					{
						CTriggerReader.ReadInto(trigger, entry, context);
					}
					break;
				case "primary":
					primary = CEventReader.Yes(entry, context);
					break;
				default:
					context.Problems.Add(EScriptProblem.UnknownKey, entry.Line,
						$"'{entry.Key}' in victory condition '{node.Key}': only trigger and primary are read");
					break;
			}
		}
		return new CVictoryCondition(node.Key, trigger, primary);
	}
}
