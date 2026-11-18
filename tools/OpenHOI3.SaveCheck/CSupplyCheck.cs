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
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

/// <summary>
/// Checks the supply network against a savegame:
///
///   OpenHOI3.SaveCheck supply &lt;install folder&gt; &lt;savegame&gt; [examples]
///
/// A save does not say which depot a province draws on, but it says three things the
/// network decides. A province that draws on none carries <c>out_of_supply_days</c>; a
/// province that passes supplies without limit carries <c>capital=yes</c>; and what
/// went through a province in a day, <c>throughput</c>, is never more than it can
/// pass. Who holds what is taken from the save, so a save of any day will do. See
/// <c>docs/mechanics/supply.md</c>, section 9.
/// </summary>
public static class CSupplyCheck
{
	public static int Run(string[] args)
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck supply <install folder> <savegame> [examples to show]");
			return 2;
		}
		int examples = args.Length > 3 && int.TryParse(args[3], out int wanted) ? wanted : 10;

		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, firstDays: false);
		ClausewitzNode save = ClausewitzParser.ParseFile(args[2]);
		Console.WriteLine($"savegame: date {save.GetString("date")}, player {save.GetString("player")}");

		// Who holds what, and where each capital is, as the save has them.
		var cut = new HashSet<int>();
		var besideCapital = new HashSet<int>();
		var through = new Dictionary<int, (CFixedPoint Supplies, CFixedPoint Fuel)>();
		var lying = new Dictionary<int, CFixedPoint>();
		foreach (ClausewitzNode node in save.Children)
		{
			if (node.Key is null || !node.IsBlock)
			{
				continue;
			}
			if (int.TryParse(node.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
				&& id > 0 && id < state.Provinces.Count)
			{
				CProvince province = state.Provinces[id];
				province.Owner = CCountryTag.TryParse(node.GetString("owner"), out CCountryTag owner) ? owner : null;
				province.Controller = CCountryTag.TryParse(node.GetString("controller"), out CCountryTag holder) ? holder : null;
				if (node.GetInt("out_of_supply_days") > 0)
				{
					cut.Add(id);
				}
				if (node.GetBool("capital"))
				{
					besideCapital.Add(id);
				}
				if (node.Get("pool") is { } pool)
				{
					lying[id] = Number(pool.GetString("supplies"));
				}
				if (node.Get("throughput") is { } passed)
				{
					through[id] = (Number(passed.GetString("supplies")), Number(passed.GetString("fuel")));
				}
			}
			else if (CCountryTag.TryParse(node.Key, out CCountryTag tag) && state.Country(tag) is { } country
				&& node.GetInt("capital") > 0)
			{
				country.Capital = node.GetInt("capital");
				country.ActingCapital = node.GetInt("acting_capital") is > 0 and var acting ? acting : country.Capital;
			}
		}

		if (Release(args, state) is { Count: > 0 } involved)
		{
			return Trial(state, involved);
		}

		var clock = System.Diagnostics.Stopwatch.StartNew();
		CSupplyNetwork network = CSupplyNetworks.Build(state);
		clock.Stop();
		int depots = Enumerable.Range(1, state.Provinces.Count - 1).Count(network.IsDepot);
		int unreached = Enumerable.Range(1, state.Provinces.Count - 1)
			.Count(id => network.DistanceOf(id) >= CSupplyNetwork.Unreached);
		Console.WriteLine($"ours: {network.Areas.Count} areas in {network.Groups.Count} groups, {depots} depots, "
			+ $"{unreached} provinces whose depot cannot be reached, made in {clock.ElapsedMilliseconds} ms");
		Console.WriteLine();

		int failures = 0;

		// 1. Which provinces draw on nothing.
		var ours = new HashSet<int>();
		for (int id = 1; id < state.Provinces.Count; id++)
		{
			if (network.DepotOf(id) == 0)
			{
				ours.Add(id);
			}
		}
		failures += Report("provinces that draw on no depot", cut, ours, state, network, examples);

		// 2. Which provinces pass supplies without limit.
		var unlimited = new HashSet<int>();
		for (int id = 1; id < state.Provinces.Count; id++)
		{
			if (CSupplyFigures.IsBesideCapital(state, state.Provinces[id]))
			{
				unlimited.Add(id);
			}
		}
		failures += Report("provinces that are a capital or beside one", besideCapital, unlimited, state, network, examples);

		// 3. Nothing passed more than it can.
		int over = 0;
		int full = 0;
		var shown = new List<string>();
		foreach ((int id, (CFixedPoint supplies, CFixedPoint fuel)) in through)
		{
			CFixedPoint limit = CSupplyFigures.Capacity(state, state.Provinces[id]);
			CFixedPoint more = supplies > fuel ? supplies : fuel;
			if (more > limit)
			{
				over++;
				if (shown.Count < examples)
				{
					shown.Add($"   {state.Provinces[id]}: passed {supplies} supplies and {fuel} fuel, can pass {limit}");
				}
			}
			else if (more == limit)
			{
				full++;
			}
		}
		Console.WriteLine($"what passed through against what can pass: {through.Count} provinces, {over} over, {full} at the limit");
		shown.ForEach(Console.WriteLine);
		failures += over;

		// 4. A convoy that carries supplies runs between a port of the land a country's
		// capital is in and a port of land that draws on another depot: were the two ends
		// on one depot the sea would not be needed. A save of the first hour has none.
		int convoys = 0;
		int wrong = 0;
		var reached = new HashSet<int>();
		shown.Clear();
		foreach (ClausewitzNode country in save.Children)
		{
			if (country.Key is null || !country.IsBlock || !CCountryTag.TryParse(country.Key, out CCountryTag tag)
				|| state.Country(tag) is not { } sender)
			{
				continue;
			}
			foreach (ClausewitzNode convoy in country.All("convoy"))
			{
				if (!convoy.IsBlock || convoy.GetBool("trade", true) || convoy.GetBool("lend_lease") || convoy.Get("path") is null)
				{
					continue;
				}
				int start = network.DepotOf(convoy.GetInt("start"));
				int end = network.DepotOf(convoy.GetInt("end"));
				convoys++;
				if (start == 0 || end == 0 || start == end || (start != sender.Capital && end != sender.Capital))
				{
					wrong++;
					if (shown.Count < examples)
					{
						shown.Add($"   {country.Key}: {Describe(state, network, convoy.GetInt("start"))}  ->  "
							+ Describe(state, network, convoy.GetInt("end")));
					}
				}
				else
				{
					reached.Add(start == sender.Capital ? end : start);
				}
			}
		}
		Console.WriteLine($"convoys that carry supplies: {convoys}, {wrong} that do not join a capital's depot to another; "
			+ $"{reached.Count} of our {depots} depots are reached by one");
		shown.ForEach(Console.WriteLine);
		failures += wrong;

		// 5. Not a test, since nothing says it must be so: where the most supplies lie in
		// each network of more than one province. Supplies go out from a depot and what is
		// over comes back to it, so it should be the depot more often than not.
		var most = new Dictionary<int, (int Province, CFixedPoint Supplies, int Size)>();
		for (int id = 1; id < state.Provinces.Count; id++)
		{
			int depot = network.DepotOf(id);
			if (depot == 0)
			{
				continue;
			}
			CFixedPoint here = lying.GetValueOrDefault(id);
			(int Province, CFixedPoint Supplies, int Size) best = most.GetValueOrDefault(depot);
			most[depot] = here > best.Supplies || best.Province == 0 ? (id, here, best.Size + 1) : (best.Province, best.Supplies, best.Size + 1);
		}
		int larger = most.Count(pair => pair.Value.Size > 1 && pair.Value.Supplies > CFixedPoint.Zero);
		int atDepot = most.Count(pair => pair.Value.Size > 1 && pair.Value.Supplies > CFixedPoint.Zero && pair.Value.Province == pair.Key);
		Console.WriteLine($"networks of more than one province with supplies in them: {larger}; the most lie at our depot in {atDepot}");
		foreach (KeyValuePair<int, (int Province, CFixedPoint Supplies, int Size)> pair in most
			.Where(pair => pair.Value.Size > 1 && pair.Value.Supplies > CFixedPoint.Zero && pair.Value.Province != pair.Key)
			.OrderByDescending(pair => pair.Value.Size).Take(examples))
		{
			Console.WriteLine($"   {pair.Value.Size} provinces on {state.Provinces[pair.Key]} with {lying.GetValueOrDefault(pair.Key)}: "
				+ $"most at {state.Provinces[pair.Value.Province]}, {pair.Value.Supplies}");
		}

		Console.WriteLine();
		WhoDrawsOnWhat(state, network, new[] { "GER", "ENG", "ITA", "JAP", "MAN", "SOV", "MON", "USA", "PHI", "ETH" });

		if (warnings.Count > 0)
		{
			Console.WriteLine();
			Console.WriteLine($"{warnings.Count} warnings while loading.");
		}
		return failures == 0 ? 0 : 1;
	}

	/// <summary>For each country, how many of the provinces it holds draw on which depot.</summary>
	private static void WhoDrawsOnWhat(CGameState state, CSupplyNetwork network, IEnumerable<string> tags)
	{
		foreach (string tag in tags)
		{
			if (state.Country(new CCountryTag(tag)) is not { Capital: > 0 } country)
			{
				continue;
			}
			var drawnOn = new SortedDictionary<int, int>();
			int onNothing = 0;
			for (int id = 1; id < state.Provinces.Count; id++)
			{
				if (state.Provinces[id].Controller != country.Tag)
				{
					continue;
				}
				if (network.DepotOf(id) is > 0 and int depot)
				{
					drawnOn[depot] = drawnOn.GetValueOrDefault(depot) + 1;
				}
				else if (network.AreaOf(id) is not null)
				{
					onNothing++;
				}
			}
			Console.WriteLine($"{tag}: " + string.Join(", ", drawnOn.OrderByDescending(pair => pair.Value)
				.Select(pair => $"{pair.Value} on {state.Provinces[pair.Key].Name}"))
				+ (onNothing > 0 ? $", {onNothing} with roads on nothing" : ""));
		}
	}

	/// <summary>
	/// Every <c>release=TAG:MASTER</c> among the arguments: the country is given every
	/// province it has a core on that the master owns and holds, and made the master's
	/// puppet - or, with <c>:free</c> after it, left nobody's, which is how the land
	/// stands in the original for the days before it groups its areas again. Answers
	/// the countries to look at: each master, its puppets, and the released.
	/// </summary>
	private static List<string> Release(string[] args, CGameState state)
	{
		var involved = new List<string>();
		foreach (string arg in args)
		{
			if (!arg.StartsWith("release=", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			string[] parts = arg["release=".Length..].Split(':');
			bool free = parts.Length == 3 && parts[2].Equals("free", StringComparison.OrdinalIgnoreCase);
			if ((parts.Length != 2 && !free) || state.Country(new CCountryTag(parts[0].ToUpperInvariant())) is not { } released
				|| state.Country(new CCountryTag(parts[1].ToUpperInvariant())) is not { } master)
			{
				Console.Error.WriteLine($"'{arg}' is not release=TAG:MASTER with two countries of the install.");
				continue;
			}
			int given = 0;
			foreach (CProvince province in state.Provinces)
			{
				if (province.Owner == master.Tag && province.Controller == master.Tag && province.Cores.Contains(released.Tag))
				{
					province.Owner = released.Tag;
					province.Controller = released.Tag;
					given++;
				}
			}
			released.Overlord = free ? null : master.Tag;
			Console.WriteLine($"released {released.Tag} {(free ? "out of the land of" : "as a puppet of")} {master.Tag}: {given} provinces, "
				+ $"capital {state.Province(released.Capital)} held by {state.Province(released.Capital).Controller?.Value ?? "nobody"}");
			foreach (string tag in new[] { master.Tag.Value, released.Tag.Value })
			{
				if (!involved.Contains(tag))
				{
					involved.Add(tag);
				}
			}
			foreach (CCountry country in state.Countries)
			{
				if (country.Overlord == master.Tag && !involved.Contains(country.Tag.Value))
				{
					involved.Add(country.Tag.Value);
				}
			}
		}
		return involved;
	}

	/// <summary>
	/// A trial and not a check: with countries released, the save has nothing to say, so
	/// this only tells who draws on what. The original was watched doing the same
	/// on 2026-10-09 - <c>docs/mechanics/supply.md</c>, section 9.
	/// </summary>
	private static int Trial(CGameState state, List<string> involved)
	{
		CSupplyNetwork network = CSupplyNetworks.Build(state);
		Console.WriteLine();
		WhoDrawsOnWhat(state, network, involved);
		Console.WriteLine();
		foreach (CSupplyGroup group in network.Groups)
		{
			if (!group.Areas.Exists(area => involved.Contains(area.Controller.Value)) || group.Areas.Count < 2)
			{
				continue;
			}
			Console.WriteLine($"a group of {group.Areas.Count} areas on {(group.Depot == 0 ? "no depot" : state.Province(group.Depot).ToString())}: "
				+ string.Join(", ", group.Areas.Select(area => $"{area.Controller} {area.Provinces.Count}")));
		}
		return 0;
	}

	private static CFixedPoint Number(string? text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}

	private static int Report(string what, HashSet<int> theirs, HashSet<int> ours, CGameState state,
		CSupplyNetwork network, int examples)
	{
		List<int> missing = theirs.Except(ours).OrderBy(id => id).ToList();
		List<int> extra = ours.Except(theirs).OrderBy(id => id).ToList();
		Console.WriteLine($"{what}: save {theirs.Count}, ours {ours.Count}, "
			+ $"{theirs.Count - missing.Count} in both, {missing.Count} only in the save, {extra.Count} only in ours");
		foreach (int id in missing.Take(examples))
		{
			Console.WriteLine($"   only in the save: {Describe(state, network, id)}");
		}
		foreach (int id in extra.Take(examples))
		{
			Console.WriteLine($"   only in ours:     {Describe(state, network, id)}");
		}
		return missing.Count + extra.Count;
	}

	private static string Describe(CGameState state, CSupplyNetwork network, int id)
	{
		CProvince province = state.Provinces[id];
		CSupplyArea? area = network.AreaOf(id);
		string inArea = area is null ? "no area"
			: $"area of {area.Provinces.Count} with {area.Ports.Count} ports, group of {area.Group?.Areas.Count}";
		return $"{province} held by {province.Controller?.Value ?? "nobody"}, infrastructure "
			+ $"{CSupplyFigures.MostInfrastructure(state, province)}, {inArea}, draws on {network.DepotOf(id)}";
	}
}
