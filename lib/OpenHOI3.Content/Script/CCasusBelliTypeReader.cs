using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Reads <c>common/cb_types.txt</c>: one block a casus belli type, numbered from one in the file's order,
/// and <c>peace_order</c>, which ranks them. The null type is number nought. See
/// <c>docs/mechanics/war.md</c>, section 2.
/// </summary>
public static class CCasusBelliTypeReader
{
	public const string File = "common/cb_types.txt";

	/// <summary>The keys the original reads and never uses (<c>FINDINGS-war.md</c> B.3): read, checked and dropped.</summary>
	private static readonly HashSet<string> Unused = new(StringComparer.OrdinalIgnoreCase)
	{
		"is_triggered_only", "mutual", "months", "truce_months", "peace_cost_factor", "penalty_factor", "war_name",
		"country_desc", "prov_desc",
	};

	/// <summary>The peace options the loader knows; <c>po_demand_regions</c> and <c>po_concede_defeat</c> are not among them.</summary>
	private static readonly HashSet<string> PeaceOptions = new(StringComparer.OrdinalIgnoreCase)
	{
		"po_annex", "po_demand_region", "po_install_government", "po_change_government", "po_demand_provinces", "po_puppet",
	};

	public static List<CCasusBelliType> Read(CDirectorySettings directories, CScriptContext context)
	{
		string path = directories.Resolve(File);
		var types = new List<CCasusBelliType> { CCasusBelliType.Null };
		if (!System.IO.File.Exists(path))
		{
			return types;
		}
		context.Problems.File = Path.GetRelativePath(directories.GameRoot, path).Replace('\\', '/');
		ReadText(Encoding.Latin1.GetString(System.IO.File.ReadAllBytes(path)), types, context);
		return types;
	}

	/// <summary>
	/// One file's types appended, then ranked by its <c>peace_order</c>. The list is a block of bare names,
	/// which the original reads with a list reader of its own: it is read so here, and blanked - its lines
	/// kept - for the script's parser, which takes every word as a key.
	/// </summary>
	public static void ReadText(string text, List<CCasusBelliType> types, CScriptContext context)
	{
		var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
		// Where each name of the order and each type stands, for what is said of them (logs.md, section 4).
		var orderLines = new Dictionary<string, int>(StringComparer.Ordinal);
		var typeLines = new Dictionary<CCasusBelliType, int>();
		ClausewitzNode? order = ClausewitzParser.Parse(text).Get("peace_order");
		if (order is not null)
		{
			foreach (ClausewitzNode entry in order.Children)
			{
				if (entry.Value is { } name && ranks.TryAdd(name, ranks.Count))
				{
					orderLines[name] = entry.Line;
				}
			}
		}
		CScriptNode root = CScriptParser.Parse(Blanked(text, "peace_order"), context.Problems);
		foreach (CScriptNode node in root.Children)
		{
			if (!node.KeyIs("peace_order") && CEventReader.Block(node, context))
			{
				CCasusBelliType type = ReadType(node, types.Count, context);
				types.Add(type);
				typeLines[type] = node.Line;
			}
		}
		foreach (CCasusBelliType type in types)
		{
			if (!type.IsReal)
			{
				continue;
			}
			if (ranks.TryGetValue(type.Name, out int rank))
			{
				type.Rank = rank;
			}
			else
			{
				context.Problems.Add(EScriptProblem.Quirk, typeLines.GetValueOrDefault(type),
					$"casus belli '{type.Name}' is not in peace_order; the original ranks it first, with nought");
			}
		}
		// A name in the order that is no type ranks nothing; said, where it was passed over (clausewitz.md, section 3).
		foreach (string name in ranks.Keys)
		{
			if (!types.Exists(type => type.IsReal && type.Name == name))
			{
				context.Problems.Add(EScriptProblem.UnknownName, orderLines.GetValueOrDefault(name),
					$"peace_order names '{name}', which is no casus belli type");
			}
		}
	}

	/// <summary>The text with the inside of the first block of that key turned to spaces, its line breaks kept.</summary>
	private static string Blanked(string text, string key)
	{
		int at = text.IndexOf(key, StringComparison.Ordinal);
		// Not in a comment.
		while (at >= 0 && text.LastIndexOf('#', at) > text.LastIndexOf('\n', at))
		{
			at = text.IndexOf(key, at + key.Length, StringComparison.Ordinal);
		}
		int open = at < 0 ? -1 : text.IndexOf('{', at);
		int close = open < 0 ? -1 : text.IndexOf('}', open);
		if (close < 0)
		{
			return text;
		}
		var blanked = new StringBuilder(text);
		for (int i = open + 1; i < close; i++)
		{
			if (blanked[i] != '\n' && blanked[i] != '\r')
			{
				blanked[i] = ' ';
			}
		}
		return blanked.ToString();
	}

	private static CCasusBelliType ReadType(CScriptNode node, int index, CScriptContext context)
	{
		var type = new CCasusBelliType(node.Key, index);
		foreach (CScriptNode entry in node.Children)
		{
			string key = entry.KeyQuoted ? "" : entry.Key.ToLowerInvariant();
			switch (key)
			{
				case "sprite_index":
					if (CEventReader.Text(entry, context) is { } sprite)
					{
						type.SpriteIndex = CArgumentReader.Whole(entry, sprite, context.Problems);
					}
					break;
				case "threat":
					if (CEventReader.Text(entry, context) is { } threat)
					{
						type.Threat = CArgumentReader.Number(entry, threat, context.Problems);
					}
					break;
				case "always":
					type.Always = CEventReader.Yes(entry, context);
					break;
				case "handles_peace":
					type.HandlesPeace = CEventReader.Yes(entry, context);
					break;
				case "is_civil_war":
					type.IsCivilWar = CEventReader.Yes(entry, context);
					break;
				case "is_valid":
					Conditions(entry, type.IsValid, context);
					break;
				case "allowed_countries":
					Conditions(entry, type.AllowedCountries, context);
					break;
				case "allowed_regions":
					Conditions(entry, type.AllowedRegions, context);
					break;
				case "allowed_province":
					Conditions(entry, type.AllowedProvince, context);
					break;
				case "is_valid_join":
				case "prerequisites":
					// Read by nothing: its conditions are still checked.
					Conditions(entry, CTrigger.All(), context);
					break;
				case "on_add":
					if (CEventReader.Block(entry, context))
					{
						CEffectReader.ReadInto(type.OnAdd, entry, context);
					}
					break;
				case "on_completion":
					if (CEventReader.Block(entry, context))
					{
						CEffectReader.ReadInto(type.OnCompletion, entry, context);
					}
					break;
				default:
					if (PeaceOptions.Contains(key))
					{
						if (CEventReader.Yes(entry, context))
						{
							type.PeaceOptions.Add(key);
						}
					}
					else if (Unused.Contains(key))
					{
						CEventReader.Scalar(entry, context);
					}
					else
					{
						context.Problems.Add(EScriptProblem.UnknownKey, entry.Line,
							$"'{entry.Key}' is not a key of a casus belli type; the original drops it");
					}
					break;
			}
		}
		return type;
	}

	private static void Conditions(CScriptNode entry, CTrigger block, CScriptContext context)
	{
		if (CEventReader.Block(entry, context))
		{
			CTriggerReader.ReadInto(block, entry, context);
		}
	}
}
