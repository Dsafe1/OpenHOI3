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
		warn ??= CDiagnostics.Problem;
		string path = directories.Resolve(BuildingsFile);
		if (!System.IO.File.Exists(path))
		{
			warn(CDiagnostics.Located(path, 0, "no buildings: the file is missing"));
			return Array.Empty<CBuilding>();
		}
		ClausewitzNode root = ClausewitzParser.ParseFile(path);

		var buildings = new List<CBuilding>();
		var lists = new List<(List<(string, int)> Required, List<(string, int)> Excluded)>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Sim.Modifiers.CModifierKeys kinds = CModifierReader.Kinds(directories);
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				continue;
			}
			if (!seen.Add(block.Key))
			{
				warn(CDiagnostics.Located(path, block.Line, $"the building '{block.Key}' is declared twice; the first stands"));
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
				if (!kinds.Contains(entry.Key))
				{
					// The original passes over a key that is no kind of modifier in silence (modifiers.md, section 2).
					warn(CModifierReader.Unknown(path, "buildings", block, entry));
					continue;
				}
				if (entry.Value is null || !CFixedPoint.TryParse(entry.Value, out CFixedPoint value))
				{
					warn(CDiagnostics.Located(path, entry.Line, $"'{entry.Key}' on the building '{block.Key}' is not a number"));
					continue;
				}
				if (modifierKey.Length != 0)
				{
					// A building holds one modifier, so a second one replaces the first.
					warn(CDiagnostics.Located(path, entry.Line,
						$"the building '{block.Key}' has two modifiers, '{modifierKey}' and '{entry.Key}'; only the last counts"));
				}
				modifierKey = entry.Key;
				modifierValue = value;
			}

			buildings.Add(new CBuilding(buildings.Count, block.Key, block.GetInt("max_level"),
				modifierKey, modifierValue)
			{
				IsOnMap = block.GetBool("onmap"),
				IsAlwaysVisible = block.GetBool("visibility"),
				DamageFactor = block.Get("damage_factor") is { Value: { } factor }
					&& CFixedPoint.TryParse(factor, out CFixedPoint parsed) ? parsed : CFixedPoint.One,
				IsRepaired = block.GetBool("repair", fallback: true),
				IsCapital = block.GetBool("capital"),
				Cost = Number(block, "cost"),
				Time = Number(block, "time"),
				OnCompletion = block.Get("on_completion")?.Value ?? "",
				CompletionSize = Number(block, "completion_size"),
				IsPort = block.GetBool("port"),
				ShowForProvince = block.GetBool("show_for_province", fallback: true),
			});
			lists.Add((Names(block, "prerequisites"), Names(block, "not_if_x_exists")));
			// A figure that is no number is read as nought - one as its damage factor - and said (clausewitz.md, section 3).
			foreach (string key in NumberKeys)
			{
				if (block.Get(key) is { Value: { } text } figure && !CFixedPoint.TryParse(text, out _))
				{
					warn(CDiagnostics.Located(path, figure.Line,
						$"{block.Key}: {key} = {text} is no number; read as {(key == "damage_factor" ? "one" : "nought")}"));
				}
			}
		}
		// The two lists of buildings are resolved once every building is read, as the original does at load
		// (docs/mechanics/buildinglines.md, section 1).
		for (int index = 0; index < buildings.Count; index++)
		{
			CBuilding building = buildings[index];
			Resolve(buildings, building, lists[index].Required, building.Prerequisites, path, warn);
			Resolve(buildings, building, lists[index].Excluded, building.Excluding, path, warn);
		}
		return buildings.ToArray();
	}

	/// <summary>A building's own figures.</summary>
	private static readonly string[] NumberKeys = { "max_level", "damage_factor", "cost", "time", "completion_size" };

	private static CFixedPoint Number(ClausewitzNode block, string key)
	{
		return block.Get(key) is { Value: { } text } && CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}

	/// <summary>The names a list key gives, written as a block of words or as one word, each with its line for a report.</summary>
	private static List<(string Name, int Line)> Names(ClausewitzNode block, string key)
	{
		var names = new List<(string, int)>();
		if (block.Get(key) is not { } list)
		{
			return names;
		}
		if (!list.IsBlock)
		{
			if (list.Value is { Length: > 0 } single)
			{
				names.Add((single, list.Line));
			}
			return names;
		}
		foreach (ClausewitzNode child in list.Children)
		{
			if ((child.Value ?? child.Key) is { Length: > 0 } name)
			{
				names.Add((name, child.Line));
			}
		}
		return names;
	}

	private static void Resolve(List<CBuilding> buildings, CBuilding building, List<(string Name, int Line)> names,
		List<CBuilding> into, string path, Action<string> warn)
	{
		foreach ((string name, int line) in names)
		{
			CBuilding? found = buildings.Find(each => each.Key.Equals(name, StringComparison.OrdinalIgnoreCase));
			if (found is null)
			{
				warn(CDiagnostics.Located(path, line, $"unknown building '{name}' referred to in the list of '{building.Key}'"));
			}
			else if (found == building)
			{
				warn(CDiagnostics.Located(path, line, $"the building '{building.Key}' names itself in its own list"));
			}
			else
			{
				into.Add(found);
			}
		}
	}
}
