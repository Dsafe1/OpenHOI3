using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Saves;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Replay;

/// <summary>
/// Whether a game goes on after a save and a load as it would have without them - what a replay of a whole game needs
/// (<c>docs/enhanced.md</c>; <c>CLAUDE.md</c>, <i>Always</i>). A new game with the install's Lua is run to a date and
/// written as a save; the save is loaded as a second game; and the two are set beside each other field by field, every
/// object either holds walked - once as loaded, once as entered, its AI passes run - and then hour by hour for some hours,
/// the hash's parts and the first figures they part on told.
/// </summary>
internal static class CReloadCheck
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck reload <install folder> [date] [hours after] [player=TAG] [save=FILE] [groups=N] [examples=N]");
			return 2;
		}
		string install = args[1];
		string[] rest = args.Skip(2).ToArray();
		CDate end = rest.Length > 0 && CDate.TryParse(rest[0], out CDate given) ? given : new CDate(1936, 2, 1);
		int hoursAfter = rest.Length > 1 && int.TryParse(rest[1], out int h) ? h : 48;
		string? player = Option(rest, "player=");
		string savePath = Option(rest, "save=") ?? Path.Combine(Path.GetTempPath(), "openhoi3_reload.hoi3");
		int groups = int.TryParse(Option(rest, "groups="), out int g) ? g : 400;
		s_examples = int.TryParse(Option(rest, "examples="), out int e) ? e : 1;

		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(install);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		// Each game its own countries: a game holds the definitions' country objects and changes them.
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);

		using var scriptsA = new CAiScripts(directories, _ => { });
		CCountryTag[] played = player is null ? Array.Empty<CCountryTag>() : new[] { new CCountryTag(player) };
		CGameState a = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scriptsA,
			played: played);
		if (player is null)
		{
			a.Played.Clear();
		}
		var runA = new CSimulation(a);
		while (a.Date < end)
		{
			runA.RunHours(1);
		}
		// The orders the last hour's AI posted run in the next drain, which a save written now would leave out.
		runA.Pump();
		CSaveFiles.Write(a, localisation.Localise, savePath);
		Console.WriteLine($"saved at {a.Date} to {savePath}");

		using var scriptsB = new CAiScripts(directories, _ => { });
		var loadWarnings = new List<string>();
		(CGameState b, CSaveReport _) = CSaveLoader.Load(directories, map, localisation, CCountries.Load(directories, localisation, warnings.Add), savePath, played.FirstOrDefault(),
			scriptsB, loadWarnings.Add);
		if (player is null)
		{
			b.Played.Clear();
		}
		foreach (string line in loadWarnings.Where(line => line.Contains("not loaded yet", StringComparison.Ordinal)))
		{
			Console.WriteLine($"  {line}");
		}

		Report("as loaded", a, b, groups);
		var runB = new CSimulation(b);
		Report("as entered, its AI passes run", a, b, groups);

		int firstPart = -1;
		for (int hour = 1; hour <= hoursAfter; hour++)
		{
			runA.RunHours(1);
			runB.RunHours(1);
			ulong[] pa = CStateHash.Of(a);
			ulong[] pb = CStateHash.Of(b);
			string[] parted = CStateHash.Parts.Where((_, i) => pa[i] != pb[i]).ToArray();
			if (parted.Length > 0 && firstPart < 0)
			{
				firstPart = hour;
				Dictionary<string, ulong> fields = CStateHash.Fields(a).ToDictionary(pair => pair.Key, pair => pair.Value);
				List<string> figures = CStateHash.Fields(b)
					.Where(pair => !fields.TryGetValue(pair.Key, out ulong value) || value != pair.Value)
					.Select(pair => pair.Key).ToList();
				Console.WriteLine($"hour {hour} ({a.Date}): first parts in {string.Join(", ", parted)} - {figures.Count} figures: " +
					string.Join("; ", figures.Take(60)));
			}
			else if (parted.Length > 0 && hour % 24 == 0)
			{
				Console.WriteLine($"hour {hour} ({a.Date}): differs in {string.Join(", ", parted)}");
			}
		}
		Console.WriteLine(firstPart < 0 ? $"{hoursAfter} hours after the load: the same" : $"parted at hour {firstPart}");
		return firstPart < 0 ? 0 : 1;
	}

	/// <summary>
	/// What the check leaves out, as noise in the check itself and not anything later play hangs on (<c>docs/enhanced.md</c>,
	/// the section's opening): the numbers a unit's file or a save names its leader by, where its file's line came, and a
	/// unit line's <c>is_reserve</c>, read only as a game is made or loaded - the leader himself and the brigades' reserve are compared; whether a game was
	/// loaded; caches made again on demand to the same figures - the distances by sea, the route finder's arrays, the land
	/// within two edges of a province, the air bases each country holds; and the count of changes of hands since a game
	/// began, with the stamps of the caches that watch it, which counts events and not state - each game's caches agree
	/// with its own count; and a unit's and a brigade's figures of a battle's hour - its circumstances and the defences it
	/// has used - which each battle hour makes again before anything reads them, and which stay on a unit, read by
	/// nothing, once its battle is over.
	/// </summary>
	private static readonly string[] Skipped =
	{
		"CUnit.LeaderId", "CUnit.LeaderLine", "CUnit.IsReserve", "CGameState.LoadedFromSave",
		"CGameState._navalDistances", "CGameState.RouteScratch", "CProvince.NearProvinces", "CGameState.AirBaseHolders",
		"CGameState._handChanges", "CProvince._handChanges", "CGameState._controlledCounts", "CGameState._controlledCountsAt",
		"CGameState.IntelFingerprint", "CCountryFacts.ControlChanges", "COwnedCounts.ControlChanges",
		"CSupplyNetwork.ControlChanges",
		"CUnit.CombatAttack", "CUnit.CombatModifiers", "CUnit.DefencesUsed",
		"CSubUnit.CombatAttack", "CSubUnit.CombatDefence", "CSubUnit.CombatModifiers",
	};

	private static void Report(string when, CGameState a, CGameState b, int groups)
	{
		var diff = new CStateDiff(examples: s_examples, skippedFields: Skipped);
		// The countries first, so that a unit's path runs through its country and not through whatever reaches it first; each
		// under its tag, which the grouping takes out and the examples keep.
		foreach (CCountry country in a.Countries)
		{
			diff.Compare(country, b.Country(country.Tag), $"country[{country.Tag}]");
		}
		diff.Compare(a, b, "state");
		Console.WriteLine($"{when}: {diff.Groups.Count} paths differ");
		foreach (var (path, (count, example)) in diff.Groups.OrderBy(pair => pair.Key, StringComparer.Ordinal).Take(groups))
		{
			Console.WriteLine($"  {count,7}  {path}   e.g. {example}");
		}
	}

	private static int s_examples = 1;

	private static string? Option(string[] args, string prefix)
	{
		return args.FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
	}
}
