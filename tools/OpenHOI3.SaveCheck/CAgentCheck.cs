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
		public CSavedUnit? Parent;
		public List<CSavedUnit> Children { get; } = new();
		public ClausewitzNode? Plan;
	}

	private static readonly string[] UnitKeys = { "theatre", "armygroup", "army", "corps", "division", "navy", "air" };

	public static int Run(string[] args)
	{
		if (args.Length < 4)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck agents <install folder> <start-date savegame> <later savegame> [tag] [examples]");
			return 2;
		}
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
			played: new[] { new CCountryTag(start.GetString("player")) });
		state.IsRunning = true;
		var simulation = new CSimulation(state);
		int hours = HoursTo(state, later.GetString("date"));
		simulation.RunHours(hours);
		Console.WriteLine($"a new game made and run {hours} hours to {state.Date} in {clock.ElapsedMilliseconds} ms; " +
			$"savegame {later.GetString("date")}, player {later.GetString("player")}");

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
						$"{unit.Parent?.Name ?? "-"}: {string.Join(" ", unit.SubUnits.Select(s => s.Type.Key))}");
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
		List<int> min = Numbers(plan.Get("min"));
		string ourMin = Wants(ours.Min);
		string theirMin = Wants(min);
		count("min", ourMin == theirMin, $"{where}: ours {ourMin}, save {theirMin}");
	}

	private static string Wants(IReadOnlyList<int> min)
	{
		return string.Join(" ", min.Select((count, index) => (count, index)).Where(p => p.count != 0).Select(p => $"{p.index}:{p.count}"));
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
				Parent = parent,
				Plan = child.Get("plan") is { IsBlock: true } plan ? plan : null,
			};
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
