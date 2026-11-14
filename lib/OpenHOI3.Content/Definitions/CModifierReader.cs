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
/// and treats every other key as a kind of modifier, of a closed list (<see cref="CModifierKeys"/>):
/// one that is none - a misspelt one - is reported and dropped, as the original logs and drops it.
/// See <c>docs/mechanics/modifiers.md</c>, section 2.
/// </summary>
public static class CModifierReader
{
	private static readonly HashSet<string> LawKeys = new(StringComparer.OrdinalIgnoreCase) { "cost", "allow" };

	private static readonly HashSet<string> PolicyKeys = new(StringComparer.OrdinalIgnoreCase) { "allow" };

	private static readonly HashSet<string> MinisterTypeKeys = new(StringComparer.OrdinalIgnoreCase) { "decay" };

	private static readonly HashSet<string> StaticModifierKeys = new(StringComparer.OrdinalIgnoreCase) { "icon" };

	private static readonly HashSet<string> NoKeys = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// A block's modifier: every key that is not one of its own, nor <c>icon</c>, which every modifier takes, with its
	/// number. A key that is no kind the original knows is added to <paramref name="unknown"/> - the block's name and the
	/// key - and dropped. <paramref name="unreadable"/> is how many kinds had something other than a number.
	/// </summary>
	public static CModifier ReadBlock(ClausewitzNode block, HashSet<string> ownKeys, CModifierKeys kinds,
		ICollection<string> unknown, out int unreadable)
	{
		var modifier = new CModifier(block.Key ?? "");
		unreadable = 0;
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is null || ownKeys.Contains(entry.Key)
				|| entry.Key.Equals(CModifierKeys.Icon, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			if (!kinds.Contains(entry.Key))
			{
				unknown.Add($"{block.Key}: {entry.Key}");
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

	/// <summary>
	/// The kinds of modifier an install's files may name once its ideologies are read: the engine's, the missions' and
	/// one for each faction of <c>common/ideologies.txt</c>, which is read for its factions' tags alone.
	/// </summary>
	public static CModifierKeys Kinds(CDirectorySettings directories)
	{
		var factions = new List<string>();
		foreach (Sim.Diplomacy.CIdeologyGroup group in CIdeologyReader.Read(directories, _ => { }))
		{
			factions.Add(group.Faction);
		}
		return new CModifierKeys(factions);
	}

	/// <summary>Ours: every key of a file's modifiers that is no kind, in one line, which the original logs one by one.</summary>
	public static void ReportUnknown(Action<string> warn, string path, string what, SortedSet<string> unknown)
	{
		if (unknown.Count > 0)
		{
			warn($"'{path}': {unknown.Count} keys of {what} are no kind of modifier and were dropped, as the original drops " +
				$"them: {string.Join("; ", unknown)}");
		}
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

		CModifierKeys kinds = Kinds(directories);
		var unknown = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
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
				laws[law.Key] = new CLaw(group.Key, ReadBlock(law, LawKeys, kinds, unknown, out int bad))
				{
					Cost = CFixedPoint.TryParse(law.GetString("cost"), out CFixedPoint cost) ? cost : CFixedPoint.Zero,
				};
				unreadable += bad;
			}
		}
		if (unreadable > 0)
		{
			warn($"'{path}': {unreadable} entries inside laws are neither the law's own keys nor a number");
		}
		ReportUnknown(warn, path, "laws", unknown);
		return groups;
	}

	/// <summary>
	/// <c>common/occupation_policies.txt</c>: <c>no_occupation</c> first, then a policy for each block in the file's
	/// order, numbered from 1, each a modifier; its <c>allow</c> is script's and is read with the rest of it
	/// (<c>docs/mechanics/occupation.md</c>, section 1).
	/// </summary>
	public static List<Sim.Occupation.COccupationPolicy> ReadOccupationPolicies(CDirectorySettings directories,
		Action<string> warn)
	{
		List<Sim.Occupation.COccupationPolicy> policies = Sim.Occupation.COccupationPolicies.WithNull();
		ClausewitzNode? root = ParseIfPresent(directories, "common/occupation_policies.txt", "occupation policies", warn,
			out string path);
		if (root is null)
		{
			return policies;
		}

		CModifierKeys kinds = Kinds(directories);
		var unknown = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		int unreadable = 0;
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				continue;
			}
			policies.Add(new Sim.Occupation.COccupationPolicy(ReadBlock(block, PolicyKeys, kinds, unknown, out int bad),
				policies.Count));
			unreadable += bad;
		}
		if (unreadable > 0)
		{
			warn($"'{path}': {unreadable} entries inside occupation policies are neither the policy's own keys nor a number");
		}
		ReportUnknown(warn, path, "occupation policies", unknown);
		return policies;
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

		CModifierKeys kinds = Kinds(directories);
		var unknown = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		int unreadable = 0;
		foreach (ClausewitzNode type in root.Children)
		{
			if (type.Key is null || !type.IsBlock)
			{
				continue;
			}
			var read = new CMinisterType(ReadBlock(type, MinisterTypeKeys, kinds, unknown, out int bad));
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
		ReportUnknown(warn, path, "minister types", unknown);
		return types;
	}

	/// <summary>
	/// <c>common/static_modifiers.txt</c>: the modifiers the engine applies by name -
	/// the difficulty levels, <c>base_values</c>, <c>overseas</c> and the rest.
	/// </summary>
	public static List<CModifier> ReadStaticModifiers(CDirectorySettings directories, Action<string> warn)
	{
		return ReadNamed(directories, "common/static_modifiers.txt", "static modifiers", warn);
	}

	/// <summary>
	/// <c>common/event_modifiers.txt</c>: the modifiers an event or a decision gives a country or a
	/// province by name, in the same shape (<c>docs/mechanics/effects.md</c>, section 6).
	/// </summary>
	public static List<CModifier> ReadEventModifiers(CDirectorySettings directories, Action<string> warn)
	{
		return ReadNamed(directories, "common/event_modifiers.txt", "event modifiers", warn);
	}

	private static List<CModifier> ReadNamed(CDirectorySettings directories, string file, string what, Action<string> warn)
	{
		var modifiers = new List<CModifier>();
		ClausewitzNode? root = ParseIfPresent(directories, file, what, warn, out string path);
		if (root is null)
		{
			return modifiers;
		}

		CModifierKeys kinds = Kinds(directories);
		var unknown = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		int unreadable = 0;
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				continue;
			}
			modifiers.Add(ReadBlock(block, StaticModifierKeys, kinds, unknown, out int bad));
			unreadable += bad;
		}
		if (unreadable > 0)
		{
			warn($"'{path}': {unreadable} entries inside {what} are not numbers");
		}
		ReportUnknown(warn, path, what, unknown);
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

		CModifierKeys kinds = Kinds(directories);
		var unknown = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		int unreadable = 0;
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				continue;
			}
			resources.Add(ReadBlock(block, NoKeys, kinds, unknown, out int bad));
			unreadable += bad;
		}
		if (unreadable > 0)
		{
			warn($"'{path}': {unreadable} entries inside strategic resources are not numbers");
		}
		ReportUnknown(warn, path, "strategic resources", unknown);
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
