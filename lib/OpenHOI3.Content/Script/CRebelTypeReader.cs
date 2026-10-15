using System.Collections.Generic;
using System.IO;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Reads <c>common/rebel_types.txt</c>: a block a type, in the file's order, with the seventeen keys the original's
/// loader names. <c>area</c>, <c>defection</c> and <c>independence</c> are yes for any word but <c>nation</c> or
/// <c>none</c>; a key the loader does not name - <c>unit_transfer</c> among them - is reported, where the original drops
/// it. See <c>docs/mechanics/rebels.md</c>, section 2.
/// </summary>
public static class CRebelTypeReader
{
	public const string File = "common/rebel_types.txt";

	/// <summary>The defines' block <c>YEARS_UNTIL_BROKEN</c> is in, the default of <c>defect_delay</c> in years.</summary>
	public const string Section = "country";

	public static List<CRebelType> Read(CDirectorySettings directories, CGameState state, CScriptContext context)
	{
		var read = new List<CRebelType>();
		string path = directories.Resolve(File);
		if (!System.IO.File.Exists(path))
		{
			return read;
		}
		context.Problems.File = Path.GetRelativePath(directories.GameRoot, path).Replace('\\', '/');
		ReadFile(CScriptParser.ParseFile(path, context.Problems), read, state, context);
		return read;
	}

	public static void ReadFile(CScriptNode root, List<CRebelType> read, CGameState state, CScriptContext context)
	{
		int delay = (int)(state.Defines[Section, "YEARS_UNTIL_BROKEN"].Raw / 1000) * 12;
		foreach (CScriptNode node in root.Children)
		{
			if (CEventReader.Block(node, context))
			{
				read.Add(ReadOne(node, read.Count, delay, state, context));
			}
		}
	}

	private static CRebelType ReadOne(CScriptNode node, int index, int delay, CGameState state, CScriptContext context)
	{
		var type = new CRebelType(node.Key, index) { DefectDelay = delay };
		foreach (CScriptNode child in node.Children)
		{
			switch (child.KeyQuoted ? "" : child.Key.ToLowerInvariant())
			{
				case "spawn_chance":
					CMeanTimeToHappenReader.Read(child, type.SpawnChance, context, aiChance: false);
					break;
				case "movement_evaluation":
					CMeanTimeToHappenReader.Read(child, type.MovementEvaluation, context, aiChance: false);
					break;
				case "siege_won_trigger":
					if (CEventReader.Block(child, context))
					{
						CTriggerReader.ReadInto(type.SiegeWonTrigger, child, context);
					}
					break;
				case "can_enforce_trigger":
					if (CEventReader.Block(child, context))
					{
						CTriggerReader.ReadInto(type.CanEnforceTrigger, child, context);
					}
					break;
				case "siege_won_effect":
					if (CEventReader.Block(child, context))
					{
						CEffectReader.ReadInto(type.SiegeWonEffect, child, context);
					}
					break;
				case "demands_enforced_effect":
					if (CEventReader.Block(child, context))
					{
						CEffectReader.ReadInto(type.DemandsEnforcedEffect, child, context);
					}
					break;
				case "area":
					type.AnyArea = !Is(child, "nation", context);
					break;
				case "defection":
					type.Defection = !Is(child, "none", context);
					break;
				case "independence":
					type.Independence = !Is(child, "none", context);
					break;
				case "ideology":
					if (CEventReader.Text(child, context) is { } ideology && !ideology.Equals("any", System.StringComparison.OrdinalIgnoreCase))
					{
						CArgumentReader.CheckName(child, EScriptName.Ideology, ideology, context);
						type.Ideology = ideology;
					}
					break;
				case "defect_delay":
					if (CEventReader.Text(child, context) is { } months)
					{
						type.DefectDelay = CArgumentReader.Whole(child, months, context.Problems);
					}
					break;
				case "unit":
					if (CEventReader.Text(child, context) is { } unit)
					{
						if (!state.SubUnitTypes.ContainsKey(unit))
						{
							context.Problems.Add(EScriptProblem.UnknownName, child.Line,
								$"'unit = {unit}': there is no brigade type of that name, so its rebels have none");
						}
						type.Unit = unit;
					}
					break;
				case "resilient":
					type.Resilient = CEventReader.Yes(child, context);
					break;
				case "reinforcing":
					type.Reinforcing = CEventReader.Yes(child, context);
					break;
				case "smart":
					type.Smart = CEventReader.Yes(child, context);
					break;
				case "auto_convert":
					type.AutoConvert = CEventReader.Yes(child, context);
					break;
				case "is_triggered_only":
					type.IsTriggeredOnly = CEventReader.Yes(child, context);
					break;
				default:
					context.Problems.Add(EScriptProblem.UnknownKey, child.Line,
						$"'{child.Key}' is not a key of a rebel type; the original drops it");
					break;
			}
		}
		return type;
	}

	/// <summary>Whether a two-valued key is given its one word; any other word is the other value.</summary>
	private static bool Is(CScriptNode node, string word, CScriptContext context)
	{
		return CEventReader.Text(node, context) is { } text && text.Equals(word, System.StringComparison.OrdinalIgnoreCase);
	}
}
