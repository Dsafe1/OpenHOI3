using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Saves;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;

/// <summary>
/// Where a country's supplies and fuel go, day by day - a look and not a check:
///
///   OpenHOI3.SaveCheck supplyledger &lt;install folder&gt; &lt;savegame | from=DATE&gt; [days] [TAG,TAG] [dump=FOLDER]
///
/// A save of the original's loaded, or a new game made at a bookmark's date, with the install's Lua and nobody playing,
/// run the days given (ten with none said). For each country (Germany with none said), at each midnight: its stockpile,
/// what lies in the provinces it holds, and the day's ledgers - made at home, sent out from the capital, sent back, what
/// its units asked for and drew of the capital, traded for and away - what changed at the midnight hour and in the other
/// hours, which a unit bringing its days and a change of hands move, what its provinces were asked for and passed, and
/// how many were asked for more than they can pass; at the end, where what lies in its provinces is
/// (<c>docs/mechanics/supply.md</c>, sections 11 to 15 and 12.1).
/// </summary>
internal static class CSupplyLedgerTrial
{
	public static int Run(string[] args)
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck supplyledger <install folder> <savegame | from=DATE> [days] [TAG,TAG] [dump=FOLDER]");
			return 2;
		}
		int days = args.Length > 3 && int.TryParse(args[3], out int d) ? d : 10;
		string[] tags = (args.Skip(4).FirstOrDefault(a => !a.Contains('=')) ?? "GER").Split(',');

		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		using var scripts = new CAiScripts(directories, warnings.Add);
		CGameState state;
		if (args[2].StartsWith("from=", StringComparison.Ordinal))
		{
			if (!CDate.TryParse(args[2]["from=".Length..], out CDate from))
			{
				Console.Error.WriteLine($"'{args[2]}' is not a date");
				return 2;
			}
			state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts, date: from);
		}
		else
		{
			(state, _) = CSaveLoader.Load(directories, map, localisation, countries, args[2], aiScripts: scripts, warn: warnings.Add);
		}
		state.Played.Clear();
		// seed=N: the AI's generator seeded afresh after the load, for runs from one state that differ only in its draws.
		if (args.FirstOrDefault(a => a.StartsWith("seed=", StringComparison.Ordinal)) is { } seedArgument
			&& uint.TryParse(seedArgument["seed=".Length..], out uint seed))
		{
			state.AiRandom.Seed(seed);
		}
		var simulation = new CSimulation(state);
		string? dump = args.FirstOrDefault(a => a.StartsWith("dump=", StringComparison.Ordinal))?["dump=".Length..];
		Console.WriteLine($"{state.Date} {state.Hour}h, nobody playing");

		var lastTotals = tags.ToDictionary(tag => tag, tag => Totals(state, tag));
		foreach (string tag in tags)
		{
			Print(state, tag, lastTotals[tag], "start");
		}
		var between = tags.ToDictionary(tag => tag, _ => (0L, 0L));
		CDate end = state.Date.AddDays(days);
		while (state.Date < end)
		{
			CDate before = state.Date;
			simulation.RunHours(1);
			foreach (string tag in tags)
			{
				var now = Totals(state, tag);
				var last = lastTotals[tag];
				if (state.Date == before)
				{
					// An hour that is no midnight: what moved in it.
					var (s, f) = between[tag];
					between[tag] = (s + (now.All - last.All), f + (now.AllFuel - last.AllFuel));
				}
				else
				{
					// The midnight hour's change, which holds the day's pass, and the other hours' together, in whole supplies.
					Print(state, tag, now, $"midnight {(now.All - last.All) / 1000,6:+0;-0} other hours {between[tag].Item1 / 1000,6:+0;-0}");
					if (dump is not null)
					{
						Dump(state, tag, dump);
					}
					between[tag] = (0, 0);
				}
				lastTotals[tag] = now;
			}
		}
		foreach (string tag in tags)
		{
			Where(state, tag);
			if (state.Country(new OpenHOI3.Sim.Countries.CCountryTag(tag)) is { } country)
			{
				var sums = Totals(state, tag);
				long own = state.TradeRoutes.Where(r => r.From == country.Tag).Sum(r => r.Terms.From(EGoods.Supplies));
				long theirs = state.TradeRoutes.Where(r => r.To == country.Tag).Sum(r => r.Terms.To(EGoods.Supplies));
				Console.WriteLine($"SUMMARY {tag} {state.Date} stock {sums.Stockpile / 1000} own {own / 1000.0:F1} buyers {theirs / 1000.0:F1} " +
					$"share {country.ProductionShares[(int)OpenHOI3.Sim.Economy.EProductionCategory.Supplies].Raw / 32768.0:F5}");
			}
		}
		return 0;
	}

	/// <summary>
	/// Where what lies in a country's provinces is: by whether the province's units ask for anything, by how far it is from
	/// its depot, and the provinces holding the most.
	/// </summary>
	private static void Where(CGameState state, string tag)
	{
		if (state.Country(new CCountryTag(tag)) is not { } country)
		{
			return;
		}
		var network = OpenHOI3.Sim.Supply.CSupplyNetworks.Refresh(state);
		CGoodsPool? stockpile = state.Stockpile(country);
		var rows = state.Provinces.Where(p => p.Controller == country.Tag && !ReferenceEquals(p.Pool, stockpile)
			&& p.Pool[EGoods.Supplies].Raw > 0).ToList();
		Console.WriteLine($"{tag}: {rows.Count} provinces hold supplies, {rows.Sum(p => p.Pool[EGoods.Supplies].Raw) / 1000} in all");
		foreach (var group in rows.GroupBy(p => (Need: p.SupplyNeed[EGoods.Supplies].Raw > 0, Depot: network.DepotOf(p.Id) > 0,
			Owned: p.Owner == country.Tag)))
		{
			Console.WriteLine($"  units asking {group.Key.Need,-5} in a network {group.Key.Depot,-5} owned {group.Key.Owned,-5}: " +
				$"{group.Count()} provinces, {group.Sum(p => p.Pool[EGoods.Supplies].Raw) / 1000} supplies, " +
				$"{group.Sum(p => 15 * p.SupplyNeed[EGoods.Supplies].Raw) / 1000} is fifteen days of their need");
		}
		foreach (var group in rows.GroupBy(p => network.DistanceOf(p.Id) / 10).OrderBy(g => g.Key))
		{
			Console.WriteLine($"  {group.Key * 10,3} to {group.Key * 10 + 9,3} from a depot: {group.Count()} provinces, " +
				$"{group.Sum(p => p.Pool[EGoods.Supplies].Raw) / 1000} supplies, need {group.Sum(p => p.SupplyNeed[EGoods.Supplies].Raw) / 1000}");
		}
		foreach (var province in rows.OrderByDescending(p => p.Pool[EGoods.Supplies].Raw).Take(25))
		{
			Console.WriteLine($"  {province.Id} owner {province.Owner} supplies {province.Pool[EGoods.Supplies]} fuel {province.Pool[EGoods.Fuel]} " +
				$"need {province.SupplyNeed[EGoods.Supplies]} asked {province.SupplyDrawn[EGoods.Supplies]} depot {network.DepotOf(province.Id)} " +
				$"distance {network.DistanceOf(province.Id)} units {state.UnitsIn(province.Id).Count}");
		}
	}

	private record struct CTotals(long Stockpile, long Provinces, long StockpileFuel, long ProvincesFuel)
	{
		public long All => Stockpile + Provinces;
		public long AllFuel => StockpileFuel + ProvincesFuel;
	}

	private static CTotals Totals(CGameState state, string tag)
	{
		CCountry? country = state.Country(new CCountryTag(tag));
		if (country is null)
		{
			return default;
		}
		CGoodsPool? stockpile = state.Stockpile(country);
		long supplies = 0, fuel = 0;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller == country.Tag && !ReferenceEquals(province.Pool, stockpile))
			{
				supplies += province.Pool[EGoods.Supplies].Raw;
				fuel += province.Pool[EGoods.Fuel].Raw;
			}
		}
		return new CTotals(stockpile?[EGoods.Supplies].Raw ?? 0, supplies, stockpile?[EGoods.Fuel].Raw ?? 0, fuel);
	}

	private static void Print(CGameState state, string tag, CTotals totals, string change)
	{
		if (state.Country(new CCountryTag(tag)) is not { } country)
		{
			return;
		}
		static string W(long raw) => (raw / 1000).ToString();
		static string P(CGoodsPool pool) => W(pool[EGoods.Supplies].Raw) + "/" + W(pool[EGoods.Fuel].Raw);
		Console.WriteLine($"{state.Date} {tag} stock {W(totals.Stockpile)}/{W(totals.StockpileFuel)} provinces {W(totals.Provinces)}/{W(totals.ProvincesFuel)} " +
			$"{change}  home {P(country.HomeProduced)} to {P(country.SentTo)} back {P(country.SentBack)} demand {P(country.UnitDemand)} " +
			$"usage {P(country.Usage)} tradedFor {P(country.TradedFor)} tradedAway {P(country.TradedAway)} convoyedOut {P(country.ConvoyedOut)}");
		// The supply share as the sliders left it, the need the Lua is handed for it over the whole IC, and the IC.
		double share = country.ProductionShares[(int)OpenHOI3.Sim.Economy.EProductionCategory.Supplies].Raw / 32768.0;
		double need = OpenHOI3.Sim.Economy.CProduction.SuppliesNeededExactly(state, country).Raw / 32768.0;
		Console.WriteLine($"    supply share {share:F5}, need {need:F3} IC of {country.TotalIc} ({(country.TotalIc > 0 ? need / country.TotalIc : 0):F5})");
		string needs = string.Join(" ", new[] { OpenHOI3.Sim.Economy.EProductionCategory.ConsumerGoods, OpenHOI3.Sim.Economy.EProductionCategory.Production,
			OpenHOI3.Sim.Economy.EProductionCategory.Reinforcement, OpenHOI3.Sim.Economy.EProductionCategory.Upgrades }
			.Select(c => $"{c} {OpenHOI3.Sim.Economy.CProduction.Needed(state, country, c).Raw / 1000.0 / Math.Max(1, country.TotalIc):F5}"));
		string shares = string.Join(" ", country.ProductionShares.Select(x => (x.Raw / 32768.0).ToString("F5")));
		Console.WriteLine($"    needs over IC: {needs}; shares {shares}");
		// Its supplies sold, on routes it proposed - exchanged in its own day, before its sliders are weighed - and on the
		// buyers', exchanged in theirs.
		long own = state.TradeRoutes.Where(r => r.From == country.Tag).Sum(r => r.Terms.From(EGoods.Supplies));
		long theirs = state.TradeRoutes.Where(r => r.To == country.Tag).Sum(r => r.Terms.To(EGoods.Supplies));
		Console.WriteLine($"    supplies sold on its own routes {own / 1000.0:F1}, on the buyers' {theirs / 1000.0:F1}");
		var ledger = OpenHOI3.Sim.Economy.CDailyLedger.Income(country, EGoods.Money);
		var spent = OpenHOI3.Sim.Economy.CDailyLedger.Expense(country, EGoods.Money);
		Console.WriteLine($"    money in {ledger / 1000.0:F3} (home {country.HomeProduced[EGoods.Money]} traded for {country.TradedFor[EGoods.Money]}) out {spent / 1000.0:F3} (traded away {country.TradedAway[EGoods.Money]}) balance {(ledger - spent) / 1000.0:F3}");
		// The day's pass over its ground: what its units ask for, what was asked of its provinces in all, and the most asked.
		var held = state.Provinces.Where(p => p.Controller == country.Tag).ToList();
		var top = held.OrderByDescending(p => p.SupplyDrawn[EGoods.Supplies].Raw).Take(4)
			.Select(p => $"{p.Id}:{W(p.SupplyDrawn[EGoods.Supplies].Raw)}/{W(p.Pool[EGoods.Supplies].Raw)}");
		// Provinces asked for more than they can pass: where the pass throws demand back (supply.md, section 12.1).
		int over = held.Count(p => p.SupplyDrawn[EGoods.Supplies] > OpenHOI3.Sim.Supply.CSupplyFigures.Capacity(state, p));
		Console.WriteLine($"    need {W(held.Sum(p => p.SupplyNeed[EGoods.Supplies].Raw))} asked {W(held.Sum(p => p.SupplyDrawn[EGoods.Supplies].Raw))} " +
			$"passed {W(held.Sum(p => p.SupplyThroughput[EGoods.Supplies].Raw))} asked over what they pass {over} " +
			$"most asked (asked/held) {string.Join(" ", top)}");
	}

	/// <summary>
	/// dump=FOLDER: each midnight a file of the country's provinces, one a line - number, depot, distance, need, asked,
	/// passed, held, units and the provinces across its edges - for a script to follow the demand along.
	/// </summary>
	private static void Dump(CGameState state, string tag, string folder)
	{
		var network = OpenHOI3.Sim.Supply.CSupplyNetworks.Refresh(state);
		var holder = new CCountryTag(tag);
		System.IO.File.WriteAllLines(System.IO.Path.Combine(folder, $"{tag}_{state.Date}.txt"), state.Provinces
			.Where(p => p.Controller == holder)
			.Select(p => $"{p.Id} {network.DepotOf(p.Id)} {network.DistanceOf(p.Id)} {p.SupplyNeed[EGoods.Supplies].Raw} " +
				$"{p.SupplyDrawn[EGoods.Supplies].Raw} {p.SupplyThroughput[EGoods.Supplies].Raw} {p.Pool[EGoods.Supplies].Raw} " +
				$"{state.UnitsIn(p.Id).Count} {string.Join(",", p.Edges.Select(e => e.To))}"));
	}
}
