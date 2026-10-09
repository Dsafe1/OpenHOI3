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
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Weather;

/// <summary>
/// Checks the march of land units against two savegames of one game, hours apart:
///
///   OpenHOI3.SaveCheck march &lt;install folder&gt; &lt;earlier save&gt; &lt;later save&gt; [examples]
///
/// Every division that has the same province, the same path and no other business in both
/// saves is given the brigades, the fuel and the weather the earlier save shows, and what an
/// hour adds to its progress here, times the hours between the two, is set against what the
/// saves say it gained. Where the weather over its province is not the same in both, the gain
/// has only to lie between the two speeds. See <c>docs/mechanics/movement.md</c>, section 5.
/// </summary>
internal static class CMarchCheck
{
	/// <summary>What a finding starts with when a difference in it is not a fault of ours.</summary>
	private const string Information = "for information: ";

	private sealed record SavedDivision(ClausewitzNode Node, CCountry Country, int Location, List<int> Path, CFixedPoint Progress);

	public static int Run(string[] args)
	{
		if (args.Length < 4)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck march <install folder> <earlier save> <later save> [examples]");
			return 2;
		}
		int examples = args.Length > 4 && int.TryParse(args[4], out int wanted) ? wanted : 5;

		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add);

		ClausewitzNode earlier = ClausewitzParser.ParseFile(args[2]);
		ClausewitzNode later = ClausewitzParser.ParseFile(args[3]);
		if (!TryHour(earlier.GetString("date"), out long from) || !TryHour(later.GetString("date"), out long to) || to <= from)
		{
			Console.Error.WriteLine($"the saves are dated '{earlier.GetString("date")}' and '{later.GetString("date")}'; the second must be later");
			return 2;
		}
		long hours = to - from;
		Console.WriteLine($"earlier: {earlier.GetString("date")}   later: {later.GetString("date")}   {hours} hours");
		Console.WriteLine();

		var findings = new Dictionary<string, List<string>>();
		var compared = new Dictionary<string, int>();
		void Compare(string what, bool agrees, Func<string> detail)
		{
			compared[what] = compared.GetValueOrDefault(what) + 1;
			if (agrees)
			{
				return;
			}
			if (!findings.TryGetValue(what, out List<string>? list))
			{
				findings[what] = list = new List<string>();
			}
			list.Add(detail());
		}

		// ---- the world as the earlier save has it, as far as a march reads it: who holds
		// what, who is in which faction, and the weather.
		var laterWeather = new Dictionary<int, CWeather>();
		foreach (ClausewitzNode node in earlier.Children)
		{
			if (!TryProvince(state, node, out CProvince? province))
			{
				continue;
			}
			if (CCountryTag.TryParse(node.GetString("controller"), out CCountryTag controller))
			{
				province.Controller = controller;
			}
			ReadWeather(node, province.Weather);
			var then = new CWeather();
			if (later.Get(node.Key!) is { } laterNode)
			{
				ReadWeather(laterNode, then);
			}
			laterWeather[province.Id] = then;
		}
		foreach (CCountry country in state.Countries)
		{
			country.Faction = "";
		}
		foreach (ClausewitzNode faction in earlier.Get("faction")?.Children ?? new List<ClausewitzNode>())
		{
			foreach (ClausewitzNode member in faction.Children)
			{
				if (member.Value is { Length: CCountryTag.Length } tag && faction.Key is not null
					&& state.Country(new CCountryTag(tag)) is { } country)
				{
					country.Faction = faction.Key;
				}
			}
		}
		CProvinceModifiers.Rebuild(state);
		CCountryModifiers.Rebuild(state);
		CIndustrialCapacity.Update(state);
		CStrategicResources.Update(state);
		CCountryModifiers.Rebuild(state);

		var brigades = new Dictionary<int, CSubUnit>();
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				foreach (CSubUnit subUnit in unit.SubUnits)
				{
					brigades[subUnit.Id] = subUnit;
				}
			}
		}

		Dictionary<int, SavedDivision> before = Divisions(state, earlier);
		Dictionary<int, SavedDivision> after = Divisions(state, later);

		int moving = 0;
		int elsewhere = 0;
		int redeploying = 0;
		int delayed = 0;
		int unknownBrigades = 0;
		foreach ((int id, SavedDivision division) in before.OrderBy(entry => entry.Key))
		{
			if (division.Path.Count == 0)
			{
				continue;
			}
			moving++;
			string who = $"{division.Country.Tag} {id} {division.Node.GetString("name")}";
			CProvince here = state.Province(division.Location);
			CProvince next = state.Province(division.Path[0]);

			var unit = new CUnit(EUnitKind.Division, division.Country.Tag)
			{
				Id = id,
				Location = division.Location,
				FuelReceived = Number(division.Node.GetString("fuel")),
			};
			unit.Path.AddRange(division.Path);
			bool known = true;
			foreach (ClausewitzNode regiment in division.Node.All("regiment"))
			{
				if (brigades.TryGetValue(regiment.Get("id")?.GetInt("id") ?? 0, out CSubUnit? brigade))
				{
					unit.SubUnits.Add(brigade);
				}
				else
				{
					known = false;
				}
			}
			if (!known || unit.SubUnits.Count == 0)
			{
				unknownBrigades++;
				continue;
			}

			// ---- the way it is on
			CProvinceEdge? edge = here.EdgeTo(next.Id);
			Compare("a moving division's next province is one its own has an edge to", edge is not null,
				() => $"{who}: in {here.Id} {here.Name}, bound for {next.Id} {next.Name}");
			if (edge is { } open)
			{
				Compare("its progress is short of the edge's distance", division.Progress < open.Distance,
					() => $"{who}: {here.Id} to {next.Id}, progress {division.Progress}, distance {open.Distance}");
			}

			List<int>? route = CRouteFinder.Find(state, division.Country, unit, division.Location, division.Path[^1]);
			// Not a fault where it differs: nearly every path in a save is the AI's, which may
			// ask for one of the two careful finders or order a unit on in stages, and a path
			// here is refused through a puppet's or an ally's ground, which is not built.
			Compare(Information + "the way found here from where it is to where it is going is the way it has",
				route is not null && route.SequenceEqual(division.Path),
				() => $"{who}: from {here.Id}, save [{string.Join(' ', division.Path)}] {Length(state, here.Id, division.Path)}, " +
					$"ours [{(route is null ? "none" : string.Join(' ', route))}] {(route is null ? "" : Length(state, here.Id, route))}");

			// ---- what the hours between added
			if (!after.TryGetValue(id, out SavedDivision? then) || then.Location != division.Location
				|| !then.Path.SequenceEqual(division.Path))
			{
				elsewhere++;
				continue;
			}
			if (division.Node.Get("strategic_redeployment") is not null || then.Node.Get("strategic_redeployment") is not null)
			{
				redeploying++;
				continue;
			}
			if (Blocked(division.Node) || Blocked(then.Node))
			{
				delayed++;
				continue;
			}

			long gained = then.Progress.Raw - division.Progress.Raw;
			long first = CMovementSpeed.Speed(state, division.Country, unit, here, next).Raw;
			long firstWeather = CWeatherEffects.Movement(state, here.Weather, division.Country);
			long lastWeather = CWeatherEffects.Movement(state, laterWeather[here.Id], division.Country);
			string types = string.Join('+', unit.SubUnits.Select(subUnit => subUnit.Type.Key).Distinct());
			string detail = $"{who} [{types}] {here.Id} to {next.Id} ({next.Terrain?.Name}, edge {edge?.Kind}): " +
				$"the saves {new CFixedPoint(gained)} in {hours} hours, ours {new CFixedPoint(first)} an hour = {new CFixedPoint(first * hours)}  " +
				$"(top speed {CMovementSpeed.SlowestBrigade(unit)}, modifier {CMovementSpeed.Modifier(state, division.Country, unit, here, next)}, " +
				$"weather {firstWeather}, local {here.Modifier[CMovementSpeed.LocalUnitSpeed]}, fuel {unit.FuelReceived})";
			CFixedPoint lastFuel = Number(then.Node.GetString("fuel"));
			if (firstWeather == lastWeather && lastFuel == unit.FuelReceived)
			{
				Compare($"what {hours} hours added to a division's progress, its weather and its fuel the same in both saves",
					gained == first * hours, () => detail);
				continue;
			}

			// The weather turned or the day's fuel came somewhere in between: the speed was
			// one for some hours and the other for the rest, if it changed once.
			CWeather kept = Swap(here, laterWeather[here.Id]);
			unit.FuelReceived = lastFuel;
			long last = CMovementSpeed.Speed(state, division.Country, unit, here, next).Raw;
			Swap(here, kept);
			long low = Math.Min(first, last) * hours;
			long high = Math.Max(first, last) * hours;
			Compare($"what {hours} hours added, its weather or its fuel having changed: between the two speeds",
				gained >= low && gained <= high,
				() => detail + $"; under the later weather ({lastWeather}) and fuel ({lastFuel}) {new CFixedPoint(last)} an hour");
		}

		// ---- report
		int faults = 0;
		foreach ((string what, int count) in compared.OrderBy(entry => entry.Key))
		{
			int wrong = findings.TryGetValue(what, out List<string>? list) ? list.Count : 0;
			bool counts = !what.StartsWith(Information, StringComparison.Ordinal);
			Console.WriteLine($"{(wrong == 0 ? "agree   " : counts ? "DISAGREE" : "differs ")}  {what}: {count - wrong} of {count}");
			if (list is not null)
			{
				faults += counts ? wrong : 0;
				foreach (string line in list.Take(examples))
				{
					Console.WriteLine("            " + line);
				}
			}
		}
		Console.WriteLine();
		Console.WriteLine($"{before.Count} divisions in the earlier save, {moving} with a path");
		Console.WriteLine($"not compared: {elsewhere} in another province or on another path by the later save, " +
			$"{redeploying} under a strategic redeployment, {delayed} with an attack delay or in a battle, " +
			$"{unknownBrigades} with a brigade a new game does not start with");
		Console.WriteLine($"{warnings.Count} warnings while loading");
		return faults == 0 ? 0 : 1;
	}

	/// <summary>The distances of a path's edges added up, and the straight line from its first step to its end.</summary>
	private static string Length(CGameState state, int from, IReadOnlyList<int> path)
	{
		long sum = 0;
		int at = from;
		foreach (int step in path)
		{
			sum += state.Province(at).EdgeTo(step)?.Distance.Raw ?? 0;
			at = step;
		}
		if (path.Count == 0)
		{
			return "";
		}
		CProvince first = state.Province(path[0]);
		long firstStep = state.Province(from).EdgeTo(path[0])?.Distance.Raw ?? 0;
		return $"(long {new CFixedPoint(sum)}; first step {new CFixedPoint(firstStep)}, then {CRouteFinder.StraightLine(state, first, state.Province(path[^1]))} pixels straight)";
	}

	/// <summary>Puts another weather over a province's own figures and hands back what was there.</summary>
	private static CWeather Swap(CProvince province, CWeather weather)
	{
		CWeather own = province.Weather;
		var kept = new CWeather();
		Copy(own, kept);
		Copy(weather, own);
		return kept;
	}

	private static void Copy(CWeather from, CWeather to)
	{
		to.Humidity = from.Humidity;
		to.Pressure = from.Pressure;
		to.WindSpeed = from.WindSpeed;
		to.Temperature = from.Temperature;
		to.Precipitation = from.Precipitation;
		to.Muddyness = from.Muddyness;
		to.Frozen = from.Frozen;
		to.CloudCoverage = from.CloudCoverage;
	}

	private static void ReadWeather(ClausewitzNode province, CWeather weather)
	{
		ClausewitzNode? saved = province.Get("weather");
		weather.Humidity = Number(saved?.GetString("humidity"));
		weather.Pressure = Number(saved?.GetString("pressure"));
		weather.WindSpeed = Number(saved?.GetString("windspeed"));
		weather.Temperature = Number(saved?.GetString("temperature"));
		weather.Precipitation = Number(saved?.GetString("precipitation"));
		weather.Muddyness = Number(saved?.GetString("muddyness"));
		weather.Frozen = Number(saved?.GetString("frozen"));
		weather.CloudCoverage = Number(saved?.GetString("cloud_coverage"));
	}

	/// <summary>Whether a division is held by something a plain march is not: a delay before an attack, or a battle.</summary>
	private static bool Blocked(ClausewitzNode division)
	{
		return division.Get("attack_delay") is not null || division.Get("combat") is not null;
	}

	private static bool TryProvince(CGameState state, ClausewitzNode node, out CProvince province)
	{
		province = null!;
		if (node.Key is null || !node.IsBlock
			|| !int.TryParse(node.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
			|| id <= 0 || id >= state.Provinces.Count)
		{
			return false;
		}
		province = state.Provinces[id];
		return true;
	}

	/// <summary>Every division of every country in a save, by its number.</summary>
	private static Dictionary<int, SavedDivision> Divisions(CGameState state, ClausewitzNode save)
	{
		var found = new Dictionary<int, SavedDivision>();
		foreach (ClausewitzNode node in save.Children)
		{
			if (node.Key is not null && node.IsBlock && CCountryTag.TryParse(node.Key, out CCountryTag tag)
				&& state.Country(tag) is { } country)
			{
				Collect(state, node, country, found);
			}
		}
		return found;
	}

	private static void Collect(CGameState state, ClausewitzNode node, CCountry country, Dictionary<int, SavedDivision> found)
	{
		foreach (ClausewitzNode child in node.Children)
		{
			if (!child.IsBlock || child.Key is null)
			{
				continue;
			}
			if (child.Key == "division")
			{
				int id = child.Get("id")?.GetInt("id") ?? 0;
				int location = child.GetInt("location");
				var path = new List<int>();
				foreach (ClausewitzNode step in child.Get("path")?.Children ?? new List<ClausewitzNode>())
				{
					if (int.TryParse(step.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int province))
					{
						path.Add(province);
					}
				}
				if (id > 0 && location > 0 && location < state.Provinces.Count
					&& path.All(province => province > 0 && province < state.Provinces.Count))
				{
					found[id] = new SavedDivision(child, country, location, path, Number(child.GetString("movement_progress")));
				}
				continue;
			}
			if (child.Key is "theatre" or "armygroup" or "army" or "corps")
			{
				Collect(state, child, country, found);
			}
		}
	}

	private static CFixedPoint Number(string? text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}

	/// <summary>A save's date as hours: the day's number times 24 and the hour that follows it.</summary>
	private static bool TryHour(string text, out long hour)
	{
		hour = 0;
		string[] parts = text.Split('.');
		if (parts.Length < 4 || !CDate.TryParse(string.Join('.', parts.Take(3)), out CDate date)
			|| !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ofDay))
		{
			return false;
		}
		hour = date.DayNumber * 24L + ofDay;
		return true;
	}
}
