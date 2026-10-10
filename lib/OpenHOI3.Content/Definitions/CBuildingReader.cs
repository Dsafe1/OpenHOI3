using System;
using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Content.Definitions;

/// <summary>
/// Reads <c>common/buildings.txt</c>. A building's own keys are a fixed list, and any
/// other key in its block is its modifier - what one level is worth. See
/// <c>docs/mechanics/ic.md</c>, section 1.
/// </summary>
public static class CBuildingReader
{
	private const string BuildingsFile = "common/buildings.txt";

	/// <summary>Every key the original reads as a field of the building itself.</summary>
	private static readonly HashSet<string> OwnKeys = new(StringComparer.OrdinalIgnoreCase)
	{
		"cost", "time", "capital", "port", "max_level", "on_completion", "completion_size",
		"damage_factor", "onmap", "visibility", "show_for_province", "prerequisites",
		"not_if_x_exists", "confirm", "orientation", "repair",
	};

	public static CBuilding[] Read(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		string path = directories.Resolve(BuildingsFile);
		if (!System.IO.File.Exists(path))
		{
			warn($"no buildings: '{path}' is missing");
			return Array.Empty<CBuilding>();
		}
		ClausewitzNode root = ClausewitzParser.ParseFile(path);

		var buildings = new List<CBuilding>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				continue;
			}
			if (!seen.Add(block.Key))
			{
				warn($"'{path}': the building '{block.Key}' is declared twice; the first stands");
				continue;
			}

			string modifierKey = "";
			CFixedPoint modifierValue = CFixedPoint.Zero;
			foreach (ClausewitzNode entry in block.Children)
			{
				if (entry.Key is null || OwnKeys.Contains(entry.Key))
				{
					continue;
				}
				if (entry.Value is null || !CFixedPoint.TryParse(entry.Value, out CFixedPoint value))
				{
					warn($"'{path}': '{entry.Key}' on the building '{block.Key}' is not a number");
					continue;
				}
				if (modifierKey.Length != 0)
				{
					// A building holds one modifier, so a second one replaces the first.
					warn($"'{path}': the building '{block.Key}' has two modifiers, '{modifierKey}' and " +
						$"'{entry.Key}'; only the last counts");
				}
				modifierKey = entry.Key;
				modifierValue = value;
			}

			buildings.Add(new CBuilding(buildings.Count, block.Key, block.GetInt("max_level"),
				modifierKey, modifierValue)
			{
				IsOnMap = block.GetBool("onmap"),
				DamageFactor = block.Get("damage_factor") is { Value: { } factor }
					&& CFixedPoint.TryParse(factor, out CFixedPoint parsed) ? parsed : CFixedPoint.One,
				IsRepaired = block.GetBool("repair", fallback: true),
			});
		}
		return buildings.ToArray();
	}
}
