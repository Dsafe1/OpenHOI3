using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Content;

/// <summary>
/// Reads the technologies: every block of every file under <c>technologies/</c>.
///
/// Only what a level is worth as a plain number is taken - <c>ic_modifier = 0.025</c>,
/// <c>metal_production = 0.05</c>. A technology's other content - the units and
/// buildings it unlocks, the statistics it gives each unit type, what researching it
/// needs - belongs to systems that do not exist yet and is skipped without comment.
/// See <c>docs/mechanics/resources.md</c>, section 4.
/// </summary>
public static class CTechnologyReader
{
	private const string TechnologyDirectory = "technologies";

	/// <summary>Numbers in a technology's block that describe the technology, not its effect.</summary>
	private static readonly HashSet<string> OwnKeys = new(StringComparer.OrdinalIgnoreCase)
	{
		"difficulty", "start_year", "first_offset", "additional_offset", "max_level",
	};

	public static List<CTechnology> Read(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		var technologies = new List<CTechnology>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		int duplicates = 0;

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
				if (!seen.Add(block.Key))
				{
					duplicates++;
					continue;
				}

				var effect = new CModifier(block.Key);
				foreach (ClausewitzNode entry in block.Children)
				{
					if (entry.Key is not null && entry.Value is not null && !OwnKeys.Contains(entry.Key)
						&& CFixedPoint.TryParse(entry.Value, out CFixedPoint value))
					{
						effect.Set(entry.Key, value);
					}
				}
				technologies.Add(new CTechnology(block.Key, block.GetInt("max_level"), effect));
			}
		}

		if (technologies.Count == 0)
		{
			warn($"no technologies were found under '{TechnologyDirectory}'");
		}
		if (duplicates > 0)
		{
			warn($"{duplicates} technologies are declared more than once; the first declaration stands");
		}
		return technologies;
	}
}
