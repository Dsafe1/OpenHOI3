using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Replay;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Units;

/// <summary>
/// Stage 2's run: a new game with the install's Lua and nobody playing, run hour by hour from the start to a year's
/// first day, with nothing given to it by hand:
///
///   OpenHOI3.SaveCheck headless &lt;install folder&gt; [until year] [savegame ...]
///
/// 1948 with none given. Twice a year it writes each great power's figures and the world's; every day it looks for a
/// stockpile or a manpower below nought; at the end it tells how long each year took, what fired, what the Lua failed
/// and what of the script met something not built. Given savegames of the same start, it sets every country's
/// stockpile, manpower, officers, influence and technology beside each save as the run reaches its hour. A run that
/// throws stops there and says where. See <c>docs/mechanics/headless.md</c>.
/// </summary>
internal static class CHeadlessTrial
{
	private static readonly string[] Majors = { "GER", "ENG", "FRA", "ITA", "SOV", "USA", "JAP", "CHI" };

	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck headless <install folder> [until year] [savegame ...] [inspect=TAG] [hash] [record=FILE | replay=FILE]");
			return 2;
		}
		CDate end = args.Length > 2 && int.TryParse(args[2], out int year) ? new CDate(year, 1, 1)
			: args.Length > 2 && CDate.TryParse(args[2], out CDate given) ? given : new CDate(1948, 1, 1);
		string lastInspected = "";
		var armySeen = new Dictionary<int, string>();
		// Each midnight, the state's hash part by part (docs/replay.md): two runs of one game should agree on every line.
		bool hash = args.Contains("hash");
		var armyMarks = new Dictionary<int, (bool Retreating, int Destination)>();
		string? inspect = args.FirstOrDefault(a => a.StartsWith("inspect=", StringComparison.Ordinal))?["inspect=".Length..];
		var saves = new List<(long Hour, string Path)>();
		foreach (string path in args.Skip(2).Where(a => a.EndsWith(".hoi3", StringComparison.OrdinalIgnoreCase)))
		{
			ClausewitzNode head = ClausewitzParser.ParseFile(path);
			if (TryHour(head.GetString("date"), out long hour))
			{
				saves.Add((hour, path));
			}
		}
		saves.Sort();

		var loading = Stopwatch.StartNew();
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		var luaReports = new List<string>();
		using var scripts = new CAiScripts(directories, luaReports.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts);
		state.Played.Clear();
		state.ScriptReport = new CScriptReport();
		if (inspect is not null)
		{
			state.UnitRemovedTrace = unit => TellRemoved(state, unit, inspect);
			// Of the AI's own trace, only the moves it posts for a unit in a battle, and which step posted each.
			state.AiTrace = (new CCountryTag(inspect), line =>
			{
				if (line.Contains("    move ", StringComparison.Ordinal) && line.Contains("(fighting)", StringComparison.Ordinal))
				{
					Console.WriteLine($"  {state.Date} {state.Hour}h ai{line}");
				}
			});
		}
		var simulation = new CSimulation(state);
		Console.WriteLine($"a new game, {state.Date}, nobody playing, loaded in {loading.ElapsedMilliseconds} ms; " +
			$"the Lua with {scripts.Failures} errors, {warnings.Count} warnings while loading");
		// The AI recorded to a file as the game runs, or a recording played back in its place, from the first hour
		// (docs/replay.md). A recording holds only while the rules are the same; a run that adds only what it tells may
		// replay one.
		string? recordTo = args.FirstOrDefault(a => a.StartsWith("record=", StringComparison.Ordinal))?["record=".Length..];
		string? replayFrom = args.FirstOrDefault(a => a.StartsWith("replay=", StringComparison.Ordinal))?["replay=".Length..];
		using CReplayFile? replayFile = recordTo is not null
			? CReplayFile.Create(recordTo, new CReplayHeader(state.Played.ToList(), state.Seed))
			: replayFrom is not null ? CReplayFile.Open(replayFrom, state) : null;
		if (replayFile is not null)
		{
			state.Replay = recordTo is not null ? CReplay.Recording(replayFile.Write) : CReplay.Replaying(replayFile);
			Console.WriteLine(recordTo is not null ? $"recording the AI to {recordTo}" : $"replaying the AI from {replayFrom}");
		}

		// What happens, counted by year.
		var fired = new SortedDictionary<int, Dictionary<string, int>>();
		var taken = new SortedDictionary<int, Dictionary<string, int>>();
		var messages = new SortedDictionary<int, Dictionary<string, int>>();
		state.EventFired += (fired_, scope) => Count(fired, state.Date.Year, fired_.Id.ToString(CultureInfo.InvariantCulture));
		state.DecisionTaken += (decision, scope) => Count(taken, state.Date.Year, decision.Name);
		state.PactMessaged += pact => Count(messages, state.Date.Year, pact.We);

		var below = new SortedDictionary<string, (CDate First, int Days)>(StringComparer.Ordinal);
		var yearTimes = new List<(int Year, long Ms)>();
		var factions = state.Countries.ToDictionary(country => country.Tag.Value, country => country.Faction);
		var wars = state.Wars.ToDictionary(war => war, war => $"{string.Join(" ", war.Attackers)} against {string.Join(" ", war.Defenders)}");
		var living = new HashSet<string>(state.Countries.Where(c => CResources.HoldsCapital(state, c)).Select(c => c.Tag.Value));
		var happenings = new List<string>();
		// What ends a country or a war, as it is told: who and to whom.
		state.PactMessaged += pact =>
		{
			if (pact.We is "ANNEX" or "THEYSURRENDERUS" or "WESURRENDER" or "WEDECLWAR" or "PEACE" or "PUPPET" or "EXILE")
			{
				happenings.Add($"{state.Date} {state.Hour}h: {pact.We} {pact.Actor} -> {pact.Recipient}");
			}
		};
		// Who took which decision, and when - the AI's foreign minister takes them all, nobody playing.
		state.DecisionTaken += (decision, scope) => happenings.Add($"{state.Date} {state.Hour}h: {scope.Country} takes {decision.Name}");
		int luaFailuresBefore = scripts.Failures;

		Snapshot(state);
		var clock = Stopwatch.StartNew();
		var yearClock = Stopwatch.StartNew();

		int saveIndex = 0;
		try
		{
			while (state.Date < end)
			{
				CDate before = state.Date;
				simulation.RunHours(1);
				while (saveIndex < saves.Count && state.TotalHours >= saves[saveIndex].Hour)
				{
					CompareWithSave(state, saves[saveIndex].Path);
					saveIndex++;
				}
				if (inspect is not null && state.Country(new CCountryTag(inspect)) is { } looked)
				{
					lastInspected = Inspect(state, looked, lastInspected);
					InspectArmy(state, looked, armySeen, armyMarks);
				}
				if (state.Date == before)
				{
					continue;
				}
				if (hash)
				{
					ulong[] parts = OpenHOI3.Sim.Replay.CStateHash.Of(state);
					Console.WriteLine($"  {state.Date} hash {OpenHOI3.Sim.Replay.CStateHash.Whole(parts):x16} " +
						string.Join(" ", parts.Select((part, i) => $"{OpenHOI3.Sim.Replay.CStateHash.Parts[i]} {part:x16}")));
				}
				Below(state, below);
				Happenings(state, factions, wars, living, happenings);
				if (state.Date.Day == 1 && state.Date.Month is 1 or 7)
				{
					Snapshot(state);
				}
				if (state.Date.Year != before.Year)
				{
					yearTimes.Add((before.Year, yearClock.ElapsedMilliseconds));
					Console.WriteLine($"   {before.Year} run in {yearClock.ElapsedMilliseconds / 1000.0:0.0} s; the Lua's errors so far " +
						$"{scripts.Failures - luaFailuresBefore}");
					yearClock.Restart();
				}
			}
			// And where the run ends, unless that was a snapshot's day.
			if (!(state.Date.Day == 1 && state.Date.Month is 1 or 7))
			{
				Snapshot(state);
			}
		}
		catch (Exception exception)
		{
			Console.WriteLine();
			Console.WriteLine($"THE RUN THREW on {state.Date} hour {state.Hour}:");
			Console.WriteLine(exception);
			Report(state, scripts, luaReports, fired, taken, messages, below, yearTimes, happenings, clock);
			return 1;
		}
		Report(state, scripts, luaReports, fired, taken, messages, below, yearTimes, happenings, clock);
		return 0;
	}

	private static void Count(SortedDictionary<int, Dictionary<string, int>> byYear, int year, string key)
	{
		if (!byYear.TryGetValue(year, out Dictionary<string, int>? counts))
		{
			byYear[year] = counts = new Dictionary<string, int>(StringComparer.Ordinal);
		}
		counts[key] = counts.GetValueOrDefault(key) + 1;
	}

	/// <summary>Every country's goods and manpower below nought today, by what, with the first day and how many days.</summary>
	private static void Below(CGameState state, SortedDictionary<string, (CDate First, int Days)> below)
	{
		foreach (CCountry country in state.Countries)
		{
			void Note(string what)
			{
				string key = country.Tag.Value + " " + what;
				(CDate first, int days) = below.GetValueOrDefault(key, (state.Date, 0));
				below[key] = (first, days + 1);
			}
			if (country.Manpower < CFixedPoint.Zero)
			{
				Note("manpower");
			}
			if (state.Stockpile(country) is { } stockpile)
			{
				foreach (EGoods goods in Enum.GetValues<EGoods>())
				{
					if (stockpile[goods] < CFixedPoint.Zero)
					{
						Note(goods.ToString());
					}
				}
			}
		}
	}

	/// <summary>Wars begun and ended and countries changing faction, as they happen.</summary>
	private static void Happenings(CGameState state, Dictionary<string, string> factions, Dictionary<OpenHOI3.Sim.Wars.CWar, string> wars,
		HashSet<string> living, List<string> happenings)
	{
		foreach (var war in state.Wars)
		{
			string sides = $"{string.Join(" ", war.Attackers)} against {string.Join(" ", war.Defenders)}";
			if (!wars.TryGetValue(war, out string? was))
			{
				happenings.Add($"{state.Date}: war '{war.Name}' - {sides}");
			}
			else if (was != sides)
			{
				happenings.Add($"{state.Date}: war '{war.Name}' now {sides}");
			}
			wars[war] = sides;
		}
		foreach (var war in wars.Keys.Where(war => !state.Wars.Contains(war)).ToList())
		{
			happenings.Add($"{state.Date}: war '{war.Name}' over ({wars[war]})");
			wars.Remove(war);
		}
		foreach (CCountry country in state.Countries)
		{
			string was = factions.GetValueOrDefault(country.Tag.Value, "");
			if (was != country.Faction)
			{
				happenings.Add($"{state.Date}: {country.Tag} from '{was}' to '{country.Faction}'");
				factions[country.Tag.Value] = country.Faction;
			}
			bool holds = CResources.HoldsCapital(state, country);
			if (holds && living.Add(country.Tag.Value))
			{
				happenings.Add($"{state.Date}: {country.Tag} appears, capital {country.Capital}");
			}
			else if (!holds && living.Remove(country.Tag.Value))
			{
				happenings.Add($"{state.Date}: {country.Tag} no longer holds its capital");
			}
		}
	}

	/// <summary>A country's hold on its land each day, for <c>inspect=TAG</c>: what its surrender is reckoned from.</summary>
	private static string Inspect(CGameState state, CCountry country, string last)
	{
		int owned = state.Provinces.Count(province => province.Owner == country.Tag);
		int controlled = state.Provinces.Count(province => province.Controller == country.Tag);
		int cores = state.Provinces.Count(province => province.Owner == country.Tag && province.Cores.Contains(country.Tag));
		int units = country.AllUnits().Count();
		string line = $"{country.Tag}: owns {owned}, controls {controlled}, owns {cores} cores, {units} units, " +
			$"core points owned {country.OwnedCoreVictoryPoints} held {country.HeldCoreVictoryPoints}, " +
			$"progress {OpenHOI3.Sim.Wars.CSurrender.Progress(state, country)}, capital {country.Capital} " +
			$"({(country.Capital > 0 ? state.Province(country.Capital).Controller?.ToString() : "-")} holds it), at war {state.IsAtWar(country.Tag)}";
		if (line != last)
		{
			Console.WriteLine($"  {state.Date} {state.Hour}h {line}");
			// Its own land that another holds, and who stands there.
			foreach (var province in state.Provinces.Where(p => p.Owner == country.Tag && p.Controller != country.Tag).Take(10))
			{
				Console.WriteLine($"      {province.Id} {province.Name} ({province.Points} points) held by {province.Controller}; " +
					$"units {string.Join(", ", state.UnitsIn(province.Id).Select(unit => unit.Owner + " " + unit.Name))}");
			}
		}
		return line;
	}

	/// <summary>
	/// A country's land army for <c>inspect=TAG</c>: on the first of each month its units, brigades, strength and
	/// organisation, with its manpower and its reinforcement share; and each land unit that leaves the game, told as it
	/// stood the hour before.
	/// </summary>
	private static void InspectArmy(CGameState state, CCountry country, Dictionary<int, string> seen,
		Dictionary<int, (bool Retreating, int Destination)> marks)
	{
		List<CUnit> land = country.AllUnits().Where(unit => unit.Kind is not (EUnitKind.Navy or EUnitKind.Air)).ToList();
		if (state.Date.Day == 1 && state.Hour == 0)
		{
			List<CSubUnit> brigades = land.SelectMany(unit => unit.SubUnits).ToList();
			long strength = brigades.Sum(brigade => brigade.Strength.Raw);
			long organisation = brigades.Count == 0 ? 0 : brigades.Sum(brigade => brigade.Organisation.Raw) / brigades.Count;
			Console.WriteLine($"  {state.Date} army {country.Tag}: land units {land.Count} (divisions " +
				$"{land.Count(unit => unit.Kind == EUnitKind.Division)}), brigades {brigades.Count}, strength {strength / 1000}, " +
				$"organisation {organisation / 1000.0:0.0} average, manpower {Whole(country.Manpower)}, " +
				$"IC shares {string.Join(" ", country.ProductionShares.Select(share => share.ToThousandths().ToString()))}, " +
				$"in the queue {country.Constructions.Count}");
		}
		// On the 1st, 11th and 21st, what its land units with brigades are doing - the figures a save can be set beside - and
		// those of every country at war with it standing on land it owns.
		if (state.Date.Day is 1 or 11 or 21 && state.Hour == 0)
		{
			string Doing(IEnumerable<CUnit> units)
			{
				List<CUnit> withBrigades = units.Where(unit => unit.SubUnits.Count > 0).ToList();
				List<CUnit> delayed = withBrigades.Where(unit => unit.AttackDelay > CFixedPoint.Zero).ToList();
				double mean = delayed.Count == 0 ? 0 : delayed.Average(unit => unit.AttackDelay.Raw / 1000.0);
				return $"{withBrigades.Count} with brigades ({withBrigades.Sum(unit => unit.SubUnits.Count)}): delayed {delayed.Count} " +
					$"(mean {mean:0.0} h), falling back {withBrigades.Count(unit => unit.IsRetreating)}, moving " +
					$"{withBrigades.Count(unit => unit.IsMoving && !unit.IsRetreating)}, fighting {withBrigades.Count(unit => unit.IsFighting)}";
			}
			Console.WriteLine($"  {state.Date} doing {country.Tag}: {Doing(land)}");
			var enemies = state.Countries.Where(other => OpenHOI3.Sim.Movement.CMovementRules.IsEnemy(state, other.Tag, country.Tag));
			foreach (CCountry enemy in enemies)
			{
				List<CUnit> inside = enemy.AllUnits().Where(unit => unit.Kind is not (EUnitKind.Navy or EUnitKind.Air)
					&& unit.Location > 0 && state.Province(unit.Location).Owner == country.Tag).ToList();
				if (inside.Count > 0)
				{
					Console.WriteLine($"  {state.Date} doing {enemy.Tag} on {country.Tag}'s land: {Doing(inside)}");
				}
			}
		}
		var now = new HashSet<int>();
		foreach (CUnit unit in land)
		{
			now.Add(unit.Id);
			if (unit.SubUnits.Count == 0)
			{
				continue;
			}
			var here = state.Province(unit.Location);
			string next = unit.Path.Count > 0 ? $"{unit.Path[0]} ({state.Province(unit.Path[0]).Controller})" : "-";
			// A unit marked falling back, or given a new way while so marked: its whole path and who holds each step.
			(bool Retreating, int Destination) mark = (unit.IsRetreating, unit.Path.Count > 0 ? unit.Path[^1] : 0);
			if (marks.TryGetValue(unit.Id, out var before) && mark != before && unit.IsRetreating)
			{
				Console.WriteLine($"  {state.Date} {state.Hour}h falling back: {unit.Kind} '{unit.Name}' in {unit.Location} ({here.Controller}), " +
					$"order {unit.Order}, was {(before.Retreating ? "falling back" : "not")} to {before.Destination}, path " +
					string.Join(" ", unit.Path.Select(p => $"{p}({state.Province(p).Controller})")));
			}
			marks[unit.Id] = mark;
			seen[unit.Id] = $"{unit.Kind} '{unit.Name}' in {unit.Location} {here.Name} ({here.Controller}), " +
				$"{unit.SubUnits.Count} brigades, strength {CUnitTotals.Strength(unit)}, organisation {CUnitTotals.AverageOrganisation(unit)}, " +
				$"retreating {unit.IsRetreating}, battles {unit.Combats.Count}, next {next}, supplies {unit.SuppliesReceived}, " +
				$"enemy here {state.UnitsIn(unit.Location).Count(other => OpenHOI3.Sim.Movement.CMovementRules.IsEnemy(state, other.Owner, unit.Owner))}";
		}
		foreach (int id in seen.Keys.Where(id => !now.Contains(id)).ToList())
		{
			Console.WriteLine($"  {state.Date} {state.Hour}h lost: {seen[id]}");
			seen.Remove(id);
		}
	}

	/// <summary>
	/// A land unit of the inspected country taken out of the game, as it is: where it stands and where it was going, who
	/// holds each and who stands there, and the calls that took it out.
	/// </summary>
	private static void TellRemoved(CGameState state, CUnit unit, string tag)
	{
		if (unit.Owner.Value != tag || unit.Kind is EUnitKind.Navy or EUnitKind.Air || unit.SubUnits.Count == 0)
		{
			return;
		}
		string Enemies(int province) => string.Join(" ", state.UnitsIn(province)
			.Where(other => OpenHOI3.Sim.Movement.CMovementRules.IsEnemy(state, other.Owner, unit.Owner))
			.Select(other => $"{other.Owner}{(other.IsRetreating ? "(falling back)" : "")}"));
		var here = state.Province(unit.Location);
		string next = unit.Path.Count > 0
			? $"{unit.Path[0]} held by {state.Province(unit.Path[0]).Controller}, enemies [{Enemies(unit.Path[0])}], " +
				$"path {unit.Path.Count} long, progress {unit.MovementProgress}"
			: "no path";
		IEnumerable<string> calls = new System.Diagnostics.StackTrace(2, false).GetFrames()
			.Select(frame => frame.GetMethod())
			.Where(method => method is not null)
			.Take(5)
			.Select(method => $"{method!.DeclaringType?.Name}.{method.Name}");
		Console.WriteLine($"  {state.Date} {state.Hour}h removed: {unit.Kind} '{unit.Name}' in {unit.Location} {here.Name} held by " +
			$"{here.Controller}, enemies [{Enemies(unit.Location)}]; next {next}; falling back {unit.IsRetreating}, battles " +
			$"{unit.Combats.Count}, strength {CUnitTotals.Strength(unit)}, organisation {CUnitTotals.AverageOrganisation(unit)}, " +
			$"supplies {unit.SuppliesReceived}; by {string.Join(" < ", calls)}");
	}

	/// <summary>The great powers' figures and the world's.</summary>
	private static void Snapshot(CGameState state)
	{
		Console.WriteLine();
		Console.WriteLine($"== {state.Date} ==");
		Console.WriteLine("        IC  maxIC     manpower  leadership  officers    money  supplies     fuel    metal   energy     rare    crude" +
			"  units brigades queue tech  unity dissent neutr  party        faction");
		foreach (string tag in Majors)
		{
			if (state.Country(new CCountryTag(tag)) is not { } country)
			{
				continue;
			}
			CGoodsPool? pool = state.Stockpile(country);
			string Goods(EGoods goods) => pool is null ? "-" : Whole(pool[goods]);
			List<CUnit> units = country.AllUnits().ToList();
			int technology = country.TechnologyLevels.Values.Sum();
			Console.WriteLine($"  {tag} {country.TotalIc,5} {country.MaxIc,6} {Whole(country.Manpower),12} {Tenth(country.TotalLeadership),11} " +
				$"{Whole(country.Officers),9} {Goods(EGoods.Money),8} {Goods(EGoods.Supplies),9} {Goods(EGoods.Fuel),8} " +
				$"{Goods(EGoods.Metal),8} {Goods(EGoods.Energy),8} {Goods(EGoods.RareMaterials),8} {Goods(EGoods.CrudeOil),8} " +
				$"{units.Count,6} {units.Sum(unit => unit.SubUnits.Count),8} {country.Constructions.Count,5} {technology,4} " +
				$"{Tenth(country.NationalUnity),6} {Tenth(country.Dissent),7} {Tenth(country.Neutrality),6}  " +
				$"{country.RulingIdeology,-12} {country.Faction}");
		}
		// What the ministers have set: the research queue and the shares of leadership and of IC (ministers.md).
		foreach (string tag in Majors)
		{
			if (state.Country(new CCountryTag(tag)) is not { } country)
			{
				continue;
			}
			string Shares(CFixed15[] shares) => string.Join(" ", shares.Select(share => share.ToThousandths().ToString()));
			Console.WriteLine($"  {tag} research {country.ResearchQueue.Count} queued; leadership {Shares(country.LeadershipShares)}; " +
				$"IC {Shares(country.ProductionShares)}; waiting to be placed {country.Deployments.Count}");
			long progress = country.ResearchProgress.Values.Sum(value => value.ToThousandths().Raw);
			Console.WriteLine($"    research progress summed {progress / 1000.0:0.00}, levels summed {country.TechnologyLevels.Values.Sum()}");
			// The wars its foreign minister has prepared, and whether each is ready as far as that is built (foreignminister.md).
			if (country.WarTargets.Count > 0)
			{
				Console.WriteLine("    wars prepared: " + string.Join(", ", country.WarTargets.Select(t =>
					$"{t.Target}{(t.Decision is { } d ? " for " + d.Name : "")} since {CDate.FromDayNumber((int)(t.Hour / CGameState.HoursPerDay))}" +
					(OpenHOI3.Sim.Ai.CWarReadiness.IsReady(state, country, t) ? " ready" : " not ready"))));
			}
		}
		var living = state.Countries.Where(country => CResources.HoldsCapital(state, country)).ToList();
		Console.WriteLine($"  the world: {living.Count} countries holding their capital, IC {living.Sum(c => c.TotalIc)}, " +
			$"{living.Sum(c => c.AllUnits().Count())} units, {state.Wars.Count} wars, {state.TradeRoutes.Count} trade routes, " +
			$"research queued in {living.Count(c => c.ResearchQueue.Count > 0)}, building in {living.Count(c => c.Constructions.Count > 0)}");
	}

	/// <summary>Every country's figures beside a save of the same game at the hour the run has reached.</summary>
	private static void CompareWithSave(CGameState state, string path)
	{
		ClausewitzNode save = ClausewitzParser.ParseFile(path);
		Console.WriteLine();
		Console.WriteLine($"== beside {System.IO.Path.GetFileName(path)}, {save.GetString("date")}, player {save.GetString("player")} ==");
		var ratios = new SortedDictionary<string, List<(double Ratio, string Who, CFixedPoint Saved, CFixedPoint Ours)>>(StringComparer.Ordinal);
		void Add(string what, string who, CFixedPoint saved, CFixedPoint ours)
		{
			if (saved == CFixedPoint.Zero && ours == CFixedPoint.Zero)
			{
				return;
			}
			if (!ratios.TryGetValue(what, out var list))
			{
				ratios[what] = list = new();
			}
			double ratio = saved == CFixedPoint.Zero ? double.PositiveInfinity : (double)ours.Raw / saved.Raw;
			list.Add((ratio, who, saved, ours));
		}
		foreach (ClausewitzNode node in save.Children)
		{
			if (node.Key is null || !node.IsBlock || !CCountryTag.TryParse(node.Key, out CCountryTag tag)
				|| state.Country(tag) is not { } country || !CResources.HoldsCapital(state, country))
			{
				continue;
			}
			string who = tag.Value;
			bool shared = state.Countries.Any(other => other != country && other.Capital == country.Capital
				&& CResources.HoldsCapital(state, other));
			ClausewitzNode? pool = save.Get(country.Capital.ToString(CultureInfo.InvariantCulture))?.Get("pool");
			if (!shared && pool is not null && state.Stockpile(country) is { } stockpile)
			{
				foreach (EGoods goods in Enum.GetValues<EGoods>())
				{
					Add(CGoodsPool.KeyOf(goods), who, Number(pool.GetString(CGoodsPool.KeyOf(goods))), stockpile[goods]);
				}
			}
			Add("manpower", who, Number(node.GetString("manpower")), country.Manpower);
			Add("officers", who, Number(node.GetString("officers")), country.Officers);
			Add("diplomatic influence", who, Number(node.GetString("diplo_influence")), country.DiplomaticInfluence);
			long technology = 0;
			foreach (ClausewitzNode entry in node.Get("technology")?.Children ?? new List<ClausewitzNode>())
			{
				if (entry.Key is not null && entry.IsBlock && entry.Children.Count > 0 && state.Technologies.ContainsKey(entry.Key))
				{
					technology += Number(entry.Children[0].Value).ToInt();
				}
			}
			Add("technology levels, summed", who, CFixedPoint.FromInt(technology),
				CFixedPoint.FromInt(country.TechnologyLevels.Values.Sum()));
		}
		Console.WriteLine("  ours over the save's, by country: median, the middle half, and the farthest");
		foreach (var (what, list) in ratios)
		{
			var finite = list.Where(entry => !double.IsInfinity(entry.Ratio)).OrderBy(entry => entry.Ratio).ToList();
			if (finite.Count == 0)
			{
				continue;
			}
			double Quantile(double q) => finite[(int)Math.Min(finite.Count - 1, Math.Floor(q * finite.Count))].Ratio;
			var farthest = list.OrderByDescending(entry => double.IsInfinity(entry.Ratio) ? double.MaxValue
				: Math.Abs(Math.Log(Math.Max(entry.Ratio, 1e-6)))).Take(3);
			Console.WriteLine($"  {what,-26} {list.Count,4} countries  median {Quantile(0.5):0.000}  middle half " +
				$"{Quantile(0.25):0.000}-{Quantile(0.75):0.000}  farthest: " +
				string.Join(", ", farthest.Select(entry => $"{entry.Who} {entry.Saved} -> {entry.Ours}")));
		}
		foreach (string tag in Majors)
		{
			var line = ratios.Where(pair => pair.Value.Any(entry => entry.Who == tag))
				.Select(pair => (pair.Key, pair.Value.First(entry => entry.Who == tag)))
				.Select(pair => $"{pair.Key} {pair.Item2.Saved}/{pair.Item2.Ours}");
			Console.WriteLine($"  {tag} save/ours: {string.Join("; ", line)}");
		}
	}

	private static void Report(CGameState state, CAiScripts scripts, List<string> luaReports,
		SortedDictionary<int, Dictionary<string, int>> fired, SortedDictionary<int, Dictionary<string, int>> taken,
		SortedDictionary<int, Dictionary<string, int>> messages, SortedDictionary<string, (CDate First, int Days)> below,
		List<(int Year, long Ms)> yearTimes, List<string> happenings, Stopwatch clock)
	{
		Console.WriteLine();
		Console.WriteLine($"== the run: to {state.Date} in {clock.ElapsedMilliseconds / 1000.0:0.0} s ==");
		if (yearTimes.Count > 0)
		{
			Console.WriteLine("  each year: " + string.Join(", ", yearTimes.Select(y => $"{y.Year} {y.Ms / 1000.0:0}s")));
		}
		Console.WriteLine($"  events fired by year: " + string.Join(", ", fired.Select(y => $"{y.Key} {y.Value.Values.Sum()} ({y.Value.Count} kinds)")));
		Console.WriteLine($"  the most often fired: " + string.Join(", ", fired.Values.SelectMany(d => d)
			.GroupBy(p => p.Key).Select(g => (g.Key, g.Sum(p => p.Value))).OrderByDescending(p => p.Item2).Take(12)
			.Select(p => $"{p.Key} x{p.Item2}")));
		Console.WriteLine($"  decisions taken by year: " + string.Join(", ", taken.Select(y => $"{y.Key} {y.Value.Values.Sum()}")));
		Console.WriteLine($"  the decisions: " + string.Join(", ", taken.Values.SelectMany(d => d)
			.GroupBy(p => p.Key).Select(g => (g.Key, g.Sum(p => p.Value))).OrderByDescending(p => p.Item2).Take(15)
			.Select(p => $"{p.Key} x{p.Item2}")));
		Console.WriteLine($"  pact messages by kind: " + string.Join(", ", messages.Values.SelectMany(d => d)
			.GroupBy(p => p.Key).Select(g => (g.Key, g.Sum(p => p.Value))).OrderByDescending(p => p.Item2).Take(15)
			.Select(p => $"{p.Key} x{p.Item2}")));
		Console.WriteLine($"  wars and factions, {happenings.Count} changes:");
		foreach (string line in happenings.Take(60))
		{
			Console.WriteLine("    " + line);
		}
		Console.WriteLine($"  below nought, {below.Count} country-goods:");
		foreach (var (what, (first, days)) in below.OrderByDescending(pair => pair.Value.Days).Take(30))
		{
			Console.WriteLine($"    {what}: from {first}, {days} days");
		}
		Console.WriteLine($"  the Lua: {scripts.Failures} errors in all; the first of what it reported:");
		foreach (string line in luaReports.GroupBy(line => line).OrderByDescending(group => group.Count()).Take(15).Select(group => group.Key))
		{
			Console.WriteLine($"    x{luaReports.Count(l => l == line)} {Shorten(line)}");
		}
		Console.WriteLine($"  the script's gaps met by events, {state.ScriptReport?.Gaps.Count ?? 0} keys:");
		foreach (var (key, (why, times)) in (state.ScriptReport?.Gaps ?? new()).OrderByDescending(pair => pair.Value.Times).Take(25))
		{
			Console.WriteLine($"    {key} ({why}) x{times}");
		}
	}

	private static string Shorten(string text)
	{
		string line = text.Replace('\n', ' ').Replace('\r', ' ');
		return line.Length > 160 ? line[..160] + "..." : line;
	}

	private static string Whole(CFixedPoint value)
	{
		return value.ToInt().ToString(CultureInfo.InvariantCulture);
	}

	private static string Tenth(CFixedPoint value)
	{
		return (value.Raw / 100 / 10.0).ToString("0.0", CultureInfo.InvariantCulture);
	}

	private static CFixedPoint Number(string? text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}

	private static bool TryHour(string text, out long hours)
	{
		hours = 0;
		string[] parts = text.Split('.');
		if (parts.Length < 3 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int year)
			|| !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int month)
			|| !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int day))
		{
			return false;
		}
		int hour = parts.Length > 3 && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) ? h : 0;
		hours = (long)new CDate(year, month, day).DayNumber * CGameState.HoursPerDay + hour;
		return true;
	}
}
