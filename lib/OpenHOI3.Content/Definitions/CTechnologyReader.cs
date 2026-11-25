using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Content.Definitions;

/// <summary>
/// Reads the technologies: the categories out of <c>common/technology.txt</c>, and every
/// block of every file under <c>technologies/</c>.
///
/// A block holds what a level is worth as plain numbers - <c>ic_modifier = 0.025</c> -
/// and what researching it takes: its difficulty, the years its levels are due, whose
/// ability makes it cheaper. A block named after a unit type is what a level adds to that
/// type (<c>docs/mechanics/units.md</c>, section 2). What it unlocks is skipped without
/// comment, and its <c>allow</c> block is script, which <c>CTechnologyAllowReader</c> reads
/// once everything it may name is loaded. See <c>docs/mechanics/research.md</c>, section 1.
/// </summary>
public static class CTechnologyReader
{
	private const string TechnologyDirectory = "technologies";
	private const string CategoryFile = "common/technology.txt";

	/// <summary>Keys in a technology's block that describe the technology, not its effect.</summary>
	private static readonly HashSet<string> OwnKeys = new(StringComparer.OrdinalIgnoreCase)
	{
		"difficulty", "start_year", "first_offset", "additional_offset", "max_level",
		"folder", "change", "can_upgrade", "is_nuclear", "on_completion", "stealable",
	};

	/// <summary>A technology's own blocks: what it is researched from, its decay and its precondition.</summary>
	private static readonly HashSet<string> OwnBlocks = new(StringComparer.OrdinalIgnoreCase)
	{
		"research_bonus_from", "decay", "allow",
	};

	/// <summary>A technology's own keys that are numbers; one that is not is reported and read as nought.</summary>
	private static readonly HashSet<string> OwnNumbers = new(StringComparer.OrdinalIgnoreCase)
	{
		"difficulty", "start_year", "first_offset", "additional_offset", "max_level",
	};

	/// <summary>A technology's own keys that name something: a unit type or a building it makes available.</summary>
	private static readonly HashSet<string> OwnWords = new(StringComparer.OrdinalIgnoreCase)
	{
		"activate_unit", "activate_building",
	};

	/// <summary>The names a block is read under as a mission's figures.</summary>
	private static readonly HashSet<string> Missions =
		new(OpenHOI3.Sim.Missions.CMissionEfficiency.Names, StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// The theories and practicals, in the order the file lists them: every theory, then
	/// every practical.
	/// </summary>
	public static List<CTechnologyCategory> ReadCategories(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		var categories = new List<CTechnologyCategory>();
		string path = directories.Resolve(CategoryFile);
		if (!File.Exists(path))
		{
			warn(CDiagnostics.Located(path, 0, "no technology categories: the file is missing"));
			return categories;
		}

		ClausewitzNode root = ClausewitzParser.ParseFile(path);
		// Said, where it was passed over (clausewitz.md, section 3): what is no list of the file's, and a pair in one.
		foreach (ClausewitzNode top in root.Children)
		{
			if (top.Key is null || !(top.Key.Equals("theoretical", StringComparison.OrdinalIgnoreCase)
				|| top.Key.Equals("practical", StringComparison.OrdinalIgnoreCase)
				|| top.Key.Equals("folders", StringComparison.OrdinalIgnoreCase)))
			{
				warn(CDiagnostics.Located(path, top.Line, $"'{top.Key ?? top.Value}' is no list of the file's; passed over"));
				continue;
			}
			foreach (ClausewitzNode entry in top.Children)
			{
				if (entry.Key is not null)
				{
					warn(CDiagnostics.Located(path, entry.Line,
						$"'{entry.Key}' in {top.Key} is a pair, where the list holds names; passed over"));
				}
			}
		}
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach ((string key, bool isTheory) in new[] { ("theoretical", true), ("practical", false) })
		{
			foreach (ClausewitzNode entry in root.Get(key)?.Children ?? new List<ClausewitzNode>())
			{
				if (entry.Value is { Length: > 0 } name && seen.Add(name))
				{
					categories.Add(new CTechnologyCategory(name, isTheory));
				}
			}
		}
		return categories;
	}

	/// <summary>
	/// The folders the technology screen sorts technologies into, in the order the file
	/// lists them under <c>folders</c>.
	/// </summary>
	public static List<string> ReadFolders(CDirectorySettings directories)
	{
		var folders = new List<string>();
		string path = directories.Resolve(CategoryFile);
		if (!File.Exists(path))
		{
			return folders;
		}
		foreach (ClausewitzNode entry in ClausewitzParser.ParseFile(path).Get("folders")?.Children ?? new List<ClausewitzNode>())
		{
			if (entry.Value is { Length: > 0 } name && !folders.Contains(name, StringComparer.OrdinalIgnoreCase))
			{
				folders.Add(name);
			}
		}
		return folders;
	}

	/// <param name="unitTypes">
	/// The unit types, by key. With them a block named after one is read as what a level
	/// adds to that type, and each type is told which technologies improve it; without,
	/// such blocks are skipped.
	/// </param>
	/// <returns>Every declaration in the files' order, the earlier of a name declared twice marked
	/// <see cref="CTechnology.IsShadowed"/>.</returns>
	public static List<CTechnology> Read(CDirectorySettings directories, Action<string>? warn = null,
		IReadOnlyCollection<CTechnologyCategory>? categories = null,
		IReadOnlyDictionary<string, CSubUnitType>? unitTypes = null, IEnumerable<string>? terrains = null)
	{
		warn ??= CDiagnostics.Problem;
		var adjusters = new HashSet<string>(CSubUnitTypeReader.Circumstances, StringComparer.OrdinalIgnoreCase);
		adjusters.UnionWith(terrains ?? Array.Empty<string>());
		var technologies = new List<CTechnology>();

		// Each fault said once at its file and line, in the files' order (logs.md, section 4).
		var reported = new HashSet<string>(StringComparer.Ordinal);
		void Report(string file, int line, string message)
		{
			string located = CDiagnostics.Located(file, line, message);
			if (reported.Add(located))
			{
				warn(located);
			}
		}
		var fileOf = new Dictionary<ClausewitzNode, string>();

		// Every block first, in the files' order. A name declared twice is two technologies in the original, each in its
		// place: a save lists both under the one name, and each unit type's levels by position count both, while every look
		// by name finds the later. So the earlier is kept, marked IsShadowed, for its unit types and the save alone - the game
		// state lists only the later (measured on a BlackICE start save, whose one technology declared twice is
		// cag_design_hidden; research.md, section 1).
		var blocks = new List<ClausewitzNode>();
		var shadowed = new HashSet<ClausewitzNode>();
		var names = new Dictionary<string, ClausewitzNode>(StringComparer.OrdinalIgnoreCase);
		// The files in the original's order, which ListFiles gives: every name over all the layers once, sorted by byte
		// with case (mods.md, section 2) - so BlackICE's Aircraft_Armament before its Aircraftsystems, as its save lists
		// their technologies.
		foreach (string file in directories.ListFiles(TechnologyDirectory, "*.txt"))
		{
			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(file);
			}
			catch (IOException e)
			{
				warn(CDiagnostics.Located(file, 0, $"could not be read: {e.Message}"));
				continue;
			}

			foreach (ClausewitzNode block in root.Children)
			{
				if (block.Key is null || !block.IsBlock)
				{
					continue;
				}
				fileOf[block] = file;
				if (names.TryGetValue(block.Key, out ClausewitzNode? earlier))
				{
					shadowed.Add(earlier);
					Report(file, block.Line, $"technology '{block.Key}' is declared again, first at {fileOf[earlier]}:{earlier.Line}; " +
						"the last is the one a name finds, the earlier kept for its unit types");
				}
				names[block.Key] = block;
				blocks.Add(block);
			}
		}

		var categoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (CTechnologyCategory category in categories ?? Array.Empty<CTechnologyCategory>())
		{
			categoryNames.Add(category.Name);
		}

		foreach (ClausewitzNode block in blocks)
		{
			string file = fileOf[block];
			var effect = new CModifier(block.Key!);
			foreach (ClausewitzNode entry in block.Children)
			{
				if (entry.Key is not null && entry.Value is not null && !OwnKeys.Contains(entry.Key)
					&& CFixedPoint.TryParse(entry.Value, out CFixedPoint value))
				{
					effect.Set(entry.Key, value);
				}
				else if (entry.Key is not null && (entry.Key.Equals(CTechnology.AllowEscorts, StringComparison.OrdinalIgnoreCase)
						|| entry.Key.Equals(CTechnology.ListeningStation, StringComparison.OrdinalIgnoreCase))
					&& "yes".Equals(entry.Value, StringComparison.OrdinalIgnoreCase))
				{
					// A flag and not a figure: a level of it is worth one.
					effect.Set(entry.Key.ToLowerInvariant(), CFixedPoint.One);
				}
			}

			string onCompletion = block.GetString("on_completion");
			if (onCompletion.Length != 0 && categories is not null && !categoryNames.Contains(onCompletion))
			{
				Report(file, block.Get("on_completion")?.Line ?? block.Line,
					$"{block.Key}: on_completion names a category that is not defined: {onCompletion}");
				onCompletion = "";
			}
			void UnknownAmount(ClausewitzNode entry, string message) => Report(file, entry.Line, message);

			var technology = new CTechnology(block.Key!, block.GetInt("max_level"), effect)
			{
				// A fraction is dropped, as the original drops it: 0.5 is no difficulty at all.
				Difficulty = WholeNumber(block.GetString("difficulty")),
				StartYear = WholeNumber(block.GetString("start_year")),
				FirstOffset = WholeNumber(block.GetString("first_offset")),
				AdditionalOffset = WholeNumber(block.GetString("additional_offset")),
				IsNuclear = block.GetString("is_nuclear").Equals("yes", StringComparison.OrdinalIgnoreCase),
				OnCompletion = onCompletion,
				Folder = block.GetString("folder"),
				ResearchBonusFrom = ReadAmounts(block.Get("research_bonus_from"), categories is null ? null : categoryNames, UnknownAmount),
				Decay = ReadAmounts(block.Get("decay"), categories is null ? null : categoryNames, UnknownAmount),
				// The allow block is script, read with the rest of it once everything it may name is loaded
				// (CTechnologyAllowReader).
				HasPrecondition = block.Get("allow") is { IsBlock: true, Children.Count: > 0 },
				ActivatesUnits = block.All("activate_unit")
					.Where(entry => entry.Value is { Length: > 0 })
					.Select(entry => entry.Value!)
					.ToList(),
				ActivatesBuilding = block.All("activate_building").LastOrDefault(entry => entry.Value is { Length: > 0 })?.Value ?? "",
				Change = !block.GetString("change").Equals("no", StringComparison.OrdinalIgnoreCase),
				CanUpgrade = !block.GetString("can_upgrade").Equals("no", StringComparison.OrdinalIgnoreCase),
				Stealable = !block.GetString("stealable").Equals("no", StringComparison.OrdinalIgnoreCase),
				IsShadowed = shadowed.Contains(block),
			};
			technologies.Add(technology);

			// A block named after a mission is what a level adds to how well it is carried out.
			foreach (ClausewitzNode entry in block.Children)
			{
				if (entry.Key is null || !entry.IsBlock || !Missions.Contains(entry.Key)
					|| (unitTypes is not null && unitTypes.ContainsKey(entry.Key)))
				{
					continue;
				}
				foreach (ClausewitzNode figure in entry.Children)
				{
					if (figure.Key is null || !CFixedPoint.TryParse(figure.Value, out CFixedPoint amount))
					{
						Report(file, figure.Line, $"{block.Key}: '{entry.Key} {{ {figure.Key ?? figure.Value ?? "?"} }}' " +
							"is a key a technology gives a mission that is not understood");
					}
					else if (figure.Key.Equals("efficiency", StringComparison.OrdinalIgnoreCase))
					{
						technology.MissionEfficiency[entry.Key] = amount;
					}
					else if (figure.Key.Equals("reduction_modifier", StringComparison.OrdinalIgnoreCase))
					{
						technology.MissionReduction[entry.Key] = amount;
					}
					else
					{
						Report(file, figure.Line, $"{block.Key}: '{entry.Key} {{ {figure.Key} }}' " +
							"is a key a technology gives a mission that is not understood");
					}
				}
			}

			// A block named after a unit type is what a level adds to that type.
			foreach (ClausewitzNode entry in block.Children)
			{
				if (entry.Key is null || !entry.IsBlock || unitTypes is null
					|| !unitTypes.TryGetValue(entry.Key, out CSubUnitType? type))
				{
					continue;
				}
				if (!technology.UnitEffects.TryGetValue(type.Key, out CSubUnitStats? stats))
				{
					technology.UnitEffects[type.Key] = stats = new CSubUnitStats();
					type.Technologies.Add(technology);
				}
				CSubUnitStatsReader.Read(entry, stats, CSubUnitTypeReader.Statistics.Contains, adjusters.Contains,
					other => Report(file, other.Line, $"{block.Key}: {entry.Key}: '{(other.IsBlock ? other.Key + " { }" : other.Key)}' " +
						"is a key a technology gives a unit type that is not understood"),
					(figure, text) => Report(file, figure.Line, $"{block.Key}: {text} is no number and was ignored"),
					Rules.CRulesReader.Has(directories, Sim.Rules.EFix.UnitFigureDecimals));
			}

			// Said, where it was passed over (clausewitz.md, section 3): a block that is no unit type, mission or block of a
			// technology's own, and a word that is no number, flag or key of its own. A numeric key is kept as an effect
			// whatever it names, nothing asking for one no rule reads.
			foreach (ClausewitzNode entry in block.Children)
			{
				if (entry.Key is null)
				{
					Report(file, entry.Line, $"{block.Key}: '{entry.Value}' is a word nothing reads in a technology; passed over");
				}
				else if (entry.IsBlock)
				{
					if (!OwnBlocks.Contains(entry.Key) && !Missions.Contains(entry.Key)
						&& (unitTypes is null || !unitTypes.ContainsKey(entry.Key)))
					{
						Report(file, entry.Line, $"{block.Key}: {entry.Key} {{ }} is a block nothing reads in a technology; passed over");
					}
				}
				else if (OwnNumbers.Contains(entry.Key))
				{
					if (!CFixedPoint.TryParse(entry.Value, out _))
					{
						Report(file, entry.Line, $"{block.Key}: {entry.Key} = {entry.Value} is no number and was ignored");
					}
				}
				else if (!OwnKeys.Contains(entry.Key) && !OwnWords.Contains(entry.Key) && !CFixedPoint.TryParse(entry.Value, out _)
					&& !"yes".Equals(entry.Value, StringComparison.OrdinalIgnoreCase))
				{
					Report(file, entry.Line, $"{block.Key}: {entry.Key} = {entry.Value} is a key nothing reads in a technology; passed over");
				}
			}
		}

		if (technologies.Count == 0)
		{
			warn($"no technologies were found under '{TechnologyDirectory}'");
		}
		return technologies;
	}

	private static int WholeNumber(string text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? (int)value.ToInt() : 0;
	}

	/// <summary>A block of <c>category = amount</c> lines. A category nobody declared is left out.</summary>
	private static List<(string, CFixedPoint)> ReadAmounts(ClausewitzNode? block, HashSet<string>? categories,
		Action<ClausewitzNode, string> unknown)
	{
		var amounts = new List<(string, CFixedPoint)>();
		foreach (ClausewitzNode entry in block?.Children ?? new List<ClausewitzNode>())
		{
			if (entry.Key is null || !CFixedPoint.TryParse(entry.Value, out CFixedPoint amount))
			{
				// Said, where it was passed over (clausewitz.md, section 3), as a category no file declares is.
				unknown(entry, $"{block!.Key}: {entry.Key ?? entry.Value} = {entry.Value} is no category and amount");
				continue;
			}
			if (categories is not null && !categories.Contains(entry.Key))
			{
				unknown(entry, $"{block!.Key} names a category that is not defined: {entry.Key}");
				continue;
			}
			amounts.Add((entry.Key, amount));
		}
		return amounts;
	}
}
