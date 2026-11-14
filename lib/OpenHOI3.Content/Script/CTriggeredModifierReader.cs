using System.Collections.Generic;
using System.IO;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Reads <c>common/triggered_modifiers.txt</c>: a block an entry, with its <c>potential</c> and <c>trigger</c>
/// conditions, asked in a country's scope, and its <c>icon</c>; every other key is a kind of modifier with its number,
/// one that is not a number is reported, and one that is no kind (<see cref="CModifierKeys"/>) is reported and dropped,
/// as the original logs and drops it. See <c>docs/mechanics/modifiers.md</c>, sections 2 and 5.
/// </summary>
public static class CTriggeredModifierReader
{
	public const string File = "common/triggered_modifiers.txt";

	/// <param name="kinds">The kinds of modifier the file may name; none for the engine's and the missions' alone.</param>
	public static List<CTriggeredModifier> Read(CDirectorySettings directories, CScriptContext context,
		CModifierKeys? kinds = null)
	{
		var read = new List<CTriggeredModifier>();
		string path = directories.Resolve(File);
		if (!System.IO.File.Exists(path))
		{
			return read;
		}
		context.Problems.File = Path.GetRelativePath(directories.GameRoot, path).Replace('\\', '/');
		ReadFile(CScriptParser.ParseFile(path, context.Problems), read, context, kinds);
		return read;
	}

	public static void ReadFile(CScriptNode root, List<CTriggeredModifier> read, CScriptContext context,
		CModifierKeys? kinds = null)
	{
		kinds ??= CModifierKeys.WithoutFactions;
		foreach (CScriptNode node in root.Children)
		{
			if (CEventReader.Block(node, context))
			{
				read.Add(ReadOne(node, context, kinds));
			}
		}
	}

	private static CTriggeredModifier ReadOne(CScriptNode node, CScriptContext context, CModifierKeys kinds)
	{
		var entry = new CTriggeredModifier(node.Key);
		foreach (CScriptNode child in node.Children)
		{
			switch (child.KeyQuoted ? "" : child.Key.ToLowerInvariant())
			{
				case "potential":
					if (CEventReader.Block(child, context))
					{
						CTriggerReader.ReadInto(entry.Potential, child, context);
					}
					break;
				case "trigger":
					if (CEventReader.Block(child, context))
					{
						CTriggerReader.ReadInto(entry.Trigger, child, context);
					}
					break;
				case "icon":
					if (CEventReader.Scalar(child, context) is { } icon)
					{
						entry.Icon = CArgumentReader.Whole(child, icon.Text, context.Problems);
					}
					break;
				default:
					// A quoted key is never a keyword, so it is no kind either.
					if (child.KeyQuoted || !kinds.Contains(child.Key))
					{
						context.Problems.Add(EScriptProblem.UnknownKey, child.Line,
							$"'{child.Key}' is no kind of modifier; the original logs 'unknown modifier' and drops it");
					}
					else if (child.IsBlock)
					{
						context.Problems.Add(EScriptProblem.WrongShape, child.Line,
							$"'{child.Key}' is not a key of a triggered modifier, and a modifier takes a number; the original drops it");
					}
					else
					{
						// A key written twice keeps the last value, as a building's modifier does.
						entry.Modifier.Set(child.Key, CArgumentReader.Number(child, child.Value ?? "", context.Problems));
					}
					break;
			}
		}
		return entry;
	}
}
