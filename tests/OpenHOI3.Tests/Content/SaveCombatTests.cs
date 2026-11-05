using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// A save's battles, finished battles and sunk ships as <c>docs/mechanics/savegame.md</c>, section 7, reads them, on
/// two provinces of land and one of sea:
///
///     1 Kiel (Germany's)      2 Posen (Poland's)      3 Baltic
/// </summary>
public class SaveCombatTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private static CGameState State(out Dictionary<int, CUnit> units)
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		// The database's order: Alpini before anti_air, upper case first.
		var alpini = new CSubUnitType("Alpini_brigade", EUnitBranch.Land) { DatabaseIndex = 1 };
		var infantry = new CSubUnitType("infantry_brigade", EUnitBranch.Land) { DatabaseIndex = 2 };
		var destroyer = new CSubUnitType("destroyer", EUnitBranch.Naval) { DatabaseIndex = 3 };
		var bomber = new CSubUnitType("tactical_bomber", EUnitBranch.Air) { DatabaseIndex = 4 };
		var state = new CGameState(new CDate(1938, 8, 3),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(1, "Kiel", isSea: false) { Owner = Ger, Controller = Ger },
				new CProvince(2, "Posen", isSea: false) { Owner = Pol, Controller = Pol },
				new CProvince(3, "Baltic", isSea: true),
			},
			new[] { germany, poland },
			buildings: new[] { new CBuilding(0, "industry", 10, "industry", CFixedPoint.One), new CBuilding(1, "anti_air", 10, "", CFixedPoint.Zero) })
		{
			SubUnitTypes = new Dictionary<string, CSubUnitType>
			{
				[alpini.Key] = alpini, [infantry.Key] = infantry, [destroyer.Key] = destroyer, [bomber.Key] = bomber,
			},
			Tactics = new[] { new CCombatTactic("basic_attack", 1), new CCombatTactic("basic_defend", 2) },
			Terrains = new[] { new CTerrain("plains", isWater: false, default) },
		};
		var made = new Dictionary<int, CUnit>();
		units = made;
		void Unit(int id, CCountry owner, EUnitKind kind, CSubUnitType type, int location)
		{
			var unit = new CUnit(kind, owner.Tag) { Id = id, Location = location, Name = $"unit {id}" };
			unit.SubUnits.Add(new CSubUnit(type) { Id = id * 10, Unit = unit, Strength = CFixedPoint.FromInt(3) });
			owner.Units.Add(unit);
			made[id] = unit;
		}
		Unit(1, germany, EUnitKind.Division, infantry, 2);
		Unit(2, germany, EUnitKind.Division, alpini, 2);
		Unit(3, poland, EUnitKind.Division, infantry, 2);
		Unit(4, germany, EUnitKind.Air, bomber, 1);
		Unit(5, poland, EUnitKind.Division, infantry, 2);
		return state;
	}

	private static void Read(CGameState state, Dictionary<int, CUnit> units, string text, CSaveReport? report = null)
	{
		CSaveCombats.Read(state, ClausewitzParser.Parse(text), units, new CSaveIds(), report ?? new CSaveReport());
	}

	[Fact]
	public void A_land_battle_takes_its_units_back_and_its_size_overwrites_what_their_joining_counted()
	{
		CGameState state = State(out Dictionary<int, CUnit> units);
		Read(state, units, """
			land_combat = { location = 2 day = 58 duration = 58
				attacker = { dice = 3 unit = { id = 1 type = 41 } unit = { id = 2 type = 41 } losses = 366.400
					size = { 0.000 997.500 29985.000 0.000 0.000 } tactic = 1
					front = { id = 2 type = 41 } reserves = { id = 1 type = 41 } }
				defender = { dice = 7 unit = { id = 3 type = 41 } losses = 250.800 size = { 0.000 0.000 12000.000 0.000 0.000 }
					tactic = 2 front = { id = 3 type = 41 } }
				terrain = "plains" }
			""");

		CCombat combat = Assert.Single(state.Combats);
		Assert.Equal((58, 58, true), (combat.Hour, combat.Duration, combat.HasBegun));
		Assert.Equal(new[] { units[1], units[2] }, combat.Attacker.Units);
		// The front keeps the save's order, not the units'.
		Assert.Equal(new[] { units[2] }, combat.Attacker.Front);
		Assert.Equal(new[] { units[1] }, combat.Attacker.Reserves);
		Assert.Equal("basic_attack", combat.Attacker.Tactic!.Name);
		Assert.Equal((3, new CFixedPoint(366400)), (combat.Attacker.Dice, combat.Attacker.Losses));
		// Each unit knows its battle again.
		Assert.All(units.Values.Where(u => u.Kind == EUnitKind.Division && u.Id != 5), u => Assert.Contains(combat, u.Combats));
		// The joins counted three hundred men a brigade; the save's figures stand, by the database's order.
		Assert.Equal(new CFixedPoint(997500), combat.Attacker.Size["Alpini_brigade"]);
		Assert.Equal(new CFixedPoint(29985000), combat.Attacker.Size["infantry_brigade"]);
		Assert.True(combat.Attacker.IsFrontSetUp && combat.Defender.IsFrontSetUp);
	}

	[Fact]
	public void A_side_saved_before_its_first_hour_is_set_up_only_where_it_attacks()
	{
		CGameState state = State(out Dictionary<int, CUnit> units);
		Read(state, units, """
			land_combat = { location = 2 day = -1 duration = 0
				attacker = { dice = 0 unit = { id = 1 type = 41 } losses = 0.000 }
				defender = { dice = 0 unit = { id = 3 type = 41 } losses = 0.000 }
				terrain = "plains" }
			""");

		CCombat combat = Assert.Single(state.Combats);
		Assert.Equal(CCombat.NotStarted, combat.Hour);
		Assert.True(combat.HasBegun);
		// The original's load sets up the attacker's front twice and never the defender's.
		Assert.True(combat.Attacker.IsFrontSetUp);
		Assert.False(combat.Defender.IsFrontSetUp);
		Assert.Empty(combat.Defender.Front);
	}

	[Fact]
	public void Bombings_come_back_turned_round_in_front_with_the_bombers_tallies_in_their_orders()
	{
		CGameState state = State(out Dictionary<int, CUnit> units);
		Read(state, units, """
			land_bombing = { location = 2 day = 1 duration = 1
				attacker = { dice = 8 unit = { id = 4 type = 41 } losses = 0.040 size = { 0 0 0 0 0 } strength = 46.039
					building = { 0.000 0.000 0.426 } pool = { 0.100 0.200 0.000 0.000 0.000 0.000 0.000 }
					current_producing = { 0.000 0.000 0.000 0.000 2.300 0.000 0.000 } }
				defender = { dice = 1 unit = { id = 5 type = 41 } losses = 45.900 size = { 0 0 0 0 0 } }
				terrain = "plains" }
			ground_bombing = { location = 1 day = 1 duration = 1
				attacker = { dice = 2 losses = 0.000 size = { 0 0 0 0 0 } strength = 0.000 building = { 0 0 0 } pool = { 0 0 0 0 0 0 0 }
					current_producing = { 0 0 0 0 0 0 0 } }
				defender = { dice = 6 losses = 0.000 size = { 0 0 0 0 0 } }
				terrain = "plains" }
			land_combat = { location = 2 day = 5 duration = 5
				attacker = { dice = 3 unit = { id = 1 type = 41 } losses = 1.000 size = { 0 0 0 0 0 } tactic = 1 front = { id = 1 type = 41 } }
				defender = { dice = 3 unit = { id = 3 type = 41 } losses = 1.000 size = { 0 0 0 0 0 } tactic = 2 front = { id = 3 type = 41 } }
				terrain = "plains" }
			""");

		Assert.Equal(new[] { ECombatKind.GroundBombing, ECombatKind.LandBombing, ECombatKind.Land }, state.Combats.Select(c => c.Kind));
		CCombat land = state.Combats[1];
		Assert.Same(land, units[4].Bombing);
		Assert.Empty(units[4].Combats);
		CCombatSide bombers = land.Attacker;
		Assert.Equal(new CFixedPoint(46039), bombers.StrengthDealt);
		// The empty building first, then the buildings in their file's order.
		Assert.Equal(new CFixedPoint(426), bombers.BuildingsDestroyed["anti_air"]);
		Assert.False(bombers.BuildingsDestroyed.ContainsKey("industry"));
		Assert.Equal(new CFixedPoint(200), bombers.StockpileDestroyed[EGoods.Fuel]);
		Assert.Equal(new CFixedPoint(2300), bombers.ProductionDestroyed[EGoods.Metal]);
		// A ground bombing's side with nobody on it is the province's holder's.
		Assert.Equal(new[] { Ger }, state.Combats[0].Defender.Standing);
	}

	[Fact]
	public void Battles_and_the_finished_ones_are_written_back_to_the_byte()
	{
		// A bombing's attacker writes its four tallies, a land side its tactic, front and reserves; a finished battle is an
		// empty key and its block, a one-space token after each - the first after a line of indentation, the closing
		// brace after the last token.
		const string Size0 = "0.000 0.000 0.000 0.000 0.000 \t\t\t}\r\n";
		string Pair(string key, int id) => $"\t\t\t{key}=\r\n\t\t\t{{\r\n\t\t\t\tid={id}\r\n\t\t\t\ttype=41\r\n\t\t\t}}\r\n";
		string saved =
			"combat=\r\n{\r\n" +
			"\tland_bombing=\r\n\t{\r\n\t\tlocation=2\r\n\t\tday=1\r\n\t\tduration=1\r\n" +
			"\t\tattacker=\r\n\t\t{\r\n\t\t\tdice=8\r\n" + Pair("unit", 4) + "\t\t\tlosses=0.040\r\n" +
			"\t\t\tsize=\r\n\t\t\t{\r\n0.000 0.000 0.000 0.000 1.000 \t\t\t}\r\n\t\t\tstrength=46.039\r\n" +
			"\t\t\tbuilding=\r\n\t\t\t{\r\n0.000 0.000 0.426 \t\t\t}\r\n" +
			"\t\t\tpool=\r\n\t\t\t{\r\n0.100 0.200 0.000 0.000 0.000 0.000 0.000 \t\t\t}\r\n" +
			"\t\t\tcurrent_producing=\r\n\t\t\t{\r\n0.000 0.000 0.000 0.000 2.300 0.000 0.000 \t\t\t}\r\n\t\t}\r\n" +
			"\t\tdefender=\r\n\t\t{\r\n\t\t\tdice=1\r\n" + Pair("unit", 5) + "\t\t\tlosses=45.900\r\n\t\t\tsize=\r\n\t\t\t{\r\n" + Size0 + "\t\t}\r\n" +
			"\t\tterrain=\"plains\"\r\n\t}\r\n" +
			"\tland_combat=\r\n\t{\r\n\t\tlocation=2\r\n\t\tday=5\r\n\t\tduration=5\r\n" +
			"\t\tattacker=\r\n\t\t{\r\n\t\t\tdice=3\r\n" + Pair("unit", 1) + Pair("unit", 2) + "\t\t\tlosses=1.000\r\n" +
			"\t\t\tsize=\r\n\t\t\t{\r\n0.000 300.000 300.000 0.000 0.000 \t\t\t}\r\n\t\t\ttactic=1\r\n" + Pair("front", 1) + Pair("reserves", 2) + "\t\t}\r\n" +
			"\t\tdefender=\r\n\t\t{\r\n\t\t\tdice=3\r\n" + Pair("unit", 3) + "\t\t\tlosses=1.000\r\n" +
			"\t\t\tsize=\r\n\t\t\t{\r\n0.000 0.000 300.000 0.000 0.000 \t\t\t}\r\n\t\t\ttactic=2\r\n" + Pair("front", 3) + "\t\t}\r\n" +
			"\t\tterrain=\"plains\"\r\n\t}\r\n" +
			"\thistory=\r\n\t{\r\n\t\thistory=\r\n\t\t{\r\n\t\t\t\r\n" +
			"\t\t\t{\r\n\t\t\t\tlocation=11932\r\n\t\t\t\tattacker=\"GER\"\r\n\t\t\t\tdefender=\"POL\"\r\n\t\t\t\tend_date=\"1938.8.1.4\"\r\n" +
			"\t\t\t\tconvoy=yes\r\n\t\t\t\ttype=2\r\n\t\t\t}\r\n \r\n" +
			"\t\t\t{\r\n\t\t\t\tlocation=5638\r\n\t\t\t\tattacker=\"GER\"\r\n\t\t\t\tdefender=\"---\"\r\n\t\t\t\tend_date=\"1938.8.2.10\"\r\n" +
			"\t\t\t\ttype=5\r\n\t\t\t}\r\n \t\t}\r\n\t}\r\n}\r\n";
		CGameState state = State(out Dictionary<int, CUnit> units);
		state.HistoryUnitNext = 1000;
		state.Province(2).Terrain = state.Terrains[0];
		CSaveCombats.Read(state, ClausewitzParser.Parse(saved).Get("combat")!, units, new CSaveIds(), new CSaveReport());

		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		CSaveCombats.Write(writer, state);

		Assert.Equal(saved, writer.Text);
	}

	[Fact]
	public void The_finished_battles_are_put_at_the_end_as_written_and_a_sides_sunk_ship_is_one_of_the_sunk_ships()
	{
		CGameState state = State(out Dictionary<int, CUnit> units);
		var ship = new CSubUnit(state.SubUnitTypes["destroyer"]) { Id = 4775, SunkById = 2090 };
		CSunkShips.Add(state, Pol, ship);
		var report = new CSaveReport();
		Read(state, units, """
			naval_combat = { location = 3 day = 2 duration = 2
				attacker = { dice = 4 losses = 0.000 size = { 0 0 0 0 0 } positioning = 0.560 }
				defender = { dice = 5 sunk_ship = { id = 4775 type = 41 } sunk_ship = { id = 9999 type = 41 } losses = 1000.000
					size = { 0 0 0 1000.000 0 } positioning = 0.300 }
				terrain = "ocean" }
			history = { history = {
				{ location = 11932 attacker = "GER" defender = "POL" end_date = "1938.8.1.4" convoy = yes type = 2 }
				{ location = 5638 attacker = "GER" defender = "---" end_date = "1938.8.2.10" type = 5 } } }
			""", report);

		CCombat combat = Assert.Single(state.Combats);
		Assert.Equal(new CFixedPoint(560), combat.Attacker.Positioning);
		Assert.Same(ship, Assert.Single(combat.Defender.Sunk));
		Assert.Contains(("naval_combat side", "a sunk ship not among sunk_ships, left out"), report.UnreadKeys.Keys);
		Assert.Contains(("naval_combat", "a terrain of no name known, ocean"), report.UnreadKeys.Keys);

		Assert.Equal(2, state.CombatHistory.Entries.Count);
		CFinishedCombat raid = state.CombatHistory.Entries[0];
		Assert.Equal((ECombatKind.Naval, 11932, true), (raid.Kind, raid.Province, raid.IsConvoy));
		CFinishedCombat bombing = state.CombatHistory.Entries[1];
		Assert.Equal((ECombatKind.LandBombing, (CCountryTag?)Ger, (CCountryTag?)null), (bombing.Kind, bombing.Attacker, bombing.Defender));
	}

	[Fact]
	public void A_side_counts_a_land_brigades_men_and_a_whole_ship_and_counts_a_unit_again_when_it_joins_again()
	{
		CGameState state = State(out Dictionary<int, CUnit> units);
		var land = new CCombat(2);
		land.Attacker.Add(units[1]);
		land.Attacker.Add(units[1]);
		// Three strength is three hundred men, twice over.
		Assert.Equal(CFixedPoint.FromInt(600), land.Attacker.Size["infantry_brigade"]);

		var fleet = new CUnit(EUnitKind.Navy, Pol) { Id = 6, Location = 3 };
		fleet.SubUnits.Add(new CSubUnit(state.SubUnitTypes["destroyer"]) { Unit = fleet, Strength = CFixedPoint.FromInt(1) });
		var sea = new CCombat(3, ECombatKind.Naval);
		sea.Defender.Add(fleet);
		Assert.Equal(CFixedPoint.One, sea.Defender.Size["destroyer"]);
	}
}
