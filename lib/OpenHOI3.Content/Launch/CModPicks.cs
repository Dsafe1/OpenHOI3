using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Mods;

namespace OpenHOI3.Content.Launch;

/// <summary>
/// Which mods are ticked, by virtual path (<c>docs/launcher.md</c>, section 4): ticking one ticks what it depends on,
/// and unticking one unticks what depends on it, so a choice never lacks a dependency that is there to be had. A
/// dependency named that no descriptor has is said and not ticked; the engine orders what is ticked.
/// </summary>
public static class CModPicks
{
	/// <summary>The ticks after a mod is ticked or unticked.</summary>
	public static HashSet<string> Set(IReadOnlyList<CMod> mods, IEnumerable<string> ticked, CMod mod, bool on)
	{
		var picks = new HashSet<string>(ticked, StringComparer.Ordinal);
		if (on)
		{
			foreach (CMod needed in WithDependencies(mods, mod))
			{
				picks.Add(needed.VirtualPath);
			}
			return picks;
		}
		picks.Remove(mod.VirtualPath);
		bool changed = true;
		while (changed)
		{
			changed = false;
			foreach (CMod other in mods.Where(m => picks.Contains(m.VirtualPath)).ToList())
			{
				if (other.Dependencies.Select(name => ByName(mods, name)).Any(dependency => dependency is not null && !picks.Contains(dependency.VirtualPath)))
				{
					picks.Remove(other.VirtualPath);
					changed = true;
				}
			}
		}
		return picks;
	}

	/// <summary>
	/// Ticks remembered from an earlier run, made whole again: those whose descriptor is gone dropped, the dependencies
	/// of the rest ticked.
	/// </summary>
	public static HashSet<string> Restore(IReadOnlyList<CMod> mods, IEnumerable<string> remembered)
	{
		var picks = new HashSet<string>(StringComparer.Ordinal);
		foreach (string virtualPath in remembered)
		{
			if (mods.FirstOrDefault(mod => mod.VirtualPath == virtualPath) is { } mod)
			{
				picks = Set(mods, picks, mod, on: true);
			}
		}
		return picks;
	}

	/// <summary>The ticked mods, in the list's order.</summary>
	public static List<CMod> Chosen(IReadOnlyList<CMod> mods, IReadOnlySet<string> picks)
	{
		return mods.Where(mod => picks.Contains(mod.VirtualPath)).ToList();
	}

	/// <summary>The names a mod depends on, directly or through another, that no descriptor has.</summary>
	public static List<string> Missing(IReadOnlyList<CMod> mods, CMod mod)
	{
		var missing = new List<string>();
		var seen = new HashSet<string>(StringComparer.Ordinal) { mod.Name };
		var queue = new Queue<CMod>(new[] { mod });
		while (queue.Count > 0)
		{
			foreach (string name in queue.Dequeue().Dependencies)
			{
				if (!seen.Add(name))
				{
					continue;
				}
				if (ByName(mods, name) is { } dependency)
				{
					queue.Enqueue(dependency);
				}
				else
				{
					missing.Add(name);
				}
			}
		}
		return missing;
	}

	/// <summary>A mod and everything it depends on that is there, directly or through another.</summary>
	private static List<CMod> WithDependencies(IReadOnlyList<CMod> mods, CMod mod)
	{
		var found = new List<CMod>();
		var seen = new HashSet<string>(StringComparer.Ordinal);
		var queue = new Queue<CMod>(new[] { mod });
		while (queue.Count > 0)
		{
			CMod next = queue.Dequeue();
			if (!seen.Add(next.Name))
			{
				continue;
			}
			found.Add(next);
			foreach (string name in next.Dependencies)
			{
				if (ByName(mods, name) is { } dependency)
				{
					queue.Enqueue(dependency);
				}
			}
		}
		return found;
	}

	/// <summary>The mod a dependency names: matched by its <c>name</c> line, case and all, as the original matches it.</summary>
	private static CMod? ByName(IReadOnlyList<CMod> mods, string name)
	{
		return mods.FirstOrDefault(mod => mod.Name == name);
	}
}
