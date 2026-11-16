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
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

/// <summary>
/// Flies one air mission with an install's own air units and says how it went:
///
///   OpenHOI3.SaveCheck airbattle &lt;install folder&gt; [attacker tag] [defender tag] [seed]
///
/// It is a trial and not a check: no savegame has a battle in the air in it to set this
/// against. The two countries are put at war if they are not. Of the first one's air
/// units with an air attack and the second's, the pair is taken whose bases are nearest
/// to each other with the second's within the first's range; the second is given an
/// intercept over its own base, as a defender would have, and the first air
/// superiority over that base. The hours are then let go by for a week, or until both
/// are home with a battle behind them; the first battle is told round by round and the
/// ones after it in a line each. With no tags it is Germany against Poland - nothing of
/// Germany's reaches France from where it starts. See <c>docs/mechanics/aircombat.md</c>.
/// </summary>
public static class CAirBattleTrial
{
	private const int MostHours = 24 * 7;

	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck airbattle <install folder> [attacker tag] [defender tag] [seed]");
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
			Console.WriteLine($"{attackers.Name} and {defenders.Name} were at peace and are now at war.");
		}

		// The pair whose bases are nearest, with the defender's in the attacker's range.
		CUnit? ours = null;
		CUnit? theirs = null;
		long least = long.MaxValue;
		foreach (CUnit mine in Fighters(attackers))
		{
			foreach (CUnit other in Fighters(defenders))
			{
				CProvince target = state.Province(other.Base);
				long distance = CMapDistance.Between(state.Province(mine.Base), target, state.MapWidth);
				if (distance < least && CUnitOrders.IsInRange(state, mine, EUnitOrder.AirSuperiority, target)
					&& new CSetUnitOrderCommand(attackerTag, mine.Id, EUnitOrder.AirSuperiority, target.Id).MayExecute(state))
				{
					least = distance;
					ours = mine;
					theirs = other;
				}
			}
		}
		if (ours is null || theirs is null)
		{
			Console.WriteLine($"No air unit of {attackers.Name} with an air attack has one of {defenders.Name}'s within its range.");
			foreach (CUnit mine in Fighters(attackers))
			{
				long nearest = Fighters(defenders).Select(other =>
					CMapDistance.Between(state.Province(mine.Base), state.Province(other.Base), state.MapWidth))
					.DefaultIfEmpty(-1).Min();
				Console.WriteLine($"    {mine.Name} in {state.Province(mine.Base).Name}: range {CUnitOrders.RangeOf(mine)}, the nearest is {nearest} away");
			}
			return 1;
		}

		int over = theirs.Base;
		simulation.Post(new CSetUnitOrderCommand(defenderTag, theirs.Id, EUnitOrder.AirIntercept, over));
		simulation.Post(new CSetUnitOrderCommand(attackerTag, ours.Id, EUnitOrder.AirSuperiority, over));
		simulation.Pump();
		Console.WriteLine($"{state.Date} {state.Hour}:00  {Describe(state, ours)}, based in {state.Province(ours.Base).Name}, "
			+ $"is given air superiority over {state.Province(over).Name} ({over}), {least} away of its range of "
			+ $"{CUnitOrders.RangeOf(ours)}.");
		Console.WriteLine($"{state.Date} {state.Hour}:00  {Describe(state, theirs)}, based there, is given an intercept over it.");

		CCombat? combat = null;
		bool fought = false;
		int battles = 0;
		int lastOurs = ours.Location;
		for (int hour = 1; hour <= MostHours; hour++)
		{
			simulation.RunHours(1);
			if (ours.IsRemoved || theirs.IsRemoved)
			{
				Console.WriteLine($"{state.Date} {state.Hour}:00  {(ours.IsRemoved ? ours.Name : theirs.Name)} is removed from the game.");
				break;
			}
			CCombat? now = ours.Combats.FirstOrDefault(battle => battle.Kind == ECombatKind.Air)
				?? theirs.Combats.FirstOrDefault(battle => battle.Kind == ECombatKind.Air);
			if (combat is null && now is not null)
			{
				combat = now;
				fought = true;
				battles++;
				if (battles == 1)
				{
					Console.WriteLine($"{state.Date} {state.Hour}:00  after {hour} hours a battle begins over "
						+ $"{state.Province(now.Province).Name}: {Names(now.Attacker)} against {Names(now.Defender)}.");
				}
			}
			// The first battle is told round by round, the ones after it in a line each.
			if (now is not null && battles == 1)
			{
				Console.WriteLine($"{state.Date} {state.Hour}:00  round {now.Duration}"
					+ (OpenHOI3.Sim.Weather.CDaylight.IsNight(state, state.Province(now.Province)) ? " (night)" : "")
					+ $": {Describe(state, ours)}; {Describe(state, theirs)}");
				foreach (CUnit unit in new[] { ours, theirs })
				{
					if (now.Duration == 1 && unit.SubUnits.Count > 0)
					{
						CSubUnit wing = unit.SubUnits[0];
						Console.WriteLine($"    {unit.Name}'s first wing attacks at {wing.CombatAttack}: "
							+ string.Join(", ", wing.CombatModifiers.Select(modifier => $"{modifier.Kind} {modifier.Attack}")));
					}
				}
			}
			if (combat is not null && now is null)
			{
				Console.WriteLine($"{state.Date} {state.Hour}:00  battle {battles} is over after {combat.Duration} hours over "
					+ $"{state.Province(combat.Province).Name}: "
					+ (combat.IsConcluded ? (combat.AttackerLost ? "the attacker lost" : "the defender lost") : "nobody lost") + ".");
				Console.WriteLine("    " + After(state, ours));
				Console.WriteLine("    " + After(state, theirs));
				combat = null;
			}
			if (ours.Location != lastOurs && now is null)
			{
				lastOurs = ours.Location;
				if (ours.Location == over || ours.Location == ours.Base)
				{
					Console.WriteLine($"{state.Date} {state.Hour}:00  {ours.Name} is over {state.Province(ours.Location).Name}"
						+ (ours.Mission.ReturnToBase ? ", returning" : "") + ".");
				}
			}
			if (fought && now is null && ours.Location == ours.Base && theirs.Location == theirs.Base && !ours.IsMoving
				&& hour % 24 == 0)
			{
				break;
			}
		}
		Console.WriteLine($"{state.Date} {state.Hour}:00  at the end: {After(state, ours)}; {After(state, theirs)}");
		Console.WriteLine("Their leaders now:");
		foreach (string line in CLeaderGainText.Lines(ours).Concat(CLeaderGainText.Lines(theirs)))
		{
			Console.WriteLine(line);
		}
		if (!fought)
		{
			Console.WriteLine($"No battle was fought in {MostHours} hours.");
		}
		return 0;
	}

	/// <summary>A country's air units that have a base and an air attack.</summary>
	private static IEnumerable<CUnit> Fighters(CCountry country)
	{
		return country.AllUnits().Where(unit => unit.Kind == EUnitKind.Air && unit.Base > 0 && unit.Location == unit.Base
			&& unit.SubUnits.Count > 0 && unit.SubUnits.Sum(wing => wing.Stats[CAirMissions.AirAttack].Raw) > 0);
	}

	private static string Names(CCombatSide side)
	{
		return string.Join(", ", side.Units.Select(unit => unit.Name));
	}

	private static string Describe(CGameState state, CUnit unit)
	{
		return $"{unit.Name} ({unit.Owner}, {unit.SubUnits.Count} wings of {string.Join("/", unit.SubUnits.Select(wing => wing.Type.Key).Distinct())}, "
			+ $"strength {CUnitTotals.Strength(unit)}, organisation {CUnitTotals.AverageOrganisation(unit)})";
	}

	private static string After(CGameState state, CUnit unit)
	{
		string where = state.Province(unit.Location).Name;
		string doing = unit.IsRetreating ? $"falling back to {state.Province(unit.Path[^1]).Name}"
			: unit.IsMoving ? $"flying to {state.Province(unit.Path[^1]).Name}"
			: $"in {where}";
		return $"{Describe(state, unit)}, {doing}, order {unit.Order}" + (unit.Mission.ReturnToBase ? " (returning)" : "");
	}
}
