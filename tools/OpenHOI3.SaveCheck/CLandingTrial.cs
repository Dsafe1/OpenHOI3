using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

/// <summary>
/// Puts a division aboard an install's own transport fleet and lands it on an enemy's coast, and says how it went:
///
///   OpenHOI3.SaveCheck landing &lt;install folder&gt; [country] [enemy] [hand|mission][-defended] [days] [seed]
///
/// It is a trial and not a check: no savegame at hand has a landing in it. The two countries are put at war; the
/// country's fleet that carries most, lying in a port, takes aboard a division of that port - one is brought there if
/// none stands in it - and the coast is the enemy's province nearest by sea to the fleet's port that a landing may be
/// made on. By hand, the fleet is sent to the sea beside it and the division ordered ashore from there, as the player
/// would; with <c>mission</c>, the fleet is given the invasion. With nothing said it is France against Siam, by hand,
/// for ten days. The enemy player is the enemy, so the warning is told. See <c>docs/mechanics/transport.md</c>.
/// </summary>
public static class CLandingTrial
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck landing <install folder> [country] [enemy] [hand|mission] [days] [seed]");
			return 2;
		}
		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add);
		var simulation = new CSimulation(state);

		var tag = new CCountryTag(args.Length > 2 ? args[2].ToUpperInvariant() : "FRA");
		var enemyTag = new CCountryTag(args.Length > 3 ? args[3].ToUpperInvariant() : "SIA");
		bool byMission = args.Length > 4 && args[4].StartsWith("mission", StringComparison.OrdinalIgnoreCase);
		// "hand-defended" or "mission-defended": the nearest coast with an enemy land unit on it.
		bool defended = args.Length > 4 && args[4].EndsWith("defended", StringComparison.OrdinalIgnoreCase);
		int days = args.Length > 5 && int.TryParse(args[5], out int wanted) ? wanted : 10;
		if (args.Length > 6 && uint.TryParse(args[6], out uint seed))
		{
			state.Seed = seed;
		}
		if (state.Country(tag) is not { } country || state.Country(enemyTag) is not { } enemy)
		{
			Console.Error.WriteLine($"no such country: {tag} or {enemyTag}");
			return 2;
		}
		state.Played.Clear();
		state.Played.Add(enemyTag);
		// The landing's own country played too, as orders given by hand are a player's: run by the AI, its fleet AI takes the
		// transports home mid-landing.
		state.Played.Add(tag);
		state.PactMessaged += message =>
		{
			if (message.We == CInvasionWarnings.Message)
			{
				Console.WriteLine($"{Now(state)}  {enemy.Name} is told {message.We}: "
					+ string.Join(", ", message.Values.Select(value => $"{value.Name}={value.Value}")));
			}
		};
		if (!CMovementRules.IsEnemy(state, tag, enemyTag))
		{
			simulation.Post(new CStartWarCommand(tag, enemyTag));
			simulation.Pump();
			Console.WriteLine($"{country.Name} and {enemy.Name} were at peace and are now at war.");
		}

		// Of the fleets in port that carry anything, the one with the enemy's coast nearest.
		CUnit? fleet = null;
		(CProvince Coast, CProvince Sea)? landing = null;
		long best = long.MaxValue;
		foreach (CUnit candidate in country.AllUnits().Where(unit => unit.Kind == EUnitKind.Navy && unit.Location > 0
			&& !state.Province(unit.Location).IsSea && CTransport.Capacity(unit) > 0).OrderBy(unit => unit.Id))
		{
			if (Nearest(state, country, enemyTag, defended, candidate) is ({ } near, { } water, long distance) && distance < best)
			{
				best = distance;
				fleet = candidate;
				landing = (near, water);
			}
		}
		if (fleet is null || landing is not ({ } coast, { } beside))
		{
			Console.WriteLine($"{country.Name} has no fleet in port that carries anything and reaches {enemy.Name}'s coast.");
			return 1;
		}
		CProvince port = state.Province(fleet.Location);
		CUnit? division = state.UnitsIn(port.Id).FirstOrDefault(unit => unit.Kind == EUnitKind.Division && CTransport.CanCarry(fleet, unit))
			?? country.AllUnits().FirstOrDefault(unit => unit.Kind == EUnitKind.Division && CTransport.CanCarry(fleet, unit));
		if (division is null)
		{
			Console.WriteLine($"{country.Name} has no division {fleet.Name} can carry.");
			return 1;
		}
		if (division.Location != port.Id)
		{
			Console.WriteLine($"{division.Name} is brought from {state.Province(division.Location).Name} to {port.Name}.");
			state.Positions.Move(division, division.Location, port.Id);
			division.Location = port.Id;
		}

		Console.WriteLine($"{Now(state)}  {fleet.Name} ({string.Join('+', fleet.SubUnits.Select(ship => ship.Type.Key).Distinct())}), "
			+ $"in {port.Name}, carries {CTransport.Capacity(fleet)}; {division.Name} weighs {CTransport.Weight(division)}. "
			+ $"The coast is {coast.Name} ({coast.Id}), held by {enemy.Name} with {Defenders(state, coast, tag)} land units, "
			+ $"from {beside.Name} ({beside.Id}).");

		Post(simulation, new CLoadUnitCommand(tag, division.Id, fleet.Id), "load");
		Console.WriteLine($"{Now(state)}  aboard: {division.Transport?.Name ?? "nothing"}; the fleet has {fleet.Passengers.Count}.");
		if (byMission)
		{
			Post(simulation, new CSetUnitOrderCommand(tag, fleet.Id, EUnitOrder.Invasion, coast.Id), "invasion");
			Console.WriteLine($"{Now(state)}  invasion given; it lands from {state.Province(fleet.Mission.LandingSea).Name} ({fleet.Mission.LandingSea}).");
		}
		else
		{
			Post(simulation, new CMoveUnitCommand(tag, fleet.Id, beside.Id), "sail");
		}

		int lastFleet = fleet.Location;
		bool ordered = byMission;
		long lastProgress = -1;
		int battles = 0;
		for (int hour = 1; hour <= days * CGameState.HoursPerDay && !division.IsRemoved; hour++)
		{
			simulation.RunHours(1);
			if (fleet.Location != lastFleet)
			{
				lastFleet = fleet.Location;
				Console.WriteLine($"{Now(state)}  {fleet.Name} in {state.Province(lastFleet).Name}; {division.Name} in "
					+ $"{state.Province(division.Location).Name}{(division.Transport is null ? ", ashore" : ", aboard")}.");
			}
			if (!ordered && fleet.Location == beside.Id && !fleet.IsMoving)
			{
				ordered = true;
				Post(simulation, new CMoveUnitCommand(tag, division.Id, coast.Id), "land");
				Console.WriteLine($"{Now(state)}  {division.Name} ordered ashore: {CMovement.HoursToArrive(state, country, division)} hours "
					+ $"at {CMovementSpeed.Speed(state, country, division)} an hour over {CMovement.Distance(state, division)}.");
			}
			if (division.IsFighting && battles == 0 || division.Combats.Count > battles)
			{
				battles = division.Combats.Count;
				Console.WriteLine($"{Now(state)}  a battle for {coast.Name}: attack scale {division.LandingAttack}, "
					+ $"defence scale {division.LandingDefence}, progress {CLanding.Progress(state, division)}.");
			}
			long progress = CLanding.IsLanding(state, division) ? CLanding.Progress(state, division) : -1;
			if (progress >= 0 && progress / 250 != lastProgress / 250)
			{
				Console.WriteLine($"{Now(state)}  progress {progress} thousandths{(division.IsFighting ? ", fighting" : "")}.");
			}
			lastProgress = progress;
			if (division.Transport is null && division.Location == coast.Id)
			{
				Console.WriteLine($"{Now(state)}  {division.Name} is ashore in {coast.Name} after {hour} hours; "
					+ $"{coast.Name} is held by {coast.Controller}. The sea's pool has {beside.Pool} and the coast's {coast.Pool}.");
				break;
			}
		}
		Console.WriteLine($"{Now(state)}  {division.Name}: {(division.IsRemoved ? "removed" : $"in {state.Province(division.Location).Name}, {(division.Transport is null ? "ashore" : "aboard")}")}; "
			+ $"{fleet.Name}: {(fleet.IsRemoved ? "removed" : $"in {state.Province(fleet.Location).Name}, order {fleet.Order}")}.");
		return 0;
	}

	/// <summary>The enemy's land nearest by sea to a fleet's port, with sea beside it the fleet may go to, and how far.</summary>
	private static (CProvince Coast, CProvince Sea, long Distance)? Nearest(CGameState state, CCountry country, CCountryTag enemy, bool defended,
		CUnit fleet)
	{
		if (CMovementRules.SeaEnd(state, state.Province(fleet.Location)) is not { } home)
		{
			return null;
		}
		(CProvince, CProvince, long)? found = null;
		long best = long.MaxValue;
		foreach (CProvince province in state.Provinces)
		{
			if (province.IsSea || province.Controller != enemy || (defended && Defenders(state, province, country.Tag) == 0))
			{
				continue;
			}
			foreach (CProvinceEdge edge in province.Edges)
			{
				if (edge.To <= 0 || edge.To >= state.Provinces.Count || !state.Province(edge.To).IsSea)
				{
					continue;
				}
				CProvince sea = state.Province(edge.To);
				long distance = state.NavalDistances.Between(home, sea);
				if (distance < best && CUnitOrders.IsInRange(state, fleet, EUnitOrder.Invasion, province)
					&& CMovementRules.MayMoveTo(state, country, fleet, sea))
				{
					best = distance;
					found = (province, sea, distance);
				}
			}
		}
		return found;
	}

	private static void Post(CSimulation simulation, CCommand command, string what)
	{
		if (!command.MayExecute(simulation.State))
		{
			Console.WriteLine($"{Now(simulation.State)}  {what}: refused.");
			return;
		}
		simulation.Post(command);
		simulation.Pump();
	}

	private static int Defenders(CGameState state, CProvince province, CCountryTag attacker)
	{
		return state.UnitsIn(province.Id).Count(unit => CMovementRules.IsLandUnit(unit) && CMovementRules.IsEnemy(state, unit.Owner, attacker));
	}

	private static string Now(CGameState state)
	{
		return $"{state.Date} {state.Hour}:00";
	}
}
