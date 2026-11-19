using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Content.Definitions;

/// <summary>
/// Reads <c>common/unit_upgrades.txt</c> into each unit type's list of what it may become: each <c>upgrade</c> in the file's
/// order, and then whatever those may become in turn, the types taken in their database's order - which is the order a list
/// shows. A name that is no type is dropped, as the original drops it. See <c>docs/mechanics/upgrades.md</c>, section 1.
/// </summary>
public static class CUnitUpgradeReader
{
	private const string UpgradeFile = "common/unit_upgrades.txt";

	public static void Read(CDirectorySettings directories, IReadOnlyDictionary<string, CSubUnitType> types, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		string path = directories.Resolve(UpgradeFile);
		if (!File.Exists(path))
		{
			return;
		}
		ClausewitzNode root;
		try
		{
			root = ClausewitzParser.ParseFile(path);
		}
		catch (Exception e)
		{
			warn($"Could not read '{path}': {e.Message}");
			return;
		}
		// Said, where it was passed over (clausewitz.md, section 3).
		foreach (ClausewitzNode top in root.Children)
		{
			if (top.Key is null || !top.Key.Equals("unit_upgrades", StringComparison.OrdinalIgnoreCase))
			{
				warn($"{UpgradeFile}: '{top.Key ?? top.Value}' is no key of the file; passed over");
			}
		}
		foreach (ClausewitzNode list in root.All("unit_upgrades"))
		{
			foreach (ClausewitzNode other in list.Children)
			{
				if (other.Key is null || !other.Key.Equals("upgrade", StringComparison.OrdinalIgnoreCase))
				{
					warn($"{UpgradeFile}: '{other.Key ?? other.Value}' in unit_upgrades is no upgrade; passed over");
				}
			}
			foreach (ClausewitzNode entry in list.All("upgrade"))
			{
				string from = "";
				string to = "";
				foreach (ClausewitzNode setting in entry.Children)
				{
					if (setting.Key is null || setting.Value is null)
					{
						warn($"{UpgradeFile}: '{setting.Key ?? setting.Value}' in an upgrade is no key of one; passed over");
						continue;
					}
					if (setting.Key.Equals("base", StringComparison.OrdinalIgnoreCase))
					{
						from = setting.Value;
					}
					else if (setting.Key.Equals("target", StringComparison.OrdinalIgnoreCase))
					{
						to = setting.Value;
					}
					else
					{
						warn($"{UpgradeFile}: '{setting.Key}' in an upgrade is no key of one; passed over");
					}
				}
				if (types.TryGetValue(from, out CSubUnitType? source) && types.TryGetValue(to, out CSubUnitType? target))
				{
					source.UpgradeTargets.Add(target);
				}
				else
				{
					warn($"{UpgradeFile}: an upgrade from '{from}' to '{to}' names no type.");
				}
			}
		}
		Close(types.Values.OrderBy(type => type.DatabaseIndex).ToList());
	}

	/// <summary>Each type is given what those it may become may become, pass after pass until a pass adds nothing.</summary>
	public static void Close(IReadOnlyList<CSubUnitType> types)
	{
		bool changed;
		do
		{
			changed = false;
			foreach (CSubUnitType type in types)
			{
				int count = type.UpgradeTargets.Count;
				for (int index = 0; index < count; index++)
				{
					CSubUnitType next = type.UpgradeTargets[index];
					int further = next.UpgradeTargets.Count;
					for (int step = 0; step < further; step++)
					{
						CSubUnitType target = next.UpgradeTargets[step];
						if (!type.UpgradeTargets.Contains(target))
						{
							type.UpgradeTargets.Add(target);
							changed = true;
						}
					}
				}
			}
		}
		while (changed);
	}
}
