using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Each unit type's <c>available_trigger</c> in the <c>units</c> folder, read as script onto the game's types: what a
/// country must answer to build one, asked of the country alone where the block holds anything. The first block of a
/// name is the type's, as the type reader takes it. See <c>docs/mechanics/builders.md</c>, section 2.
/// </summary>
public static class CUnitAvailableTriggerReader
{
	private const string Folder = "units";

	public static void Read(CDirectorySettings directories, CGameState state, CScriptContext context)
	{
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string path in directories.ListFiles(Folder, "*.txt"))
		{
			context.Problems.File = Path.GetRelativePath(directories.GameRoot, path).Replace('\\', '/');
			ReadText(Encoding.Latin1.GetString(File.ReadAllBytes(path)), state, context, seen);
		}
	}

	public static void ReadText(string text, CGameState state, CScriptContext context, ISet<string> seen)
	{
		// A units file is not script - its usable_by is a list of words - so what the parser makes of the rest of it is not
		// reported; what the trigger reader finds in the block is.
		CScriptNode root = CScriptParser.Parse(text, new CScriptProblems());
		foreach (CScriptNode typeNode in root.Children)
		{
			if (!typeNode.IsBlock || !seen.Add(typeNode.Key) || !state.SubUnitTypes.TryGetValue(typeNode.Key, out CSubUnitType? type))
			{
				continue;
			}
			foreach (CScriptNode entry in typeNode.Children)
			{
				if (entry.KeyIs("available_trigger") && CEventReader.Block(entry, context))
				{
					CTrigger trigger = CTrigger.All();
					CTriggerReader.ReadInto(trigger, entry, context);
					type.AvailableTrigger = trigger;
				}
			}
		}
	}
}
