using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Content;

/// <summary>
/// Reads the technologies: the categories out of <c>common/technology.txt</c>, and every
/// block of every file under <c>technologies/</c>.
///
/// A block holds what a level is worth as plain numbers - <c>ic_modifier = 0.025</c> -
/// and what researching it takes: its difficulty, the years its levels are due, whose
/// ability makes it cheaper and what must hold first. The statistics it gives each unit
/// type and what it unlocks belong to units, and are skipped without comment. See
/// <c>docs/mechanics/research.md</c>, section 1.
/// </summary>
public static class CTechnologyReader
{
	private const string TechnologyDirectory = "technologies";
	private const string CategoryFile = "common/technology.txt";

	/// <summary>Keys in a technology's block that describe the technology, not its effect.</summary>
	private static readonly HashSet<string> OwnKeys = new(StringComparer.OrdinalIgnoreCase)
	{
		"difficulty", "start_year", "first_offset", "additional_offset", "max_level",
		"folder", "change", "can_upgrade", "is_nuclear", "on_completion",
	};

	/// <summary>
	/// The theories and practicals, in the order the file lists them: every theory, then
	/// every practical.
	/// </summary>
	public static List<CTechnologyCategory> ReadCategories(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		var categories = new List<CTechnologyCategory>();
		string path = directories.Resolve(CategoryFile);
		if (!File.Exists(path))
		{
			warn($"no technology categories: '{path}' is missing");
			return categories;
		}

		ClausewitzNode root = ClausewitzParser.ParseFile(path);
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

	public static List<CTechnology> Read(CDirectorySettings directories, Action<string>? warn = null,
		IReadOnlyCollection<CTechnologyCategory>? categories = null)
	{
		warn ??= CDiagnostics.Warn;
		var technologies = new List<CTechnology>();
		int duplicates = 0;

		// Every block first, because a technology's conditions name other technologies.
		var blocks = new List<ClausewitzNode>();
		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string file in directories.ListFiles(TechnologyDirectory, "*.txt"))
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
				if (!names.Add(block.Key))
				{
					duplicates++;
					continue;
				}
				blocks.Add(block);
			}
		}

		var categoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (CTechnologyCategory category in categories ?? Array.Empty<CTechnologyCategory>())
		{
			categoryNames.Add(category.Name);
		}
		var unknownConditions = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		var unknownCategories = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (ClausewitzNode block in blocks)
		{
			var effect = new CModifier(block.Key!);
			foreach (ClausewitzNode entry in block.Children)
			{
				if (entry.Key is not null && entry.Value is not null && !OwnKeys.Contains(entry.Key)
					&& CFixedPoint.TryParse(entry.Value, out CFixedPoint value))
				{
					effect.Set(entry.Key, value);
				}
			}

			string onCompletion = block.GetString("on_completion");
			if (onCompletion.Length != 0 && categories is not null && !categoryNames.Contains(onCompletion))
			{
				unknownCategories.Add(onCompletion);
				onCompletion = "";
			}

			technologies.Add(new CTechnology(block.Key!, block.GetInt("max_level"), effect)
			{
				// A fraction is dropped, as the original drops it: 0.5 is no difficulty at all.
				Difficulty = WholeNumber(block.GetString("difficulty")),
				StartYear = WholeNumber(block.GetString("start_year")),
				FirstOffset = WholeNumber(block.GetString("first_offset")),
				AdditionalOffset = WholeNumber(block.GetString("additional_offset")),
				IsNuclear = block.GetString("is_nuclear").Equals("yes", StringComparison.OrdinalIgnoreCase),
				OnCompletion = onCompletion,
				ResearchBonusFrom = ReadAmounts(block.Get("research_bonus_from"), categories is null ? null : categoryNames, unknownCategories),
				Decay = ReadAmounts(block.Get("decay"), categories is null ? null : categoryNames, unknownCategories),
				Allow = block.Get("allow") is { IsBlock: true } allow
					? ReadGroup(allow, EResearchCondition.All, names, categoryNames, unknownConditions)
					: null,
			});
		}

		if (technologies.Count == 0)
		{
			warn($"no technologies were found under '{TechnologyDirectory}'");
		}
		if (duplicates > 0)
		{
			warn($"{duplicates} technologies are declared more than once; the first declaration stands");
		}
		if (unknownCategories.Count > 0)
		{
			warn($"technologies name {unknownCategories.Count} categories that are not defined: " +
				string.Join(", ", unknownCategories));
		}
		if (unknownConditions.Count > 0)
		{
			warn($"technologies' allow blocks have {unknownConditions.Count} conditions that are not understood " +
				$"and were left out: {string.Join(", ", unknownConditions)}");
		}
		return technologies;
	}

	private static int WholeNumber(string text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? (int)value.ToInt() : 0;
	}

	/// <summary>A block of <c>category = amount</c> lines. A category nobody declared is left out.</summary>
	private static List<(string, CFixedPoint)> ReadAmounts(ClausewitzNode? block, HashSet<string>? categories,
		SortedSet<string> unknown)
	{
		var amounts = new List<(string, CFixedPoint)>();
		foreach (ClausewitzNode entry in block?.Children ?? new List<ClausewitzNode>())
		{
			if (entry.Key is null || !CFixedPoint.TryParse(entry.Value, out CFixedPoint amount))
			{
				continue;
			}
			if (categories is not null && !categories.Contains(entry.Key))
			{
				unknown.Add(entry.Key);
				continue;
			}
			amounts.Add((entry.Key, amount));
		}
		return amounts;
	}

	/// <summary>
	/// The conditions a technology's <c>allow</c> block uses. Anything else is collected
	/// to be reported, and left out.
	/// </summary>
	private static CResearchCondition ReadGroup(ClausewitzNode block, EResearchCondition kind,
		HashSet<string> technologies, HashSet<string> categories, SortedSet<string> unknown)
	{
		var group = new CResearchCondition(kind);
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is null)
			{
				continue;                                    // a stray word, such as a comment mark the file left in
			}

			string key = entry.Key;
			if (entry.IsBlock)
			{
				if (key.Equals("OR", StringComparison.OrdinalIgnoreCase))
				{
					group.Children.Add(ReadGroup(entry, EResearchCondition.Any, technologies, categories, unknown));
				}
				else if (key.Equals("AND", StringComparison.OrdinalIgnoreCase))
				{
					group.Children.Add(ReadGroup(entry, EResearchCondition.All, technologies, categories, unknown));
				}
				else if (key.Equals("NOT", StringComparison.OrdinalIgnoreCase))
				{
					group.Children.Add(ReadGroup(entry, EResearchCondition.None, technologies, categories, unknown));
				}
				else if (key.Equals("any_owned_province", StringComparison.OrdinalIgnoreCase)
					&& entry.Children.Count == 1 && entry.Children[0] is { Key: { } inner, Value: { } building }
					&& inner.Equals("has_building", StringComparison.OrdinalIgnoreCase))
				{
					group.Children.Add(new CResearchCondition(EResearchCondition.OwnsProvinceWithBuilding, building));
				}
				else
				{
					unknown.Add(key);
				}
				continue;
			}

			if (!CFixedPoint.TryParse(entry.Value, out CFixedPoint amount))
			{
				unknown.Add(key);
			}
			else if (technologies.Contains(key))
			{
				group.Children.Add(new CResearchCondition(EResearchCondition.TechnologyLevel, key, amount));
			}
			else if (categories.Contains(key))
			{
				group.Children.Add(new CResearchCondition(EResearchCondition.Ability, key, amount));
			}
			else if (key.Equals("num_of_ports", StringComparison.OrdinalIgnoreCase))
			{
				group.Children.Add(new CResearchCondition(EResearchCondition.Ports, "", amount));
			}
			else
			{
				unknown.Add(key);
			}
		}
		return group;
	}
}
