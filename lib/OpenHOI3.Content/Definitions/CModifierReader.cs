using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Content.Definitions;

/// <summary>
/// Reads the files whose blocks are modifiers. Such a block has a few keys of its own
/// and treats every other key as a kind of modifier. See
/// <c>docs/mechanics/modifiers.md</c>, section 2.
///
/// The kinds of modifier are not a closed list here yet, so a misspelt one is taken at
/// its word rather than reported.
/// </summary>
public static class CModifierReader
{
	private static readonly HashSet<string> LawKeys = new(StringComparer.OrdinalIgnoreCase) { "cost", "allow" };

	private static readonly HashSet<string> MinisterTypeKeys = new(StringComparer.OrdinalIgnoreCase) { "decay" };

	private static readonly HashSet<string> StaticModifierKeys = new(StringComparer.OrdinalIgnoreCase) { "icon" };

	private static readonly HashSet<string> NoKeys = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// A block's modifier: every key that is not one of its own, with its number.
	/// Returns how many such keys had something other than a number.
	/// </summary>
	public static CModifier ReadBlock(ClausewitzNode block, HashSet<string> ownKeys, out int unreadable)
	{
		var modifier = new CModifier(block.Key ?? "");
		unreadable = 0;
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is null || ownKeys.Contains(entry.Key))
			{
				continue;
			}
			if (entry.Value is not null && CFixedPoint.TryParse(entry.Value, out CFixedPoint value))
			{
				// A key written twice keeps the last value, as a building's modifier does.
				modifier.Set(entry.Key, value);
			}
			else
			{
				unreadable++;
			}
		}
		return modifier;
	}

	/// <summary><c>common/laws.txt</c>: groups, each holding the laws a country picks one of.</summary>
	public static Dictionary<string, Dictionary<string, CLaw>> ReadLaws(CDirectorySettings directories,
		Action<string> warn)
	{
		var groups = new Dictionary<string, Dictionary<string, CLaw>>(StringComparer.OrdinalIgnoreCase);
		ClausewitzNode? root = ParseIfPresent(directories, "common/laws.txt", "laws", warn, out string path);
		if (root is null)
		{
			return groups;
		}

		int unreadable = 0;
		foreach (ClausewitzNode group in root.Children)
		{
			if (group.Key is null || !group.IsBlock)
			{
				continue;
			}
			if (!groups.TryGetValue(group.Key, out Dictionary<string, CLaw>? laws))
			{
				laws = new Dictionary<string, CLaw>(StringComparer.OrdinalIgnoreCase);
				groups[group.Key] = laws;
			}
			foreach (ClausewitzNode law in group.Children)
			{
				if (law.Key is null || !law.IsBlock)
				{
					continue;
				}
				laws[law.Key] = new CLaw(group.Key, ReadBlock(law, LawKeys, out int bad));
				unreadable += bad;
			}
		}
		if (unreadable > 0)
		{
			warn($"'{path}': {unreadable} entries inside laws are neither the law's own keys nor a number");
		}
		return groups;
	}

	/// <summary><c>common/minister_types.txt</c>: what a minister can be, each a modifier.</summary>
	public static Dictionary<string, CMinisterType> ReadMinisterTypes(CDirectorySettings directories,
		Action<string> warn)
	{
		var types = new Dictionary<string, CMinisterType>(StringComparer.OrdinalIgnoreCase);
		ClausewitzNode? root = ParseIfPresent(directories, "common/minister_types.txt", "minister types",
			warn, out string path);
		if (root is null)
		{
			return types;
		}

		int unreadable = 0;
		foreach (ClausewitzNode type in root.Children)
		{
			if (type.Key is null || !type.IsBlock)
			{
				continue;
			}
			var read = new CMinisterType(ReadBlock(type, MinisterTypeKeys, out int bad));
			unreadable += bad;

			// decay = { a_category = -0.25 }: what he adds to that category's decay. A type
			// may have several such blocks, and a category named twice adds up.
			foreach (ClausewitzNode decay in type.All("decay"))
			{
				foreach (ClausewitzNode entry in decay.Children)
				{
					if (entry.Key is not null && CFixedPoint.TryParse(entry.Value, out CFixedPoint amount))
					{
						read.Decay[entry.Key] = read.Decay.GetValueOrDefault(entry.Key) + amount;
					}
					else
					{
						unreadable++;
					}
				}
			}
			types[type.Key] = read;
		}
		if (unreadable > 0)
		{
			warn($"'{path}': {unreadable} entries inside minister types are not numbers");
		}
		return types;
	}

	/// <summary>
	/// <c>common/static_modifiers.txt</c>: the modifiers the engine applies by name -
	/// the difficulty levels, <c>base_values</c>, <c>overseas</c> and the rest.
	/// </summary>
	public static List<CModifier> ReadStaticModifiers(CDirectorySettings directories, Action<string> warn)
	{
		var modifiers = new List<CModifier>();
		ClausewitzNode? root = ParseIfPresent(directories, "common/static_modifiers.txt", "static modifiers",
			warn, out string path);
		if (root is null)
		{
			return modifiers;
		}

		int unreadable = 0;
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				continue;
			}
			modifiers.Add(ReadBlock(block, StaticModifierKeys, out int bad));
			unreadable += bad;
		}
		if (unreadable > 0)
		{
			warn($"'{path}': {unreadable} entries inside static modifiers are not numbers");
		}
		return modifiers;
	}

	/// <summary>
	/// <c>common/strategic_resources.txt</c>: what each strategic resource is worth to
	/// the country that has it. A block has no keys of its own, so every one is a modifier.
	/// </summary>
	public static List<CModifier> ReadStrategicResources(CDirectorySettings directories, Action<string> warn)
	{
		var resources = new List<CModifier>();
		ClausewitzNode? root = ParseIfPresent(directories, "common/strategic_resources.txt", "strategic resources",
			warn, out string path);
		if (root is null)
		{
			return resources;
		}

		int unreadable = 0;
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				continue;
			}
			resources.Add(ReadBlock(block, NoKeys, out int bad));
			unreadable += bad;
		}
		if (unreadable > 0)
		{
			warn($"'{path}': {unreadable} entries inside strategic resources are not numbers");
		}
		return resources;
	}

	/// <summary><c>common/government_positions.txt</c>: the seats a government has.</summary>
	public static HashSet<string> ReadGovernmentPositions(CDirectorySettings directories, Action<string> warn)
	{
		var positions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		ClausewitzNode? root = ParseIfPresent(directories, "common/government_positions.txt",
			"government positions", warn, out _);
		if (root is null)
		{
			return positions;
		}
		foreach (ClausewitzNode position in root.Children)
		{
			if (position.Key is not null && position.IsBlock)
			{
				positions.Add(position.Key);
			}
		}
		return positions;
	}

	private static ClausewitzNode? ParseIfPresent(CDirectorySettings directories, string gameRelative,
		string what, Action<string> warn, out string path)
	{
		path = directories.Resolve(gameRelative);
		if (!File.Exists(path))
		{
			warn($"no {what}: '{path}' is missing");
			return null;
		}
		return ClausewitzParser.ParseFile(path);
	}
}
