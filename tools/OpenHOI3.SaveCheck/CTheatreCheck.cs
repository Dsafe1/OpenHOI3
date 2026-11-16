using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

/// <summary>
/// Makes a new game as a player gets it and sets every country's theatres beside the <c>area_theatre</c> blocks a savegame
/// the original wrote on the start date keeps under each country's <c>ai</c>:
///
///   OpenHOI3.SaveCheck theatres &lt;install folder&gt; &lt;start-date savegame&gt; [tag] [examples]
///
/// Each theatre's number, headquarters, heat, key province, fronts - provinces in order, enemy, heat - and own provinces in
/// order; the headquarters made, by name, place and brigade; and the game's next theatre number. Given a tag, that
/// country's both ways. See <c>docs/mechanics/theatres.md</c>, section 13.
/// </summary>
internal static class CTheatreCheck
{
	private sealed record CSaved(int Id, int Unit, bool Hot, List<int> Key, List<(List<int> Provinces, string Enemy, bool Hot)> Fronts,
		List<int> Provinces);

	public static int Run(string[] args)
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck theatres <install folder> <start-date savegame> [tag] [examples]");
			return 2;
		}
		string? only = args.Length > 3 && args[3].Length == 3 && !int.TryParse(args[3], out _) ? args[3] : null;
		int examples = args.Skip(3).Select(a => int.TryParse(a, out int n) ? n : -1).FirstOrDefault(n => n >= 0, 5);
		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		ClausewitzNode save = ClausewitzParser.ParseFile(args[2]);
		using var scripts = new OpenHOI3.Lua.CAiScripts(directories, warnings.Add);
		var clock = System.Diagnostics.Stopwatch.StartNew();
		string? traced = args.Skip(3).FirstOrDefault(a => a.StartsWith("trace=", StringComparison.Ordinal))?["trace=".Length..];
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts,
			played: new[] { new CCountryTag(save.GetString("player")) },
			beforeFirstDays: traced is null ? null : made => made.TheatreTrace = (new CCountryTag(traced), Console.WriteLine),
			date: CStartSave.Date(args[2]));
		Console.WriteLine($"a new game made in {clock.ElapsedMilliseconds} ms; savegame {save.GetString("date")}, player {save.GetString("player")}");
		Console.WriteLine($"next theatre number: ours {state.NextTheatreId}, save {save.GetString("theatre")}; " +
			$"last unit number: ours {state.LastUnitId}, save's next {save.GetString("unit")}");

		var saved = new Dictionary<string, List<CSaved>>();
		var units = new Dictionary<int, ClausewitzNode>();
		foreach (ClausewitzNode block in save.Children)
		{
			if (!block.IsBlock || block.Key is not { Length: 3 } key)
			{
				continue;
			}
			if (block.Get("ai") is { IsBlock: true } ai)
			{
				saved[key] = ai.All("area_theatre").Select(Read).ToList();
			}
			CollectUnits(block, units);
		}

		if (only is not null)
		{
			Show(state, saved.GetValueOrDefault(only) ?? new List<CSaved>(), only);
			return 0;
		}

		int countriesCompared = 0;
		int sameCount = 0;
		int theatresCompared = 0;
		var counts = new Dictionary<string, int>();
		var misses = new Dictionary<string, List<string>>();
		void Count(string what, bool same, string miss)
		{
			counts[what] = counts.GetValueOrDefault(what) + (same ? 1 : 0);
			if (!same)
			{
				if (!misses.TryGetValue(what, out List<string>? list))
				{
					misses[what] = list = new List<string>();
				}
				list.Add(miss);
			}
		}
		foreach (CCountry country in state.Countries)
		{
			List<CTheatre> ours = country.Strategy.Theatres;
			List<CSaved> theirs = saved.GetValueOrDefault(country.Tag.Value) ?? new List<CSaved>();
			if (ours.Count == 0 && theirs.Count == 0)
			{
				continue;
			}
			countriesCompared++;
			bool sameNumber = ours.Count == theirs.Count;
			if (sameNumber)
			{
				sameCount++;
			}
			else
			{
				Count("theatre count", false, $"{country.Tag}: ours {ours.Count} ({Ids(ours)}), save {theirs.Count} ({string.Join(" ", theirs.Select(t => t.Id))})");
			}
			for (int at = 0; at < Math.Min(ours.Count, theirs.Count); at++)
			{
				CTheatre o = ours[at];
				CSaved s = theirs[at];
				theatresCompared++;
				string where = $"{country.Tag} #{at} (ours {o.Id}, save {s.Id})";
				Count("number", o.Id == s.Id, where);
				Count("headquarters", o.Headquarters == s.Unit, $"{where}: ours {o.Headquarters}, save {s.Unit}");
				Count("hot", o.IsHot == s.Hot, $"{where}: ours {o.IsHot}, save {s.Hot}");
				Count("key", o.KeyProvinces.SequenceEqual(s.Key), $"{where}: ours {Text(o.KeyProvinces)}, save {Text(s.Key)}");
				bool frontsSame = o.Fronts.Count == s.Fronts.Count && o.Fronts.Zip(s.Fronts).All(pair =>
					pair.First.Provinces.SequenceEqual(pair.Second.Provinces) && (pair.First.Enemy?.Value ?? "---") == pair.Second.Enemy
					&& pair.First.IsHot == pair.Second.Hot);
				bool frontSets = o.Fronts.SelectMany(f => f.Provinces).OrderBy(p => p)
					.SequenceEqual(s.Fronts.SelectMany(f => f.Provinces).OrderBy(p => p));
				Count("fronts", frontsSame, $"{where}: ours {Fronts(o.Fronts.Select(f => (f.Provinces, f.Enemy?.Value ?? "---", f.IsHot)))}\n" +
					$"         save {Fronts(s.Fronts)}");
				Count("front provinces as a set", frontSets, where);
				Count("provinces", o.Provinces.SequenceEqual(s.Provinces), $"{where}: ours {o.Provinces.Count}, save {s.Provinces.Count}; " +
					$"first difference {FirstDifference(o.Provinces, s.Provinces)}");
				Count("provinces as a set", o.Provinces.OrderBy(p => p).SequenceEqual(s.Provinces.OrderBy(p => p)),
					$"{where}: only ours {Text(o.Provinces.Except(s.Provinces).ToList())}, only save {Text(s.Provinces.Except(o.Provinces).ToList())}");
			}
		}

		// The headquarters a new game made, by name, place and brigade, beside the save's units of the same numbers.
		int made = 0;
		int namesSame = 0;
		int placesSame = 0;
		var headquartersMisses = new List<string>();
		int brigadesCompared = 0;
		int brigadesSame = 0;
		var brigadeMisses = new List<string>();
		var fieldSame = new Dictionary<string, int>();
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.Kind != EUnitKind.Theatre || country.Strategy.Theatres.All(t => t.Headquarters != unit.Id)
					|| !units.TryGetValue(unit.Id, out ClausewitzNode? theirs))
				{
					continue;
				}
				made++;
				string name = theirs.GetString("name");
				int location = theirs.GetInt("location");
				namesSame += name == unit.Name ? 1 : 0;
				placesSame += location == unit.Location ? 1 : 0;
				if (name != unit.Name || location != unit.Location)
				{
					headquartersMisses.Add($"{country.Tag} {unit.Id}: ours {unit.Name} at {unit.Location}, save {name} at {location}");
				}
				// A brigade made with the headquarters: its name and figures beside the save's.
				if (unit.SubUnits.Count == 1 && theirs.Get("regiment") is { IsBlock: true } regiment
					&& unit.SubUnits[0].Id == (regiment.Get("id")?.GetInt("id") ?? 0) && unit.Id > state.LastUnitId - 2 * 120)
				{
					CSubUnit brigade = unit.SubUnits[0];
					string ours = $"{brigade.Name} strength {brigade.Strength} highest {brigade.Highest} experience {brigade.Experience} " +
						$"organisation {brigade.Organisation}";
					string theirText = $"{regiment.GetString("name")} strength {regiment.GetString("strength")} highest {regiment.GetString("highest")} " +
						$"experience {regiment.GetString("experience")} organisation {regiment.GetString("organisation")}";
					brigadesCompared++;
					fieldSame["name"] = fieldSame.GetValueOrDefault("name") + (brigade.Name == regiment.GetString("name") ? 1 : 0);
					fieldSame["strength"] = fieldSame.GetValueOrDefault("strength")
						+ (brigade.Strength.ToString() == regiment.GetString("strength") && brigade.Highest.ToString() == regiment.GetString("highest") ? 1 : 0);
					fieldSame["experience"] = fieldSame.GetValueOrDefault("experience")
						+ (brigade.Experience.ToString() == regiment.GetString("experience") ? 1 : 0);
					fieldSame["organisation"] = fieldSame.GetValueOrDefault("organisation")
						+ (brigade.Organisation.ToString() == regiment.GetString("organisation") ? 1 : 0);
					if (ours == theirText)
					{
						brigadesSame++;
					}
					else
					{
						brigadeMisses.Add($"{country.Tag} {unit.Id}: ours {ours}\n         save {theirText}");
					}
				}
			}
		}

		Console.WriteLine($"{countriesCompared} countries with theatres here or in the save; the same number of theatres in {sameCount}");
		Console.WriteLine($"{theatresCompared} theatres set side by side, in order:");
		foreach (string what in new[] { "number", "headquarters", "hot", "key", "fronts", "front provinces as a set", "provinces",
			"provinces as a set", "theatre count" })
		{
			if (what == "theatre count")
			{
				Console.WriteLine($"   theatre count differs in {misses.GetValueOrDefault(what)?.Count ?? 0}");
			}
			else
			{
				Console.WriteLine($"   {what}: {counts.GetValueOrDefault(what)} of {theatresCompared}");
			}
			foreach (string miss in misses.GetValueOrDefault(what, new List<string>()).Take(examples))
			{
				Console.WriteLine($"      {miss}");
			}
		}
		Console.WriteLine($"headquarters of our theatres that the save has by number: {made}; named the same {namesSame}, placed the same {placesSame}");
		foreach (string miss in headquartersMisses.Take(examples))
		{
			Console.WriteLine($"   {miss}");
		}
		Console.WriteLine($"brigades of the headquarters made: {brigadesSame} of {brigadesCompared} the save's in name, strength, highest, " +
			"experience and organisation; " + string.Join(", ", fieldSame.Select(pair => $"{pair.Key} {pair.Value}")));
		foreach (string miss in brigadeMisses.Take(examples))
		{
			Console.WriteLine($"   {miss}");
		}
		Console.WriteLine($"{warnings.Count} warnings while loading");
		return 0;
	}

	private static void Show(CGameState state, List<CSaved> theirs, string tag)
	{
		if (state.Country(new CCountryTag(tag)) is not { } country)
		{
			Console.WriteLine($"no country {tag}");
			return;
		}
		Console.WriteLine($"{tag}: ours");
		foreach (CTheatre t in country.Strategy.Theatres)
		{
			Console.WriteLine($"  {t.Id} hq {t.Headquarters}{(t.IsHot ? " hot" : "")} key {Text(t.KeyProvinces)} provinces {t.Provinces.Count}");
			foreach (CFront f in t.Fronts)
			{
				Console.WriteLine($"     {f}");
			}
			Console.WriteLine($"     provinces {Text(t.Provinces)}");
		}
		Console.WriteLine($"{tag}: save");
		foreach (CSaved t in theirs)
		{
			Console.WriteLine($"  {t.Id} hq {t.Unit}{(t.Hot ? " hot" : "")} key {Text(t.Key)} provinces {t.Provinces.Count}");
			foreach ((List<int> provinces, string enemy, bool hot) in t.Fronts)
			{
				Console.WriteLine($"     {enemy}{(hot ? " hot" : "")}: {Text(provinces)}");
			}
			Console.WriteLine($"     provinces {Text(t.Provinces)}");
		}
	}

	private static CSaved Read(ClausewitzNode block)
	{
		var fronts = new List<(List<int>, string, bool)>();
		foreach (ClausewitzNode front in block.All("front"))
		{
			if (!front.IsBlock)
			{
				continue;
			}
			fronts.Add((Numbers(front.Get("provinces")), front.GetString("local_enemy", "---"), front.GetBool("front")));
		}
		ClausewitzNode? provinces = block.Children.LastOrDefault(child => child.Key == "provinces");
		return new CSaved(block.Get("id")?.GetInt("id") ?? 0, block.Get("unit")?.GetInt("id") ?? 0, block.GetBool("hot"),
			Numbers(block.Get("key")), fronts, Numbers(provinces));
	}

	private static List<int> Numbers(ClausewitzNode? list)
	{
		var numbers = new List<int>();
		foreach (ClausewitzNode child in list?.Children ?? new List<ClausewitzNode>())
		{
			if (int.TryParse(child.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
			{
				numbers.Add(number);
			}
		}
		return numbers;
	}

	private static readonly HashSet<string> UnitKeys = new() { "theatre", "armygroup", "army", "corps", "division", "navy", "air" };

	private static void CollectUnits(ClausewitzNode block, Dictionary<int, ClausewitzNode> units)
	{
		foreach (ClausewitzNode child in block.Children)
		{
			if (!child.IsBlock || child.Key is null || !UnitKeys.Contains(child.Key))
			{
				continue;
			}
			if (child.Get("id")?.GetInt("id") is int id && id > 0)
			{
				units[id] = child;
			}
			CollectUnits(child, units);
		}
	}

	private static string Ids(List<CTheatre> theatres) => string.Join(" ", theatres.Select(t => t.Id));

	private static string Text(IEnumerable<int> numbers) => string.Join(" ", numbers);

	private static string Fronts(IEnumerable<(List<int> Provinces, string Enemy, bool Hot)> fronts) =>
		string.Join(" | ", fronts.Select(f => $"{f.Enemy}{(f.Hot ? "*" : "")}: {Text(f.Provinces)}"));

	private static string FirstDifference(List<int> ours, List<int> theirs)
	{
		for (int at = 0; at < Math.Max(ours.Count, theirs.Count); at++)
		{
			int o = at < ours.Count ? ours[at] : 0;
			int s = at < theirs.Count ? theirs[at] : 0;
			if (o != s)
			{
				return $"at {at}: ours {o}, save {s}";
			}
		}
		return "none";
	}
}
