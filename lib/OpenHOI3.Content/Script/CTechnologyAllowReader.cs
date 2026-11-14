using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Each technology's <c>allow</c> block in the <c>technologies</c> folder, read as script onto the game's
/// technologies: what a country must answer to research one, asked of the country alone. The original reads it with
/// the whole condition language, so any condition a mod writes is a condition here. The first block of a name is the
/// technology's, as the technology reader takes it, and a second <c>allow</c> in one block adds to the first, as the
/// original loads each into the one block it holds. See <c>docs/mechanics/research.md</c>, section 5.
/// </summary>
public static class CTechnologyAllowReader
{
	private const string Folder = "technologies";

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
		// A technology file is not script beyond its allow blocks - what a level adds to a unit type is figures - so
		// what the parser makes of the rest of it is not reported; what the trigger reader finds in a block is.
		CScriptNode root = CScriptParser.Parse(text, new CScriptProblems());
		foreach (CScriptNode technologyNode in root.Children)
		{
			if (!technologyNode.IsBlock || !seen.Add(technologyNode.Key)
				|| !state.Technologies.TryGetValue(technologyNode.Key, out CTechnology? technology))
			{
				continue;
			}
			foreach (CScriptNode entry in technologyNode.Children)
			{
				if (entry.KeyIs("allow") && CEventReader.Block(entry, context))
				{
					technology.Allow ??= CTrigger.All();
					CTriggerReader.ReadInto(technology.Allow, entry, context);
				}
			}
		}
	}
}
