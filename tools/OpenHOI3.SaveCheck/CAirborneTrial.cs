using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

/// <summary>
/// The carrier and carrying by air on an install's own map and units, telling each hour of it:
///
///   OpenHOI3.SaveCheck airborne &lt;install folder&gt; [paradrop|carrier|supply][-defended] [country] [enemy] [days] [seed]
///
/// It is a trial and not a check: no savegame at hand has a drop, a carrier group rebased or an air supply in it, and no
/// 1936 order of battle has a transport plane or a paratrooper. <c>paradrop</c> makes a wing of the install's
/// <c>transport_plane</c> and a division of three <c>paratrooper_brigade</c>s at the country's air base nearest the
/// enemy, loads the division and drops it on the enemy's nearest province - one with a land unit of the enemy's in it with
/// <c>-defended</c> - at the earliest start the window allows: Germany on Poland with nothing said. <c>carrier</c> makes
/// a <c>cag</c> wing at the air base nearest the country's first carrier in port, rebases it to the carrier, sails the
/// fleet a sea province and then takes the fleet away: the United Kingdom with nothing said. <c>supply</c> runs a day,
/// makes a transport wing at the country's capital and gives it the air supply over every land province it holds within
/// the wing's range. See <c>docs/mechanics/transport.md</c>, section 13.
/// </summary>
public static class CAirborneTrial
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck airborne <install folder> [paradrop|carrier|supply][-defended] [country] [enemy] [days] [seed]");
			return 2;
		}
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add);
		var simulation = new CSimulation(state);

		string what = args.Length > 2 ? args[2].ToLowerInvariant() : "paradrop";
		bool defended = what.EndsWith("-defended", StringComparison.Ordinal);
		string kind = what.Split('-')[0];
		var tag = new CCountryTag(args.Length > 3 ? args[3].ToUpperInvariant() : kind == "carrier" ? "ENG" : "GER");
		var enemyTag = new CCountryTag(args.Length > 4 ? args[4].ToUpperInvariant() : "POL");
		int days = args.Length > 5 && int.TryParse(args[5], out int wanted) ? wanted : 5;
		if (args.Length > 6 && uint.TryParse(args[6], out uint seed))
		{
			state.Seed = seed;
		}
		if (state.Country(tag) is not { } country)
		{
			Console.Error.WriteLine($"no such country: {tag}");
			return 2;
		}
		return kind switch
		{
			"carrier" => Carrier(state, simulation, country, days),
			"supply" => Supply(state, simulation, country, days),
			_ => Paradrop(state, simulation, country, enemyTag, defended, days),
		};
	}

	private static int Paradrop(CGameState state, CSimulation simulation, CCountry country, CCountryTag enemyTag, bool defended, int days)
	{
		if (state.Country(enemyTag) is not { } enemy)
		{
			Console.Error.WriteLine($"no such country: {enemyTag}");
			return 2;
		}
		state.Played.Clear();
		state.Played.Add(enemyTag);
		// The trial's own country played too, as orders given by hand are a player's: run by the AI, its air AI takes the
		// transport planes off the drop.
		state.Played.Add(country.Tag);
		state.PactMessaged += message =>
		{
			if (message.We == CParadrop.Message)
			{
				Console.WriteLine($"{Now(state)}  {enemy.Name} is told {message.We}: "
					+ string.Join(", ", message.Values.Select(value => $"{value.Name}={value.Value}")));
			}
		};
		if (!CMovementRules.IsEnemy(state, country.Tag, enemyTag))
		{
			simulation.Post(new CStartWarCommand(country.Tag, enemyTag));
			simulation.Pump();
			Console.WriteLine($"{country.Name} and {enemy.Name} were at peace and are now at war.");
		}

		// The country's air base and the enemy's province nearest each other, the second in the plane's range.
		CUnit probe = Make(state, country, EUnitKind.Air, "transport_plane", 1, 0);
		(CProvince Base, CProvince Target)? pair = null;
		long best = long.MaxValue;
		foreach (CProvince home in AirBases(state, country))
		{
			probe.Base = home.Id;
			foreach (CProvince target in state.Provinces.Where(province => !province.IsSea && province.Controller == enemyTag
				&& (!defended || Defenders(state, province, country.Tag) > 0)))
			{
				long distance = CMapDistance.Between(home, target, state.MapWidth);
				if (distance < best && CUnitOrders.IsInRange(state, probe, EUnitOrder.AirSuperiority, target))
				{
					best = distance;
					pair = (home, target);
				}
			}
		}
		Remove(state, country, probe);
		if (pair is not ({ } airBase, { } goal))
		{
			Console.WriteLine($"{country.Name} has no air base within a transport plane's range of {enemy.Name}'s land.");
			return 1;
		}
		CUnit plane = Make(state, country, EUnitKind.Air, "transport_plane", 1, airBase.Id);
		CUnit division = Make(state, country, EUnitKind.Division, "paratrooper_brigade", 3, airBase.Id);
		Console.WriteLine($"{Now(state)}  {plane.Name} at {airBase.Name} ({airBase.Id}) carries {CTransport.Capacity(plane)}; "
			+ $"{division.Name} weighs {CTransport.Weight(division)}. The target is {goal.Name} ({goal.Id}), {best} away, held by "
			+ $"{enemy.Name} with {Defenders(state, goal, country.Tag)} land units.");

		Post(simulation, new CLoadOnPlaneCommand(country.Tag, division.Id, plane.Id), "load");
		Console.WriteLine($"{Now(state)}  aboard: {division.Plane?.Name ?? "nothing"}.");
		long start = CParadrop.EarliestStart(state);
		Post(simulation, new CSetUnitOrderCommand(country.Tag, plane.Id, EUnitOrder.Paradrop, goal.Id, 1, 0, 0,
			new[] { goal.Id }, CMission.DayAndNight, CMission.NoEnd, start), "paradrop");
		Console.WriteLine($"{Now(state)}  paradrop given, to start at {CDate.FromDayNumber((int)(start / CGameState.HoursPerDay))} "
			+ $"{start % CGameState.HoursPerDay}:00.");

		int lastPlane = plane.Location;
		bool dropped = false;
		for (int hour = 1; hour <= days * CGameState.HoursPerDay; hour++)
		{
			simulation.RunHours(1);
			if (plane.IsRemoved)
			{
				Console.WriteLine($"{Now(state)}  {plane.Name} is gone; {division.Name} {(division.IsRemoved ? "with it" : "is not")}.");
				break;
			}
			if (plane.Location != lastPlane)
			{
				lastPlane = plane.Location;
				Console.WriteLine($"{Now(state)}  {plane.Name} over {state.Province(lastPlane).Name}"
					+ $"{(division.Plane is null ? "" : $", {division.Name} aboard")}.");
			}
			if (!dropped && division.Plane is null)
			{
				dropped = true;
				Console.WriteLine($"{Now(state)}  dropped on {state.Province(division.Location).Name}: marked {division.WasParadropped}, "
					+ $"attack delay {division.AttackDelay}, {(division.IsFighting ? "in a battle" : "in no battle")}; "
					+ $"{goal.Name} is held by {goal.Controller} and its pool has {goal.Pool[EGoods.Supplies]} supplies; "
					+ $"the planes are at {CUnitTotals.AverageOrganisation(plane)} organisation.");
			}
			if (dropped && !division.WasParadropped && division.SubUnits.Count > 0)
			{
				Console.WriteLine($"{Now(state)}  {division.Name} settled: organisation {CUnitTotals.AverageOrganisation(division)}.");
				break;
			}
		}
		Console.WriteLine($"{Now(state)}  {plane.Name}: {(plane.IsRemoved ? "removed" : $"in {state.Province(plane.Location).Name}, order {plane.Order}")}; "
			+ $"{division.Name}: {(division.IsRemoved ? "removed" : $"in {state.Province(division.Location).Name}")}.");
		return 0;
	}

	private static int Carrier(CGameState state, CSimulation simulation, CCountry country, int days)
	{
		CUnit? fleet = country.AllUnits().FirstOrDefault(unit => unit.Kind == EUnitKind.Navy && unit.Location > 0
			&& !state.Province(unit.Location).IsSea && unit.SubUnits.Any(CShipCensus.IsCarrier));
		if (fleet is null)
		{
			Console.WriteLine($"{country.Name} has no carrier in port.");
			return 1;
		}
		CProvince port = state.Province(fleet.Location);
		CProvince? airBase = AirBases(state, country).Where(province => province.Id != port.Id)
			.OrderBy(province => CMapDistance.Between(port, province, state.MapWidth))
			.FirstOrDefault();
		if (airBase is null)
		{
			Console.WriteLine($"{country.Name} has no air base.");
			return 1;
		}
		foreach (CSubUnit ship in fleet.SubUnits.Where(CShipCensus.IsCarrier))
		{
			Console.WriteLine($"{Now(state)}  {fleet.Name} in {port.Name}: {ship.Name} holds {ship.Stats[CShipCensus.CarrierSize]} "
				+ $"wings, aboard {string.Join(", ", ship.Carried.Select(unit => $"{unit.Name} ({unit.SubUnits.Count} wing, base {unit.Base})"))}.");
		}
		// A group already aboard is taken off to an airfield first; otherwise one is made at the airfield.
		CUnit? group = fleet.SubUnits.Where(CShipCensus.IsCarrier).SelectMany(ship => ship.Carried).FirstOrDefault();
		if (group is { Name.Length: 0 })
		{
			group.Name = $"air group {group.Id}";      // the start date's groups aboard have no name
		}
		if (group is not null)
		{
			simulation.RunHours(1);
			Console.WriteLine($"{Now(state)}  {group.Name}, with no order aboard, has {group.Order} for {state.Province(group.OrderProvince).Name}.");
			Post(simulation, new CSetUnitOrderCommand(country.Tag, group.Id, EUnitOrder.RebaseAir, airBase.Id), "rebase to the airfield");
			Watch(state, simulation, group, days, EUnitOrder.RebaseAir);
			Console.WriteLine($"{Now(state)}  off the carrier: aboard {group.CarriedBy?.Name ?? "nothing"}, in {state.Province(group.Location).Name}, "
				+ $"based at {state.Province(group.Base).Name}, organisation {CUnitTotals.AverageOrganisation(group)}, order {group.Order}.");
		}
		else
		{
			group = Make(state, country, EUnitKind.Air, "cag", 1, airBase.Id);
			Console.WriteLine($"{Now(state)}  {group.Name} made at {airBase.Name}.");
		}
		Console.WriteLine($"{Now(state)}  a carrier with room in {port.Name}: {CCarriers.FindCarrierWithRoom(state, group, port)?.Name ?? "none"}.");
		Post(simulation, new CSetUnitOrderCommand(country.Tag, group.Id, EUnitOrder.RebaseCarrier, port.Id, 1, 0, 0,
			new[] { port.Id }, CMission.DayAndNight, CMission.NoEnd, state.TotalHours), "rebase to carrier");
		Console.WriteLine($"{Now(state)}  given: aboard {group.CarriedBy?.Name ?? "nothing"}, based at {state.Province(group.Base).Name}, "
			+ $"on its way {group.Path.Count} provinces.");
		Watch(state, simulation, group, days, EUnitOrder.RebaseCarrier);
		simulation.RunHours(1);
		Console.WriteLine($"{Now(state)}  {group.Name} in {state.Province(group.Location).Name}, at its base {CAirMissions.IsAtOwnBase(group)}, "
			+ $"order {group.Order} for {state.Province(group.OrderProvince).Name}, organisation {CUnitTotals.AverageOrganisation(group)}.");
		if (CMovementRules.SeaEnd(state, port) is { } sea)
		{
			Post(simulation, new CMoveUnitCommand(country.Tag, fleet.Id, sea.Id), "sail");
			for (int hour = 1; hour <= 24 && fleet.Location != sea.Id; hour++)
			{
				simulation.RunHours(1);
			}
			simulation.RunHours(1);
			Console.WriteLine($"{Now(state)}  {fleet.Name} in {sea.Name}; {group.Name} in {state.Province(group.Location).Name}, "
				+ $"base {group.Base}, at its base {CAirMissions.IsAtOwnBase(group)}, order {group.Order} for {group.OrderProvince}.");
		}
		CCombatRetreat.RemoveFromGame(state, fleet);
		Console.WriteLine($"{Now(state)}  {fleet.Name} taken away: {group.Name} aboard {group.CarriedBy?.Name ?? "nothing"}, base "
			+ $"{(group.Base == 0 ? "none" : state.Province(group.Base).Name)}, order {group.Order}.");
		return 0;
	}

	private static int Supply(CGameState state, CSimulation simulation, CCountry country, int days)
	{
		simulation.RunHours(CGameState.HoursPerDay);
		CProvince capital = state.Province(country.ActingCapital);
		CUnit plane = Make(state, country, EUnitKind.Air, "transport_plane", 2, capital.Id);
		var area = state.Provinces.Where(province => !province.IsSea && province.Controller == country.Tag
			&& CUnitOrders.IsInRange(state, plane, EUnitOrder.AirSupply, province)).Select(province => province.Id).ToList();
		Console.WriteLine($"{Now(state)}  {plane.Name} at {capital.Name} carries {CAirSupply.Capability(plane) / CFixedPoint.Scale}; "
			+ $"its area is {area.Count} provinces, {area.Count(id => CAirSupply.Shortfall(state.Province(id)) > 0)} short today.");
		Post(simulation, new CSetUnitOrderCommand(country.Tag, plane.Id, EUnitOrder.AirSupply, capital.Id, 1, 0, 0,
			area, CMission.DayAndNight, CMission.NoEnd, state.TotalHours), "air supply");
		Console.WriteLine($"{Now(state)}  picked {(plane.Mission.Destination == 0 ? "nothing" : state.Province(plane.Mission.Destination).Name)}.");
		CFixedPoint carried = CFixedPoint.Zero;
		for (int hour = 1; hour <= days * CGameState.HoursPerDay; hour++)
		{
			int target = plane.Mission.Destination;
			int at = plane.Location;
			CFixedPoint aboard = plane.Mission.Supplies;
			simulation.RunHours(1);
			// What the hour's order did, where it stood before the hour's flight.
			if (plane.Mission.Supplies != aboard)
			{
				Console.WriteLine($"{Now(state)}  {plane.Name}, in {state.Province(at).Name}, "
					+ $"{(plane.Mission.Supplies > aboard ? $"loaded {plane.Mission.Supplies} supplies and {plane.Mission.Fuel} fuel for {state.Province(plane.Mission.Destination).Name}" : $"delivered {aboard} to {state.Province(target).Name}")}.");
				if (plane.Mission.Supplies < aboard)
				{
					carried = new CFixedPoint(carried.Raw + aboard.Raw);
				}
			}
		}
		Console.WriteLine($"{Now(state)}  {plane.Name} carried {carried} supplies in {days} days; order {plane.Order}.");
		return 0;
	}

	/// <summary>Runs the hours while a unit keeps an order, telling each province it comes to.</summary>
	private static void Watch(CGameState state, CSimulation simulation, CUnit unit, int days, EUnitOrder order)
	{
		int last = unit.Location;
		for (int hour = 1; hour <= days * CGameState.HoursPerDay && unit.Order == order; hour++)
		{
			simulation.RunHours(1);
			if (unit.Location != last)
			{
				last = unit.Location;
				Console.WriteLine($"{Now(state)}  {unit.Name} over {state.Province(last).Name}.");
			}
		}
		Console.WriteLine($"{Now(state)}  {unit.Name}: {order} {(unit.Order == order ? "still going" : "done")}.");
	}

	/// <summary>A unit of the install's own type, with the country's own equipment, full, in a province.</summary>
	private static CUnit Make(CGameState state, CCountry country, EUnitKind kind, string type, int count, int province)
	{
		var unit = new CUnit(kind, country.Tag)
		{
			Id = ++state.LastUnitId, Location = province, Base = kind == EUnitKind.Air ? province : 0,
			Name = $"Trial {type}",
		};
		CSubUnitType subType = state.SubUnitTypes[type];
		for (int i = 0; i < count; i++)
		{
			var order = new CBrigadeOrder(subType);
			CBuildQueue.SetModelToCountrys(country, order);
			CSubUnit sub = CBuildQueue.AsBuilt(order, isReserve: false, out _);
			sub.Id = ++state.LastUnitId;
			sub.Unit = unit;
			sub.Name = $"{type} {i + 1}";
			sub.Strength = sub.Stats[CSubUnitType.MaxStrength];
			unit.SubUnits.Add(sub);
		}
		country.Units.Add(unit);
		state.ForgetPositions();
		foreach (CSubUnit sub in unit.SubUnits)
		{
			sub.Organisation = COrganisation.Ceiling(state, country, sub);
		}
		return unit;
	}

	private static void Remove(CGameState state, CCountry country, CUnit unit)
	{
		country.Units.Remove(unit);
		state.ForgetPositions();
	}

	private static IEnumerable<CProvince> AirBases(CGameState state, CCountry country)
	{
		return state.Provinces.Where(province => !province.IsSea && province.Controller == country.Tag
			&& CMovementRules.BuildingLevel(state, province, CMovementRules.AirBase) > CFixedPoint.Zero);
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
