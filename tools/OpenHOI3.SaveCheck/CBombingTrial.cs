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
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

/// <summary>
/// Flies one bombing mission with an install's own units and says how it went:
///
///   OpenHOI3.SaveCheck bombing &lt;install folder&gt; [mission] [attacker tag] [defender tag] [days] [seed]
///
/// It is a trial and not a check: no savegame has a bombing in it to set this against.
/// The two countries are put at war if they are not. The mission is one of
/// strategicbombing, logisticalstrike, runwaycratering, installationstrike, groundattack,
/// interdiction, portstrike and navalstrike. Of the first country's air units, the one
/// with the most of the attack the mission fires is taken that has, within its range, a
/// province of the second's worth the mission - the most industry, the most roads, the
/// most aircraft based, the most radar, the most men, the most ships in port; for a naval
/// strike the second's strongest fleet is put in the water off its port first, which is
/// the trial's own doing. The days are then let go by, each sortie told in a line and the
/// first hour by hour, and at the end what the province has lost and what the day's
/// repair gives back. With nothing said it is Germany's strategic bombing of Poland for
/// three days. See <c>docs/mechanics/bombing.md</c>.
/// </summary>
public static class CBombingTrial
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck bombing <install folder> [mission] [attacker tag] [defender tag] [days] [seed]");
			return 2;
		}
		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add);
		var simulation = new CSimulation(state);

		if (!Enum.TryParse(args.Length > 2 ? args[2] : "StrategicBombing", ignoreCase: true, out EUnitOrder mission)
			|| !CBombingOrders.IsBombingMission(mission))
		{
			Console.Error.WriteLine($"'{args[2]}' is not a bombing mission.");
			return 2;
		}
		var attackerTag = new CCountryTag(args.Length > 3 ? args[3].ToUpperInvariant() : "GER");
		var defenderTag = new CCountryTag(args.Length > 4 ? args[4].ToUpperInvariant() : "POL");
		int days = args.Length > 5 && int.TryParse(args[5], out int asked) ? asked : 3;
		if (args.Length > 6 && uint.TryParse(args[6], out uint seed))
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

		// A naval strike needs a fleet at sea, and with no AI none leaves port.
		if (mission == EUnitOrder.NavalStrike)
		{
			CUnit? fleet = defenders.AllUnits().Where(unit => unit.Kind == EUnitKind.Navy && unit.Location > 0)
				.OrderByDescending(unit => unit.SubUnits.Count).FirstOrDefault();
			if (fleet is null || CMovementRules.SeaEnd(state, state.Province(fleet.Location)) is not { } sea)
			{
				Console.WriteLine($"{defenders.Name} has no fleet that can be put to sea.");
				return 1;
			}
			state.Positions.Move(fleet, fleet.Location, sea.Id);
			fleet.Location = sea.Id;
			Console.WriteLine($"{state.Date} {state.Hour}:00  {fleet.Name} of {defenders.Name}, {fleet.SubUnits.Count} ships, is put in {sea.Name} ({sea.Id}).");
		}

		string[] fired = CBombingOrders.GoesForLandUnits(mission) ? new[] { CUnitTotals.SoftAttack, CUnitTotals.HardAttack }
			: CBombingOrders.GoesForShips(mission) ? new[] { CNavalFire.SeaAttack }
			: new[] { CBombingOrders.StrategicAttack };
		CUnit? bomber = null;
		CProvince? target = null;
		foreach (CUnit unit in attackers.AllUnits()
			.Where(unit => unit.Kind == EUnitKind.Air && unit.Base > 0 && unit.Location == unit.Base && unit.CarriedBy is null)
			.OrderByDescending(unit => unit.SubUnits.Sum(wing => fired.Sum(statistic => wing.Stats[statistic].Raw))))
		{
			long most = 0;
			foreach (CProvince province in state.Provinces)
			{
				if (province.Id == 0 || !CUnitOrders.IsInRange(state, unit, mission, province)
					|| !new CSetUnitOrderCommand(attackerTag, unit.Id, mission, province.Id).MayExecute(state))
				{
					continue;
				}
				long worth = Worth(state, mission, defenderTag, province);
				if (worth > most)
				{
					most = worth;
					target = province;
				}
			}
			if (target is not null)
			{
				bomber = unit;
				break;
			}
		}
		if (bomber is null || target is null)
		{
			Console.WriteLine($"No air unit of {attackers.Name} has a province of {defenders.Name}'s worth a {mission} within its range.");
			return 1;
		}

		CProvince before = Snapshot(state, target);
		simulation.Post(new CSetUnitOrderCommand(attackerTag, bomber.Id, mission, target.Id));
		simulation.Pump();
		Console.WriteLine($"{state.Date} {state.Hour}:00  {Describe(bomber)}, based in {state.Province(bomber.Base).Name}, "
			+ $"is given {mission} for {target.Name} ({target.Id}), "
			+ $"{CMapDistance.Between(state.Province(bomber.Base), target, state.MapWidth)} away of its range of {CUnitOrders.RangeOf(bomber)}.");
		Console.WriteLine($"    {target.Name}: {Buildings(state, target)}; guns and units fire {new CFixedPoint(CAntiAircraft.ProvinceFire(state, target))} shots an hour.");

		CCombat? bombing = null;
		int sorties = 0;
		for (int hour = 1; hour <= days * 24; hour++)
		{
			simulation.RunHours(1);
			if (bomber.IsRemoved)
			{
				Console.WriteLine($"{state.Date} {state.Hour}:00  {bomber.Name} is removed from the game.");
				break;
			}
			CCombat? now = bomber.Bombing;
			if (bombing is null && now is not null)
			{
				bombing = now;
				sorties++;
				Console.WriteLine($"{state.Date} {state.Hour}:00  sortie {sorties}: a bombing of {Kind(now.Kind)} begins over "
					+ $"{state.Province(now.Province).Name} against {now.Defender.Units.Count} units"
					+ (now.Defender.Units.Count > 0 ? $" ({string.Join(", ", now.Defender.Units.Take(4).Select(unit => unit.Name))})" : "") + ".");
			}
			if (now is not null && sorties == 1)
			{
				CSubUnit wing = bomber.SubUnits[0];
				Console.WriteLine($"{state.Date} {state.Hour}:00  hour {now.Duration}"
					+ (OpenHOI3.Sim.Weather.CDaylight.IsNight(state, state.Province(now.Province)) ? " (night)" : "")
					+ $": {Describe(bomber)}; a wing attacks at {wing.CombatAttack}"
					+ (wing.CombatModifiers.Count > 0 ? " (" + string.Join(", ", wing.CombatModifiers.Select(modifier => $"{modifier.Kind} {modifier.Attack}")) + ")" : "")
					+ $"; targets {Targets(now)}");
			}
			if (bombing is not null && now is null)
			{
				Console.WriteLine($"{state.Date} {state.Hour}:00  sortie {sorties} is over after {bombing.Duration} hours: {Describe(bomber)}; "
					+ $"buildings destroyed so far {bombing.Attacker.BuildingsDestroyed}; {Buildings(state, target)}");
				bombing = null;
			}
		}

		Console.WriteLine($"{state.Date} {state.Hour}:00  at the end, after {sorties} sorties: {Describe(bomber)}, "
			+ (bomber.IsMoving ? $"flying to {state.Province(bomber.Path[^1]).Name}" : $"in {state.Province(bomber.Location).Name}")
			+ $", order {bomber.Order}.");
		Console.WriteLine($"    {target.Name} before: {Buildings(state, before)}");
		Console.WriteLine($"    {target.Name} now:    {Buildings(state, target)}");
		foreach (EGoods goods in Enum.GetValues<EGoods>())
		{
			if (before.Pool[goods] != target.Pool[goods] || before.CurrentProducing[goods] != target.CurrentProducing[goods])
			{
				Console.WriteLine($"    {goods}: lying there {before.Pool[goods]} -> {target.Pool[goods]}, "
					+ $"made a day {before.CurrentProducing[goods]} -> {target.CurrentProducing[goods]} of {target.MaxProducing[goods]}");
			}
		}
		Console.WriteLine($"    a day's repair there is {new CFixedPoint(CProvinceRepair.Rate(state, target))} of each good's yield and "
			+ $"{new CFixedPoint(CProvinceRepair.Rate(state, target) * state.Defines[CProvinceRepair.EconomySection, CProvinceRepair.RepairSpeed].Raw / CFixedPoint.Scale)} of a level of each building.");
		Console.WriteLine("The bombers' leaders now:");
		foreach (string line in CLeaderGainText.Lines(bomber))
		{
			Console.WriteLine(line);
		}
		return 0;
	}

	/// <summary>What a province is worth to the trial for a mission, or nothing for one that is no target.</summary>
	private static long Worth(CGameState state, EUnitOrder mission, CCountryTag defender, CProvince province)
	{
		if (mission == EUnitOrder.NavalStrike)
		{
			return state.UnitsIn(province.Id).Where(unit => unit.Kind == EUnitKind.Navy && unit.Owner == defender)
				.Sum(unit => (long)unit.SubUnits.Count);
		}
		if (province.IsSea || province.Controller != defender)
		{
			return 0;
		}
		switch (mission)
		{
			case EUnitOrder.StrategicBombing:
				return province.Modifier[CBombingOrders.Industry].Raw;
			case EUnitOrder.LogisticalStrike:
				return province.Modifier[CBombingOrders.Infrastructure].Raw + province.Pool[EGoods.Supplies].Raw;
			case EUnitOrder.RunwayCratering:
				return state.Countries.SelectMany(country => country.AllUnits())
					.Count(unit => unit.Kind == EUnitKind.Air && unit.Base == province.Id && unit.Location == province.Id);
			case EUnitOrder.InstallationStrike:
				return province.Modifier[CBombingOrders.RadarLevel].Raw;
			case EUnitOrder.PortStrike:
				return state.UnitsIn(province.Id).Where(unit => unit.Kind == EUnitKind.Navy).Sum(unit => (long)unit.SubUnits.Count);
			default:
				return state.UnitsIn(province.Id).Where(unit => CMovementRules.IsLandUnit(unit) && unit.Owner == defender)
					.Sum(unit => CUnitTotals.Strength(unit).Raw);
		}
	}

	/// <summary>A copy of what the trial reports of a province, taken before the first bomb.</summary>
	private static CProvince Snapshot(CGameState state, CProvince province)
	{
		var copy = new CProvince(province.Id, province.Name, province.IsSea);
		foreach (CBuilding building in state.Buildings)
		{
			copy.SetBuildingLevel(building, province.BuildingMaxLevel(building));
			copy.SetBuildingCurrentLevel(building, province.BuildingLevel(building));
		}
		copy.Pool.CopyFrom(province.Pool);
		copy.CurrentProducing.CopyFrom(province.CurrentProducing);
		return copy;
	}

	private static string Buildings(CGameState state, CProvince province)
	{
		var parts = new List<string>();
		foreach (CBuilding building in state.Buildings)
		{
			CFixedPoint most = province.BuildingMaxLevel(building);
			if (most > CFixedPoint.Zero)
			{
				parts.Add($"{building.Key} {province.BuildingLevel(building)}/{most}");
			}
		}
		return parts.Count == 0 ? "no buildings" : string.Join(", ", parts);
	}

	private static string Targets(CCombat bombing)
	{
		if (bombing.Defender.Units.Count == 0)
		{
			return "the ground alone";
		}
		return string.Join("; ", bombing.Defender.Units.Take(3).Select(unit =>
			$"{unit.Name} strength {CUnitTotals.Strength(unit)} organisation {CUnitTotals.AverageOrganisation(unit)}"))
			+ (bombing.Defender.Sunk.Count > 0 ? $"; {bombing.Defender.Sunk.Count} ships sunk" : "");
	}

	private static string Kind(ECombatKind kind)
	{
		return kind == ECombatKind.GroundBombing ? "the ground" : kind == ECombatKind.NavalBombing ? "ships" : "land units";
	}

	private static string Describe(CUnit unit)
	{
		return $"{unit.Name} ({unit.Owner}, {unit.SubUnits.Count} wings of {string.Join("/", unit.SubUnits.Select(wing => wing.Type.Key).Distinct())}, "
			+ $"strength {CUnitTotals.Strength(unit)}, organisation {CUnitTotals.AverageOrganisation(unit)})";
	}
}
