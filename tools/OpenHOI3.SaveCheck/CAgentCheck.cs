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
/// Makes a new game as a player gets it, runs it to the hour of a later savegame of the same start, and sets the order of
/// battle the AI's agents built and every theatre's plan beside the save's:
///
///   OpenHOI3.SaveCheck agents &lt;install folder&gt; &lt;start-date savegame&gt; &lt;later savegame&gt; [tag] [examples]
///
/// For each theatre of a country nobody plays, by its headquarters' name: the army groups, armies, corps, divisions, fleets
/// and air units under it, and its plan - active, the three stances, the two powers, the area of operations in order, the
/// objectives and the counts of what it wants; and each country's fleets and air units. Given a tag, that country's theatres
/// both ways. See <c>docs/mechanics/agents.md</c>, section 14.
/// </summary>
internal static class CAgentCheck
{
	private sealed class CSavedUnit
	{
		public string Kind = "";
		public string Name = "";
		public int Id;
		public int Location;
		public int Bound;
		public string Mission = "";
		public int MissionProvince;
		public string MissionEnd = "";
		public int MissionStance;
		public List<int> MissionProvinces { get; } = new();
		public CSavedUnit? Parent;
		public List<CSavedUnit> Children { get; } = new();
		public ClausewitzNode? Plan;
		public ClausewitzNode? Node;
	}

	private static readonly string[] UnitKeys = { "theatre", "armygroup", "army", "corps", "division", "navy", "air" };

	/// <summary>The AI's air missions by the save's keys.</summary>
	private static readonly Dictionary<EUnitOrder, string> AirMissions = new()
	{
		[EUnitOrder.GroundAttack] = "ground_attack",
		[EUnitOrder.AirIntercept] = "air_intercept",
		[EUnitOrder.LogisticalStrike] = "logistical_strike",
		[EUnitOrder.StrategicBombing] = "strategic_bomb",
		[EUnitOrder.NavalStrike] = "naval_strike",
		[EUnitOrder.PortStrike] = "port_strike",
		[EUnitOrder.AirSuperiority] = "air_superiority",
		[EUnitOrder.Interdiction] = "interdiction",
	};

	public static int Run(string[] args)
	{
		if (args.Length < 4)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck agents <install folder> <start-date savegame> <later savegame> " +
				"[tag] [examples] [player=TAG]");
			return 2;
		}
		string? player = args.Skip(4).FirstOrDefault(a => a.StartsWith("player=", StringComparison.Ordinal))?["player=".Length..];
		string? trace = args.Skip(4).FirstOrDefault(a => a.StartsWith("trace=", StringComparison.Ordinal))?["trace=".Length..];
		string? ready = args.Skip(4).FirstOrDefault(a => a.StartsWith("ready=", StringComparison.Ordinal))?["ready=".Length..];
		string? follow = args.Skip(4).FirstOrDefault(a => a.StartsWith("unit=", StringComparison.Ordinal))?["unit=".Length..];
		// tracedays=N: the trace only over the run's last N days, for a run too long to trace whole.
		int traceDays = args.Skip(4).Where(a => a.StartsWith("tracedays=", StringComparison.Ordinal))
			.Select(a => int.Parse(a["tracedays=".Length..], CultureInfo.InvariantCulture)).FirstOrDefault(int.MaxValue);
		args = args.Where(a => !a.StartsWith("player=", StringComparison.Ordinal) && !a.StartsWith("trace=", StringComparison.Ordinal)
			&& !a.StartsWith("ready=", StringComparison.Ordinal) && !a.StartsWith("unit=", StringComparison.Ordinal)
			&& !a.StartsWith("tracedays=", StringComparison.Ordinal)).ToArray();
		string? only = args.Length > 4 && args[4].Length == 3 && !int.TryParse(args[4], out _) ? args[4] : null;
		int examples = args.Skip(4).Select(a => int.TryParse(a, out int n) ? n : -1).FirstOrDefault(n => n >= 0, 5);
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		ClausewitzNode start = ClausewitzParser.ParseFile(args[2]);
		ClausewitzNode later = ClausewitzParser.ParseFile(args[3]);
		using var scripts = new OpenHOI3.Lua.CAiScripts(directories, warnings.Add);
		var clock = System.Diagnostics.Stopwatch.StartNew();
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts,
			played: new[] { new CCountryTag(player ?? later.GetString("player")) });
		state.IsRunning = true;
		if (trace is not null && traceDays == int.MaxValue)
		{
			state.AiTrace = (new CCountryTag(trace), Console.WriteLine);
		}
		var simulation = new CSimulation(state);
		int hours = HoursTo(state, later.GetString("date"));

		var saved = new Dictionary<string, List<CSavedUnit>>();
		foreach (ClausewitzNode block in later.Children)
		{
			if (block.IsBlock && block.Key is { Length: 3 } key && state.Country(new CCountryTag(key)) is not null)
			{
				var tops = new List<CSavedUnit>();
				Collect(block, null, tops);
				saved[key] = tops;
			}
		}
		// For ready=: the divisions to follow day by day over the run's last weeks - those the save has on the country's
		// fronts facing the targets, as the save's theatres list them, and whatever of ours stands on our fronts that day.
		CCountry? readyCountry = ready is not null ? state.Country(new CCountryTag(ready.Split(':')[0])) : null;
		var watched = new HashSet<string>(StringComparer.Ordinal);
		if (readyCountry is not null)
		{
			var saveFronts = later.Get(readyCountry.Tag.Value)?.Children.Where(c => c.Key == "area_theatre" && c.IsBlock)
				.SelectMany(t => t.Children.Where(f => f.Key == "front" && f.IsBlock))
				.Where(f => ready!.Split(':').ElementAtOrDefault(1)?.Split(',').Contains(f.GetString("local_enemy")) == true)
				.SelectMany(f => Numbers(f.Get("provinces"))).ToHashSet() ?? new HashSet<int>();
			foreach (CSavedUnit unit in saved.GetValueOrDefault(readyCountry.Tag.Value)?.SelectMany(All) ?? Enumerable.Empty<CSavedUnit>())
			{
				if (unit.Kind == "division" && saveFronts.Contains(unit.Location))
				{
					watched.Add(unit.Name);
				}
			}
		}

		// Each day: a land unit marked falling back with no way left, which nothing will ever clear.
		var stuck = new List<string>();
		string? lastEnemies = null;
		for (int left = hours; left > 0; left -= CGameState.HoursPerDay)
		{
			if (trace is not null && state.AiTrace is null && left <= (long)traceDays * CGameState.HoursPerDay)
			{
				state.AiTrace = (new CCountryTag(trace), Console.WriteLine);
			}
			if (follow is not null && left <= 8 * CGameState.HoursPerDay)
			{
				// unit=: the named land unit hour by hour over the run's last eight days.
				for (int hour = 0; hour < Math.Min(left, CGameState.HoursPerDay); hour++)
				{
					simulation.RunHours(1);
					FollowUnit(state, follow);
				}
			}
			else
			{
				simulation.RunHours(Math.Min(left, CGameState.HoursPerDay));
			}
			if (readyCountry is not null && left <= 45 * CGameState.HoursPerDay)
			{
				WatchFront(state, readyCountry, ready!, watched);
			}
			if (trace is not null && state.Country(new CCountryTag(trace)) is { } traced)
			{
				// The traced country's wars as they change, and where each of its fleets is each day.
				string enemies = string.Join(" ", state.Countries
					.Where(other => other.Tag.Value != OpenHOI3.Sim.Revolts.CRebelFactions.Rebels && OpenHOI3.Sim.Movement.CMovementRules.IsEnemy(state, traced.Tag, other.Tag))
					.Select(other => other.Tag.Value));
				enemies += string.Concat(state.Wars.Where(war => war.Involves(traced.Tag)).Select(war =>
					$" [{war.Name}: {string.Join(",", war.Attackers.Select(tag => tag.Value))} v {string.Join(",", war.Defenders.Select(tag => tag.Value))}]"));
				if (enemies != lastEnemies)
				{
					Console.WriteLine($"{state.Date} {traced.Tag} at war with: {(enemies.Length == 0 ? "nobody" : enemies)}");
					lastEnemies = enemies;
				}
				foreach ((EUnitKind kind, string label) in new[] { (EUnitKind.Navy, "fleets"), (EUnitKind.Air, "air") })
				{
					Console.WriteLine($"{state.Date} {label}: " + string.Join(" ", traced.AllUnits()
						.Where(unit => unit.Kind == kind && !unit.IsRemoved)
						.Select(unit => $"{unit.Id}x{unit.SubUnits.Count}@{unit.Location}/{unit.Base}{(unit.Path.Count > 0 ? ">" + unit.Path[^1] : "")}"
							+ (unit.Order == EUnitOrder.None ? "" : ":" + unit.Order)
							+ (kind == EUnitKind.Air && unit.SubUnits.Count > 0
								? $" org {unit.SubUnits.Min(wing => wing.Organisation)}/{unit.SubUnits.Min(wing => OpenHOI3.Sim.Agents.CFleetRegroup.OrganisationShare(state, wing))}"
								: ""))));
				}
			}
			foreach (CCountry country in state.Countries)
			{
				foreach (CUnit unit in country.AllUnits())
				{
					if (!unit.IsRemoved && unit.IsRetreating && !unit.IsMoving && unit.Kind is not (EUnitKind.Navy or EUnitKind.Air)
						&& unit.CarriedBy is null)
					{
						stuck.Add($"{state.Date} {country.Tag} {unit.Name} at {unit.Location}");
					}
				}
			}
		}
		Console.WriteLine($"a new game made and run {hours} hours to {state.Date} in {clock.ElapsedMilliseconds} ms; " +
			$"savegame {later.GetString("date")}, player {later.GetString("player")}");

		if (ready is not null)
		{
			Readiness(state, ready, saved);
		}

		var startPlaces = new Dictionary<string, Dictionary<int, int>>();
		foreach (ClausewitzNode block in start.Children)
		{
			if (block.IsBlock && block.Key is { Length: 3 } key && state.Country(new CCountryTag(key)) is not null)
			{
				var tops = new List<CSavedUnit>();
				Collect(block, null, tops);
				startPlaces[key] = tops.SelectMany(All).Where(u => u.Kind == "division")
					.GroupBy(u => u.Id).ToDictionary(g => g.Key, g => g.First().Location);
			}
		}

		var counts = new Dictionary<string, (int Same, int All)>();
		var misses = new Dictionary<string, List<string>>();
		void Count(string what, bool same, string miss)
		{
			(int s, int a) = counts.GetValueOrDefault(what);
			counts[what] = (s + (same ? 1 : 0), a + 1);
			if (!same)
			{
				if (!misses.TryGetValue(what, out List<string>? list))
				{
					misses[what] = list = new List<string>();
				}
				list.Add(miss);
			}
		}

		var totals = new Dictionary<string, (int Ours, int Save)>();
		foreach (CCountry country in state.Countries)
		{
			if (only is not null && country.Tag.Value != only)
			{
				continue;
			}
			List<CSavedUnit> theirs = saved.GetValueOrDefault(country.Tag.Value) ?? new List<CSavedUnit>();
			var theirAll = theirs.SelectMany(All).ToList();
			var ourAll = country.AllUnits().Where(u => !u.IsRemoved && u.CarriedBy is null).ToList();
			foreach (string kind in UnitKeys)
			{
				int o = ourAll.Count(u => Key(u) == kind);
				int s = theirAll.Count(u => u.Kind == kind);
				(int to, int ts) = totals.GetValueOrDefault(kind);
				totals[kind] = (to + o, ts + s);
				if (kind is "navy" or "air")
				{
					Count($"{kind} count", o == s, $"{country.Tag}: ours {o}, save {s}");
				}
			}
			if (only is not null)
			{
				Console.WriteLine($"{country.Tag}'s fleets and air units, ours:");
				foreach (CUnit unit in ourAll.Where(u => u.Kind is EUnitKind.Navy or EUnitKind.Air))
				{
					Console.WriteLine($"  {unit.Id,6} {Key(unit),-5} {unit.Name,-28} at {unit.Location,5} base {unit.Base,5} under " +
						$"{unit.Parent?.Name ?? "-"}, {unit.Order}{(unit.IsMoving ? " moving" : "")} fuel {unit.FuelReceived} supplies {unit.SuppliesReceived}: {string.Join(" ", unit.SubUnits.Select(s => s.Type.Key))}");
				}
				Console.WriteLine("and the save's:");
				foreach (CSavedUnit unit in theirAll.Where(u => u.Kind is "navy" or "air"))
				{
					Console.WriteLine($"  {unit.Id,6} {unit.Kind,-5} {unit.Name,-28} at {unit.Location,5} under {unit.Parent?.Name ?? "-"}");
				}
			}
			if (state.Played.Contains(country.Tag))
			{
				continue;
			}
			ComparePlaces(country, ourAll, theirAll, startPlaces.GetValueOrDefault(country.Tag.Value), Count);
			CompareAirMissions(country, ourAll, theirAll, Count);
			foreach (CTheatre theatre in country.Strategy.Theatres)
			{
				if (CTheatres.Headquarters(state, theatre) is not { } headquarters)
				{
					continue;
				}
				CSavedUnit? match = theirAll.FirstOrDefault(u => u.Kind == "theatre" && u.Name == headquarters.Name);
				string where = $"{country.Tag} {headquarters.Name}";
				if (match is null)
				{
					Count("theatre found", false, where);
					continue;
				}
				Count("theatre found", true, where);
				var below = headquarters.WithAllBelow().Where(u => !u.IsRemoved && u.CarriedBy is null).ToList();
				var theirBelow = All(match).ToList();
				foreach (string kind in new[] { "armygroup", "army", "corps", "division", "navy", "air" })
				{
					int o = below.Count(u => Key(u) == kind);
					int s = theirBelow.Count(u => u.Kind == kind);
					Count($"{kind}s under a theatre", o == s, $"{where}: ours {o}, save {s}");
				}
				ComparePlan(headquarters.Plan, match.Plan, where, Count);
			}
		}

		Console.WriteLine();
		Console.WriteLine("units of every country, ours and the save's:");
		foreach ((string kind, (int o, int s)) in totals)
		{
			Console.WriteLine($"  {kind,-10} {o,6} {s,6}");
		}
		Console.WriteLine();
		Console.WriteLine("land held by a country that does not own it, ours and the save's:");
		var heldSaved = new Dictionary<(string Holder, string Owner), int>();
		foreach (ClausewitzNode block in later.Children)
		{
			if (block.IsBlock && int.TryParse(block.Key, out _) && block.GetString("controller") is { Length: 3 } holder
				&& block.GetString("owner") is { Length: 3 } owner && holder != owner)
			{
				heldSaved[(holder, owner)] = heldSaved.GetValueOrDefault((holder, owner)) + 1;
			}
		}
		var heldOurs = new Dictionary<(string Holder, string Owner), int>();
		foreach (OpenHOI3.Sim.Provinces.CProvince province in state.Provinces)
		{
			if (province.Controller is { } holder && province.Owner is { } owner && holder != owner)
			{
				heldOurs[(holder.Value, owner.Value)] = heldOurs.GetValueOrDefault((holder.Value, owner.Value)) + 1;
			}
		}
		foreach ((string holder, string owner) in heldOurs.Keys.Union(heldSaved.Keys).OrderBy(k => k.Holder).ThenBy(k => k.Owner))
		{
			Console.WriteLine($"  {holder} holds of {owner}: ours {heldOurs.GetValueOrDefault((holder, owner))}, " +
				$"save {heldSaved.GetValueOrDefault((holder, owner))}");
		}
		Console.WriteLine();
		Console.WriteLine($"land units marked falling back with no way left, counted daily: {stuck.Count} unit-days, " +
			$"{stuck.Select(s => s[(s.IndexOf(' ') + 1)..]).Distinct().Count()} units");
		foreach (string entry in stuck.Take(examples))
		{
			Console.WriteLine($"  {entry}");
		}
		Console.WriteLine();
		Console.WriteLine("land held over a land unit of a country at war with its holder, nobody of the holder's side standing there:");
		foreach (OpenHOI3.Sim.Provinces.CProvince province in state.Provinces)
		{
			if (province.IsSea || province.Controller is not { } holder)
			{
				continue;
			}
			var units = state.Positions.Of(province.Id);
			if (units.Any(u => u.Kind is EUnitKind.Navy or EUnitKind.Air || u.IsRemoved) && units.All(u => u.Kind is EUnitKind.Navy or EUnitKind.Air))
			{
				continue;
			}
			var enemies = units.Where(u => u.Kind is not (EUnitKind.Navy or EUnitKind.Air) && !u.IsRemoved
				&& OpenHOI3.Sim.Movement.CMovementRules.IsEnemy(state, holder, u.Owner)).ToList();
			if (enemies.Count == 0 || units.Any(u => u.Kind is not (EUnitKind.Navy or EUnitKind.Air) && !u.IsRemoved
				&& !u.IsRetreating && !OpenHOI3.Sim.Movement.CMovementRules.IsEnemy(state, enemies[0].Owner, u.Owner)
				&& u.Owner != enemies[0].Owner))
			{
				continue;
			}
			foreach (CUnit unit in enemies)
			{
				Console.WriteLine($"  {province.Id} {province.Name}, held by {holder}: {unit.Owner} {unit.Name} - " +
					$"moving {unit.IsMoving}{(unit.IsMoving ? $" to {string.Join(" ", unit.Path)}" : "")}, retreating {unit.IsRetreating}, " +
					$"fighting {unit.IsFighting}, order {unit.Order}, attack delay {unit.AttackDelay.Raw}, " +
					$"aboard {unit.Transport is not null || unit.Plane is not null}, previous {unit.Previous}");
			}
		}
		Console.WriteLine();
		foreach ((string what, (int same, int all)) in counts)
		{
			Console.WriteLine($"{what,-28} {same} of {all}");
			foreach (string miss in misses.GetValueOrDefault(what)?.Take(examples) ?? Enumerable.Empty<string>())
			{
				Console.WriteLine($"    {miss}");
			}
		}
		return 0;
	}

	/// <summary>
	/// Each division of a country nobody plays found by its number in both saves: whether it stands where the later save
	/// has it, and whether it has left its first place - or is on its way - as the save's has.
	/// </summary>
	private static void ComparePlaces(CCountry country, List<CUnit> ours, List<CSavedUnit> theirs, Dictionary<int, int>? start,
		Action<string, bool, string> count)
	{
		if (start is null)
		{
			return;
		}
		var byId = theirs.Where(u => u.Kind == "division").GroupBy(u => u.Id).ToDictionary(g => g.Key, g => g.First());
		foreach (CUnit unit in ours.Where(u => u.Kind == EUnitKind.Division))
		{
			if (!byId.TryGetValue(unit.Id, out CSavedUnit? saved) || !start.TryGetValue(unit.Id, out int first))
			{
				continue;
			}
			string where = $"{country.Tag} {unit.Name}";
			bool savedMoved = saved.Location != first || saved.Bound > 0;
			bool ourMoved = unit.Location != first || unit.IsMoving;
			int ourEnd = unit.IsMoving ? unit.Path[^1] : unit.Location;
			int savedEnd = saved.Bound > 0 ? saved.Bound : saved.Location;
			count("division place", unit.Location == saved.Location,
				$"{where}: ours {unit.Location}{(unit.IsMoving ? $" to {ourEnd}" : "")}, save {saved.Location}" +
				$"{(saved.Bound > 0 ? $" to {saved.Bound}" : "")}, from {first}");
			count("division moved or not", ourMoved == savedMoved,
				$"{where}: ours {(ourMoved ? $"to {ourEnd}" : "stays")}, save {(savedMoved ? $"to {savedEnd}" : "stays")}");
			if (savedMoved)
			{
				count("division bound where the save's is", ourEnd == savedEnd,
					$"{where}: ours {ourEnd}, save {savedEnd}, from {first}");
			}
		}
	}

	/// <summary>
	/// Each air unit of a country nobody plays found by its number in both: its mission, and where both have the same one,
	/// its province, its stance, the hour it ends and the provinces it covers.
	/// </summary>
	private static void CompareAirMissions(CCountry country, List<CUnit> ours, List<CSavedUnit> theirs,
		Action<string, bool, string> count)
	{
		var byId = theirs.Where(u => u.Kind == "air").GroupBy(u => u.Id).ToDictionary(g => g.Key, g => g.First());
		foreach (CUnit unit in ours.Where(u => u.Kind == EUnitKind.Air))
		{
			if (!byId.TryGetValue(unit.Id, out CSavedUnit? saved))
			{
				continue;
			}
			string where = $"{country.Tag} {unit.Name}";
			count("air unit place", unit.Location == saved.Location, $"{where}: ours {unit.Location}, save {saved.Location}");
			string mission = AirMissions.GetValueOrDefault(unit.Order, "");
			count("air mission", mission == saved.Mission,
				$"{where}: ours {(mission.Length > 0 ? $"{mission} {unit.OrderProvince}" : "none")}, " +
				$"save {(saved.Mission.Length > 0 ? $"{saved.Mission} {saved.MissionProvince}" : "none")}");
			if (mission.Length == 0 || mission != saved.Mission)
			{
				continue;
			}
			count("air mission province", unit.OrderProvince == saved.MissionProvince,
				$"{where}: ours {unit.OrderProvince}, save {saved.MissionProvince}");
			count("air mission stance", unit.Mission.Stance == saved.MissionStance,
				$"{where}: ours {unit.Mission.Stance}, save {saved.MissionStance}");
			long end = unit.Mission.EndHour;
			string ourEnd = end == OpenHOI3.Sim.Missions.CMission.NoEnd ? "never"
				: $"{CDate.FromDayNumber((int)(end / CGameState.HoursPerDay))}.{end % CGameState.HoursPerDay}";
			count("air mission end", ourEnd == saved.MissionEnd, $"{where}: ours {ourEnd}, save {saved.MissionEnd}");
			count("air mission provinces", unit.Mission.Provinces.SequenceEqual(saved.MissionProvinces),
				$"{where}: ours {unit.Mission.Provinces.Count} ({Text(unit.Mission.Provinces.Except(saved.MissionProvinces).Take(8))} " +
				$"not the save's), save {saved.MissionProvinces.Count} ({Text(saved.MissionProvinces.Except(unit.Mission.Provinces).Take(8))} " +
				"not ours)");
		}
	}

	private static void ComparePlan(CUnitPlan ours, ClausewitzNode? plan, string where, Action<string, bool, string> count)
	{
		bool active = plan?.GetBool("active") ?? false;
		count("plan active", ours.Active == active, $"{where}: ours {ours.Active}, save {active}");
		if (plan is null)
		{
			return;
		}
		int stance = plan.GetInt("stance", CUnitPlan.Defensive);
		count("stance", ours.Stance == stance, $"{where}: ours {ours.Stance}, save {stance}");
		int air = plan.GetInt("air_stance", CUnitPlan.DefensiveAirOrNaval);
		int naval = plan.GetInt("naval_stance", CUnitPlan.DefensiveAirOrNaval);
		count("air and naval stance", ours.AirStance == air && ours.NavalStance == naval,
			$"{where}: ours {ours.AirStance} {ours.NavalStance}, save {air} {naval}");
		long our = Thousandths(plan.GetString("our_power", "0"));
		long their = Thousandths(plan.GetString("their_power", "0"));
		count("our power", ours.OurPower == our, $"{where}: ours {ours.OurPower}, save {our}");
		count("their power", ours.TheirPower == their, $"{where}: ours {ours.TheirPower}, save {their}");
		List<int> area = Numbers(plan.Get("ops_area"));
		count("area", ours.Area.SequenceEqual(area), $"{where}: ours {Text(ours.Area)}\n        save {Text(area)}");
		count("area as a set", ours.Area.OrderBy(p => p).SequenceEqual(area.OrderBy(p => p)),
			$"{where}: only ours {Text(ours.Area.Except(area))}, only save {Text(area.Except(ours.Area))}");
		var objectives = plan.All("objective").Where(o => o.IsBlock)
			.Select(o => (o.GetInt("province"), o.GetInt("priority"))).ToList();
		count("objectives", ours.Objectives.Select(o => (o.Province, o.Priority)).SequenceEqual(objectives),
			$"{where}: ours {string.Join(" ", ours.Objectives.Select(o => $"{o.Province}:{o.Priority}"))}, " +
			$"save {string.Join(" ", objectives.Select(o => $"{o.Item1}:{o.Item2}"))}");
		int embark = plan.GetInt("embark");
		int debark = plan.GetInt("debark");
		count("embark and debark", ours.Embark == embark && ours.Debark == debark,
			$"{where}: ours {ours.Embark} {ours.Debark}, save {embark} {debark}");
		List<int> path = Numbers(plan.Get("path"));
		count("path", ours.Path.SequenceEqual(path), $"{where}: ours {Text(ours.Path)}, save {Text(path)}");
		List<int> min = Numbers(plan.Get("min"));
		string ourMin = Wants(ours.Min);
		string theirMin = Wants(min);
		count("min", ourMin == theirMin, $"{where}: ours {ourMin}, save {theirMin}");
	}

	private static string Wants(IReadOnlyList<int> min)
	{
		return string.Join(" ", min.Select((count, index) => (count, index)).Where(p => p.count != 0).Select(p => $"{p.index}:{p.count}"));
	}

	/// <summary>
	/// For <c>unit=</c>: each land unit of that name, one line an hour - place, way, order, its first brigade's organisation
	/// against its ceiling, the commander's bonus and how far the army's command reaches it, and the chain above it.
	/// </summary>
	private static void FollowUnit(CGameState state, string name)
	{
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits().Where(u => !u.IsRemoved && u.Name == name && u.SubUnits.Count > 0))
			{
				CSubUnit brigade = unit.SubUnits[0];
				CUnit? army = COrganisation.ArmyAbove(unit);
				long reach = army is null ? 0 : OpenHOI3.Sim.Leaders.CCommandReach.Through(state, unit, OpenHOI3.Sim.Leaders.CCommandReach.Level(army));
				var chain = new List<string>();
				for (CUnit? up = unit.Parent; up is not null; up = up.Parent)
				{
					chain.Add($"{up.Name}@{up.Location}");
				}
				Console.WriteLine($"{state.Date}.{state.Hour} {country.Tag} {unit.Name} at {unit.Location}" +
					$"{(unit.Path.Count > 0 ? " > " + unit.Path[^1] : "")} mp {unit.MovementProgress} path {string.Join(",", unit.Path)} w t{state.Province(unit.Location).Weather.Temperature} p{state.Province(unit.Location).Weather.Precipitation} m{state.Province(unit.Location).Weather.Muddyness} f{state.Province(unit.Location).Weather.Frozen} {unit.Order}{(unit.IsHeldBack ? " held back" : "")}: " +
					$"org {brigade.Organisation} of {COrganisation.Ceiling(state, country, brigade)}, bonus {COrganisation.CommanderBonus(state, unit)}, " +
					$"reach {reach}, army {army?.Name ?? "none"} led by {army?.Leader?.Name ?? "nobody"}; under {string.Join(" < ", chain)}");
			}
		}
	}

	/// <summary>
	/// For <c>ready=</c>, once a day over the run's last weeks: each target's front with how many provinces stand empty, and
	/// each followed division - the save's on the front and ours on it that day - with where it is, where it is bound, its
	/// order and the lowest share of its ceiling any of its brigades has.
	/// </summary>
	private static void WatchFront(CGameState state, CCountry country, string spec, HashSet<string> watched)
	{
		string[] targets = spec.Split(':').ElementAtOrDefault(1)?.Split(',') ?? Array.Empty<string>();
		var onFront = new HashSet<int>();
		var summary = new List<string>();
		foreach (string target in targets)
		{
			OpenHOI3.Sim.Ai.CReadinessTally tally = OpenHOI3.Sim.Ai.CWarReadiness.Count(state, country, new CCountryTag(target));
			summary.Add($"{target} {tally.Fronts.Count - tally.Empty}/{tally.Fronts.Count} held");
			onFront.UnionWith(tally.Fronts.Select(front => front.Province));
		}
		Console.WriteLine($"{state.Date} {country.Tag} front: {string.Join(", ", summary)}");
		foreach (CUnit unit in country.AllUnits().Where(u => !u.IsRemoved && u.Kind == EUnitKind.Division
			&& (watched.Contains(u.Name) || onFront.Contains(u.Location))))
		{
			long share = unit.SubUnits.Count == 0 ? 0 : unit.SubUnits.Min(brigade =>
			{
				long ceiling = COrganisation.Ceiling(state, country, brigade).Raw;
				return ceiling == 0 ? 0 : brigade.Organisation.Raw * 1000 / ceiling;
			});
			Console.WriteLine($"    {unit.Name,-22} at {unit.Location,5}{(onFront.Contains(unit.Location) ? "*" : " ")}" +
				$"{(unit.Path.Count > 0 ? " > " + unit.Path[^1] : ""),8} {unit.Order,-18} org {share / 10.0,5:0.0}%" +
				$"{(unit.IsMoving ? " moving" : "")}{(unit.NoBattleHours > 0 ? " waiting" : "")}");
		}
	}

	/// <summary>
	/// For <c>ready=TAG:TARGET,TARGET</c>: what the readiness test counts on the country's fronts facing each target at the
	/// save's hour - each front province with our fit strength there, our land units in it and the save's divisions there -
	/// and the country's prepared wars (<c>docs/mechanics/foreignminister.md</c>, section 7).
	/// </summary>
	private static void Readiness(CGameState state, string spec, Dictionary<string, List<CSavedUnit>> saved)
	{
		string[] parts = spec.Split(':');
		if (state.Country(new CCountryTag(parts[0])) is not { } country)
		{
			return;
		}
		var theirs = saved.GetValueOrDefault(country.Tag.Value)?.SelectMany(All).Where(u => u.Kind == "division").ToList()
			?? new List<CSavedUnit>();
		Console.WriteLine($"{country.Tag}'s wars prepared: " + (country.WarTargets.Count == 0 ? "none" : string.Join(", ",
			country.WarTargets.Select(t => $"{t.Target} since {CDate.FromDayNumber((int)(t.Hour / CGameState.HoursPerDay))}" +
				(OpenHOI3.Sim.Ai.CWarReadiness.IsReady(state, country, t) ? " ready" : " not ready")))));
		foreach (string target in parts.Length > 1 ? parts[1].Split(',') : Array.Empty<string>())
		{
			OpenHOI3.Sim.Ai.CReadinessTally tally = OpenHOI3.Sim.Ai.CWarReadiness.Count(state, country, new CCountryTag(target));
			Console.WriteLine($"{country.Tag} facing {target}: {tally.Fronts.Count} front provinces, {tally.Empty} empty, " +
				$"ours {tally.Ours}, enemy {tally.Enemy} in {tally.EnemyProvinces.Count} provinces: {Text(tally.EnemyProvinces)}");
			foreach ((int province, long ours) in tally.Fronts)
			{
				string units = string.Join(", ", state.Positions.Of(province).Where(u => u.Owner == country.Tag && CTheatres.IsLand(u))
					.Select(u => $"{u.Name} {u.SubUnits.Sum(s => s.Strength.Raw)}/{u.SubUnits.Sum(s => s.Organisation.Raw)}" +
						(u.IsMoving ? " moving" : "") + (u.NoBattleHours > 0 ? " waiting" : "")));
				string save = string.Join(", ", theirs.Where(u => u.Location == province).Select(u => u.Name));
				Console.WriteLine($"  {province,5} {state.Province(province).Name,-24} counted {ours,7}  ours: {(units.Length == 0 ? "-" : units)}" +
					$"  save: {(save.Length == 0 ? "-" : save)}");
			}
		}

		// Why a unit on the front is or is not fit: each brigade's organisation against its ceiling and what the ceiling is
		// made of, beside the save's division of the same name.
		Console.WriteLine($"{country.Tag}: officer ratio {country.OfficerRatio}, land organisation modifier " +
			$"{country.Modifier[COrganisation.LandOrganisation]}, laws {string.Join(" ", country.Laws.Values.Select(law => law.Name))}");
		// The ladder: the manpower a new command's brigade needs, and every corps with the divisions under it, ours and the save's.
		Console.WriteLine($"{country.Tag}: manpower {country.Manpower}");
		foreach (CUnit corps in country.AllUnits().Where(u => !u.IsRemoved && u.Kind == EUnitKind.Corps))
		{
			Console.WriteLine($"  ours corps {corps.Name} at {corps.Location} under {corps.Parent?.Name ?? "-"}: " +
				$"{corps.Children.Count(c => c.Kind == EUnitKind.Division)} divisions");
		}
		foreach (CSavedUnit corps in saved.GetValueOrDefault(country.Tag.Value)?.SelectMany(All).Where(u => u.Kind == "corps")
			?? Enumerable.Empty<CSavedUnit>())
		{
			Console.WriteLine($"  save corps {corps.Name} at {corps.Location} under {corps.Parent?.Name ?? "-"}: " +
				$"{corps.Children.Count(c => c.Kind == "division")} divisions");
		}
		foreach (CUnit theatre in country.AllUnits().Where(u => !u.IsRemoved && u.Kind == EUnitKind.Theatre))
		{
			var under = new List<CUnit>();
			var stack = new Stack<CUnit>(theatre.Children);
			while (stack.Count > 0)
			{
				CUnit unit = stack.Pop();
				under.Add(unit);
				foreach (CUnit child in unit.Children)
				{
					stack.Push(child);
				}
			}
			int divisions = under.Count(u => u.Kind == EUnitKind.Division && !OpenHOI3.Sim.Agents.CAgentUnits.IsGarrison(u));
			Console.WriteLine($"  ours theatre {theatre.Name}: {divisions} divisions not garrison-led, " +
				$"{under.Count(u => u.Kind == EUnitKind.Corps)} corps, wanted {divisions / 4 + 1}");
		}
		// Every land brigade of the country by type: how many, and their average organisation, ours and the save's.
		var ourTypes = country.AllUnits().Where(u => !u.IsRemoved && CTheatres.IsLand(u)).SelectMany(u => u.SubUnits)
			.GroupBy(s => s.Type.Key).ToDictionary(g => g.Key, g => (Count: g.Count(), Average: g.Average(s => (double)s.Organisation.Raw) / 1000));
		var saveTypes = theirs.SelectMany(u => u.Node!.Children.Where(c => c.Key == "regiment" && c.IsBlock))
			.GroupBy(r => r.GetString("type")).ToDictionary(g => g.Key, g => (Count: g.Count(),
				Average: g.Average(r => double.Parse(r.GetString("organisation"), CultureInfo.InvariantCulture))));
		foreach (string type in ourTypes.Keys.Union(saveTypes.Keys).OrderBy(k => k, StringComparer.Ordinal))
		{
			(int oc, double oa) = ourTypes.GetValueOrDefault(type);
			(int sc, double sa) = saveTypes.GetValueOrDefault(type);
			Console.WriteLine($"  {type,-28} ours {oc,4} at {oa,7:0.000}   save {sc,4} at {sa,7:0.000}");
		}
		var fronts = parts.Length > 1
			? parts[1].Split(',').SelectMany(target => OpenHOI3.Sim.Ai.CWarReadiness.Count(state, country, new CCountryTag(target)).Fronts)
				.Select(front => front.Province).Distinct().ToList()
			: new List<int>();
		foreach (CUnit unit in fronts.SelectMany(province => state.Positions.Of(province))
			.Where(u => u.Owner == country.Tag && CTheatres.IsLand(u)).Distinct())
		{
			CUnit? army = COrganisation.ArmyAbove(unit);
			Console.WriteLine($"  ours {unit.Name} at {unit.Location} under {unit.Parent?.Name ?? "-"}; army {army?.Name ?? "none"}" +
				$" led by {army?.Leader?.Name ?? "nobody"} (skill {army?.Leader?.Skill ?? 0}); commander bonus {COrganisation.CommanderBonus(state, unit)}");
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				Console.WriteLine($"      {brigade.Type.Key}: strength {brigade.Strength} of {CUnits.MaxStrength(state, country, brigade)}, " +
					$"organisation {brigade.Organisation} of {COrganisation.Ceiling(state, country, brigade)} " +
					$"(own figure {brigade.Stats[CSubUnitType.DefaultOrganisation]}{(brigade.IsReserve ? ", reserve" : "")}); levels " +
					string.Join(" ", brigade.TechnologyLevels.Where(level => level.Value > 0).Select(level => $"{level.Key}={level.Value}")));
			}
			foreach (CSavedUnit twin in theirs.Where(u => u.Name == unit.Name && u.Node is not null))
			{
				Console.WriteLine($"  save {twin.Name} at {twin.Location} under {twin.Parent?.Name ?? "-"}");
				foreach (ClausewitzNode regiment in twin.Node!.Children.Where(c => c.Key == "regiment" && c.IsBlock))
				{
					string levels = string.Join(" ", regiment.Children
						.Where(c => c.IsBlock && c.Key is not ("id") && c.Children.FirstOrDefault()?.Value is { } first && first != "0")
						.Select(c => $"{c.Key}={c.Children.First().Value}"));
					Console.WriteLine($"      {regiment.GetString("type")}: strength {regiment.GetString("strength")}, " +
						$"organisation {regiment.GetString("organisation")}; levels {levels}");
				}
			}
		}
	}

	private static int HoursTo(CGameState state, string date)
	{
		string[] parts = date.Split('.');
		var day = new CDate(int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture),
			int.Parse(parts[2], CultureInfo.InvariantCulture));
		int hour = parts.Length > 3 ? int.Parse(parts[3], CultureInfo.InvariantCulture) : 0;
		long target = day.DayNumber * (long)CGameState.HoursPerDay + hour;
		long now = state.Date.DayNumber * (long)CGameState.HoursPerDay + state.TotalHours % CGameState.HoursPerDay;
		return (int)Math.Max(0, target - now);
	}

	private static string Key(CUnit unit) => unit.Kind switch
	{
		EUnitKind.Theatre => "theatre",
		EUnitKind.ArmyGroup => "armygroup",
		EUnitKind.Army => "army",
		EUnitKind.Corps => "corps",
		EUnitKind.Division => "division",
		EUnitKind.Navy => "navy",
		_ => "air",
	};

	private static void Collect(ClausewitzNode block, CSavedUnit? parent, List<CSavedUnit> tops)
	{
		foreach (ClausewitzNode child in block.Children)
		{
			if (!child.IsBlock || child.Key is null || !UnitKeys.Contains(child.Key))
			{
				continue;
			}
			var unit = new CSavedUnit
			{
				Kind = child.Key,
				Name = child.GetString("name"),
				Id = child.Get("id")?.GetInt("id") ?? 0,
				Location = child.GetInt("location"),
				Bound = Numbers(child.Get("path")) is { Count: > 0 } path ? path[^1] : 0,
				Parent = parent,
				Plan = child.Get("plan") is { IsBlock: true } plan ? plan : null,
				Node = child,
			};
			if (child.Key == "air" && child.Children.FirstOrDefault(c => c.IsBlock && c.Key is { } k && AirMissions.ContainsValue(k))
				is { } mission)
			{
				unit.Mission = mission.Key!;
				unit.MissionProvince = mission.GetInt("province");
				unit.MissionEnd = mission.GetString("death_date");
				unit.MissionStance = mission.GetInt("stance");
				foreach (ClausewitzNode entry in mission.Children.Where(c => c.Key == "provinces"))
				{
					if (int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
					{
						unit.MissionProvinces.Add(id);
					}
				}
			}
			if (parent is null)
			{
				tops.Add(unit);
			}
			else
			{
				parent.Children.Add(unit);
			}
			Collect(child, unit, tops);
		}
	}

	private static IEnumerable<CSavedUnit> All(CSavedUnit unit)
	{
		yield return unit;
		foreach (CSavedUnit child in unit.Children)
		{
			foreach (CSavedUnit below in All(child))
			{
				yield return below;
			}
		}
	}

	private static long Thousandths(string text)
	{
		return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value)
			? (long)Math.Round(value * 1000m)
			: 0;
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

	private static string Text(IEnumerable<int> numbers) => string.Join(" ", numbers);
}
