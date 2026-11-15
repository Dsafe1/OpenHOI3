using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

/// <summary>
/// Checks the carrying of supplies against a savegame of the start date:
///
///   OpenHOI3.SaveCheck supplypass &lt;install folder&gt; &lt;savegame&gt; [examples]
///
/// The original fills the provinces before a game's first hour and has run two days by
/// the first save. So the units are made here as the save has them, the provinces are
/// filled, two days are run - the pass, then every unit's draw - and every province's
/// supplies and fuel are set beside the save's: what lies there, what was asked of it
/// today and yesterday, and what passed through it. See
/// <c>docs/mechanics/supply.md</c>, section 17.
/// </summary>
internal static class CSupplyPassCheck
{
	private static readonly EGoods[] Carried = { EGoods.Supplies, EGoods.Fuel };

	public static int Run(string[] args)
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck supplypass <install folder> <start-date savegame> [examples to show]");
			return 2;
		}
		int examples = args.Length > 3 && int.TryParse(args[3], out int wanted) ? wanted : 10;

		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		var clock = System.Diagnostics.Stopwatch.StartNew();
		// Not filled: the original's own sequence is run over it below.
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, fill: false,
			date: CStartSave.Date(args[2]));
		Console.WriteLine($"a new game made in {clock.ElapsedMilliseconds} ms");
		ClausewitzNode save = ClausewitzParser.ParseFile(args[2]);
		Console.WriteLine($"savegame: date {save.GetString("date")}, player {save.GetString("player")}");

		// The units as the save has them, which takes the laws they were made under.
		Dictionary<string, Dictionary<string, CLaw>> lawBook = CModifierReader.ReadLaws(directories, warnings.Add);
		HashSet<string> seats = CModifierReader.ReadGovernmentPositions(directories, warnings.Add);
		var savedCountries = new List<(ClausewitzNode Node, CCountry Country)>();
		foreach (ClausewitzNode node in save.Children)
		{
			if (node.Key is not null && node.IsBlock && CCountryTag.TryParse(node.Key, out CCountryTag tag)
				&& state.Country(tag) is { } country)
			{
				savedCountries.Add((node, country));
			}
		}
		CUnitCheck.Run(directories, savedCountries, state, lawBook, (_, _, _) => { });

		// The ministers and the technology the save has, which bear on what a unit uses
		// and on what a province passes.
		foreach ((ClausewitzNode node, CCountry country) in savedCountries)
		{
			// The laws the save has - but for a group named with `history=<tag>:<group>+<group>`,
			// which keeps its history's: the original's AI goes on changing laws after the last
			// day is run, so the law a save shows is not always the one its days were run under.
			string keep = args.Skip(3).FirstOrDefault(argument =>
				argument.StartsWith("history=" + country.Tag.Value + ":", StringComparison.OrdinalIgnoreCase)) ?? "";
			string[] kept = keep.Length == 0 ? Array.Empty<string>() : keep[(keep.IndexOf(':') + 1)..].Split('+');
			foreach ((string group, Dictionary<string, CLaw> laws) in lawBook)
			{
				if (!kept.Contains(group, StringComparer.OrdinalIgnoreCase) && laws.TryGetValue(node.GetString(group), out CLaw? law))
				{
					country.Laws[group] = law;
				}
			}
			country.Government.Clear();
			foreach (string seat in seats)
			{
				if (int.TryParse(node.GetString(seat), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
					&& country.Ministers.TryGetValue(id, out CMinister? minister))
				{
					country.Government[seat] = minister;
				}
			}
			foreach (ClausewitzNode entry in node.Get("technology")?.Children ?? new List<ClausewitzNode>())
			{
				if (entry.Key is not null && entry.IsBlock && entry.Children.Count > 0 && state.Technologies.ContainsKey(entry.Key))
				{
					country.TechnologyLevels[entry.Key] = (int)Number(entry.Children[0].Value).ToInt();
				}
			}
			// And its shares of IC, which decide how many supplies it makes in a day.
			List<ClausewitzNode> shares = node.Get("distribution")?.Children ?? new List<ClausewitzNode>();
			for (int i = 0; i < CProduction.CategoryCount && shares.Count == CProduction.CategoryCount; i++)
			{
				country.ProductionShares[i] = decimal.TryParse(shares[i].Value, NumberStyles.Float,
					CultureInfo.InvariantCulture, out decimal share)
					? new CFixed15((long)Math.Round(share * CFixed15.Scale))
					: CFixed15.Zero;
			}
		}
		// And what a new game makes of a country under those laws: the manpower it starts
		// with, worked out before any strategic resource counts, and its build queue.
		foreach (CCountry country in state.Countries)
		{
			country.StrategicResources.Clear();
		}
		CManpower.SetAtStart(state);
		CBuildQueue.SetUpAtStart(state);
		CTick.RunEntryPass(state);
		CStrategicResources.Update(state);
		CTick.RunEntryPass(state);

		// The original's AI moves the shares after the day is run, so the share the
		// supplies were made with is not the one the save shows. It is taken to be
		// whatever makes the supplies the save says were made.
		int fitted = 0;
		foreach ((ClausewitzNode node, CCountry country) in savedCountries)
		{
			CFixedPoint made = Number(node.Get("home")?.GetString("supplies"));
			// A country that does not exist never has what it made counted afresh, so two
			// days in the save holds two days' worth (`stockpile.md`, section 6).
			if (!CResources.HoldsCapital(state, country))
			{
				made = new CFixedPoint(made.Raw / 2);
			}
			int supplies = (int)EProductionCategory.Supplies;
			// The share the save shows is the one, where nothing has moved it since.
			if (CProduction.SuppliesMade(state, country) == made)
			{
				fitted++;
				continue;
			}
			country.ProductionShares[supplies] = CFixed15.One;
			long whole = CProduction.SuppliesMade(state, country).Raw;
			if (whole <= 0)
			{
				country.ProductionShares[supplies] = CFixed15.Zero;
				continue;
			}
			long guess = made.Raw * CFixed15.Scale / whole;
			for (long share = Math.Max(0, guess - 40); share <= guess + 40; share++)
			{
				country.ProductionShares[supplies] = new CFixed15(share);
				if (CProduction.SuppliesMade(state, country) == made)
				{
					fitted++;
					break;
				}
			}
		}
		Console.WriteLine($"{fitted} of {savedCountries.Count} countries given a supply share that makes the save's supplies to the thousandth");

		// The mud of the save's day, which is part of what a load loses, and the frost that undoes it.
		var savedProvinces = new Dictionary<int, ClausewitzNode>();
		foreach (ClausewitzNode node in save.Children)
		{
			if (node.Key is not null && node.IsBlock
				&& int.TryParse(node.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
				&& id > 0 && id < state.Provinces.Count && !savedProvinces.ContainsKey(id))
			{
				savedProvinces[id] = node;
				state.Provinces[id].Weather.Muddyness = Number(node.Get("weather")?.GetString("muddyness"));
				state.Provinces[id].Weather.Frozen = Number(node.Get("weather")?.GetString("frozen"));
			}
		}

		// A new game again, as far as supplies go: nothing anywhere, then the stockpiles, then the filling.
		foreach (CProvince province in state.Provinces)
		{
			foreach (EGoods goods in Carried)
			{
				province.Pool[goods] = CFixedPoint.Zero;
			}
			province.LastPool.Clear();
			province.BeginSupplyDay();
			province.BeginSupplyDay();
		}
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				unit.SuppliesReceived = CFixedPoint.One;
				unit.FuelReceived = CFixedPoint.One;
			}
		}
		// As a new game does it: the stockpile reckoned with only the leaders the files
		// name, and the rest chosen after.
		CLeaders.SetAtStart(state);
		CStockpile.AddUnitGoodsAtStart(state);
		CLeaders.AssignAtStart(state);

		// The original runs a day, then fills the provinces, then runs another day. What
		// the first day makes away from a capital is on its way there while the filling
		// puts the stockpiles back, and is lost.
		// All of it with a game "being made", which is when a convoy is manned for nothing.
		// It is the game's own doing that is run here - what a new game does before its
		// first hour - so that what is checked is what is played.
		clock.Restart();
		CTick.RunFirstDays(state, weather: false);                 // the save's mud and frost stand (weather.md, section 4)
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		Console.WriteLine($"the two days run in {clock.ElapsedMilliseconds} ms: the furthest province is {network.MostDistance} from its depot");
		Console.WriteLine();

		string? convoysOf = args.Skip(3).FirstOrDefault(argument => argument.StartsWith("convoys=", StringComparison.Ordinal));
		CConvoyCheck.Compare(state, savedCountries, examples, convoysOf?["convoys=".Length..]);
		CConvoyCheck.ComparePools(state, savedProvinces, examples);

		Moving(state, network, savedProvinces);

		// With `countries` after the save: each country as these two days left it beside the
		// save - the like-for-like of what `firstdays` sets side by side with nothing given.
		if (args.Skip(3).Contains("countries"))
		{
			Console.WriteLine("the countries, the save's laws, ministers, technology and shares given:");
			CFirstDaysCheck.Compare(state, save, examples, heldByLaw: true);
			Console.WriteLine();
		}

		// ---- the comparison, network by network
		(string Name, Func<CProvince, CGoodsPool> Ours, string Key, bool AtCapitals)[] figures =
		{
			("lying there", province => province.Pool, "pool", false),
			("asked of it today", province => province.SupplyDrawn, "drawn", true),
			("asked of it yesterday", province => province.LastSupplyDrawn, "last_drawn", true),
			("passed today", province => province.SupplyThroughput, "throughput", true),
			("passed yesterday", province => province.LastSupplyThroughput, "last_throughput", true),
		};

		// Most provinces hold nothing and pass nothing in the save and here alike, which
		// says little. So only a figure that is something on one side or the other counts.
		var perNetwork = new Dictionary<int, (int Provinces, int Agree)>();
		var totals = new Dictionary<string, (int Compared, int Agree, int Near)>();
		var shown = new Dictionary<string, List<string>>();
		foreach (CProvince province in state.Provinces)
		{
			if (!CSupplyDay.TakesPart(province) || !savedProvinces.TryGetValue(province.Id, out ClausewitzNode? node))
			{
				continue;
			}
			bool isCapital = state.Country(province.Controller) is { } holder && holder.Capital == province.Id;
			bool whole = true;
			foreach ((string name, Func<CProvince, CGoodsPool> ours, string key, bool atCapitals) in figures)
			{
				if (isCapital && !atCapitals)
				{
					continue;                                    // a stockpile has two days of production in it too
				}
				foreach (EGoods goods in Carried)
				{
					CFixedPoint theirs = Number(node.Get(key)?.GetString(CGoodsPool.KeyOf(goods)));
					CFixedPoint mine = ours(province)[goods];
					string what = $"{CGoodsPool.KeyOf(goods)} {name}";
					if (theirs == CFixedPoint.Zero && mine == CFixedPoint.Zero)
					{
						continue;
					}
					(int compared, int agree, int near) = totals.GetValueOrDefault(what);
					bool same = theirs == mine;
					long larger = Math.Max(Math.Abs(theirs.Raw), Math.Abs(mine.Raw));
					bool close = Math.Abs(theirs.Raw - mine.Raw) * 50 <= larger;      // within a fiftieth
					totals[what] = (compared + 1, agree + (same ? 1 : 0), near + (close ? 1 : 0));
					if (!same)
					{
						whole = false;
						if (!shown.TryGetValue(what, out List<string>? list))
						{
							shown[what] = list = new List<string>();
						}
						list.Add($"{province.Id} {province.Name} ({province.Controller}, on {network.DepotOf(province.Id)} "
							+ $"{network.DistanceOf(province.Id)} away): save {theirs}, ours {mine}");
					}
				}
			}
			int depot = network.DepotOf(province.Id);
			(int provinces, int agreeing) = perNetwork.GetValueOrDefault(depot);
			perNetwork[depot] = (provinces + 1, agreeing + (whole ? 1 : 0));
		}

		// One country's day, for looking into: "country=<tag>" after the save.
		foreach (string argument in args.Skip(3))
		{
			if (!argument.StartsWith("country=", StringComparison.Ordinal)
				|| !CCountryTag.TryParse(argument["country=".Length..], out CCountryTag asked)
				|| state.Country(asked) is not { } country)
			{
				continue;
			}
			ClausewitzNode? node = savedCountries.FirstOrDefault(saved => saved.Country == country).Node;
			Console.WriteLine($"{country.Tag}: IC {country.TotalIc} (save {node?.GetString("max_ic")}), own ground at home "
				+ $"{new CFixedPoint(country.CoreHomeIc)}, shares {string.Join(' ', country.ProductionShares.Select(share => share.ToThousandths()))}"
				+ $" (save {string.Join(' ', (node?.Get("distribution")?.Children ?? new List<ClausewitzNode>()).Select(child => child.Value))})");
			CSubUnit? reserve = country.AllUnits().SelectMany(unit => unit.SubUnits).FirstOrDefault(subUnit => subUnit.IsReserve);
			Console.WriteLine($"   laws {string.Join(' ', country.Laws.Select(law => law.Value.Name))}; a reserve brigade is at "
				+ $"{reserve?.Strength} of {reserve?.Highest}; what its units use in a day {CUnitSupply.DailyUse(state, country)}");
			foreach ((string name, CGoodsPool ours, string key) in new[]
			{
				("made", country.HomeProduced, "home"), ("made away from the capital", country.SentTo, "to"),
				("come back", country.SentBack, "back"), ("used, the capital's takes among it", country.Usage, "usage"),
			})
			{
				Console.WriteLine($"   {name,-30} supplies {ours[EGoods.Supplies]} / {Number(node?.Get(key)?.GetString("supplies"))}"
					+ $"   fuel {ours[EGoods.Fuel]} / {Number(node?.Get(key)?.GetString("fuel"))}");
			}
			Console.WriteLine();
		}

		// One network in full, for looking into: "network=<depot's number>" after the save.
		foreach (string argument in args.Skip(3))
		{
			if (!argument.StartsWith("network=", StringComparison.Ordinal)
				|| !int.TryParse(argument["network=".Length..], out int asked))
			{
				continue;
			}
			var needSupplies = new long[state.Provinces.Count];
			var needFuel = new long[state.Provinces.Count];
			CSupplyNeed.Count(state, beforeGame: false, needSupplies, needFuel);
			Console.WriteLine($"network {asked} {state.Provinces[asked].Name}: ours / the save's, supplies then fuel");
			foreach (CProvince province in state.Provinces.Where(p => network.DepotOf(p.Id) == asked)
				.OrderBy(p => network.DistanceOf(p.Id)).ThenBy(p => p.Id))
			{
				savedProvinces.TryGetValue(province.Id, out ClausewitzNode? node);
				var parts = new List<string>();
				foreach (EGoods goods in Carried)
				{
					string key = CGoodsPool.KeyOf(goods);
					parts.Add($"need {new CFixedPoint(goods == EGoods.Supplies ? needSupplies[province.Id] : needFuel[province.Id])}"
						+ $" pool {province.Pool[goods]}/{Number(node?.Get("pool")?.GetString(key))}"
						+ $" asked {province.SupplyDrawn[goods]}/{Number(node?.Get("drawn")?.GetString(key))}"
						+ $" passed {province.SupplyThroughput[goods]}/{Number(node?.Get("throughput")?.GetString(key))}");
				}
				Console.WriteLine($"   {province.Id,5} {province.Name,-18} {network.DistanceOf(province.Id),3} away, "
					+ $"cap {new CFixedPoint(CSupplyFigures.Capacity(state, province).Raw)}: {string.Join("  |  ", parts)}");
			}
			Console.WriteLine();
		}

		Console.WriteLine("of the provinces where the figure is something in the save or here:");
		foreach ((string what, (int compared, int agree, int near)) in totals.OrderBy(total => total.Key))
		{
			Console.WriteLine($"{what,-36} {agree,5} of {compared,5} the same to the thousandth, {near,5} within a fiftieth");
			if (shown.TryGetValue(what, out List<string>? list))
			{
				foreach (string line in list.Take(examples))
				{
					Console.WriteLine("      " + line);
				}
			}
		}

		// Further days, for looking into: "days=<how many>" with "country=<tag>" after the save.
		foreach (string argument in args.Skip(3))
		{
			if (!argument.StartsWith("days=", StringComparison.Ordinal) || !int.TryParse(argument["days=".Length..], out int days))
			{
				continue;
			}
			foreach (string other in args.Skip(3))
			{
				if (!other.StartsWith("country=", StringComparison.Ordinal)
					|| !CCountryTag.TryParse(other["country=".Length..], out CCountryTag asked)
					|| state.Country(asked) is not { } country || state.Stockpile(country) is not { } stockpile)
				{
					continue;
				}
				for (int day = 0; day < days; day++)
				{
					CTick.RunDailyPass(state);
					CFixedPoint inProvinces = CFixedPoint.Zero;
					foreach (CProvince province in state.Provinces)
					{
						if (province.Controller == country.Tag && province.Id != country.Capital)
						{
							inProvinces += province.Pool[EGoods.Supplies];
						}
					}
					Console.WriteLine($"{country.Tag} day {day + 3}: stockpile {stockpile[EGoods.Supplies]}, in its provinces {inProvinces}, "
						+ $"made {country.HomeProduced[EGoods.Supplies]} (away {country.SentTo[EGoods.Supplies]}), "
						+ $"back {country.SentBack[EGoods.Supplies]}, used {country.Usage[EGoods.Supplies]}, "
						+ $"asked by units {country.UnitDemand[EGoods.Supplies]}");
				}
			}
		}

		Console.WriteLine();
		int wholeNetworks =perNetwork.Count(entry => entry.Key != 0 && entry.Value.Provinces == entry.Value.Agree);
		Console.WriteLine($"networks where every province agrees in every figure: {wholeNetworks} of {perNetwork.Count(entry => entry.Key != 0)}");
		foreach ((int depot, (int provinces, int agreeing)) in perNetwork.OrderByDescending(entry => entry.Value.Provinces).Take(examples * 3))
		{
			string name = depot == 0 ? "(no depot)" : $"{depot} {state.Provinces[depot].Name} ({state.Provinces[depot].Controller})";
			Console.WriteLine($"   {name,-40} {agreeing,5} of {provinces,5}");
		}
		return 0;
	}

	/// <summary>Says how much of what differs is the original's AI having units on the move.</summary>
	private static void Moving(CGameState state, CSupplyNetwork network, Dictionary<int, ClausewitzNode> saved)
	{
		// The provinces a fleet's or an air unit's fuel goes out along, when the original's
		// AI has one on the move: the save has fuel asked for where nothing here burns any.
		int burning = 0;
		foreach (CProvince province in state.Provinces)
		{
			if (network.DepotOf(province.Id) != 0 && saved.TryGetValue(province.Id, out ClausewitzNode? node)
				&& Number(node.Get("drawn")?.GetString("fuel")) > CFixedPoint.Zero
				&& province.SupplyDrawn[EGoods.Fuel] == CFixedPoint.Zero)
			{
				burning++;
			}
		}
		Console.WriteLine($"{burning} provinces were asked for fuel in the save and for none here: nothing here is on the move");
		Console.WriteLine();
	}

	private static CFixedPoint Number(string? text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}
}
