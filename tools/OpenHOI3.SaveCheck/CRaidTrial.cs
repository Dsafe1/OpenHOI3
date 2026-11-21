using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Messages;
using OpenHOI3.Interface.Messages;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

/// <summary>
/// Sends an install's own raiders at an install's own convoys and says how it went:
///
///   OpenHOI3.SaveCheck raid &lt;install folder&gt; [raider tag] [victim tag] [days] [seed] [air]
///
/// It is a trial and not a check: no savegame at hand has a convoy raid in it. The two
/// countries are put at war if they are not; the raider's fleet with the most
/// <c>convoy_attack</c> is given the order to raid convoys in the sea province within
/// its range that the most of the victim's convoys go through; and the days are let go
/// by, each sinking told. With <c>air</c> it is the raider's air unit with the most
/// <c>sea_attack</c>, given the raid on convoys from the air for that province within its
/// range. With <c>patrol</c> the fleet is given a patrol of that province instead, its country played: a patrol tries
/// the convoys as a raider does and shadows the foreign fleets it sights, each fleet it takes up told as it happens
/// with its <c>PATROLTRACK</c>. Every message the raids raise is counted by its type, and the first of each
/// written as the log would. With no tags it is Germany against the United Kingdom, for
/// thirty days. See <c>docs/mechanics/navalmissions.md</c>, sections 8 and 13.
/// </summary>
public static class CRaidTrial
{
	public static int Run(string[] args)
	{
		bool fromTheAir = args.Any(arg => arg.Equals("air", StringComparison.OrdinalIgnoreCase));
		bool patrol = args.Any(arg => arg.Equals("patrol", StringComparison.OrdinalIgnoreCase));
		args = args.Where(arg => !arg.Equals("air", StringComparison.OrdinalIgnoreCase)
			&& !arg.Equals("patrol", StringComparison.OrdinalIgnoreCase)).ToArray();
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck raid <install folder> [raider tag] [victim tag] [days] [seed] [air | patrol]");
			return 2;
		}
		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add);
		CMessageTypes messageTypes = CMessageTypes.Load(directories);
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

		// From the air the raider's country is played, as the bombing trial's is, so that the AI neither takes its air unit
		// for its own nor sends fleets of its own raiding beside it.
		// A patrol's country is played too: its PATROLTRACK is a player's message alone (navalmissions.md, section 3).
		if (fromTheAir || patrol)
		{
			state.Played.Add(raiderTag);
		}
		EUnitOrder order = fromTheAir ? EUnitOrder.AirConvoyRaid : patrol ? EUnitOrder.Patrol : EUnitOrder.ConvoyRaid;
		List<CUnit> candidates = (fromTheAir
			? raiders.AllUnits()
				.Where(unit => unit.Kind == EUnitKind.Air && unit.Location > 0 && unit.CarriedBy is null)
				.OrderByDescending(unit => unit.SubUnits.Sum(wing => wing.Stats[CNavalFire.SeaAttack].Raw))
			: raiders.AllUnits()
				.Where(unit => unit.Kind == EUnitKind.Navy && unit.Location > 0)
				.OrderByDescending(unit => unit.SubUnits.Sum(ship => ship.Stats[CConvoyRaid.ConvoyAttack].Raw)))
			.Where(unit => CConvoyRaid.Attack(unit) > 0)
			.ToList();
		if (candidates.Count == 0)
		{
			Console.WriteLine($"{raiders.Name} has no {(fromTheAir ? "air unit" : "fleet")} that can attack a convoy.");
			return 1;
		}
		Console.WriteLine($"{victims.Name} has {victims.Convoys.Count} convoys with "
			+ $"{victims.Convoys.Sum(convoy => convoy.Transports)} transports and {victims.Convoys.Sum(convoy => convoy.Escorts)} escorts, "
			+ $"and {victims.Transports} transports with no convoy.");

		// The water within the raider's range that the most of the victim's convoys go through.
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
		// The fleet with the most convoy attack; or the air unit with the most sea attack that reaches any such water.
		CUnit raider = candidates[0];
		int target = 0;
		foreach (CUnit candidate in fromTheAir ? candidates : candidates.Take(1))
		{
			target = through
				.Where(pair => (!fromTheAir || CUnitOrders.IsInRange(state, candidate, order, state.Province(pair.Key)))
					&& new CSetUnitOrderCommand(raiderTag, candidate.Id, order, pair.Key).MayExecute(state))
				.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key)
				.Select(pair => pair.Key).FirstOrDefault();
			if (target != 0)
			{
				raider = candidate;
				break;
			}
		}
		if (target == 0)
		{
			Console.WriteLine($"None of {victims.Name}'s convoys goes through water {raider.Name} can reach from {state.Province(raider.Base).Name}.");
			return 1;
		}

		// Every message a raid raises, by its type, and the first of each as its player would read it in the log.
		var told = new Dictionary<string, int>(StringComparer.Ordinal);
		state.ConvoyRaidMessaged += raid =>
		{
			told[raid.Type] = told.GetValueOrDefault(raid.Type) + 1;
			if (told[raid.Type] == 1 && CMessages.ConvoyRaid(localisation.Localise, messageTypes, state, raid.Told, raid) is { } message)
			{
				Console.WriteLine($"    {raid.Type} to {raid.Told}:{CMessageText.Log(localisation.Localise, message)}");
			}
		};
		// A patrol's new shadow, counted for everybody - the AI's patrols raise it too, which only a player would see - and
		// told as it happens for the raider's country.
		state.PatrolTracked += track =>
		{
			told[CPatrolTrackMessage.Type] = told.GetValueOrDefault(CPatrolTrackMessage.Type) + 1;
			if (track.Told == raiderTag && CMessages.PatrolTrack(localisation.Localise, messageTypes, state, track.Told, track) is { } message)
			{
				Console.WriteLine($"    {CPatrolTrackMessage.Type} to {track.Told}:{CMessageText.Log(localisation.Localise, message)}");
			}
		};

		CProvince sea = state.Province(target);
		string attacking = fromTheAir ? "raid convoys from the air" : patrol ? "patrol" : "raid convoys";
		Console.WriteLine($"{state.Date} {state.Hour}:00  {Describe(raider)}, at {state.Province(raider.Location).Name}, is to {attacking} in "
			+ $"{sea.Name} ({sea.Id}), which {through[target]} of them go through. It attacks at {new CFixedPoint(CConvoyRaid.Attack(raider))}"
			+ (fromTheAir ? $", five times that against escorts." : "."));
		simulation.Post(new CSetUnitOrderCommand(raiderTag, raider.Id, order, target));
		simulation.Pump();

		// How the raider goes, where it does: as it stood and what took it out.
		state.UnitRemovedTrace = unit =>
		{
			if (unit == raider)
			{
				Console.WriteLine($"{state.Date} {state.Hour}:00  {Describe(raider)}, in {state.Province(raider.Location).Name}, order {raider.Order}, is taken out of the game by");
				foreach (string frame in Environment.StackTrace.Split('\n').Where(line => line.Contains("OpenHOI3.Sim")).Skip(1).Take(5))
				{
					Console.WriteLine("        " + frame.Trim());
				}
			}
		};
		var before = victims.Convoys.ToDictionary(convoy => convoy.Id, convoy => (convoy.Transports, convoy.Escorts));
		int raids = 0;
		int transports = 0;
		int escorts = 0;
		int arrived = -1;
		CUnit? shadowing = null;
		for (int hour = 1; hour <= days * CGameState.HoursPerDay && !raider.IsRemoved; hour++)
		{
			simulation.RunHours(1);
			if (patrol && raider.Mission.LiveTarget != shadowing)
			{
				shadowing = raider.Mission.LiveTarget;
				Console.WriteLine($"{state.Date} {state.Hour}:00  in {state.Province(raider.Location).Name}: "
					+ (shadowing is null ? "shadowing nobody." : $"shadowing {shadowing.Name} ({shadowing.Owner})."));
			}
			if (arrived < 0 && raider.Location == target)
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
				Console.WriteLine($"{state.Date} {state.Hour}:00  in {state.Province(raider.Location).Name}: the convoy "
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
		Console.WriteLine("    " + (raider.IsRemoved ? raider.Name + ": removed from the game"
			: Describe(raider) + $", in {state.Province(raider.Location).Name}, order {raider.Order}"));
		Console.WriteLine($"    {victims.Name} now has {victims.Convoys.Count} convoys with "
			+ $"{victims.Convoys.Sum(convoy => convoy.Transports)} transports and {victims.Convoys.Sum(convoy => convoy.Escorts)} escorts, "
			+ $"and {victims.Transports} transports with no convoy.");
		Console.WriteLine("Messages raised: " + (told.Count == 0 ? "none"
			: string.Join(", ", told.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key} {pair.Value}"))));
		Console.WriteLine("The raider's leaders now:");
		foreach (string line in CLeaderGainText.Lines(raider))
		{
			Console.WriteLine(line);
		}
		return 0;
	}

	private static string Describe(CUnit raider)
	{
		string parts = raider.Kind == EUnitKind.Air ? "wings" : "ships";
		return $"{raider.Name} ({raider.Owner}, {raider.SubUnits.Count} {parts}, strength {CUnitTotals.Strength(raider)}, "
			+ $"organisation {CUnitTotals.AverageOrganisation(raider)})";
	}
}
