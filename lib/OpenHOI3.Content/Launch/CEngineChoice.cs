using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content.Mods;

namespace OpenHOI3.Content.Launch;

/// <summary>
/// Which engine plays a choice of mods (<c>docs/launcher.md</c>, section 4): the launcher's own where no mod names one,
/// the one every naming mod names where they agree - found in the folder of a mod that names it, or the launcher's own if
/// its version is that - and none where two are named.
/// </summary>
public static class CEngineChoice
{
	/// <summary>The engine for the mods chosen, or the reason there is none.</summary>
	/// <param name="mods">The mods chosen, dependencies among them.</param>
	/// <param name="own">The launcher's own engine, or null where it has none.</param>
	public static CEngineChoiceResult Choose(IReadOnlyList<CMod> mods, CEngine? own)
	{
		List<IGrouping<string, CMod>> named = mods.Where(mod => mod.Engine is not null)
			.GroupBy(mod => mod.Engine!, StringComparer.Ordinal).ToList();
		if (named.Count > 1)
		{
			string who = string.Join("; ", named.Select(group =>
				$"{string.Join(", ", group.Select(mod => mod.Name))} engine {group.Key}"));
			return Refused($"These mods are made for different engines and cannot be played together: {who}");
		}
		CEngine? engine;
		if (named.Count == 0)
		{
			engine = own;
			if (engine is null)
			{
				return Refused($"The launcher has no engine of its own: there is none in the '{CEngineFile.FolderName}' folder beside it");
			}
		}
		else
		{
			string version = named[0].Key;
			engine = named[0]
				.Where(mod => mod.Folder is not null)
				.Select(mod => CEngineFile.Read(Path.Combine(mod.Folder!, CEngineFile.FolderName)))
				.FirstOrDefault(found => found?.Version == version);
			if (engine is null && own?.Version == version)
			{
				engine = own;
			}
			if (engine is null)
			{
				return Refused($"{string.Join(", ", named[0].Select(mod => mod.Name))} wants engine {version}, "
					+ $"which is not in its '{CEngineFile.FolderName}' folder");
			}
		}
		if (engine.Contract != CLaunchContract.Version)
		{
			return Refused($"Engine {engine.Version} understands the launcher's command line of version {engine.Contract}, "
				+ $"and this launcher writes version {CLaunchContract.Version}");
		}
		return new CEngineChoiceResult(engine, null);
	}

	private static CEngineChoiceResult Refused(string problem) => new(null, problem);
}
