using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

/// <summary>
/// Fights one battle at sea with an install's own fleets and says how it went:
///
///   OpenHOI3.SaveCheck seabattle &lt;install folder&gt; [attacker tag] [defender tag] [seed]
///
/// It is a trial and not a check: no savegame has a battle at sea in it to set this
/// against. The two countries are put at war if they are not, the strongest fleet of
/// each is put in the sea province the first one's port opens onto, the first country is
/// played by a person - an idle fleet of anybody else's starts no battle - and the hours
/// are let go by until the battle is over. With no tags it is Germany against the United
/// Kingdom. See <c>docs/mechanics/navalcombat.md</c>.
/// </summary>
public static class CSeaBattleTrial
{
	private const int MostHours = 24 * 30;

	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck seabattle <install folder> [attacker tag] [defender tag] [seed]");
			return 2;
		}
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add);
		var simulation = new CSimulation(state);

		var attackerTag = new CCountryTag(args.Length > 2 ? args[2].ToUpperInvariant() : "GER");
		var defenderTag = new CCountryTag(args.Length > 3 ? args[3].ToUpperInvariant() : "ENG");
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
			Console.WriteLine($"{attackers.Name} and {defenders.Name} were at peace and are now at war.");
		}
		state.Played.Add(attackerTag);

		if (Strongest(attackers) is not { } ours || Strongest(defenders) is not { } theirs)
		{
			Console.WriteLine("One of the two has no fleet that can fight.");
			return 1;
		}
		CProvince here = state.Province(ours.Location);
		CProvince? sea = CMovementRules.SeaEnd(state, here);
		if (sea is null)
		{
			Console.WriteLine($"{ours.Name} lies in {here.Name}, which no sea is reached from.");
			return 1;
		}
		// The trial's own doing: both fleets are simply put there.
		Put(state, ours, sea.Id);
		Put(state, theirs, sea.Id);
		Console.WriteLine($"{state.Date} {state.Hour}:00  {Describe(ours)} and {Describe(theirs)} are put in {sea.Name} ({sea.Id}).");

		CCombat? combat = null;
		int sightedAfter = 0;
		for (int hour = 1; hour <= MostHours; hour++)
		{
			simulation.RunHours(1);
			CCombat? now = CCombats.In(state, sea.Id, ECombatKind.Naval);
			if (combat is null && now is not null)
			{
				combat = now;
				sightedAfter = hour;
				Console.WriteLine($"{state.Date} {state.Hour}:00  sighted after {hour} hours; the battle begins. Positioning "
					+ $"{now.Attacker.Positioning} against {now.Defender.Positioning}.");
				Console.WriteLine("    " + Distances(ours));
				Console.WriteLine("    " + Distances(theirs));
			}
			if (combat is not null && now is not null && (now.Duration is 1 or 2 or 4 || now.Duration % 12 == 0))
			{
				Console.WriteLine($"{state.Date} {state.Hour}:00  round {now.Duration}"
					+ (CWeatherIsNight(state, sea) ? " (night)" : "") + $": {Describe(ours)}"
					+ (ours.Disengage > 0 ? $", breaking away {ours.Disengage}" : "") + $"; {Describe(theirs)}"
					+ (theirs.Disengage > 0 ? $", breaking away {theirs.Disengage}" : "")
					+ $"; in reach {InReach(state, now.Attacker, now.Defender)} and {InReach(state, now.Defender, now.Attacker)}");
			}
			if (combat is not null && now is null)
			{
				Console.WriteLine($"{state.Date} {state.Hour}:00  the battle is over after {combat.Duration} hours: "
					+ (combat.IsConcluded ? (combat.AttackerLost ? "the attacker gave way" : "the defender gave way") : "both broke off")
					+ $". Sunk: {Names(combat.Attacker.Sunk)} and {Names(combat.Defender.Sunk)}.");
				Console.WriteLine("    " + After(state, ours));
				Console.WriteLine("    " + After(state, theirs));
				Console.WriteLine("Their leaders now:");
				foreach (string line in CLeaderGainText.Lines(ours).Concat(CLeaderGainText.Lines(theirs)))
				{
					Console.WriteLine(line);
				}
				return 0;
			}
		}
		Console.WriteLine(combat is null
			? $"Nothing was sighted in {MostHours} hours."
			: $"The battle was still being fought after {MostHours} hours (sighted after {sightedAfter}).");
		return 0;
	}

	private static bool CWeatherIsNight(CGameState state, CProvince province)
	{
		return OpenHOI3.Sim.Weather.CDaylight.IsNight(state, province);
	}

	/// <summary>The fleet of a country with the most sea attack in it.</summary>
	private static CUnit? Strongest(CCountry country)
	{
		CUnit? best = null;
		long most = 0;
		foreach (CUnit unit in country.AllUnits())
		{
			if (unit.Kind != EUnitKind.Navy || unit.Location <= 0)
			{
				continue;
			}
			long attack = unit.SubUnits.Sum(ship => ship.Stats[CNavalFire.SeaAttack].Raw);
			if (attack > most)
			{
				most = attack;
				best = unit;
			}
		}
		return best;
	}

	private static void Put(CGameState state, CUnit fleet, int province)
	{
		state.Positions.Move(fleet, fleet.Location, province);
		fleet.Location = province;
		fleet.Path.Clear();
	}

	private static int InReach(CGameState state, CCombatSide side, CCombatSide other)
	{
		int count = 0;
		foreach (CUnit fleet in side.Units)
		{
			foreach (CSubUnit ship in fleet.SubUnits)
			{
				long reach = CNavalTargets.ReachNow(state, ship);
				if (other.Units.Any(unit => unit.SubUnits.Any(target => target.Distance.Raw + ship.Distance.Raw <= reach)))
				{
					count++;
				}
			}
		}
		return count;
	}

	private static string Describe(CUnit fleet)
	{
		return $"{fleet.Name} ({fleet.Owner}, {fleet.SubUnits.Count} ships, strength {CUnitTotals.Strength(fleet)}, "
			+ $"organisation {CUnitTotals.AverageOrganisation(fleet)})";
	}

	private static string Distances(CUnit fleet)
	{
		return fleet.Name + ": " + string.Join(", ",
			fleet.SubUnits.Select(ship => $"{ship.Type.Key} at {ship.Distance} (reach {new CFixedPoint(CNavalBattle.Reach(ship))})"));
	}

	private static string Names(List<CSubUnit> ships)
	{
		return ships.Count == 0 ? "nothing" : string.Join(", ", ships.Select(ship => ship.ToString()));
	}

	private static string After(CGameState state, CUnit fleet)
	{
		if (fleet.IsRemoved)
		{
			return $"{fleet.Name}: removed from the game";
		}
		return Describe(fleet) + (fleet.IsRetreating && fleet.IsMoving
			? $", falling back to {state.Province(fleet.Path[^1]).Name}"
			: $", in {state.Province(fleet.Location).Name}, {fleet.EngageWait} hours before it will start another");
	}
}
