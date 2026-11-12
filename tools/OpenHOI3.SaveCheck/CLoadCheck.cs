using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

/// <summary>
/// Loads a save of the original's as a game here and says how it went:
///
///   OpenHOI3.SaveCheck load &lt;install folder&gt; &lt;savegame&gt; [hours] [later savegame] [examples=TAG] [series=A,B,C]
///
/// what of the save is not loaded yet, block by block; the loaded game's figures set beside the save's where the loader
/// reads them, and the ones it works out again - IC - beside nothing; and then the hours given run, to show the game goes
/// on from there. Nothing is written. See <c>docs/mechanics/savegame.md</c>, section 4. With <c>route=UNIT:FROM:TO,...</c> it
/// says instead the way ours finds for each, plain and very safe, on the save as loaded - to set beside a search the
/// running game was seen to make - and stops; with <c>garrison=UNIT,...</c> (numbers or names) each unit's garrison rating and
/// strength and organisation shares.
/// </summary>
public static class CLoadCheck
{
	/// <summary>Given as <c>examples=TAG</c>: the examples of every miss are that country's, a dozen of them.</summary>
	private static string? ExampleCountry;

	/// <summary>The hours of the running game's clock are this many ahead of ours.</summary>
	private const long OriginalTickOffset = 43_800_000;

	private static void PutGenerator(CGameState state, string folder, CCombat combat)
	{
		string path = System.IO.Path.Combine(folder, $"mt_{state.TotalHours + OriginalTickOffset}_{combat.Province}_{(combat.Kind == ECombatKind.Land ? "L" : combat.Kind == ECombatKind.Air ? "A" : combat.Kind == ECombatKind.Naval ? "N" : "B")}.txt");
		if (!System.IO.File.Exists(path))
		{
			return;
		}
		string[] lines = System.IO.File.ReadAllLines(path);
		string[] head = lines[0].Split(' ');
		var words = new uint[624];
		for (int i = 0; i < words.Length; i++)
		{
			words[i] = uint.Parse(lines[i + 1], System.Globalization.NumberStyles.HexNumber);
		}
		state.Random.Restore((words, int.Parse(head[1]), long.Parse(head[0])));

		// What a save does not keep and a running game does: the target each wing is holding to.
		string targets = System.IO.Path.ChangeExtension(path, ".targets");
		if (System.IO.File.Exists(targets))
		{
			// And for a ship the distance it stands at, which a load draws up afresh, and its experience, rolled before the battles.
			// The dump is in the battle's own order, attackers then defenders, each unit's sub units in turn, and so are ours;
			// a sub unit a load renumbered (its number taken twice in the save) is matched by its place, and a target named by
			// the save's number is turned into ours.
			string[][] rows = System.IO.File.ReadAllLines(targets).Select(line => line.Split(' ')).ToArray();
			List<CSubUnit> ours = new[] { combat.Attacker, combat.Defender }.SelectMany(side => side.Units).SelectMany(unit => unit.SubUnits).ToList();
			var held = new Dictionary<CSubUnit, string[]>();
			var renamed = new Dictionary<int, int>();
			bool byPlace = rows.Length == ours.Count;
			for (int i = 0; i < rows.Length; i++)
			{
				CSubUnit? match = byPlace ? ours[i] : ours.FirstOrDefault(subUnit => subUnit.Id == int.Parse(rows[i][0]));
				if (match is not null)
				{
					held[match] = rows[i];
					renamed.TryAdd(int.Parse(rows[i][0]), match.Id);
				}
			}
			foreach (CCombatSide side in new[] { combat.Attacker, combat.Defender })
			{
				foreach (CSubUnit wing in side.Units.SelectMany(unit => unit.SubUnits))
				{
					if (held.TryGetValue(wing, out string[]? parts))
					{
						int target = int.Parse(parts[1]);
						wing.TargetId = parts[2] == "1" ? renamed.GetValueOrDefault(target, target) : 0;
						if (parts.Length >= 5)
						{
							wing.Distance = new CFixedPoint(long.Parse(parts[3]));
							wing.Experience = new CFixedPoint(long.Parse(parts[4]));
						}
					}
				}
			}
		}
	}

	/// <summary>model=FILE: each logged attack-estimate modifier beside ours, by country, with the mean difference.</summary>
	private static void CompareModel(CGameState state, string file)
	{
		var seen = new HashSet<string>();
		var byCountry = new Dictionary<string, (int Count, int Same, double Sum)>();
		foreach (string line in System.IO.File.ReadLines(file, System.Text.Encoding.Latin1))
		{
			string[] part = line.Split('\t');
			if (part.Length < 8 || !seen.Add(string.Join('\t', part.Skip(1))))
			{
				continue;
			}
			string tag = part[1];
			int at = int.Parse(part[3]);
			int target = int.Parse(part[4]);
			// Read off the running game as unsigned words: -2 comes as 4294967294.
			long brigades = unchecked((int)uint.Parse(part[5]));
			long provinces = unchecked((int)uint.Parse(part[6]));
			double theirs = double.Parse(part[7], System.Globalization.CultureInfo.InvariantCulture);
			CUnit? unit = state.Country(new CCountryTag(tag))?.AllUnits().FirstOrDefault(each => each.Name == part[2] && each.Location == at);
			if (unit is null)
			{
				Console.WriteLine($"MODEL {tag} {part[2]} at {at}: no such unit");
				continue;
			}
			// A target of -1 is a defender's line: its crowding already less three a province, and the enemy's provinces.
			long ours = target < 0
				? OpenHOI3.Sim.Agents.CAiCombatModel.Defence(state, unit, brigades, (int)provinces)
				: OpenHOI3.Sim.Agents.CAiCombatModel.Attack(state, unit, state.Province(target), brigades, provinces);
			double difference = ours / 1000.0 - theirs;
			bool same = Math.Abs(difference) < 0.0015;
			(int count, int sameCount, double sum) = byCountry.GetValueOrDefault(tag);
			byCountry[tag] = (count + 1, sameCount + (same ? 1 : 0), sum + difference);
			CUnit top = unit;
			while (top.Parent is not null)
			{
				top = top.Parent;
			}
			Console.WriteLine($"MODEL {tag}\t{part[2]}\t{at}>{target}\t{brigades}/{provinces}\tours {ours / 1000.0:F3}\ttheirs {theirs:F3}\t" +
				$"{(same ? "" : $"diff {difference:F3}")}\ttop {top.Kind} {top.Name} leader {top.Leader?.Name ?? "none"} skill {top.Leader?.Skill ?? -1}");
		}
		foreach ((string tag, (int count, int same, double sum)) in byCountry)
		{
			Console.WriteLine($"MODEL {tag}: {same} of {count} the same, mean difference {sum / count:F4}");
		}
	}

	/// <summary>watch=N,N: what a province holds after the hours, set beside the running game's.</summary>
	private static void WriteProvince(CGameState state, System.IO.TextWriter log, int id)
	{
		CProvince province = state.Province(id);
		string Goods(CGoodsPool pool) => string.Join(",", Enum.GetValues<EGoods>().Select(good => pool[good].Raw));
		log.WriteLine($"P {id}");
		log.WriteLine($" pool {Goods(province.Pool)}");
		log.WriteLine($" current {Goods(province.CurrentProducing)}");
		int at = 0;
		foreach (CBuilding building in state.Buildings)
		{
			at++;
			log.WriteLine($" B {at} {province.BuildingMaxLevel(building).Raw} {province.BuildingLevel(building).Raw}");
		}
		foreach (CUnit unit in state.UnitsIn(id))
		{
			log.WriteLine($" U {unit.Id} {unit.Owner.Value} " + string.Join(" ", unit.SubUnits.Select(b => $"{b.Id}:{b.Strength.Raw}/{b.Organisation.Raw}")));
		}
	}

	private static void WriteBattles(CGameState state, System.IO.TextWriter log)
	{
		foreach (CCombat combat in state.Combats)
		{
			string kind = combat.Kind == ECombatKind.Land ? "L" : combat.Kind == ECombatKind.Air ? "A" : combat.Kind == ECombatKind.Naval ? "N" : "B";
			log.WriteLine($"AFTER prov {combat.Province} {kind} day {combat.Hour} dur {combat.Duration}");
			foreach ((string name, CCombatSide side) in new[] { ("A", combat.Attacker), ("D", combat.Defender) })
			{
				log.WriteLine($" SIDE {name} dice {side.Dice} losses {side.Losses.Raw} tactic {side.Tactic?.Number ?? -1} " +
					$"front {string.Join(",", side.Front.Select(u => u.Id))} reserves {string.Join(",", side.Reserves.Select(u => u.Id))}");
				foreach (CUnit unit in side.Units)
				{
					log.WriteLine($"  U {unit.Id} " + string.Join(" ", unit.SubUnits.Select(b => $"{b.Id}:{b.Strength.Raw}/{b.Organisation.Raw}")));
				}
			}
		}
	}

	public static int Run(string[] args)
	{
		ExampleCountry = args.FirstOrDefault(arg => arg.StartsWith("examples=", StringComparison.Ordinal))?["examples=".Length..];
		string? trace = args.FirstOrDefault(arg => arg.StartsWith("trace=", StringComparison.Ordinal))?["trace=".Length..];
		string? generator = args.FirstOrDefault(arg => arg.StartsWith("generator=", StringComparison.Ordinal))?["generator=".Length..];
		string? battles = args.FirstOrDefault(arg => arg.StartsWith("battles=", StringComparison.Ordinal))?["battles=".Length..];
		string? watch = args.FirstOrDefault(arg => arg.StartsWith("watch=", StringComparison.Ordinal))?["watch=".Length..];
		string? routes = args.FirstOrDefault(arg => arg.StartsWith("route=", StringComparison.Ordinal))?["route=".Length..];
		args = args.Where(arg => !arg.StartsWith("route=", StringComparison.Ordinal)).ToArray();
		string? garrisons = args.FirstOrDefault(arg => arg.StartsWith("garrison=", StringComparison.Ordinal))?["garrison=".Length..];
		args = args.Where(arg => !arg.StartsWith("garrison=", StringComparison.Ordinal)).ToArray();
		// model=FILE: lines logged from the running game's attack estimate, a unit's own modifier total each - tick, owner,
		// name, where it stands, target, brigades, provinces, value - each set beside ours on the save as loaded, and stop.
		string? modelFile = args.FirstOrDefault(arg => arg.StartsWith("model=", StringComparison.Ordinal))?["model=".Length..];
		args = args.Where(arg => !arg.StartsWith("model=", StringComparison.Ordinal)).ToArray();
		// skipentry: the save is of a game already entered, so the first pass's orders are in it and are not given again.
		bool skipEntry = args.Contains("skipentry");
		args = args.Where(arg => arg != "skipentry").ToArray();
		// orders=FILE: every way either side gave, one a line.
		string? ordersFile = args.FirstOrDefault(arg => arg.StartsWith("orders=", StringComparison.Ordinal))?["orders=".Length..];
		args = args.Where(arg => !arg.StartsWith("orders=", StringComparison.Ordinal)).ToArray();
		// series=A,B,C: saves an hour apart of the original's own run on from the later save, A being that save: after it,
		// each hour is played and the ways given in that hour alone are set beside the original's.
		string[]? series = args.FirstOrDefault(arg => arg.StartsWith("series=", StringComparison.Ordinal))?["series=".Length..].Split(',');
		args = args.Where(arg => !arg.StartsWith("series=", StringComparison.Ordinal)).ToArray();
		// agents=FILE: every agent's hold list and committed and held-back divisions after the hours, to set beside the running game's.
		string? agentsFile = args.FirstOrDefault(arg => arg.StartsWith("agents=", StringComparison.Ordinal))?["agents=".Length..];
		args = args.Where(arg => !arg.StartsWith("agents=", StringComparison.Ordinal)).ToArray();
		args = args.Where(arg => !arg.StartsWith("examples=", StringComparison.Ordinal) && !arg.StartsWith("trace=", StringComparison.Ordinal)
			&& !arg.StartsWith("generator=", StringComparison.Ordinal) && !arg.StartsWith("battles=", StringComparison.Ordinal)
			&& !arg.StartsWith("watch=", StringComparison.Ordinal)).ToArray();
		if (args.Length < 3)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck load <install folder> <savegame> [hours] [later savegame] [examples=TAG] [trace=TAG]");
			return 2;
		}
		int hours = args.Length > 3 && int.TryParse(args[3], out int h) ? h : 24;
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		var clock = Stopwatch.StartNew();
		// Asked to trace a country's AI, the game gets the install's Lua, as the client gives it.
		using OpenHOI3.Lua.CAiScripts? scripts = trace is null ? null : new OpenHOI3.Lua.CAiScripts(directories, warnings.Add);
		(CGameState state, CSaveReport report) = CSaveLoader.Load(directories, map, localisation, countries, args[2],
			aiScripts: scripts, warn: warnings.Add);
		if (trace is not null)
		{
			state.AiTrace = (new CCountryTag(trace), Console.WriteLine);
		}
		Console.WriteLine($"loaded {System.IO.Path.GetFileName(args[2])} in {clock.ElapsedMilliseconds} ms: " +
			$"{state.Date} {state.Hour}:00, {report.Player} played, seed {state.Seed}");
		Console.WriteLine("not loaded yet:");
		foreach (string line in report.Lines())
		{
			Console.WriteLine("  " + line);
		}

		// garrison=UNIT,...: what ours weighs a division as a garrison by - its rating and its organisation share - and stop.
		if (garrisons is not null)
		{
			foreach (string asked in garrisons.Split(','))
			{
				// A number, or a unit's name.
				CUnit? unit = state.Countries.SelectMany(country => country.AllUnits())
					.FirstOrDefault(each => int.TryParse(asked, out int number) ? each.Id == number : each.Name == asked);
				string id = asked;
				if (unit is null)
				{
					Console.WriteLine($"GARRISON {id}: no unit");
					continue;
				}
				OpenHOI3.Sim.Agents.CUnitShares.Of(state, unit, out long strength, out long organisation);
				Console.WriteLine($"GARRISON {id} {unit.Name} at {unit.Location}: rating {OpenHOI3.Sim.Agents.CUnitRatings.Garrison(state, unit)} strength {strength} organisation {organisation} front {OpenHOI3.Sim.Theatres.CTheatreFigures.IsFrontProvince(state, state.Province(unit.Location), false)}");
			}
			return 0;
		}

		if (modelFile is not null)
		{
			CompareModel(state, modelFile);
			return 0;
		}

		// route=UNIT:FROM:TO,...: the way ours finds for that unit on the save as loaded, plain and very safe, and stop.
		if (routes is not null)
		{
			foreach (string asked in routes.Split(','))
			{
				int[] part = asked.Split(':').Select(int.Parse).ToArray();
				CUnit? unit = state.Countries.SelectMany(country => country.AllUnits()).FirstOrDefault(each => each.Id == part[0]);
				if (unit is null || state.Country(unit.Owner) is not { } owner)
				{
					Console.WriteLine($"ROUTE {asked}: no unit");
					continue;
				}
				foreach (ERouteSafety safety in new[] { ERouteSafety.Plain, ERouteSafety.VerySafe })
				{
					List<int>? way = CRouteFinder.Find(state, owner, unit, part[1], part[2], landSafety: safety);
					Console.WriteLine($"ROUTE {asked} {safety} at {unit.Location}: {(way is null ? "none" : string.Join(' ', way))}");
				}
			}
			return 0;
		}

		// What was read, set beside the save it was read from: a miss here is a fault of the reading.
		ClausewitzNode save = ClausewitzParser.ParseFile(args[2]);
		int owners = 0, ownersSame = 0, laws = 0, lawsSame = 0, levels = 0, levelsSame = 0;
		foreach (ClausewitzNode node in save.Children)
		{
			if (int.TryParse(node.Key, out int id) && id > 0 && id < state.Provinces.Count)
			{
				owners++;
				CProvince province = state.Province(id);
				string saved = node.Get("owner")?.Value?.Trim('"') ?? "";
				if (saved == (province.Owner?.Value ?? ""))
				{
					ownersSame++;
				}
			}
			else if (CCountryTag.TryParse(node.Key, out CCountryTag tag) && state.Country(tag) is { } country)
			{
				foreach (CLawGroup group in state.LawGroups)
				{
					laws++;
					if ((node.Get(group.Name)?.Value ?? "") == (country.Laws.GetValueOrDefault(group.Name)?.Name ?? ""))
					{
						lawsSame++;
					}
				}
				foreach (ClausewitzNode entry in node.Get("technology")?.Children ?? new List<ClausewitzNode>())
				{
					if (entry.Key is { } key && entry.IsBlock && state.Technologies.ContainsKey(key))
					{
						levels++;
						if (country.TechnologyLevels.GetValueOrDefault(key) == (int)CSaveValues.Whole(entry.Children[0].Value))
						{
							levelsSame++;
						}
					}
				}
			}
		}
		Console.WriteLine($"owners {ownersSame} of {owners}, laws {lawsSame} of {laws}, technology levels {levelsSame} of {levels}");
		foreach (CCountry country in state.Countries.Where(c => c.Tag.Value is "GER" or "ENG" or "SOV" or "USA").ToList())
		{
			Console.WriteLine($"  {country.Tag}: IC {country.TotalIc} of base {country.BaseIc}, manpower {country.Manpower}, " +
				$"officers {country.Officers}, unity {country.NationalUnity}, faction '{country.Faction}'");
		}

		Console.WriteLine("the units, beside the save they were read from:");
		CompareUnits(state, save, "  ", exact: true);
		Console.WriteLine("the wars, pacts and convoys, beside the save they were read from:");
		CompareDiplomacy(state, save, "  ");
		CompareNapBalances(state, "  ");
		Console.WriteLine("the battles, the finished battles and the sunk ships, beside the save they were read from:");
		CompareCombats(state, save, "  ");

		// Given a later save of the same game, the hours to it are run and the units set beside it.
		ClausewitzNode? later = null;
		if (args.Length > 4)
		{
			later = ClausewitzParser.ParseFile(args[4]);
			if (CSaveValues.TryHours(later.Get("date")?.Value, out long then))
			{
				hours = (int)(then - state.TotalHours);
			}
		}
		clock.Restart();
		System.IO.StreamWriter? battleLog = battles is null ? null : new System.IO.StreamWriter(battles);
		if (battleLog is not null)
		{
			state.CombatTrace = battleLog.WriteLine;
		}
		if (generator is not null)
		{
			// generator=FOLDER: the original's generator as dumped as each battle's hour began, mt_<its tick>_<province>.txt.
			state.BeforeBattle = combat => PutGenerator(state, generator, combat);
		}
		// Through the simulation, as the client plays a load: entering the game runs the AI's pass and what it posts.
		var simulation = new CSimulation(state, discardEntryOrders: skipEntry);
		simulation.RunHours(hours);
		// The orders the last hour's AI posted wait for the next drain; the save was written after they ran.
		simulation.Pump();
		if (agentsFile is not null)
		{
			using var dump = new System.IO.StreamWriter(agentsFile);
			using (var roles = new System.IO.StreamWriter(agentsFile + ".roles"))
			{
				foreach (CCountry country in state.Countries)
				{
					foreach (CUnit unit in country.AllUnits().Where(u => u.Kind == EUnitKind.Division))
					{
						roles.WriteLine($"{country.Tag}	{unit.Name}	{CArmyRole.Of(unit)}	" + string.Join(",", unit.SubUnits.Select(s => s.Type.Key)));
					}
				}
			}
			foreach (CCountry country in state.Countries)
			{
				foreach (OpenHOI3.Sim.Agents.CAgent agent in country.UnitAi.Agents)
				{
					dump.WriteLine($"{country.Tag}\t{agent.Unit?.Name ?? "none"}\t" + string.Join(" ", agent.HoldProvinces.Select(h => $"{h.Province}:{h.Score}"))
						+ $"\t{agent.Committed.Count}\t{agent.HeldBack.Count}\t{agent.CommittedAtFirst.Count}"
						+ "\t" + string.Join("|", agent.Committed.Select(u => u.Name)) + "\t" + string.Join("|", agent.HeldBack.Select(u => u.Name))
						+ "\t" + string.Join("|", agent.CommittedAtFirst.Where(u => u.IsGarrison).Select(u => u.Name)));
				}
			}
		}
		if (battleLog is not null)
		{
			WriteBattles(state, battleLog);
			foreach (string text in (watch ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
			{
				WriteProvince(state, battleLog, int.Parse(text));
			}
			battleLog.Dispose();
		}
		Console.WriteLine($"ran {hours} hours in {clock.ElapsedMilliseconds} ms, to {state.Date} {state.Hour}:00");
		if (later is not null)
		{
			Console.WriteLine($"the units, beside {System.IO.Path.GetFileName(args[4])} - where the AI and the player " +
				"gave other orders they differ, so a look:");
			CompareUnits(state, later, "  ", exact: false);
			Console.WriteLine("the ways given in those hours, beside the original's:");
			CAiOrderCheck.Report(state, save, later, "  ", ExampleCountry, ordersFile);
			Console.WriteLine("the wars, pacts and convoys, beside the later save:");
			CompareDiplomacy(state, later, "  ");
		}
		if (series is not null)
		{
			ClausewitzNode earlier = ClausewitzParser.ParseFile(series[0]);
			for (int step = 1; step < series.Length; step++)
			{
				var before = CAiOrderCheck.Snapshot(state);
				var holders = state.Provinces.ToDictionary(province => province.Id, province => province.Controller?.Value);
				simulation.RunHours(1);
				simulation.Pump();
				ClausewitzNode next = ClausewitzParser.ParseFile(series[step]);
				Console.WriteLine($"hour {step} of the series, {state.Date} {state.Hour}:00:");
				var changes = state.Provinces.Where(province => province.Controller?.Value != holders[province.Id])
					.Select(province => $"{province.Id}:{holders[province.Id]}>{province.Controller?.Value}").ToList();
				var theirs = next.Children.Where(node => node.IsBlock && int.TryParse(node.Key, out _))
					.Select(node => (Id: int.Parse(node.Key!), Holder: node.Get("controller")?.Value ?? node.Get("owner")?.Value))
					.Zip(earlier.Children.Where(node => node.IsBlock && int.TryParse(node.Key, out _)),
						(now, then) => (now.Id, now.Holder, Was: then.Get("controller")?.Value ?? then.Get("owner")?.Value))
					.Where(change => change.Holder != change.Was).Select(change => $"{change.Id}:{change.Was?.Trim('"')}>{change.Holder?.Trim('"')}").ToList();
				Console.WriteLine($"  hands: ours {string.Join(' ', changes)} | the original {string.Join(' ', theirs)}");
				CAiOrderCheck.Report(state, earlier, next, "  ", ExampleCountry, ordersFile is null ? null : $"{ordersFile}.h{step}", before);
				earlier = next;
			}
		}
		Console.WriteLine($"{warnings.Count} warnings; the first few:");
		foreach (string warning in warnings.Take(8))
		{
			Console.WriteLine("  " + warning);
		}
		return 0;
	}

	/// <summary>
	/// The wars by name and sides, every kind of pact as a set of pairs, the trade routes with their convoys, every
	/// convoy by its number, and each country's pool of transports and escorts and whether it is mobilised, set beside
	/// the save's.
	/// </summary>
	/// <summary>Each pact's border measured again and set beside what the save holds, which is as of the last seventh day.</summary>
	private static void CompareNapBalances(CGameState state, string indent)
	{
		int same = 0;
		foreach (OpenHOI3.Sim.Agreements.CNap nap in state.Naps)
		{
			(long balance, int ours, int theirs) = (nap.Balance, nap.OurPower, nap.TheirPower);
			OpenHOI3.Sim.Agreements.CNapBalance.Measure(state, nap);
			if (balance == nap.Balance && ours == nap.OurPower && theirs == nap.TheirPower)
			{
				same++;
			}
			else
			{
				Console.WriteLine($"{indent}  pact {nap.First}-{nap.Second}: the save {ours} to {theirs}, {balance}; measured {nap.OurPower} to {nap.TheirPower}, {nap.Balance}");
			}
			(nap.Balance, nap.OurPower, nap.TheirPower) = (balance, ours, theirs);
		}
		Console.WriteLine($"{indent}a pact's border measured again: {same} of {state.Naps.Count} the save's");
	}

	private static void CompareDiplomacy(CGameState state, ClausewitzNode save, string indent)
	{
		static string Tag(ClausewitzNode node, string key) => node.Get(key)?.Value?.Trim('"') ?? "";
		void Line(string what, IEnumerable<string> ours, IEnumerable<string> saved)
		{
			var a = new HashSet<string>(ours);
			var b = new HashSet<string>(saved);
			string Few(IEnumerable<string> set) => string.Join(", ", set.Take(4));
			Console.WriteLine($"{indent}{what}: {a.Intersect(b).Count()} of {b.Count} the save's" +
				(a.Except(b).Any() ? $"; ours only {Few(a.Except(b))}" : "") + (b.Except(a).Any() ? $"; save only {Few(b.Except(a))}" : ""));
		}

		static string War(IEnumerable<CCountryTag> attackers, IEnumerable<CCountryTag> defenders, string name)
			=> $"{name}: {string.Join(' ', attackers)} v {string.Join(' ', defenders)}";
		Line("wars, by name and sides", state.Wars.Select(w => War(w.Attackers, w.Defenders, w.Name)),
			save.All("active_war").Select(w => War(w.All("attacker").Select(a => new CCountryTag(a.Value!.Trim('"'))),
				w.All("defender").Select(d => new CCountryTag(d.Value!.Trim('"'))), w.Get("name")?.Value?.Trim('"') ?? "")));
		// A name a declaration made is read back into its parts, which a declaration after the load names against.
		Console.WriteLine($"{indent}wars named by a declaration, read back into their parts: " +
			$"{state.Wars.Concat(state.FinishedWars).Count(w => w.MadeName is not null)} - " +
			string.Join(", ", state.Wars.Where(w => w.MadeName is not null).Select(w => w.MadeName!.ToString()).Take(4)));
		Line("war goals", state.Wars.SelectMany(w => w.Goals.Values.SelectMany(g => g)).Select(g => $"{g.Type.Name} {g.Actor} {g.Recipient}"),
			save.All("active_war").SelectMany(w => w.All("war_goal")).Select(g => $"{Tag(g, "casus_belli")} {Tag(g, "actor")} {Tag(g, "receiver")}"));

		ClausewitzNode diplomacy = save.Get("diplomacy") ?? new ClausewitzNode();
		IEnumerable<string> Pacts(string kind) => diplomacy.All(kind).Select(p => $"{Tag(p, "first")}>{Tag(p, "second")}");
		Line("guarantees", state.Guarantees.Select(g => $"{g.Guarantor}>{g.Guaranteed}"), Pacts("guarantee"));
		Line("alliances", state.Alliances.Select(a => $"{a.First}>{a.Second}"), Pacts("alliance"));
		Line("puppets", state.Countries.Where(c => c.Overlord is not null).Select(c => $"{c.Overlord}>{c.Tag}"), Pacts("vassal"));
		Line("NAPs", state.Naps.Select(n => $"{n.First}>{n.Second}"), Pacts("nap"));
		Line("embargoes", state.Embargoes.Select(e => $"{e.First}>{e.Second}"), Pacts("embargo"));
		Line("influences", state.Influences.Select(i => $"{i.Influencer}>{i.Influenced}"), Pacts("influence"));
		Line("alignments", state.Aligns.Select(a => $"{a.Aligner}>{a.Leader}"), Pacts("align"));
		Line("trade routes, with their convoy's number",
			state.TradeRoutes.Select(r => $"{r.From}>{r.To} {r.Convoy?.Id ?? 0}"),
			diplomacy.All("trade").Select(t => $"{Tag(t, "from")}>{Tag(t, "to")} {t.Get("convoy")?.GetInt("id") ?? 0}"));

		var ourConvoys = new List<string>();
		var savedConvoys = new List<string>();
		var ourPools = new List<string>();
		var savedPools = new List<string>();
		foreach (ClausewitzNode node in save.Children)
		{
			if (!CCountryTag.TryParse(node.Key, out CCountryTag tag) || state.Country(tag) is not { } country || !node.IsBlock)
			{
				continue;
			}
			foreach (ClausewitzNode convoy in node.All("convoy").Where(c => c.IsBlock))
			{
				savedConvoys.Add($"{tag} {convoy.Get("id")?.GetInt("id")} {convoy.GetInt("start")}>{convoy.GetInt("end")} " +
					$"{convoy.GetInt("convoys")}/{convoy.GetInt("escorts")} [{string.Join(' ', convoy.Get("ship")?.Children.Select(s => s.Value) ?? Array.Empty<string?>())}] " +
					$"{convoy.Get("path")?.Children.Count ?? 0} steps");
			}
			foreach (OpenHOI3.Sim.Convoys.CConvoy convoy in country.Convoys)
			{
				ourConvoys.Add($"{tag} {convoy.Id} {convoy.Start}>{convoy.End} {convoy.Transports}/{convoy.Escorts} " +
					$"[{string.Join(' ', convoy.Carries.Select(c => c ? "1" : "0"))}] {convoy.Path.Count} steps");
			}
			savedPools.Add($"{tag} {node.GetInt("convoys")}/{node.GetInt("escorts")} {(node.Get("mobilize")?.Value == "yes" ? "mobilised" : "")}");
			ourPools.Add($"{tag} {country.Transports}/{country.Escorts} {(country.Mobilised ? "mobilised" : "")}");
		}
		Line("convoys: harbours, transports and escorts, goods, way", ourConvoys, savedConvoys);
		Line("each country's pool of transports and escorts, and mobilised", ourPools, savedPools);
	}

	/// <summary>
	/// Every battle by its kind, province, hours and sides' units, each side's figures and lists, the order of the list
	/// of battles - the save's bombings turned round in front of the rest - every finished battle and every sunk ship,
	/// set beside the save's.
	/// </summary>
	private static void CompareCombats(CGameState state, ClausewitzNode save, string indent)
	{
		var ids = new CSaveIds();
		foreach (ClausewitzNode node in save.Children)
		{
			if (node.Key == "unit")
			{
				ids.SetHistoryNext((int)CSaveValues.Whole(node.Value));
			}
			else if (node.Key == "id" && node.IsBlock)
			{
				ids.SetHighest(node.GetInt("id"), node.GetInt("type"));
			}
		}
		void Line(string what, IEnumerable<string> ours, IEnumerable<string> saved)
		{
			var a = new List<string>(ours);
			var b = new List<string>(saved);
			var onlyOurs = a.Except(b).Take(2).ToList();
			var onlySaved = b.Except(a).Take(2).ToList();
			Console.WriteLine($"{indent}{what}: {a.Intersect(b).Count()} of {b.Count} the save's" +
				(onlyOurs.Count > 0 ? $"; ours only {string.Join(" | ", onlyOurs)}" : "") +
				(onlySaved.Count > 0 ? $"; save only {string.Join(" | ", onlySaved)}" : ""));
		}
		static string Figures(IEnumerable<string?> values) => string.Join(' ', values.Select(v => CSaveValues.Fixed(v).ToString()));
		static string Nonzero(IEnumerable<(int Index, string Value)> figures)
			=> string.Join(' ', figures.Where(f => f.Value != "0.000").OrderBy(f => f.Index).Select(f => $"{f.Index}:{f.Value}"));

		ClausewitzNode block = save.Get("combat") ?? new ClausewitzNode();
		var savedCombats = new List<string>();
		var savedSides = new List<string>();
		var savedOrder = new List<string>();
		var savedBombings = new List<string>();
		foreach (ClausewitzNode combat in block.Children)
		{
			if (combat.Key is not { } key || !CSaveCombats.Kinds.TryGetValue(key, out ECombatKind kind))
			{
				continue;
			}
			string name = $"{kind} {combat.GetInt("location")}";
			(CBombing.IsBombing(kind) ? savedBombings : savedOrder).Add(name);
			IEnumerable<int> Units(ClausewitzNode side, string list) => side.All(list).Select(u => ids.Of(u));
			ClausewitzNode attacker = combat.Get("attacker") ?? new ClausewitzNode();
			ClausewitzNode defender = combat.Get("defender") ?? new ClausewitzNode();
			savedCombats.Add($"{name} {combat.GetInt("day")}/{combat.GetInt("duration")} " +
				$"[{string.Join(' ', Units(attacker, "unit"))}] v [{string.Join(' ', Units(defender, "unit"))}]");
			foreach ((string which, ClausewitzNode side) in new[] { ("A", attacker), ("D", defender) })
			{
				savedSides.Add($"{name} {which} dice {side.GetInt("dice")} losses {CSaveValues.Fixed(side, "losses")} " +
					$"tactic {side.Get("tactic")?.Value ?? "-"} front [{string.Join(' ', Units(side, "front"))}] " +
					$"reserves [{string.Join(' ', Units(side, "reserves"))}] sunk [{string.Join(' ', Units(side, "sunk_ship"))}] " +
					$"positioning {(side.Get("positioning") is { } p ? CSaveValues.Fixed(p.Value) : CFixedPoint.Zero)} " +
					$"size {Nonzero(side.Get("size")?.Children.Select((f, i) => (i, CSaveValues.Fixed(f.Value).ToString())) ?? [])} " +
					$"strength {CSaveValues.Fixed(side, "strength")} " +
					$"building {Nonzero(side.Get("building")?.Children.Select((f, i) => (i, CSaveValues.Fixed(f.Value).ToString())) ?? [])} " +
					$"pool [{Figures(side.Get("pool")?.Children.Select(f => f.Value) ?? [])}] " +
					$"producing [{Figures(side.Get("current_producing")?.Children.Select(f => f.Value) ?? [])}]");
			}
		}
		savedBombings.Reverse();

		var ourCombats = new List<string>();
		var ourSides = new List<string>();
		foreach (CCombat combat in state.Combats)
		{
			string name = $"{combat.Kind} {combat.Province}";
			ourCombats.Add($"{name} {combat.Hour}/{combat.Duration} " +
				$"[{string.Join(' ', combat.Attacker.Units.Select(u => u.Id))}] v [{string.Join(' ', combat.Defender.Units.Select(u => u.Id))}]");
			bool bombing = CBombing.IsBombing(combat.Kind);
			foreach ((string which, CCombatSide side) in new[] { ("A", combat.Attacker), ("D", combat.Defender) })
			{
				var sizes = side.Size.Select(pair => (pair.Key == CSaveCombats.EmptyType ? 0 : state.SubUnitTypes[pair.Key].DatabaseIndex,
					pair.Value.ToString()));
				var buildings = side.BuildingsDestroyed.Select(pair => (state.Buildings.First(b => b.Key == pair.Key).Index + 1,
					pair.Value.ToString()));
				bool bomber = bombing && side.IsAttacker;
				ourSides.Add($"{name} {which} dice {side.Dice} losses {side.Losses} " +
					$"tactic {(side.Tactic is { } tactic ? (state.Tactics.ToList().IndexOf(tactic) + 1).ToString() : "-")} " +
					$"front [{string.Join(' ', side.Front.Select(u => u.Id))}] reserves [{string.Join(' ', side.Reserves.Select(u => u.Id))}] " +
					$"sunk [{string.Join(' ', side.Sunk.Select(s => s.Id))}] positioning {side.Positioning} size {Nonzero(sizes)} " +
					$"strength {side.StrengthDealt} building {Nonzero(buildings)} " +
					$"pool [{(bomber ? string.Join(' ', Enumerable.Range(0, 7).Select(g => side.StockpileDestroyed[(EGoods)g].ToString())) : "")}] " +
					$"producing [{(bomber ? string.Join(' ', Enumerable.Range(0, 7).Select(g => side.ProductionDestroyed[(EGoods)g].ToString())) : "")}]");
			}
		}
		Line("battles: kind, province, hours, the two sides' units", ourCombats, savedCombats);
		Line("their sides: dice, losses, tactic, front, reserves, sunk ships, positioning, size, a bomber's tallies", ourSides, savedSides);
		string expected = string.Join(", ", savedBombings.Concat(savedOrder));
		string order = string.Join(", ", state.Combats.Select(c => $"{c.Kind} {c.Province}"));
		Console.WriteLine($"{indent}the list of battles in the original's order after a load, bombings turned round in front: " +
			(order == expected ? "the same" : $"ours {order}; expected {expected}"));
		Console.WriteLine($"{indent}units in a battle: {state.Combats.Sum(c => c.Attacker.Units.Count + c.Defender.Units.Count)}, " +
			$"each holding it: {state.Combats.Sum(c => c.Attacker.Units.Concat(c.Defender.Units).Count(u => CBombing.IsBombing(c.Kind) ? u.Bombing == c : u.Combats.Contains(c)))}");

		string Entry(long hour, int kind, int province, string attacker, string defender, bool convoy)
			=> $"{kind} {province} {attacker}>{defender} {hour}{(convoy ? " convoy" : "")}";
		var savedHistory = new List<string>();
		foreach (ClausewitzNode entry in block.Get("history")?.Get("history")?.Children ?? [])
		{
			if (entry.Key is null && entry.IsBlock && CSaveValues.TryHours(entry.Get("end_date")?.Value?.Trim('"'), out long hour))
			{
				savedHistory.Add(Entry(hour, entry.GetInt("type"), entry.GetInt("location"), entry.Get("attacker")?.Value?.Trim('"') ?? "",
					entry.Get("defender")?.Value?.Trim('"') ?? "", entry.Get("convoy")?.Value == "yes"));
			}
		}
		Line("finished battles, in order", state.CombatHistory.Entries.Select(e => Entry(e.Hour, (int)e.Kind + 1, e.Province,
			e.Attacker?.ToString() ?? "---", e.Defender?.ToString() ?? "---", e.IsConvoy)), savedHistory);

		var savedSunk = new List<string>();
		foreach (ClausewitzNode entry in save.Get("sunk_ships")?.Children ?? [])
		{
			if (entry.Key is { } owner && entry.IsBlock)
			{
				savedSunk.Add($"{owner} {ids.Of(entry.Get("id"))} {entry.Get("type")?.Value?.Trim('"')} {CSaveValues.Fixed(entry, "strength")} " +
					$"by {(entry.Get("sunk_by") is { } by ? ids.Of(by) : 0)}");
			}
		}
		Line("sunk ships: owner, id, type, strength, sinker", state.SunkShips.Select(s => $"{s.Owner?.ToString() ?? "---"} {s.Ship.Id} " +
			$"{s.Ship.Type.Key} {s.Ship.Strength} by {s.Ship.SunkById}"), savedSunk);
	}

	/// <summary>
	/// Every unit, brigade, leader in service and line of the queue the save has, found in the game by its number and set
	/// beside it. With <paramref name="exact"/> a miss is a fault of the reading, and the first few are shown.
	/// </summary>
	private static void CompareUnits(CGameState state, ClausewitzNode save, string indent, bool exact)
	{
		var ids = new CSaveIds();
		foreach (ClausewitzNode node in save.Children)
		{
			if (node.Key == "unit")
			{
				ids.SetHistoryNext((int)CSaveValues.Whole(node.Value));
			}
			else if (node.Key == "id" && node.IsBlock)
			{
				ids.SetHighest(node.GetInt("id"), node.GetInt("type"));
			}
		}
		var ours = new Dictionary<int, CUnit>();
		var brigades = new Dictionary<int, CSubUnit>();
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits().Concat(country.Deployments))
			{
				ours[unit.Id] = unit;
				foreach (CSubUnit subUnit in unit.SubUnits)
				{
					brigades[subUnit.Id] = subUnit;
				}
			}
		}
		var tally = new SortedDictionary<string, (int Same, int All)>(StringComparer.Ordinal);
		var examples = new Dictionary<string, List<string>>();
		var byCountry = new Dictionary<string, Dictionary<string, int>>();
		void Count(string what, bool same, Func<string> detail)
		{
			(int s, int a) = tally.GetValueOrDefault(what);
			tally[what] = (s + (same ? 1 : 0), a + 1);
			if (!same)
			{
				// Each detail starts with the country's tag, which the misses are counted by.
				string tag = detail()[..3];
				Dictionary<string, int> countries = byCountry.TryGetValue(what, out Dictionary<string, int>? had) ? had
					: byCountry[what] = new Dictionary<string, int>();
				countries[tag] = countries.GetValueOrDefault(tag) + 1;
				List<string> list = examples.TryGetValue(what, out List<string>? found) ? found : examples[what] = new List<string>();
				if (ExampleCountry is { } only ? tag == only && list.Count < 12 : list.Count < 3)
				{
					list.Add(detail());
				}
			}
		}

		int seen = 0;
		var inSave = new HashSet<int>();
		void Walk(ClausewitzNode node, string tag, bool deployed)
		{
			seen++;
			int id = ids.Of(node.Get("id"));
			string name = node.Get("name")?.Value ?? "";
			string who = $"{tag} {id} {name}";
			bool found = ours.TryGetValue(id, out CUnit? unit);
			inSave.Add(id);
			Count("a unit is found by its number", found, () => who);
			if (unit is not null)
			{
				if (!deployed)
				{
					int location = node.GetInt("location");
					Count("its location", unit.Location == location, () => $"{who}: ours {unit.Location}, save {location}");
				}
				List<int> path = node.Get("path")?.Children.Select(step => (int)CSaveValues.Whole(step.Value)).ToList() ?? new List<int>();
				Count("its path", unit.Path.SequenceEqual(path), () => $"{who}: ours [{string.Join(' ', unit.Path)}], save [{string.Join(' ', path)}]");
				CFixedPoint progress = CSaveValues.Fixed(node, "movement_progress");
				Count("its movement progress", unit.MovementProgress == progress, () => $"{who}: ours {unit.MovementProgress}, save {progress}");
				int leader = node.Get("leader")?.GetInt("id") ?? 0;
				Count("its leader", (unit.Leader?.Id ?? 0) == leader, () => $"{who}: ours {unit.Leader?.Id ?? 0}, save {leader}");
				string order = node.Children.FirstOrDefault(child => child.Key is { } key && child.IsBlock
					&& CSaveOrders.Kinds.ContainsKey(key))?.Key ?? "";
				string ourOrder = CSaveOrders.Kinds.FirstOrDefault(pair => pair.Value == unit.Order).Key ?? "";
				Count("its order", order == ourOrder || order.Length == 0 && unit.Order == EUnitOrder.None,
					() => $"{who}: ours {unit.Order}, save '{order}'");
			}
			foreach (ClausewitzNode child in node.Children)
			{
				if (child.Key is not { } key || !child.IsBlock)
				{
					continue;
				}
				if (CSaveUnitReader.UnitKinds.ContainsKey(key))
				{
					Walk(child, tag, deployed);
				}
				else if (key is "regiment" or "ship" or "wing")
				{
					int brigadeId = ids.Of(child.Get("id"));
					string brigade = $"{who} / {brigadeId} {child.Get("name")?.Value}";
					bool has = brigades.TryGetValue(brigadeId, out CSubUnit? ourBrigade);
					Count("a brigade is found by its number", has, () => brigade);
					if (ourBrigade is not null)
					{
						Count("a brigade is under its unit", ourBrigade.Unit == unit, () => brigade);
						Same("a brigade's strength", ourBrigade.Strength, "strength");
						Same("a brigade's organisation", ourBrigade.Organisation, "organisation");
						Same("a brigade's experience", ourBrigade.Experience, "experience");
					}
					foreach (ClausewitzNode air in child.All("air").Where(air => air.IsBlock))
					{
						Walk(air, tag, deployed);
					}

					void Same(string what, CFixedPoint value, string key)
					{
						CFixedPoint saved = CSaveValues.Fixed(child, key);
						Count(what, value == saved, () => $"{brigade}: ours {value}, save {saved}");
					}
				}
			}
		}

		foreach (ClausewitzNode node in save.Children)
		{
			if (!CCountryTag.TryParse(node.Key, out CCountryTag tag) || state.Country(tag) is not { } country || !node.IsBlock)
			{
				continue;
			}
			var lines = new List<ClausewitzNode>();
			foreach (ClausewitzNode child in node.Children)
			{
				if (child.Key is not { } key || !child.IsBlock)
				{
					continue;
				}
				if (CSaveUnitReader.UnitKinds.ContainsKey(key))
				{
					Walk(child, tag.Value, deployed: false);
				}
				else if (key == "unit_deployment")
				{
					foreach (ClausewitzNode unit in child.Children.Where(u => u.Key is { } k && CSaveUnitReader.UnitKinds.ContainsKey(k)))
					{
						Walk(unit, tag.Value, deployed: true);
					}
				}
				else if (key is "military_construction" or "building_construction" or "convoy_construction")
				{
					lines.Add(child);
				}
				else if (key == "active_leaders")
				{
					List<int> saved = child.Children.Where(entry => entry.IsBlock && int.TryParse(entry.Key, out _))
						.Select(entry => int.Parse(entry.Key!, System.Globalization.CultureInfo.InvariantCulture)).ToList();
					Count("a country's leaders in service, in order", country.ActiveLeaders.Select(l => l.Id).SequenceEqual(saved),
						() => $"{tag}: ours {country.ActiveLeaders.Count}, save {saved.Count}");
					foreach (ClausewitzNode entry in child.Children.Where(entry => entry.IsBlock))
					{
						if (int.TryParse(entry.Key, out int leaderId) && state.Leaders.TryGetValue(leaderId, out OpenHOI3.Sim.Leaders.CLeader? leader))
						{
							CFixed15 experience = CSaveValues.Fixed15(entry.Get("current_experience")?.Value);
							Count("a leader's experience", leader.Experience == experience,
								() => $"{tag} {leaderId} {leader.Name}: ours {leader.Experience}, save {experience}");
							int rank = entry.GetInt("rank");
							Count("a leader's rank", leader.Rank == rank, () => $"{tag} {leaderId}: ours {leader.Rank}, save {rank}");
						}
					}
				}
			}
			Count("a country's lines of the queue", country.Constructions.Count == lines.Count,
				() => $"{tag}: ours {country.Constructions.Count}, save {lines.Count}");
			for (int i = 0; i < Math.Min(lines.Count, country.Constructions.Count); i++)
			{
				OpenHOI3.Sim.Construction.CConstruction ourLine = country.Constructions[i];
				CFixedPoint progress = CSaveValues.Fixed(lines[i], "progress");
				Count("a line's progress", ourLine.Progress == progress,
					() => $"{tag} line {i} {ourLine}: ours {ourLine.Progress}, save {progress}");
				int id = ids.Of(lines[i].Get("id"));
				Count("a line's number", ourLine.Id == id, () => $"{tag} line {i}: ours {ourLine.Id}, save {id}");
			}
		}
		int ourCount = state.Countries.Sum(country => country.AllUnits().Count() + country.Deployments.Count);
		Console.WriteLine($"{indent}{seen} unit blocks in the save, {ourCount} units in the game");
		foreach (CUnit extra in ours.Values.Where(unit => !inSave.Contains(unit.Id)).Take(5))
		{
			Console.WriteLine($"{indent}  ours and not the save's: {extra.Owner} {extra.Id} {extra.Name} ({extra.Kind}, " +
				$"{extra.SubUnits.Count} brigades, in {extra.Location}, parent {extra.Parent?.Name ?? "none"})");
		}
		foreach ((string what, (int same, int all)) in tally)
		{
			Console.WriteLine($"{indent}{what}: {same} of {all}");
			if (byCountry.TryGetValue(what, out Dictionary<string, int>? countries) && countries.Count > 1)
			{
				Console.WriteLine($"{indent}    by country: " + string.Join(" ", countries.OrderByDescending(pair => pair.Value)
					.Take(8).Select(pair => $"{pair.Key} {pair.Value}")));
			}
			foreach (string example in examples.GetValueOrDefault(what) ?? new List<string>())
			{
				Console.WriteLine($"{indent}    {example}");
			}
		}
	}
}
