// Checks an hour of weather against two saves of one game an hour apart:
//
//   dotnet run --project tools/OpenHOI3.SaveCheck -- weather <install folder> <earlier save> <later save> [examples]
//
// Every province is given the earlier save's weather and the game its fronts, the clock is set to the earlier save's
// hour, and one hour of weather is run - the fronts, then every province - and set beside the later save. A front born
// in that hour comes of draws and of cooldowns no save keeps, so none is born here: the provinces of a front the later
// save has and the earlier does not, and its home, are left out. See docs/mechanics/weather.md, section 7.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Weather;

internal static class CWeatherCheck
{
	private static readonly string[] Fields =
	{
		"humidity", "pressure", "windspeed", "temperature", "precipitation", "muddyness", "frozen", "cloud_coverage",
	};

	public static int Run(string[] args)
	{
		if (args.Length == 3)
		{
			return NewGame(args);
		}
		if (args.Length < 4)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck weather <install folder> <earlier save> <later save> [examples]");
			return 2;
		}
		int examples = args.Length > 4 && int.TryParse(args[4], out int wanted) ? wanted : 5;
		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, firstDays: false);
		CProvinceModifiers.Rebuild(state);

		ClausewitzNode earlier = ClausewitzParser.ParseFile(args[2]);
		ClausewitzNode later = ClausewitzParser.ParseFile(args[3]);
		if (!TryHour(earlier.GetString("date"), out long from) || !TryHour(later.GetString("date"), out long to) || to != from + 1)
		{
			Console.Error.WriteLine($"the saves are dated '{earlier.GetString("date")}' and '{later.GetString("date")}'; the second must be an hour later");
			return 2;
		}
		long start = state.Date.DayNumber * 24L + state.Hour;
		for (long hour = start; hour < from; hour++)
		{
			state.AdvanceOneHour();
		}
		Console.WriteLine($"earlier: {earlier.GetString("date")}   later: {later.GetString("date")}");

		CFixedPoint pressureDefault = state.Defines[CProvinceWeather.Section, CProvinceWeather.PressureDefault];
		Dictionary<int, ClausewitzNode> before = Provinces(earlier);
		Dictionary<int, ClausewitzNode> after = Provinces(later);
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id == 0)
			{
				continue;
			}
			ClausewitzNode? weather = before.GetValueOrDefault(province.Id)?.Get("weather");
			Set(province.Weather, weather, pressureDefault);
			province.Climate = CClimate.Of(state, province);
		}
		List<CWeatherFront> fronts = Fronts(earlier);
		state.Weather.Fronts.AddRange(fronts);
		// No front is born here: every zone's cooldown far off.
		foreach ((int _, int _) in state.Weather.LowPressureZones)
		{
			state.Weather.Cooldowns.Add(int.MaxValue / 2);
			state.Weather.Headings.Add(0);
		}

		// explain=<province> tells what the hour starts from for one province and what it is pulled toward.
		int explain = args.Skip(4).Where(arg => arg.StartsWith("explain=", StringComparison.Ordinal))
			.Select(arg => int.TryParse(arg[8..], out int id) ? id : 0).FirstOrDefault();
		state.AdvanceOneHour();
		CWeatherFronts.RunHourly(state);
		if (explain > 0 && explain < state.Provinces.Count)
		{
			CProvince province = state.Province(explain);
			CWeather weather = province.Weather;
			province.IsNight = CDaylight.IsNight(state, province);
			Console.WriteLine($"explain {explain} {province.Name}: {(province.IsSea ? "sea" : "land")}, terrain {province.Terrain?.Name}"
				+ $" (map's {map.Terrain[explain]?.Name}), climate index {map.ClimateIndex[explain]}, base {province.BaseTemperature},"
				+ $" season {province.Climate}, latitude {CDaylight.LatitudeNorth(state, province)}, night {province.IsNight},"
				+ $" front {province.WeatherFront is not null}");
			Console.WriteLine($"  before the update: temperature {weather.Temperature}, humidity {weather.Humidity}, cloud {weather.CloudCoverage},"
				+ $" precipitation {weather.Precipitation}, frozen {weather.Frozen}, wind {weather.WindSpeed}, pressure {weather.Pressure},"
				+ $" infrastructure {OpenHOI3.Sim.Supply.CSupplyFigures.EffectiveInfrastructure(province)}");
		}
		// What each province holds as the fronts leave it, for trying another terrain on one that disagrees.
		var spread = new Dictionary<int, CWeather>();
		foreach (CProvince province in state.Provinces)
		{
			var copy = new CWeather();
			Copy(province.Weather, copy);
			spread[province.Id] = copy;
		}
		CWeatherPass.RunHourly(state);
		if (explain > 0 && explain < state.Provinces.Count)
		{
			CProvince province = state.Province(explain);
			Console.WriteLine($"  after: temperature {province.Weather.Temperature}, humidity {province.Weather.Humidity}, "
				+ $"cloud {province.Weather.CloudCoverage}, its target now {new CFixedPoint(CProvinceWeather.BaseTemperature(state, province))}");
		}

		// A front of the later save that none of the earlier's became was born this hour: its provinces and its home
		// are left out.
		List<CWeatherFront> theirs = Fronts(later);
		var left = new HashSet<int>();
		var born = new List<CWeatherFront>();
		foreach (CWeatherFront front in theirs)
		{
			if (!fronts.Any(old => old.Home == front.Home && old.Provinces.Intersect(front.Provinces).Any()
				|| old.Home == front.Home && Math.Abs(old.Direction - front.Direction) <= 10))
			{
				born.Add(front);
				left.Add(front.Home);
				left.UnionWith(front.Provinces);
			}
		}
		Console.WriteLine($"fronts: {fronts.Count} before, {theirs.Count} after, {born.Count} born this hour and left out "
			+ $"with {left.Count} provinces; ours {state.Weather.Fronts.Count}");

		var compared = new SortedDictionary<string, int>();
		var findings = new SortedDictionary<string, List<string>>();
		var terrains = new List<string>();
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

		foreach (CProvince province in state.Provinces)
		{
			if (province.Id == 0 || left.Contains(province.Id) || !after.ContainsKey(province.Id))
			{
				continue;
			}
			var theirsWeather = new CWeather();
			Set(theirsWeather, after[province.Id].Get("weather"), pressureDefault);
			string kind = (province.IsSea ? "sea" : "land") + (province.WeatherFront is null ? "" : ", under a front");
			bool all = true;
			foreach (string field in Fields)
			{
				CFixedPoint ours = Field(province.Weather, field);
				CFixedPoint save = Field(theirsWeather, field);
				all &= ours == save;
				Compare($"{field}, {kind}", ours == save,
					() => $"{province.Id} {province.Name}: ours {ours}, save {save}");
			}
			if (all || province.IsSea)
			{
				continue;
			}
			// Which terrains would have given the save's hour: each tried with its own temperature in the base.
			CTerrain? had = province.Terrain;
			CFixedPoint hadBase = province.BaseTemperature;
			var fits = new List<string>();
			foreach (CTerrain terrain in state.Terrains)
			{
				Copy(spread[province.Id], province.Weather);
				province.Terrain = terrain;
				province.BaseTemperature = new CFixedPoint((map.ClimateIndex[province.Id] - 100) * 150L + terrain.Temperature.Raw);
				CProvinceWeather.Update(state, province);
				if (Fields.All(field => Field(province.Weather, field) == Field(theirsWeather, field)))
				{
					fits.Add(terrain.Name);
				}
			}
			province.Terrain = had;
			province.BaseTemperature = hadBase;
			Compare("land provinces that disagree, which another terrain would make agree", fits.Count > 0,
				() => $"{province.Id} {province.Name}: ours {had?.Name}, and no terrain fits");
			terrains.Add($"{province.Id} {province.Name}: ours {had?.Name}, the save's hour fits {string.Join(", ", fits)}");
		}

		// The fronts the earlier save had, as they stand now beside the later's: their provinces, in order.
		for (int i = 0; i < state.Weather.Fronts.Count; i++)
		{
			CWeatherFront ours = state.Weather.Fronts[i];
			CWeatherFront? save = i < theirs.Count ? theirs[i] : null;
			Compare("fronts: the provinces of each, in order", save is not null && ours.Home == save.Home
				&& ours.Provinces.SequenceEqual(save.Provinces),
				() => $"{i}: home {ours.Home}, ours {string.Join(' ', ours.Provinces.Take(8))} ({ours.Provinces.Count}), "
					+ $"save {(save is null ? "none" : $"home {save.Home} {string.Join(' ', save.Provinces.Take(8))} ({save.Provinces.Count})")}");
		}

		Console.WriteLine();
		foreach ((string what, int count) in compared)
		{
			int bad = findings.TryGetValue(what, out List<string>? list) ? list.Count : 0;
			Console.WriteLine($"{(bad == 0 ? "agree   " : "DISAGREE")}  {what}: {count - bad} of {count}");
			foreach (string line in list?.Take(examples) ?? Enumerable.Empty<string>())
			{
				Console.WriteLine("            " + line);
			}
		}
		Console.WriteLine();
		if (terrains.Count > 0)
		{
			Console.WriteLine($"the terrain each land province that disagrees would need ({terrains.Count}; docs/mechanics/map.md, *Not established*):");
			foreach (string line in terrains.Take(examples))
			{
				Console.WriteLine("  " + line);
			}
			Console.WriteLine();
		}
		Console.WriteLine($"{warnings.Count} warnings while loading");
		return 0;
	}

	/// <summary>
	/// A look and not a check: a new game's weather, after its 96 hours and two days, beside the start-date save's, figure
	/// by figure as spread over the provinces. The fronts' draws are not the original's, so no province is compared.
	/// </summary>
	private static int NewGame(string[] args)
	{
		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		var clock = System.Diagnostics.Stopwatch.StartNew();
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add);
		Console.WriteLine($"a new game made in {clock.Elapsed.TotalSeconds:F1} s");
		ClausewitzNode save = ClausewitzParser.ParseFile(args[2]);
		CFixedPoint pressureDefault = state.Defines[CProvinceWeather.Section, CProvinceWeather.PressureDefault];
		Dictionary<int, ClausewitzNode> provinces = Provinces(save);

		var ours = new List<CWeather>();
		var theirs = new List<CWeather>();
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id == 0 || !provinces.ContainsKey(province.Id))
			{
				continue;
			}
			ours.Add(province.Weather);
			var weather = new CWeather();
			Set(weather, provinces[province.Id].Get("weather"), pressureDefault);
			theirs.Add(weather);
		}
		Console.WriteLine($"fronts: ours {state.Weather.Fronts.Count}, the save's {Fronts(save).Count}; provinces under one: "
			+ $"ours {state.Weather.Fronts.Sum(front => front.Provinces.Count)}, the save's {Fronts(save).Sum(front => front.Provinces.Count)}");
		Console.WriteLine($"{"",-15} {"ours: least",12} {"median",9} {"most",9} {"not nought",11}   {"save: least",12} {"median",9} {"most",9} {"not nought",11}");
		foreach (string field in Fields)
		{
			Console.WriteLine($"{field,-15} {Spread(ours, field)}   {Spread(theirs, field)}");
		}
		return 0;
	}

	private static string Spread(List<CWeather> weathers, string field)
	{
		List<CFixedPoint> values = weathers.Select(weather => Field(weather, field)).OrderBy(value => value.Raw).ToList();
		int notNought = values.Count(value => value != CFixedPoint.Zero);
		return $"{values[0],12} {values[values.Count / 2],9} {values[^1],9} {notNought,11}";
	}

	private static Dictionary<int, ClausewitzNode> Provinces(ClausewitzNode save)
	{
		var provinces = new Dictionary<int, ClausewitzNode>();
		foreach (ClausewitzNode node in save.Children)
		{
			if (node.IsBlock && int.TryParse(node.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
			{
				provinces[id] = node;
			}
		}
		return provinces;
	}

	/// <summary>A save's fronts, in its order: a direction and rotation in thousandths, a countdown, a home, provinces.</summary>
	private static List<CWeatherFront> Fronts(ClausewitzNode save)
	{
		var fronts = new List<CWeatherFront>();
		foreach (ClausewitzNode node in save.Get("weather")?.All("front") ?? Enumerable.Empty<ClausewitzNode>())
		{
			var front = new CWeatherFront(node.GetInt("home"), (int)Number(node.GetString("direction")).Raw,
				(int)Number(node.GetString("rotation")).Raw) { MoveCountdown = node.GetInt("speed") };
			front.Provinces.Clear();
			foreach (ClausewitzNode id in node.Get("provinces")?.Children ?? new List<ClausewitzNode>())
			{
				if (int.TryParse(id.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int province))
				{
					front.Provinces.Add(province);
				}
			}
			fronts.Add(front);
		}
		return fronts;
	}

	/// <summary>A province's weather as a save writes it: a key left out where nought, and pressure where the default.</summary>
	private static void Set(CWeather weather, ClausewitzNode? node, CFixedPoint pressureDefault)
	{
		weather.Humidity = Number(node?.GetString("humidity"));
		string? pressure = node?.Get("pressure")?.Value;
		weather.Pressure = pressure is null ? pressureDefault : Number(pressure);
		weather.WindSpeed = Number(node?.GetString("windspeed"));
		weather.Temperature = Number(node?.GetString("temperature"));
		weather.Precipitation = Number(node?.GetString("precipitation"));
		weather.Muddyness = Number(node?.GetString("muddyness"));
		weather.Frozen = Number(node?.GetString("frozen"));
		weather.CloudCoverage = Number(node?.GetString("cloud_coverage"));
	}

	private static CFixedPoint Field(CWeather weather, string field) => field switch
	{
		"humidity" => weather.Humidity,
		"pressure" => weather.Pressure,
		"windspeed" => weather.WindSpeed,
		"temperature" => weather.Temperature,
		"precipitation" => weather.Precipitation,
		"muddyness" => weather.Muddyness,
		"frozen" => weather.Frozen,
		_ => weather.CloudCoverage,
	};

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

	private static CFixedPoint Number(string? text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}

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
