using System.IO;
using System.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Each law's <c>allow</c> block in <c>common/laws.txt</c>, read as script onto the game's laws: what a country must
/// hold to change to it, which the politics screen, the peace-time revert and the AI's Lua ask. See
/// <c>docs/mechanics/politics.md</c>, section 9.
/// </summary>
public static class CLawAllowReader
{
	private const string File = "common/laws.txt";

	public static void Read(CDirectorySettings directories, CGameState state, CScriptContext context)
	{
		string path = directories.Resolve(File);
		if (!System.IO.File.Exists(path))
		{
			return;
		}
		context.Problems.File = Path.GetRelativePath(directories.GameRoot, path).Replace('\\', '/');
		ReadText(Encoding.Latin1.GetString(System.IO.File.ReadAllBytes(path)), state, context);
	}

	public static void ReadText(string text, CGameState state, CScriptContext context)
	{
		CScriptNode root = CScriptParser.Parse(text, context.Problems);
		foreach (CScriptNode groupNode in root.Children)
		{
			if (!groupNode.IsBlock || state.LawGroup(groupNode.Key) is not { } group)
			{
				continue;
			}
			foreach (CScriptNode lawNode in groupNode.Children)
			{
				if (!lawNode.IsBlock || group.Laws.Find(law => law.Name == lawNode.Key) is not { } law)
				{
					continue;
				}
				foreach (CScriptNode entry in lawNode.Children)
				{
					if (entry.KeyIs("allow") && CEventReader.Block(entry, context))
					{
						CTrigger allow = CTrigger.All();
						CTriggerReader.ReadInto(allow, entry, context);
						law.Allow = allow;
					}
				}
			}
		}
	}
}
