using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Content.Definitions;

/// <summary>
/// Reads the kinds of brigade, ship and wing: every block of every file directly under
/// <c>units/</c>. The folder below it, <c>units/models</c>, is the historical models and
/// has a reader of its own. See <c>docs/mechanics/units.md</c>, section 1.
/// </summary>
public static class CSubUnitTypeReader
{
	private const string TypeDirectory = "units";

	/// <summary>The keys whose value is a number, as the original's own loader lists them.</summary>
	public static readonly HashSet<string> Statistics = new(StringComparer.OrdinalIgnoreCase)
	{
		"air_attack", "air_defence", "air_detection", "amphibious_invasion_defence", "amphibious_invasion_speed",
		"ap_attack", "armor_value", "build_cost_ic", "build_cost_manpower", "build_time", "carrier_size",
		"combat_width", "completion_size", "convoy_attack", "default_morale", "default_organisation",
		"defensiveness", "distance", "fuel_consumption", "hard_attack", "hull", "max_percentage_of_type",
		"max_strength", "maximum_speed", "minimum_of_type", "officers", "positioning", "priority",
		"radio_strength", "range", "repair_cost_multiplier", "sea_attack", "sea_defence", "shore_bombardment",
		"soft_attack", "softness", "strategic_attack", "sub_attack", "sub_detection", "supply_consumption",
		"suppression", "surface_defence", "surface_detection", "toughness", "transport_capability",
		"transport_weight", "visibility",
	};

	/// <summary>The keys answered with yes or no.</summary>
	private static readonly HashSet<string> Flags = new(StringComparer.OrdinalIgnoreCase)
	{
		"active", "can_be_pride", "can_paradrop", "capital", "is_armor", "is_bomber", "is_buildable", "is_cag",
		"is_mobile", "is_rocket", "is_sub", "transport",
	};

	/// <summary>The circumstances a type may have figures for besides the terrains.</summary>
	public static readonly HashSet<string> Circumstances = new(StringComparer.OrdinalIgnoreCase)
	{
		"amphibious", "fort", "night", "river",
	};

	/// <summary>Blocks that are read and not yet kept: script, and a table nothing here uses.</summary>
	private static readonly HashSet<string> BlocksNotKept = new(StringComparer.OrdinalIgnoreCase)
	{
		"available_trigger", "extra_amphibious_defence",
	};

	/// <param name="terrains">The names of the terrains, each of which a type may have a block for.</param>
	public static List<CSubUnitType> Read(CDirectorySettings directories, IEnumerable<string> terrains,
		Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		var adjusters = new HashSet<string>(Circumstances, StringComparer.OrdinalIgnoreCase);
		adjusters.UnionWith(terrains);

		var types = new List<CSubUnitType>();
		var fileOf = new Dictionary<CSubUnitType, string>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var unknownKeys = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		var notNumbers = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		var noBranch = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		int duplicates = 0;

		foreach (string file in directories.ListFiles(TypeDirectory, "*.txt"))
		{
			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(file);
			}
			catch (IOException e)
			{
				warn($"Could not read '{file}': {e.Message}");
				continue;
			}

			foreach (ClausewitzNode block in root.Children)
			{
				if (block.Key is null || !block.IsBlock)
				{
					continue;
				}
				if (!seen.Add(block.Key))
				{
					duplicates++;
					continue;
				}
				if (!TryParseBranch(block.GetString("type"), out EUnitBranch branch))
				{
					noBranch.Add(block.Key);
					continue;
				}

				var type = new CSubUnitType(block.Key, branch)
				{
					Sprite = block.GetString("sprite"),
					UnitGroup = block.GetString("unit_group"),
					OnCompletion = block.GetString("on_completion"),
				};
				CSubUnitStatsReader.Read(block, type.Stats, Statistics.Contains, adjusters.Contains,
					entry => ReadOther(type, entry, unknownKeys), notNumbers);
				types.Add(type);
				fileOf[type] = file;
			}
		}
		NumberInDatabaseOrder(types, fileOf);

		if (types.Count == 0)
		{
			warn($"no unit types were found under '{TypeDirectory}'");
		}
		if (duplicates > 0)
		{
			warn($"{duplicates} unit types are declared more than once; the first declaration stands");
		}
		if (noBranch.Count > 0)
		{
			warn($"{noBranch.Count} unit types have no type of land, naval or air and were left out: " +
				string.Join(", ", noBranch));
		}
		if (notNumbers.Count > 0)
		{
			warn($"{notNumbers.Count} figures of unit types are not numbers and were ignored: " +
				string.Join("; ", notNumbers));
		}
		if (unknownKeys.Count > 0)
		{
			warn($"unit types use {unknownKeys.Count} keys that are not understood: " + string.Join(", ", unknownKeys));
		}
		return types;
	}

	/// <summary>
	/// Numbers the types as the original's database holds them: the files of each folder searched together, the lower
	/// layer's first as the files are listed, but in the byte order of their names - upper case before lower, where
	/// the listing ignores case - and a file's types in its order. See <c>docs/mechanics/savegame.md</c>, section 7.
	/// </summary>
	private static void NumberInDatabaseOrder(List<CSubUnitType> types, Dictionary<CSubUnitType, string> fileOf)
	{
		var files = new List<string>();
		foreach (CSubUnitType type in types)
		{
			if (files.Count == 0 || files[^1] != fileOf[type])
			{
				files.Add(fileOf[type]);
			}
		}
		// The listing keeps each folder's files together; within a folder the names are put in byte order.
		var ordered = new List<string>();
		int start = 0;
		while (start < files.Count)
		{
			string folder = Path.GetDirectoryName(files[start]) ?? "";
			int end = start;
			while (end < files.Count && (Path.GetDirectoryName(files[end]) ?? "") == folder)
			{
				end++;
			}
			List<string> run = files.GetRange(start, end - start);
			run.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));
			ordered.AddRange(run);
			start = end;
		}
		int index = 0;
		foreach (string file in ordered)
		{
			foreach (CSubUnitType type in types)
			{
				if (fileOf[type] == file)
				{
					type.DatabaseIndex = ++index;
				}
			}
		}
	}

	private static void ReadOther(CSubUnitType type, ClausewitzNode entry, SortedSet<string> unknownKeys)
	{
		string key = entry.Key!;
		if (!entry.IsBlock && Flags.Contains(key))
		{
			type.Flags[key] = entry.Value!.Equals("yes", StringComparison.OrdinalIgnoreCase);
		}
		else if (entry.IsBlock && key.Equals("usable_by", StringComparison.OrdinalIgnoreCase))
		{
			foreach (ClausewitzNode tag in entry.Children)
			{
				if (tag.Value is { Length: > 0 } value)
				{
					type.UsableBy.Add(value);
				}
			}
		}
		else if (key.Equals("type", StringComparison.OrdinalIgnoreCase)
			|| key.Equals("sprite", StringComparison.OrdinalIgnoreCase)
			|| key.Equals("unit_group", StringComparison.OrdinalIgnoreCase)
			|| key.Equals("on_completion", StringComparison.OrdinalIgnoreCase)
			|| (entry.IsBlock && BlocksNotKept.Contains(key)))
		{
			// Taken where the type was made, or read and not kept.
		}
		else
		{
			unknownKeys.Add(entry.IsBlock ? key + " { }" : key);
		}
	}

	private static bool TryParseBranch(string text, out EUnitBranch branch)
	{
		if (text.Equals("land", StringComparison.OrdinalIgnoreCase))
		{
			branch = EUnitBranch.Land;
			return true;
		}
		if (text.Equals("naval", StringComparison.OrdinalIgnoreCase))
		{
			branch = EUnitBranch.Naval;
			return true;
		}
		branch = EUnitBranch.Air;
		return text.Equals("air", StringComparison.OrdinalIgnoreCase);
	}
}
