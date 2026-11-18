using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content.Mods;

namespace OpenHOI3.Content.Launch;

/// <summary>
/// Every mod of an install as the launcher offers it: the descriptors in <c>tfh/mod</c>, in the order the original lists
/// them (<see cref="CModReader.Descriptors"/>), each read as the engine reads it, a refused one kept and said to be.
/// The original also lists the install's own <c>mod</c> folder and the player's folder's; **only <c>tfh/mod</c> holds
/// Their Finest Hour's mods** - the install's <c>mod</c> holds the base game's expansions from before it, which are not
/// mods (the user's account, 2026-11-18) - so the others are not offered (<c>docs/launcher.md</c>, section 4).
/// </summary>
public static class CModList
{
	/// <summary>The folder, below the install, whose descriptors the launcher offers.</summary>
	public static readonly string ModFolder = Path.Combine("tfh", "mod");

	public static List<CModEntry> Read(string gameRoot, string documentsFolder)
	{
		var entries = new List<CModEntry>();
		var names = new HashSet<string>(StringComparer.Ordinal);
		string offered = Path.GetFullPath(Path.Combine(gameRoot, ModFolder));
		foreach ((string virtualPath, string file) in CModReader.Descriptors(gameRoot, documentsFolder))
		{
			if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(file)), offered, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			var problems = new List<string>();
			CMod? mod = null;
			string? refusal = null;
			try
			{
				mod = CModReader.Read(gameRoot, file, virtualPath, problem =>
				{
					if (!problem.Contains(CModReader.UnknownKeyNote, StringComparison.Ordinal) && !problems.Contains(problem))
					{
						problems.Add(problem);
					}
				});
				if (mod is null)
				{
					// The reader's own reason is its last word; the player is told it plainly.
					problems.Clear();
					refusal = "Not a mod Their Finest Hour plays: its descriptor has no name, or neither a path nor an archive";
				}
			}
			catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException)
			{
				refusal = $"It could not be read: {exception.Message}";
			}
			// Of two descriptors of one name the first is kept, as the original keeps it.
			if (mod is not null && !names.Add(mod.Name))
			{
				refusal = $"Another descriptor named \"{mod.Name}\" comes first, so this one is never played";
				mod = null;
			}
			entries.Add(new CModEntry(virtualPath, file, mod, problems, refusal));
		}
		return entries;
	}

	/// <summary>The mods that may be played, in the list's order.</summary>
	public static List<CMod> Playable(IEnumerable<CModEntry> entries)
	{
		return entries.Where(entry => entry.Mod is not null).Select(entry => entry.Mod!).ToList();
	}
}
