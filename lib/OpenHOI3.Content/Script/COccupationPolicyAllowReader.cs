using System.IO;
using System.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Occupation;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Each occupation policy's <c>allow</c> block in <c>common/occupation_policies.txt</c>, read as script onto the game's
/// policies: asked of the country choosing with the occupied country as FROM, by a capture's look for
/// <c>civil_war</c> and by the politics screen's choice. See <c>docs/mechanics/occupation.md</c>, sections 1 and 2.
/// </summary>
public static class COccupationPolicyAllowReader
{
	private const string File = "common/occupation_policies.txt";

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
		foreach (CScriptNode policyNode in root.Children)
		{
			if (!policyNode.IsBlock || COccupationPolicies.Find(state, policyNode.Key) is not { IsReal: true } policy)
			{
				continue;
			}
			foreach (CScriptNode entry in policyNode.Children)
			{
				if (entry.KeyIs("allow") && CEventReader.Block(entry, context))
				{
					CTrigger allow = CTrigger.All();
					CTriggerReader.ReadInto(allow, entry, context);
					policy.Allow = allow;
				}
			}
		}
	}
}
