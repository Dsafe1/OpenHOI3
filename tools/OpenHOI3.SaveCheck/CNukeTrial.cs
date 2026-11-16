using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

/// <summary>
/// Drops one nuclear bomb with an install's own units and says what it did:
///
///   OpenHOI3.SaveCheck nuke &lt;install folder&gt; [attacker tag] [defender tag] [seed]
///
/// It is a trial and not a check: no savegame has a bomb in it. The two countries are put at war if they are not, the first
/// is given one bomb and its air unit with the most strategic attack is sent at the province of the second's within its
/// range with the most industry, an hour at a time. With nothing said it is Germany on Poland. See
/// <c>docs/mechanics/nukes.md</c>.
/// </summary>
public static class CNukeTrial
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck nuke <install folder> [attacker tag] [defender tag] [seed]");
			return 2;
		}
		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add);
		var simulation = new CSimulation(state);
		var attackerTag = new CCountryTag(args.Length > 2 ? args[2].ToUpperInvariant() : "GER");
		var defenderTag = new CCountryTag(args.Length > 3 ? args[3].ToUpperInvariant() : "POL");
		if (args.Length > 4 && uint.TryParse(args[4], out uint seed))
		{
			state.Seed = seed;
		}
		if (state.Country(attackerTag) is not { } attackers || state.Country(defenderTag) is not { } defenders)
		{
			Console.Error.WriteLine($"no such country: {attackerTag} or {defenderTag}");
			return 2;
		}
		if (!CMovementRules.IsEnemy(state, attackerTag, defenderTag))
		{
			simulation.Post(new CStartWarCommand(attackerTag, defenderTag));
			simulation.Pump();
		}
		state.Played.Add(attackerTag);
		attackers.Nukes = CFixedPoint.FromInt(1);
		state.PactMessaged += pact => Console.WriteLine($"  message: {pact.We} / {pact.They} / {pact.Other}: "
			+ string.Join(", ", pact.Values.Select(value => value.Item1 + "=" + value.Item2)));

		CUnit? bomber = null;
		CProvince? target = null;
		foreach (CUnit unit in attackers.AllUnits()
			.Where(unit => CNukeMission.MayCarry(unit) && unit.Base > 0 && unit.Location == unit.Base && unit.CarriedBy is null)
			.OrderByDescending(unit => unit.SubUnits.Sum(wing => wing.Stats[CBombingOrders.StrategicAttack].Raw)))
		{
			target = state.Provinces.Where(province => province.Id != 0 && province.Controller == defenderTag
					&& new CSetUnitOrderCommand(attackerTag, unit.Id, EUnitOrder.Nuke, province.Id).MayExecute(state))
				.OrderByDescending(province => province.Modifier[CIndustrialCapacity.ProvinceIc].Raw).FirstOrDefault();
			if (target is not null)
			{
				bomber = unit;
				break;
			}
		}
		if (bomber is null || target is null)
		{
			Console.WriteLine($"No air unit of {attackers.Name} has a province of {defenders.Name}'s within its range.");
			return 1;
		}

		Console.WriteLine($"{bomber.Name} of {attackers.Name}, at {state.Province(bomber.Base).Name}, is sent at {target.Name}.");
		Console.WriteLine($"  the stock {attackers.Nukes}; {defenders.Name}'s unity {defenders.NationalUnity}; the province's IC {target.Modifier[CIndustrialCapacity.ProvinceIc]}, a core: {target.Cores.Contains(defenderTag)}, the capital: {target.Id == defenders.ActingCapital}");
		Tell(state, target, "before");
		simulation.Post(new CSetUnitOrderCommand(attackerTag, bomber.Id, EUnitOrder.Nuke, target.Id));
		simulation.Pump();
		Console.WriteLine($"  given: the stock {attackers.Nukes}, carrying {bomber.Mission.Nukes}");
		for (int hour = 1; hour <= 240 && bomber.Order == EUnitOrder.Nuke; hour++)
		{
			simulation.RunHours(1);
		}
		Console.WriteLine($"{state.Date} {state.Hour}:00  the order is over; {bomber.Name} stands in {state.Province(bomber.Location).Name}.");
		Console.WriteLine($"  the stock {attackers.Nukes}; {defenders.Name}'s unity {defenders.NationalUnity}");
		Tell(state, target, "after");
		return 0;
	}

	private static void Tell(CGameState state, CProvince target, string when)
	{
		string buildings = string.Join(", ", state.Buildings.Where(building => target.BuildingMaxLevel(building).Raw > 0)
			.Select(building => $"{building.Key} {target.BuildingLevel(building)} of {target.BuildingMaxLevel(building)}"));
		Console.WriteLine($"  {when}: {buildings}");
		Console.WriteLine($"  {when}: supplies lying there {target.Pool[EGoods.Supplies]}, its energy made {target.CurrentProducing[EGoods.Energy]}");
		foreach (CUnit unit in state.Positions.Of(target.Id))
		{
			string parts = string.Join(" ", unit.SubUnits.Select(part => $"{part.Strength}/{part.Organisation}"));
			Console.WriteLine($"  {when}: {unit.Name} of {unit.Owner}: {parts}");
		}
	}
}
