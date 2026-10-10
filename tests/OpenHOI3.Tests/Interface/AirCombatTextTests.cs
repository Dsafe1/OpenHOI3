using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the battle window shows of a battle in the air and of a bombing, against
/// <c>docs/mechanics/combatview.md</c>, section 11. Germany bombs Poznan and fights
/// over it; the texts are the base game's English.
/// </summary>
public class AirCombatTextTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Berlin = 1;
	private const int Poznan = 2;

	private static readonly CBuilding AirBase = new(0, "air_base", 10, CBombingOrders.AirBaseLevel, CFixedPoint.One);
	private static readonly CBuilding Guns = new(1, "anti_air", 10, CBombingOrders.AntiAir, CFixedPoint.One);
	private static readonly CBuilding Factory = new(2, "industry", 10, CBombingOrders.Industry, CFixedPoint.One);
	private static readonly CBuilding Roads = new(3, "infra", 10, CBombingOrders.Infrastructure, CFixedPoint.Parse("0.1"));
	private static readonly CBuilding Radar = new(4, "radar_station", 10, CBombingOrders.RadarLevel, CFixedPoint.One);

	private static readonly Dictionary<string, string> Texts = new()
	{
		["BATTLE_OF"] = "Battle of",
		["BOMBING_OF"] = "Bombing of",
		["PLANES_LABEL"] = "air wings",
		["anti_air"] = "Anti-Aircraft Guns",
		["industry"] = "Industrial Capacity",
		["SUPPLIES_IRO"] = "Supplies",
		["FUEL_IRO"] = "Fuel",
		["METAL_IRO"] = "Metal",
		["RAREMAT_IRO"] = "Rare Materials",
	};

	private static string Localise(string key) => Texts.GetValueOrDefault(key, key);

	private static CGameState State(out CCountry germany, out CCountry poland)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin };
		poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "", Capital = Poznan };
		var poznan = new CProvince(Poznan, "Poznan", isSea: false) { Owner = Pol, Controller = Pol };
		poznan.SetBuildingLevel(Guns, CFixedPoint.FromInt(2));
		poznan.SetBuildingLevel(Factory, CFixedPoint.FromInt(4));
		poznan.SetBuildingLevel(Roads, CFixedPoint.FromInt(9));
		poznan.MaxProducing[EGoods.Metal] = CFixedPoint.FromInt(9);
		poznan.CurrentProducing[EGoods.Metal] = CFixedPoint.FromInt(3);
		poznan.MaxProducing[EGoods.RareMaterials] = CFixedPoint.FromInt(2);
		poznan.CurrentProducing[EGoods.RareMaterials] = CFixedPoint.FromInt(2);
		poznan.Pool[EGoods.Supplies] = CFixedPoint.FromInt(100);
		return new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(Berlin, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
				poznan,
			},
			new[] { germany, poland }, buildings: new[] { AirBase, Guns, Factory, Roads, Radar });
	}

	private static CUnit Unit(CGameState state, CCountry country, EUnitKind kind, EUnitBranch branch, params string[] types)
	{
		var unit = new CUnit(kind, country.Tag) { Id = ++state.LastUnitId, Location = Poznan, Name = "Unit" };
		foreach (string key in types)
		{
			var type = new CSubUnitType(key, branch);
			type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.Parse("100"));
			type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.Parse("40"));
			type.Stats.Values.Set(CUnitTotals.SoftAttack, CFixedPoint.Parse("14"));
			type.Stats.Values.Set(CUnitTotals.HardAttack, CFixedPoint.Parse("2"));
			type.Stats.Values.Set(CUnitTotals.Defensiveness, CFixedPoint.Parse("4"));
			var subUnit = new CSubUnit(type)
			{
				Id = ++state.LastUnitId, Unit = unit, Name = "the " + key,
				Organisation = CFixedPoint.Parse("30"), Strength = CFixedPoint.Parse("75"),
			};
			subUnit.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(subUnit);
		}
		country.Units.Add(unit);
		return unit;
	}

	private static CUnit Wings(CGameState state, CCountry country, params string[] types)
	{
		return Unit(state, country, EUnitKind.Air, EUnitBranch.Air, types);
	}

	private static CCombat Bombing(CGameState state, CUnit bombers, EUnitOrder order,
		ECombatKind kind = ECombatKind.GroundBombing)
	{
		bombers.Order = order;
		var bombing = new CCombat(Poznan, kind);
		state.Combats.Add(bombing);
		bombing.Attacker.Add(bombers);
		return bombing;
	}

	[Fact]
	public void A_bombing_is_named_bombing_of_its_province_and_a_battle_in_the_air_battle_of_it()
	{
		CGameState state = State(out CCountry germany, out _);
		CCombat bombing = Bombing(state, Wings(state, germany, "strategic_bomber"), EUnitOrder.StrategicBombing);

		Assert.Equal("Bombing of Poznan", CCombatViewText.Name(Localise, state, bombing));
		Assert.Equal("Battle of Poznan", CCombatViewText.Name(Localise, state, new CCombat(Poznan, ECombatKind.Air)));
		Assert.True(CAirCombatText.HasAirSide(bombing));
		Assert.True(CAirCombatText.HasAirSide(new CCombat(Poznan, ECombatKind.Air)));
		Assert.False(CAirCombatText.HasAirSide(new CCombat(Poznan, ECombatKind.Naval)));
	}

	[Theory]
	[InlineData(ECombatKind.Land, 3, false)]
	[InlineData(ECombatKind.Land, 4, true)]
	[InlineData(ECombatKind.Naval, 9, false)]
	[InlineData(ECombatKind.Air, 9, false)]
	[InlineData(ECombatKind.GroundBombing, 9, false)]
	[InlineData(ECombatKind.LandBombing, 9, false)]
	[InlineData(ECombatKind.NavalBombing, 9, false)]
	public void Only_a_battle_on_land_has_a_marker_and_not_before_its_fourth_hour(ECombatKind kind, int hours, bool marked)
	{
		Assert.Equal(marked, CCombatViewText.HasMarker(new CCombat(Poznan, kind) { Duration = hours }));
	}

	[Fact]
	public void A_side_says_how_many_wings_it_has_and_the_bombed_side_says_nothing()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		var battle = new CCombat(Poznan, ECombatKind.Air);
		battle.Attacker.Add(Wings(state, germany, "interceptor", "interceptor"));
		battle.Attacker.Add(Wings(state, germany, "tactical_bomber"));
		battle.Defender.Add(Wings(state, poland, "interceptor"));

		Assert.Equal("§Y3§W air wings", CAirCombatText.PlanesLabel(Localise, battle.Attacker));
		Assert.Equal("§Y1§W air wings", CAirCombatText.PlanesLabel(Localise, battle.Defender));

		CCombat bombing = Bombing(state, Wings(state, germany, "strategic_bomber"), EUnitOrder.StrategicBombing);
		Assert.Equal("§Y1§W air wings", CAirCombatText.PlanesLabel(Localise, bombing.Attacker));
		Assert.Equal("", CAirCombatText.PlanesLabel(Localise, bombing.Defender));
	}

	[Fact]
	public void A_side_in_the_air_lists_its_wings_unit_by_unit()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		var battle = new CCombat(Poznan, ECombatKind.Air);
		CUnit first = Wings(state, germany, "interceptor", "cas");
		CUnit second = Wings(state, germany, "tactical_bomber");
		battle.Attacker.Add(first);
		battle.Attacker.Add(second);
		battle.Defender.Add(Wings(state, poland, "interceptor"));

		List<CCombatRow> rows = CAirCombatText.Rows(state, battle.Attacker);
		Assert.All(rows, row => Assert.Equal(ECombatRowKind.SubUnit, row.Kind));
		Assert.Equal(new[] { first.SubUnits[0], first.SubUnits[1], second.SubUnits[0] }, rows.Select(row => row.SubUnit));
		Assert.Single(CAirCombatText.Rows(state, battle.Defender));
	}

	[Fact]
	public void The_bombed_side_lists_what_the_first_bombers_mission_bombs_and_then_its_units()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit bombers = Wings(state, germany, "strategic_bomber");
		CCombat bombing = Bombing(state, bombers, EUnitOrder.StrategicBombing, ECombatKind.LandBombing);
		CUnit division = Unit(state, poland, EUnitKind.Division, EUnitBranch.Land, "infantry_brigade", "artillery_brigade");
		bombing.Defender.Add(division);

		// The guns and the factory, in the buildings' own order - there is no radar and
		// the roads are not this mission's; then what is made of each resource the
		// province can make any of; then the division's regiments.
		List<CCombatRow> rows = CAirCombatText.Rows(state, bombing.Defender);
		Assert.Equal(new[]
		{
			new CCombatRow(ECombatRowKind.Building, Building: Guns),
			new CCombatRow(ECombatRowKind.Building, Building: Factory),
			new CCombatRow(ECombatRowKind.GoodMade, Goods: EGoods.Metal),
			new CCombatRow(ECombatRowKind.GoodMade, Goods: EGoods.RareMaterials),
			new CCombatRow(ECombatRowKind.SubUnit, SubUnit: division.SubUnits[0]),
			new CCombatRow(ECombatRowKind.SubUnit, SubUnit: division.SubUnits[1]),
		}, rows);
		// The bombers' own list has their wings and nothing else.
		Assert.Equal(new[] { new CCombatRow(ECombatRowKind.SubUnit, SubUnit: bombers.SubUnits[0]) },
			CAirCombatText.Rows(state, bombing.Attacker));

		// A logistical strike: the roads and the guns, and what lies there of supplies
		// and of fuel - of which there is none, and the row is there all the same.
		bombers.Order = EUnitOrder.LogisticalStrike;
		Assert.Equal(new[]
		{
			new CCombatRow(ECombatRowKind.Building, Building: Guns),
			new CCombatRow(ECombatRowKind.Building, Building: Roads),
			new CCombatRow(ECombatRowKind.GoodHeld, Goods: EGoods.Supplies),
			new CCombatRow(ECombatRowKind.GoodHeld, Goods: EGoods.Fuel),
		}, CAirCombatText.Rows(state, bombing.Defender).Take(4));

		// It is the first bomber's mission that says: a second unit under another adds nothing.
		CUnit others = Wings(state, germany, "tactical_bomber");
		others.Order = EUnitOrder.StrategicBombing;
		bombing.Attacker.Add(others);
		Assert.Equal(6, CAirCombatText.Rows(state, bombing.Defender).Count);

		// A mission that bombs nothing of the province leaves the units alone.
		bombers.Order = EUnitOrder.GroundAttack;
		Assert.Equal(2, CAirCombatText.Rows(state, bombing.Defender).Count);
	}

	[Fact]
	public void A_row_has_its_name_its_picture_and_its_bars()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit bombers = Wings(state, germany, "strategic_bomber");
		CCombat bombing = Bombing(state, bombers, EUnitOrder.StrategicBombing);
		CProvince poznan = state.Province(Poznan);
		poznan.SetBuildingCurrentLevel(Factory, CFixedPoint.Parse("1.5"));

		var wing = new CCombatRow(ECombatRowKind.SubUnit, SubUnit: bombers.SubUnits[0]);
		Assert.Equal("the strategic_bomber", CAirCombatText.RowName(Localise, wing));
		Assert.Equal("GFX_counter_strategic_bomber", CAirCombatText.RowSprite(wing));
		Assert.True(CAirCombatText.HasOrganisation(wing));
		Assert.Equal(75, CAirCombatText.StrengthBar(state, bombing, wing));
		Assert.Equal(75, CAirCombatText.OrganisationBar(wing));

		// What works of a building, of what was built.
		var factory = new CCombatRow(ECombatRowKind.Building, Building: Factory);
		Assert.Equal("Industrial Capacity", CAirCombatText.RowName(Localise, factory));
		Assert.Equal("icon_build_industry", CAirCombatText.RowSprite(factory));
		Assert.False(CAirCombatText.HasOrganisation(factory));
		Assert.Equal(37, CAirCombatText.StrengthBar(state, bombing, factory));
		Assert.Equal(100, CAirCombatText.StrengthBar(state, bombing, new CCombatRow(ECombatRowKind.Building, Building: Guns)));
		// Of a building the province has none of, nothing.
		Assert.Equal(0, CAirCombatText.StrengthBar(state, bombing, new CCombatRow(ECombatRowKind.Building, Building: Radar)));

		// What lies there, full at two hundred; a good's picture is its frame of a strip.
		var supplies = new CCombatRow(ECombatRowKind.GoodHeld, Goods: EGoods.Supplies);
		Assert.Equal("Supplies", CAirCombatText.RowName(Localise, supplies));
		Assert.Equal("", CAirCombatText.RowSprite(supplies));
		Assert.Equal(1, CAirCombatText.GoodsFrame(supplies));
		Assert.Equal(50, CAirCombatText.StrengthBar(state, bombing, supplies));
		poznan.Pool[EGoods.Supplies] = CFixedPoint.FromInt(500);
		Assert.Equal(250, CAirCombatText.StrengthBar(state, bombing, supplies));

		// What is made there today, of the most that can be.
		var metal = new CCombatRow(ECombatRowKind.GoodMade, Goods: EGoods.Metal);
		Assert.Equal("Metal", CAirCombatText.RowName(Localise, metal));
		Assert.Equal(5, CAirCombatText.GoodsFrame(metal));
		Assert.Equal(33, CAirCombatText.StrengthBar(state, bombing, metal));
		Assert.Equal(7, CAirCombatText.GoodsFrame(new CCombatRow(ECombatRowKind.GoodMade, Goods: EGoods.RareMaterials)));
		Assert.Equal(0, CAirCombatText.StrengthBar(state, bombing, new CCombatRow(ECombatRowKind.GoodMade, Goods: EGoods.Energy)));
	}

	[Fact]
	public void A_wing_is_worth_its_own_and_a_regiment_under_bombs_what_it_is_on_land()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit bombers = Wings(state, germany, "tactical_bomber", "tactical_bomber");
		CCombat bombing = Bombing(state, bombers, EUnitOrder.GroundAttack, ECombatKind.LandBombing);

		// 30 organisation by 75 strength a wing, whatever it carries; and the bombed
		// side with nobody on it counts for the least there is.
		Assert.Equal(2 * 2250000, CCombatViewText.Worth(bombing.Attacker));
		Assert.Equal(0, CCombatViewText.Worth(bombing.Defender));
		Assert.Equal(999, CCombatViewText.Share(bombing));
		bombers.SubUnits[0].CombatAttack = CFixedPoint.Parse("0.5");
		Assert.Equal(2250000 + 1125000, CCombatViewText.Worth(bombing.Attacker));
		// The unit's own figures are a land unit's and do nothing for a wing.
		bombers.CombatAttack = CFixedPoint.Parse("3");
		Assert.Equal(2250000 + 1125000, CCombatViewText.Worth(bombing.Attacker));

		// A regiment counts as in a battle on land: by its unit's hour and a tenth of
		// its arms - 14 and 2 and a defender's 4 make two.
		CUnit division = Unit(state, poland, EUnitKind.Division, EUnitBranch.Land, "infantry_brigade");
		bombing.Defender.Add(division);
		Assert.Equal(2250000 * 2, CCombatViewText.Worth(bombing.Defender));
		division.CombatDefence = CFixedPoint.Parse("0.5");
		Assert.Equal(2250000, CCombatViewText.Worth(bombing.Defender));
		division.SubUnits[0].CombatAttack = CFixedPoint.Parse("9");
		Assert.Equal(2250000, CCombatViewText.Worth(bombing.Defender));
	}

	[Fact]
	public void The_modifiers_of_the_air_stand_among_the_others_in_their_own_order()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		var battle = new CCombat(Poznan, ECombatKind.Air);
		CUnit ours = Wings(state, germany, "interceptor");
		CUnit theirs = Wings(state, poland, "interceptor");
		battle.Attacker.Add(ours);
		battle.Defender.Add(theirs);
		void Note(CUnit unit, ECombatModifier kind) =>
			unit.SubUnits[0].CombatModifiers.Add(new CCombatModifier(kind, CFixedPoint.One, CFixedPoint.One));
		Note(ours, ECombatModifier.Night);
		Note(ours, ECombatModifier.Intercept);
		Note(ours, ECombatModifier.Radio);
		Note(ours, ECombatModifier.Stacking);
		Note(theirs, ECombatModifier.Radar);
		Note(theirs, ECombatModifier.BaseProximity);
		Note(theirs, ECombatModifier.AirCombat);
		Note(theirs, ECombatModifier.Night);

		Assert.Equal(new[]
		{
			"CMOD_BM_DIVISION_PENALTY", "CMOD_BM_RADIO", "CMOD_BM_BASE_PROXIMITY", "CMOD_BM_RADAR_STATION",
			"CMOD_BM_INTERCEPT", "CMOD_BM_AIRCOMBAT", "CMOD_BM_NIGHT_MODIFIER",
		}, CCombatViewText.Modifiers(battle));
	}

	[Fact]
	public void The_window_stays_while_the_bombers_have_a_row_and_an_air_battle_s_while_both_sides_have()
	{
		CGameState state = State(out CCountry germany, out _);
		CCombat bombing = Bombing(state, Wings(state, germany, "strategic_bomber"), EUnitOrder.StrategicBombing);
		var battle = new CCombat(Poznan, ECombatKind.Air);

		Assert.True(CAirCombatText.KeepsWindowUp(bombing.Attacker, 1));
		Assert.False(CAirCombatText.KeepsWindowUp(bombing.Attacker, 0));
		Assert.True(CAirCombatText.KeepsWindowUp(bombing.Defender, 0));     // its list is not asked
		Assert.False(CAirCombatText.KeepsWindowUp(battle.Attacker, 0));
		Assert.False(CAirCombatText.KeepsWindowUp(battle.Defender, 0));
		Assert.True(CAirCombatText.KeepsWindowUp(battle.Defender, 2));
	}
}
