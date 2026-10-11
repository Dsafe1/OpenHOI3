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
/// Fights one battle with an install's own units and says how it went:
///
///   OpenHOI3.SaveCheck battle &lt;install folder&gt; [attacker tag] [defender tag] [seed]
///
/// It is a trial and not a check: no savegame has a battle in it to set this against.
/// The two countries are put at war if they are not - Italy and Ethiopia are, on the
/// start date, and are what it takes with no tags given - the first division of the
/// attacker's that stands next to one of the defender's is sent at it, and the hours are
/// let go by until the battle is over and the province has changed hands or not. See
/// <c>docs/mechanics/landcombat.md</c>.
/// </summary>
public static class CBattleTrial
{
	private const int MostHours = 24 * 120;

	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck battle <install folder> [attacker tag] [defender tag] [seed] [part of the attacking unit's name]");
			return 2;
		}
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add);
		var simulation = new CSimulation(state);

		var attackerTag = new CCountryTag(args.Length > 2 ? args[2].ToUpperInvariant() : "ITA");
		var defenderTag = new CCountryTag(args.Length > 3 ? args[3].ToUpperInvariant() : "ETH");
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

		string named = args.Length > 5 ? args[5] : "";
		if (FindPair(state, attackers, defenderTag, named) is not var (unit, target, enemy))
		{
			Console.WriteLine($"No division of {attackers.Name} stands next to one of {defenders.Name}.");
			return 1;
		}
		CProvince from = state.Province(unit.Location);
		CProvince to = state.Province(target);
		Console.WriteLine($"{state.Date} {state.Hour}:00  {Describe(unit)} in {from.Name} ({from.Id}) attacks "
			+ $"{to.Name} ({to.Id}), held by {to.Controller} with {Describe(enemy)}");

		var order = new CMoveUnitCommand(attackerTag, unit.Id, target);
		if (!order.MayExecute(state))
		{
			Console.WriteLine("The order was refused.");
			return 1;
		}
		simulation.Post(order);

		CCombat? combat = null;
		bool reported = false;
		string tactics = "";
		for (int hour = 1; hour <= MostHours; hour++)
		{
			simulation.RunHours(1);
			CCombat? now = CCombats.In(state, target);
			// The two tactics, whenever a pick has changed either.
			if (now is not null)
			{
				string picked = $"{now.Attacker.Tactic?.Name ?? "no tactic"} against {now.Defender.Tactic?.Name ?? "no tactic"}";
				if (picked != tactics)
				{
					tactics = picked;
					string countered = CCombatTactics.Counters(now.Attacker.Tactic, now.Defender.Tactic) ? ", the defender's countered"
						: CCombatTactics.Counters(now.Defender.Tactic, now.Attacker.Tactic) ? ", the attacker's countered" : "";
					Console.WriteLine($"{state.Date} {state.Hour}:00  tactics: {picked}{countered}");
				}
			}
			if (combat is null && now is not null)
			{
				combat = now;
				Console.WriteLine($"{state.Date} {state.Hour}:00  the battle begins: {now.Attacker.Units.Count} against "
					+ $"{now.Defender.Units.Count}, width {CFrontLine.Width(state, now)}");
			}
			// What the hour makes of the two, once by day and once by night.
			if (combat is not null && now is not null && now.Duration is 2 or 14)
			{
				Console.WriteLine($"    round {now.Duration}: {unit.Name} attacks at {unit.CombatAttack}: {Modifiers(unit)}");
				Console.WriteLine($"    round {now.Duration}: {enemy.Name} defends at {enemy.CombatDefence}, attacks at "
					+ $"{enemy.CombatAttack}: {Modifiers(enemy)}");
			}
			if (combat is not null && now is not null && hour % 24 == 0)
			{
				Console.WriteLine($"{state.Date} {state.Hour}:00  hour {now.Hour}: front {now.Attacker.Front.Count} against "
					+ $"{now.Defender.Front.Count}, reserves {now.Attacker.Reserves.Count} and {now.Defender.Reserves.Count}; "
					+ $"{Describe(unit)}; {Describe(enemy)}");
			}
			if (combat is not null && now is null && !reported)
			{
				reported = true;
				Console.WriteLine($"{state.Date} {state.Hour}:00  the battle is over after {combat.Duration} hours: "
					+ (combat.AttackerLost ? "the attack failed" : "the defence broke")
					+ $". Men lost: {combat.Attacker.Losses} and {combat.Defender.Losses}.");
				Console.WriteLine($"    {Describe(unit)}, attack delay {unit.AttackDelay} hours"
					+ (unit.IsRemoved ? ", removed" : ""));
				Console.WriteLine($"    {Describe(enemy)}"
					+ (enemy.IsRemoved ? ", removed" : enemy.IsRetreating
						? $", falling back to {state.Province(enemy.Path[^1]).Name}" : $", in {state.Province(enemy.Location).Name}"));
			}
			if (reported && (!unit.IsMoving || unit.IsRemoved))
			{
				break;
			}
			if (combat is null && !unit.IsMoving)
			{
				break;
			}
		}
		Console.WriteLine($"{state.Date} {state.Hour}:00  {unit.Name} is in {state.Province(unit.Location).Name}; "
			+ $"{to.Name} is held by {to.Controller} and owned by {to.Owner}.");
		Console.WriteLine("Their leaders now:");
		foreach (string line in CLeaderGainText.Lines(unit).Concat(CLeaderGainText.Lines(enemy)))
		{
			Console.WriteLine(line);
		}
		return 0;
	}

	/// <summary>The first division of a country with a division of its enemy's in a province next to it.</summary>
	private static (CUnit Unit, int Target, CUnit Enemy)? FindPair(CGameState state, CCountry attackers, CCountryTag defender,
		string named)
	{
		foreach (CUnit unit in attackers.UnitsInOrder())
		{
			if (unit.Kind != EUnitKind.Division || unit.SubUnits.Count == 0 || !CUnitTotals.CanFight(unit)
				|| !unit.Name.Contains(named, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			foreach (CProvinceEdge edge in state.Province(unit.Location).Edges)
			{
				CProvince province = state.Province(edge.To);
				if (province.IsSea || edge.Kind == EProvinceEdgeKind.Impassable || province.Controller != defender)
				{
					continue;
				}
				foreach (CUnit other in state.UnitsIn(province.Id))
				{
					if (other.Owner == defender && other.Kind == EUnitKind.Division && CUnitTotals.CanFight(other))
					{
						return (unit, province.Id, other);
					}
				}
			}
		}
		return null;
	}

	private static string Modifiers(CUnit unit)
	{
		var parts = new List<string>();
		foreach (CCombatModifier modifier in unit.CombatModifiers)
		{
			parts.Add(modifier.Attack == modifier.Defence
				? $"{modifier.Kind} {modifier.Attack}"
				: $"{modifier.Kind} {modifier.Attack}/{modifier.Defence}");
		}
		return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
	}

	private static string Describe(CUnit unit)
	{
		return $"{unit.Name} ({unit.SubUnits.Count} brigades, strength {CUnitTotals.Strength(unit)}, "
			+ $"organisation {CUnitTotals.AverageOrganisation(unit)})";
	}
}
