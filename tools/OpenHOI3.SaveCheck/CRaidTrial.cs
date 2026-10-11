using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

/// <summary>
/// Sends an install's own raiders at an install's own convoys and says how it went:
///
///   OpenHOI3.SaveCheck raid &lt;install folder&gt; [raider tag] [victim tag] [days] [seed]
///
/// It is a trial and not a check: no savegame at hand has a convoy raid in it. The two
/// countries are put at war if they are not; the raider's fleet with the most
/// <c>convoy_attack</c> is given the order to raid convoys in the sea province within
/// its range that the most of the victim's convoys go through; and the days are let go
/// by, each sinking told. With no tags it is Germany against the United Kingdom, for
/// thirty days. See <c>docs/mechanics/navalmissions.md</c>.
/// </summary>
public static class CRaidTrial
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck raid <install folder> [raider tag] [victim tag] [days] [seed]");
			return 2;
		}
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add);
		var simulation = new CSimulation(state);

		var raiderTag = new CCountryTag(args.Length > 2 ? args[2].ToUpperInvariant() : "GER");
		var victimTag = new CCountryTag(args.Length > 3 ? args[3].ToUpperInvariant() : "ENG");
		int days = args.Length > 4 && int.TryParse(args[4], out int wanted) ? wanted : 30;
		if (args.Length > 5 && uint.TryParse(args[5], out uint seed))
		{
			state.Seed = seed;
		}
		if (state.Country(raiderTag) is not { } raiders || state.Country(victimTag) is not { } victims)
		{
			Console.Error.WriteLine($"no such country: {raiderTag} or {victimTag}");
			return 2;
		}
		if (!CMovementRules.IsEnemy(state, raiderTag, victimTag))
		{
			simulation.Post(new CStartWarCommand(raiderTag, victimTag));
			simulation.Pump();
			Console.WriteLine($"{raiders.Name} and {victims.Name} were at peace and are now at war.");
		}

		CUnit? fleet = raiders.AllUnits()
			.Where(unit => unit.Kind == EUnitKind.Navy && unit.Location > 0)
			.OrderByDescending(unit => unit.SubUnits.Sum(ship => ship.Stats[CConvoyRaid.ConvoyAttack].Raw))
			.FirstOrDefault();
		if (fleet is null || CConvoyRaid.Attack(fleet) == 0)
		{
			Console.WriteLine($"{raiders.Name} has no fleet that can attack a convoy.");
			return 1;
		}
		Console.WriteLine($"{victims.Name} has {victims.Convoys.Count} convoys with "
			+ $"{victims.Convoys.Sum(convoy => convoy.Transports)} transports and {victims.Convoys.Sum(convoy => convoy.Escorts)} escorts, "
			+ $"and {victims.Transports} transports with no convoy.");

		// The water within the fleet's range that the most of the victim's convoys go through.
		var through = new Dictionary<int, int>();
		foreach (CConvoy convoy in victims.Convoys)
		{
			foreach (int id in convoy.Path.Distinct())
			{
				if (state.Province(id).IsSea && convoy.Transports > 0)
				{
					through[id] = through.GetValueOrDefault(id) + 1;
				}
			}
		}
		int target = through
			.Where(pair => new CSetUnitOrderCommand(raiderTag, fleet.Id, EUnitOrder.ConvoyRaid, pair.Key).MayExecute(state))
			.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key)
			.Select(pair => pair.Key).FirstOrDefault();
		if (target == 0)
		{
			Console.WriteLine($"None of {victims.Name}'s convoys goes through water {fleet.Name} can reach from {state.Province(fleet.Base).Name}.");
			return 1;
		}
		CProvince sea = state.Province(target);
		Console.WriteLine($"{state.Date} {state.Hour}:00  {Describe(fleet)}, at {state.Province(fleet.Location).Name}, is to raid convoys in "
			+ $"{sea.Name} ({sea.Id}), which {through[target]} of them go through. It attacks at {new CFixedPoint(CConvoyRaid.Attack(fleet))}.");
		simulation.Post(new CSetUnitOrderCommand(raiderTag, fleet.Id, EUnitOrder.ConvoyRaid, target));
		simulation.Pump();

		var before = victims.Convoys.ToDictionary(convoy => convoy.Id, convoy => (convoy.Transports, convoy.Escorts));
		int raids = 0;
		int transports = 0;
		int escorts = 0;
		int arrived = -1;
		for (int hour = 1; hour <= days * CGameState.HoursPerDay && !fleet.IsRemoved; hour++)
		{
			simulation.RunHours(1);
			if (arrived < 0 && fleet.Location == target)
			{
				arrived = hour;
				Console.WriteLine($"{state.Date} {state.Hour}:00  it is in {sea.Name} after {hour} hours.");
			}
			foreach (CConvoy convoy in victims.Convoys)
			{
				if (convoy.LastAttackHour != state.TotalHours || !before.TryGetValue(convoy.Id, out var was))
				{
					continue;
				}
				raids++;
				transports += was.Transports - convoy.Transports;
				escorts += was.Escorts - convoy.Escorts;
				Console.WriteLine($"{state.Date} {state.Hour}:00  in {state.Province(fleet.Location).Name}: the convoy "
					+ $"{state.Province(convoy.Start).Name} - {state.Province(convoy.End).Name} loses "
					+ $"{was.Transports - convoy.Transports} of {was.Transports} transports and {was.Escorts - convoy.Escorts} of {was.Escorts} escorts.");
			}
			if (victims.Convoys.Count != before.Count)
			{
				Console.WriteLine($"{state.Date} {state.Hour}:00  {victims.Name} now has {victims.Convoys.Count} convoys, where it had {before.Count}.");
			}
			// What the day's sharing out gives back is not a loss undone: count from what there is now.
			before = victims.Convoys.ToDictionary(convoy => convoy.Id, convoy => (convoy.Transports, convoy.Escorts));
		}
		Console.WriteLine($"{state.Date} {state.Hour}:00  after {days} days: {raids} raids that sank something, "
			+ $"{transports} transports and {escorts} escorts in all.");
		Console.WriteLine("    " + (fleet.IsRemoved ? fleet.Name + ": removed from the game"
			: Describe(fleet) + $", in {state.Province(fleet.Location).Name}, order {fleet.Order}"));
		Console.WriteLine($"    {victims.Name} now has {victims.Convoys.Count} convoys with "
			+ $"{victims.Convoys.Sum(convoy => convoy.Transports)} transports and {victims.Convoys.Sum(convoy => convoy.Escorts)} escorts, "
			+ $"and {victims.Transports} transports with no convoy.");
		Console.WriteLine("The raider's leaders now:");
		foreach (string line in CLeaderGainText.Lines(fleet))
		{
			Console.WriteLine(line);
		}
		return 0;
	}

	private static string Describe(CUnit fleet)
	{
		return $"{fleet.Name} ({fleet.Owner}, {fleet.SubUnits.Count} ships, strength {CUnitTotals.Strength(fleet)}, "
			+ $"organisation {CUnitTotals.AverageOrganisation(fleet)})";
	}
}
