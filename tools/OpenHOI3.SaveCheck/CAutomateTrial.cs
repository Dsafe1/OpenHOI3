using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

/// <summary>
/// Hands a played country's theatre headquarters to the AI, and says what its units do:
///
///   OpenHOI3.SaveCheck automate &lt;install folder&gt; [country] [objective's country] [days] [seed]
///
/// It is a trial: no save has an automated headquarters. The country's theatre with the most land units under it is handed over
/// and, given another country, that country's capital is made its objective by hand. Each day says how many of its divisions are
/// marching and how many have left where they stood, and what its area is. Germany and Poland for ten days with nothing said. See
/// <c>docs/mechanics/mapunits.md</c>, section 12.
/// </summary>
public static class CAutomateTrial
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck automate <install folder> [country] [objective's country] [days] [seed]");
			return 2;
		}
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		var tag = new CCountryTag(args.Length > 2 ? args[2].ToUpperInvariant() : "GER");
		using var scripts = new CAiScripts(directories);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts, played: new[] { tag });
		if (args.Length > 5 && uint.TryParse(args[5], out uint seed))
		{
			state.Seed = seed;
		}
		var simulation = new CSimulation(state);
		int days = args.Length > 4 && int.TryParse(args[4], out int wanted) ? wanted : 10;
		CCountry country = state.Country(tag)!;
		// Past the first hours, when a played country's theatres have been made.
		simulation.RunHours(2);
		CUnit? theatre = country.AllUnits().Where(unit => unit.Kind == EUnitKind.Theatre && CAgents.TheatreHeadedBy(state, country, unit) is not null)
			.OrderByDescending(unit => Divisions(unit).Count()).FirstOrDefault();
		if (theatre is null)
		{
			Console.WriteLine($"{country.Name} has no theatre with a headquarters.");
			return 1;
		}
		simulation.Post(new CAutomateUnitCommand(tag, theatre.Id));
		simulation.Pump();
		Console.WriteLine($"{theatre.Name}: {Divisions(theatre).Count()} divisions, stance {theatre.Plan.Stance}, area {theatre.Plan.Area.Count} provinces.");
		if (args.Length > 3 && state.Country(new CCountryTag(args[3].ToUpperInvariant())) is { } target)
		{
			simulation.Post(new CTogglePlanObjectiveCommand(tag, theatre.Id, target.Capital));
			simulation.Pump();
			Console.WriteLine($"Objective: {state.Province(target.Capital).Name}. Area now {theatre.Plan.Area.Count} provinces.");
		}
		Dictionary<int, int> started = Divisions(theatre).ToDictionary(unit => unit.Id, unit => unit.Location);
		for (int day = 1; day <= days; day++)
		{
			simulation.RunHours(CGameState.HoursPerDay);
			var divisions = Divisions(theatre).ToList();
			int moved = divisions.Count(unit => started.TryGetValue(unit.Id, out int was) && was != unit.Location);
			string area = string.Join(", ", theatre.Plan.Area.Select(id => state.Province(id).Name));
			Console.WriteLine($"{state.Date}  marching {divisions.Count(unit => unit.IsMoving)}, moved from where they stood {moved} of {divisions.Count}, area {theatre.Plan.Area.Count}: {area}");
		}
		foreach (string problem in warnings.Take(5))
		{
			Console.WriteLine("warning: " + problem);
		}
		return 0;
	}

	private static IEnumerable<CUnit> Divisions(CUnit unit)
	{
		foreach (CUnit child in unit.Children)
		{
			if (child.Kind == EUnitKind.Division)
			{
				yield return child;
			}
			foreach (CUnit under in Divisions(child))
			{
				yield return under;
			}
		}
	}
}
