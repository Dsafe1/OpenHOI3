using System.Collections.Generic;
using System.IO;
using OpenHOI3.Sim.Espionage;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Reads <c>common/covert_ops.txt</c>: a block an operation, with its <c>cost</c>, its <c>is_valid</c> and
/// <c>is_selectable</c> conditions and its <c>effect</c>, the conditions asked and the effects run in a province's
/// scope. Any other key is reported. See <c>docs/mechanics/espionage.md</c>, section 11.
/// </summary>
public static class CCovertOpsReader
{
	public const string File = "common/covert_ops.txt";

	public static List<CCovertOperation> Read(CDirectorySettings directories, CScriptContext context)
	{
		var read = new List<CCovertOperation>();
		string path = directories.Resolve(File);
		if (!System.IO.File.Exists(path))
		{
			return read;
		}
		context.Problems.File = Path.GetRelativePath(directories.GameRoot, path).Replace('\\', '/');
		ReadFile(CScriptParser.ParseFile(path, context.Problems), read, context);
		return read;
	}

	public static void ReadFile(CScriptNode root, List<CCovertOperation> read, CScriptContext context)
	{
		foreach (CScriptNode node in root.Children)
		{
			if (CEventReader.Block(node, context))
			{
				read.Add(ReadOne(node, context));
			}
		}
	}

	private static CCovertOperation ReadOne(CScriptNode node, CScriptContext context)
	{
		var operation = new CCovertOperation(node.Key);
		foreach (CScriptNode entry in node.Children)
		{
			switch (entry.KeyQuoted ? "" : entry.Key.ToLowerInvariant())
			{
				case "cost":
					if (CEventReader.Scalar(entry, context) is { } cost)
					{
						operation.Cost = CArgumentReader.Whole(entry, cost.Text, context.Problems);
					}
					break;
				case "is_valid":
					if (CEventReader.Block(entry, context))
					{
						CTriggerReader.ReadInto(operation.IsValid, entry, context);
					}
					break;
				case "is_selectable":
					if (CEventReader.Block(entry, context))
					{
						CTriggerReader.ReadInto(operation.IsSelectable, entry, context);
					}
					break;
				case "effect":
					if (CEventReader.Block(entry, context))
					{
						CEffectReader.ReadInto(operation.Effects, entry, context);
					}
					break;
				default:
					context.Problems.Add(EScriptProblem.UnknownKey, entry.Line,
						$"'{entry.Key}' is not a key of a covert operation; the original drops it");
					break;
			}
		}
		return operation;
	}
}
