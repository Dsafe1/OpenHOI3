using System;
using System.Collections.Generic;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the panel of a selected unit says, against <c>docs/mechanics/unitpanel.md</c>. The
/// base game's infantry brigade has a strength of 30, an organisation of 30, a width of 1
/// and a weight of 10, and walks at 4.
/// </summary>
public class UnitPanelTextTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Berlin = 1;
	private const int Potsdam = 2;
	private const int Dessau = 3;

	private static readonly Dictionary<string, string> Texts = new()
	{
		["NO_LEADER"] = "(no leader)",
		["SUPPLIED_FROM"] = "Supplied from:",
		["KPH"] = "KPH",
		["MOVE_ORDER_D"] = "Move to $WHERE$.",
		["ATTACKING_TO_OI"] = "Attacking $WHERE$.",
		["DEFENDING_OI"] = "Defending.",
		["RETREAT_TO_OI"] = "Retreating to $WHERE$.",
		["NO_ORDERS"] = "No Order",
		["LAND_COUNT"] = "§Y$NUM$§W Brigades",
		["NAVAL_COUNT"] = "$CAPITAL$ Capital Ships, $ESCORTS$ Screens & $OTHER$ others.",
		["AIR_COUNT"] = "$FIGHTER$ Fighters & $BOMBER$ Bombers.",
		["ORDER_REBASE"] = "Rebase",
		["ORDER_AIR_REBASE_SHORT_DESC"] = "Rebase($PROV$)",
		["NUM_GROUPS"] = "Army group(s)",
		["NUM_ARMIES"] = "Army(s)",
		["NUM_CORPS"] = "Corps",
		["NUM_DIVISONS"] = "Division(s)",
	};

	private static string Localise(string key) => Texts.GetValueOrDefault(key, key);

	private static CSubUnitType Type(string key, EUnitBranch branch = EUnitBranch.Land, string speed = "4",
		string strength = "30", string organisation = "30", string width = "1", string weight = "10")
	{
		var type = new CSubUnitType(key, branch);
		type.Stats.Values.Set(CMovementSpeed.MaximumSpeed, CFixedPoint.Parse(speed));
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.Parse(strength));
		type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.Parse(organisation));
		type.Stats.Values.Set(CSubUnitType.CombatWidth, CFixedPoint.Parse(width));
		type.Stats.Values.Set(CSubUnitType.TransportWeight, CFixedPoint.Parse(weight));
		return type;
	}

	private static CSubUnitType Infantry() => Type("infantry_brigade");

	private static CGameState State(out CCountry country)
	{
		country = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin };
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		var defines = new CDefines();
		defines.Set(CManpower.MilitarySection, CMovementSpeed.LandSpeedModifier, CFixedPoint.Parse("0.05"));
		return new CGameState(new CDate(1936, 1, 1), new[]
		{
			new CProvince(0, "", isSea: false),
			new CProvince(Berlin, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
			new CProvince(Potsdam, "Potsdam", isSea: false) { Owner = Ger, Controller = Ger },
			new CProvince(Dessau, "Dessau", isSea: false) { Owner = Ger, Controller = Ger },
		}, new[] { country, poland }, defines: defines);
	}

	private static CUnit Unit(CCountry country, EUnitKind kind, int location, params CSubUnitType[] types)
	{
		var unit = new CUnit(kind, country.Tag) { Id = country.Units.Count + 1, Location = location, Name = "Unit" };
		foreach (CSubUnitType type in types)
		{
			var subUnit = new CSubUnit(type) { Unit = unit, Strength = type.Stats[CSubUnitType.MaxStrength] };
			subUnit.Stats.CopyFrom(type.Stats);
			subUnit.Organisation = type.Stats[CSubUnitType.DefaultOrganisation];
			unit.SubUnits.Add(subUnit);
		}
		country.Units.Add(unit);
		return unit;
	}

	// --- the header ---

	[Fact]
	public void TheActivityStripIsEmptyForAUnitAtRestAndAGreenArrowForOneOnTheMarch()
	{
		State(out CCountry country);
		CUnit division = Unit(country, EUnitKind.Division, Berlin, Infantry());
		CUnit empty = Unit(country, EUnitKind.Division, Berlin);

		Assert.Equal(1, CUnitPanelText.ActivityFrame(division));
		division.Path.Add(Potsdam);
		Assert.Equal(5, CUnitPanelText.ActivityFrame(division));
		Assert.Equal(6, CUnitPanelText.ActivityFrame(empty));            // the cross: nothing in it to act
	}

	[Fact]
	public void ABattleARetreatAndAnAttackDelayEachHaveTheirMarkAndTheirLine()
	{
		CGameState state = State(out CCountry country);
		CUnit attacker = Unit(country, EUnitKind.Division, Berlin, Infantry());
		CUnit defender = Unit(country, EUnitKind.Division, Potsdam, Infantry());
		var combat = new CCombat(Potsdam);
		combat.Attacker.Add(attacker);
		combat.Defender.Add(defender);
		attacker.Path.Add(Potsdam);

		Assert.Equal(4, CUnitPanelText.ActivityFrame(attacker));       // the red arrow
		Assert.Equal("Attacking Potsdam.", CUnitPanelText.Order(Localise, state, attacker));
		Assert.Equal(3, CUnitPanelText.ActivityFrame(defender));       // the star
		Assert.Equal("Defending.", CUnitPanelText.Order(Localise, state, defender));

		// An attacker with its way taken from it is still attacking the battle's province.
		attacker.Path.Clear();
		Assert.Equal("Attacking Potsdam.", CUnitPanelText.Order(Localise, state, attacker));

		defender.Combats.Clear();
		defender.IsRetreating = true;
		defender.Path.Add(Dessau);
		Assert.Equal(2, CUnitPanelText.ActivityFrame(defender));       // the white arrow
		Assert.Equal("Retreating to Dessau.", CUnitPanelText.Order(Localise, state, defender));

		attacker.Combats.Clear();
		attacker.AttackDelay = CFixedPoint.FromInt(100);
		attacker.Path.Add(Potsdam);
		Assert.Equal(6, CUnitPanelText.ActivityFrame(attacker));       // the cross, though it has somewhere to go
		Assert.Equal("Move to Potsdam.", CUnitPanelText.Order(Localise, state, attacker));
	}

	[Fact]
	public void TheStatusLineMarksAUnitDugInAndOneWithAnAttackDelayRunning()
	{
		CGameState state = State(out CCountry country);
		CUnit unit = Unit(country, EUnitKind.Division, Berlin, Infantry());
		Assert.False(CUnitPanelText.IsDugIn(unit));
		Assert.False(CUnitPanelText.IsDelayed(unit));
		Assert.False(CUnitPanelText.HasCombinedArms(state, unit));     // no groups are declared here

		unit.DigIn = CFixedPoint.One;
		unit.AttackDelay = CFixedPoint.Parse("0.5");
		Assert.True(CUnitPanelText.IsDugIn(unit));
		Assert.True(CUnitPanelText.IsDelayed(unit));
	}

	[Fact]
	public void TheStrengthIsMenAHundredToThePointEachBrigadeCutToAWholeMan()
	{
		State(out CCountry country);
		CUnit division = Unit(country, EUnitKind.Division, Berlin, Infantry(), Infantry(), Infantry());

		Assert.Equal("9,000", CUnitPanelText.Strength(division));
		foreach (CSubUnit subUnit in division.SubUnits)
		{
			subUnit.Strength = CFixedPoint.Parse("29.995");              // 2999.5 men: 2999
		}
		Assert.Equal(8997, CUnitPanelText.Men(division));
		Assert.Equal("8,997", CUnitPanelText.Strength(division));

		CUnit weak = Unit(country, EUnitKind.Division, Berlin, Infantry());
		weak.SubUnits[0].Strength = CFixedPoint.Parse("9.07");
		Assert.Equal("907", CUnitPanelText.Strength(weak));              // whole under a thousand
		weak.SubUnits[0].Strength = CFixedPoint.Parse("10.05");
		Assert.Equal("1,005", CUnitPanelText.Strength(weak));            // the last three digits padded

		CUnit fleet = Unit(country, EUnitKind.Navy, Berlin, Type("destroyer", EUnitBranch.Naval));
		Assert.Equal("", CUnitPanelText.Strength(fleet));
	}

	[Fact]
	public void TheOrganisationBarIsTheAverageOverTheTypesOwnFiguresAndTheBranchsModifier()
	{
		State(out CCountry country);
		CUnit division = Unit(country, EUnitKind.Division, Berlin, Infantry(), Type("artillery_brigade", organisation: "20"));

		Assert.Equal(CFixedPoint.FromInt(25), CUnitPanelText.MaxOrganisation(country, division));
		Assert.Equal(100, CUnitPanelText.OrganisationBar(country, division));

		division.SubUnits[0].Organisation = CFixedPoint.FromInt(15);     // 15 and 20: 17.5 of 25
		Assert.Equal(70, CUnitPanelText.OrganisationBar(country, division));

		// Officers do not come into this bar; the country's modifier for the branch does.
		country.OfficerRatio = CFixedPoint.Parse("0.5");
		Assert.Equal(70, CUnitPanelText.OrganisationBar(country, division));
		country.Modifier.Set(COrganisation.LandOrganisation, CFixedPoint.Parse("0.25"));
		Assert.Equal(CFixedPoint.Parse("31.25"), CUnitPanelText.MaxOrganisation(country, division));
		Assert.Equal(56, CUnitPanelText.OrganisationBar(country, division));

		Assert.Equal(0, CUnitPanelText.OrganisationBar(country, Unit(country, EUnitKind.Division, Berlin)));
	}

	[Fact]
	public void ABarIsCutToWholeHundredthsAndNeverPastFull()
	{
		State(out CCountry country);
		CUnit division = Unit(country, EUnitKind.Division, Berlin, Infantry(), Infantry());
		division.SubUnits[0].Strength = CFixedPoint.Parse("19.99");      // 49.99 of 60

		Assert.Equal(83, CUnitPanelText.StrengthBar(division));
		division.SubUnits[0].Strength = CFixedPoint.FromInt(40);         // more than its type is made with
		Assert.Equal(100, CUnitPanelText.StrengthBar(division));

		Assert.Equal(0, CUnitPanelText.Share(0, 0));
		Assert.Equal(99, CUnitPanelText.Share(999, 1000));
		Assert.Equal(100, CUnitPanelText.Share(1200, 1000));
	}

	// --- the status ---

	[Fact]
	public void TheSpeedShownIsTheTopSpeedTimesItsModifierWithoutTheDefineOfTheMarch()
	{
		CGameState state = State(out CCountry country);
		CUnit division = Unit(country, EUnitKind.Division, Berlin, Infantry(), Type("artillery_brigade", speed: "3"));

		Assert.Equal(CFixedPoint.FromInt(3), CUnitPanelText.ShownSpeed(state, country, division));
		Assert.Equal("3KPH", CUnitPanelText.Speed(Localise, state, country, division));
		Assert.Equal(CFixedPoint.Parse("0.15"), CMovementSpeed.Speed(state, country, division));   // what an hour adds

		// Not a whole number: two decimals.
		state.Province(Berlin).Modifier.Set(CMovementSpeed.LocalUnitSpeed, CFixedPoint.Parse("-0.25"));
		Assert.Equal("2.25KPH", CUnitPanelText.Speed(Localise, state, country, division));
	}

	[Fact]
	public void AUnitIsSuppliedFromItsCapitalUnlessSomebodyElseHoldsIt()
	{
		CGameState state = State(out CCountry country);
		CUnit division = Unit(country, EUnitKind.Division, Potsdam, Infantry());

		Assert.Equal("Potsdam", CUnitPanelText.Location(state, division));
		Assert.Equal("Supplied from: Berlin", CUnitPanelText.SuppliedFrom(Localise, state, country, division));

		state.Province(Berlin).Controller = Pol;
		Assert.Equal("Supplied from: ???", CUnitPanelText.SuppliedFrom(Localise, state, country, division));
	}

	[Fact]
	public void WidthAndWeightAreTheBrigadesAddedUp()
	{
		State(out CCountry country);
		CUnit division = Unit(country, EUnitKind.Division, Berlin, Infantry(), Infantry(), Infantry(),
			Type("artillery_brigade", width: "0", weight: "5"));

		Assert.Equal(CFixedPoint.FromInt(3), CUnitPanelText.Width(division));
		Assert.Equal("35", CUnitPanelText.Weight(division));
		Assert.Equal("", CUnitPanelText.Weight(Unit(country, EUnitKind.Navy, Berlin, Type("destroyer", EUnitBranch.Naval))));

		Assert.Equal(1, CUnitPanelText.WidthFrame(CFixedPoint.Parse("1.999")));
		Assert.Equal(2, CUnitPanelText.WidthFrame(CFixedPoint.FromInt(2)));
		Assert.Equal(2, CUnitPanelText.WidthFrame(CFixedPoint.FromInt(3)));
		Assert.Equal(3, CUnitPanelText.WidthFrame(CFixedPoint.FromInt(4)));
		Assert.Equal(4, CUnitPanelText.WidthFrame(CFixedPoint.FromInt(6)));
		Assert.Equal(5, CUnitPanelText.WidthFrame(CFixedPoint.FromInt(8)));
	}

	[Fact]
	public void AUnitWithNoBrigadesHasNoAttritionFigure()
	{
		CGameState state = State(out CCountry country);

		// At home and away from any front: nothing.
		Assert.Equal("0%", CUnitPanelText.Attrition(state, Unit(country, EUnitKind.Division, Berlin, Infantry())));
		Assert.Equal("?%", CUnitPanelText.Attrition(state, Unit(country, EUnitKind.Division, Berlin)));
	}

	// --- the order ---

	[Fact]
	public void TheOrderNamesTheNextProvinceOfTheWayAndNotItsEnd()
	{
		CGameState state = State(out CCountry country);
		CUnit division = Unit(country, EUnitKind.Division, Berlin, Infantry());

		Assert.Equal("No Order", CUnitPanelText.Order(Localise, state, division));
		division.Path.AddRange(new[] { Potsdam, Dessau });
		Assert.Equal("Move to Potsdam.", CUnitPanelText.Order(Localise, state, division));
	}

	// --- the brigades ---

	[Fact]
	public void ABrigadesRowShowsItsModelItsMenAndItsType()
	{
		CGameState state = State(out CCountry country);
		CUnit division = Unit(country, EUnitKind.Division, Berlin, Infantry(), Infantry());
		CSubUnit brigade = division.SubUnits[0];

		Assert.Equal("§Y2§W Brigades", CUnitPanelText.BrigadesHeader(Localise, division));
		Assert.Equal("GFX_counter_infantry_brigade", CUnitPanelText.BrigadeSprite(brigade));
		Assert.Equal("I", CUnitPanelText.BrigadeLevel(state, country, brigade));          // a type with no model shows the first
		// An order of battle's model is not what the numeral shows: the levels are (BrigadeModelTests).
		brigade.HistoricalModel = 2;
		Assert.Equal("I", CUnitPanelText.BrigadeLevel(state, country, brigade));
		Assert.Equal("3,000", CUnitPanelText.BrigadeMen(brigade));
		brigade.Strength = CFixedPoint.Parse("9.99");
		Assert.Equal("999", CUnitPanelText.BrigadeMen(brigade));
		Assert.Equal(33, CUnitPanelText.BrigadeStrengthBar(brigade));
		// A ship's or a wing's is its strength as a whole percentage.
		var ship = new CSubUnit(new CSubUnitType("destroyer", EUnitBranch.Naval)) { Strength = CFixedPoint.FromInt(100) };
		Assert.Equal("100%", CUnitPanelText.BrigadeMen(ship));

		// The brigade's own bar is against what it may have, which the officers decide.
		Assert.Equal(100, CUnitPanelText.BrigadeOrganisationBar(state, country, brigade));
		brigade.Organisation = CFixedPoint.FromInt(15);
		Assert.Equal(50, CUnitPanelText.BrigadeOrganisationBar(state, country, brigade));
		country.OfficerRatio = CFixedPoint.Parse("0.5");
		Assert.Equal(100, CUnitPanelText.BrigadeOrganisationBar(state, country, brigade));
	}

	[Theory]
	[InlineData("0", 1)]
	[InlineData("0.999", 1)]
	[InlineData("1", 2)]          // 0.1 rounds to none: the first star frame
	[InlineData("5", 2)]          // 0.5 goes to the even 0
	[InlineData("5.01", 3)]
	[InlineData("15", 4)]         // 1.5 goes to the even 2
	[InlineData("25", 4)]         // 2.5 too
	[InlineData("45", 6)]         // 4.5 to 4
	[InlineData("55", 8)]         // 5.5 to 6
	[InlineData("90", 11)]
	[InlineData("100", 11)]       // 12 would be past the strip
	public void TheStarsGoByExperienceOverTenRoundedToEvenAndTwoMore(string experience, int frame)
	{
		Assert.Equal(frame, CUnitPanelText.ExperienceFrame(CFixedPoint.Parse(experience)));
	}

	[Fact]
	public void AWindowOfRowsIsAsHighAsItsLabelAndItsRowsUpToItsOwnHeight()
	{
		Assert.Equal(16, CUnitPanelText.RowsHeight(label: 16, rows: 0, rowHeight: 32, windowHeight: 176));
		Assert.Equal(112, CUnitPanelText.RowsHeight(label: 16, rows: 3, rowHeight: 32, windowHeight: 176));
		Assert.Equal(176, CUnitPanelText.RowsHeight(label: 16, rows: 5, rowHeight: 32, windowHeight: 176));
		Assert.Equal(176, CUnitPanelText.RowsHeight(label: 16, rows: 9, rowHeight: 32, windowHeight: 176));
	}

	// --- the units under it ---

	[Fact]
	public void TheHeadingOverSubordinatesCountsThemByLevelTheHighestFirst()
	{
		State(out CCountry country);
		CUnit army = Unit(country, EUnitKind.Army, Berlin, Type("hq_brigade"));
		CUnit corps = Unit(country, EUnitKind.Corps, Berlin, Type("hq_brigade", strength: "1"));
		CUnit first = Unit(country, EUnitKind.Division, Berlin, Infantry(), Infantry());
		CUnit second = Unit(country, EUnitKind.Division, Berlin, Infantry());
		CUnit under = Unit(country, EUnitKind.Division, Berlin, Infantry());
		army.Children.AddRange(new[] { first, corps, second });
		corps.Children.Add(under);

		Assert.Equal("1 Corps, 2 Division(s)", CUnitPanelText.ChildrenHeader(Localise, army));
		Assert.Equal("1 Division(s)", CUnitPanelText.ChildrenHeader(Localise, corps));
		Assert.Equal("", CUnitPanelText.ChildrenHeader(Localise, first));

		// Everything under it at any depth, without itself: 60 + 1 + 30 + 30 points.
		Assert.Equal("12,100", CUnitPanelText.ChildrenMen(army));
		Assert.Equal("6,000", CUnitPanelText.ChildMen(first));
		Assert.Equal("100", CUnitPanelText.ChildMen(corps));
	}

	// --- the buttons ---

	[Fact]
	public void AUnitsLevelShowsOneButtonOfTheFive()
	{
		State(out CCountry country);

		Assert.Equal("create_corps", CUnitPanelText.LevelButton(Unit(country, EUnitKind.Division, Berlin, Infantry())));
		Assert.Equal("create_army", CUnitPanelText.LevelButton(Unit(country, EUnitKind.Corps, Berlin, Infantry())));
		Assert.Equal("create_armygroup", CUnitPanelText.LevelButton(Unit(country, EUnitKind.Army, Berlin, Infantry())));
		Assert.Equal("create_theatre", CUnitPanelText.LevelButton(Unit(country, EUnitKind.ArmyGroup, Berlin, Infantry())));
		Assert.Equal("edit_theatre", CUnitPanelText.LevelButton(Unit(country, EUnitKind.Theatre, Berlin, Infantry())));
		Assert.Equal("", CUnitPanelText.LevelButton(Unit(country, EUnitKind.Navy, Berlin, Type("destroyer", EUnitBranch.Naval))));
		Assert.Equal(5, CUnitPanelText.LevelButtons.Count);
	}

	// --- a fleet and an air unit ---

	private static CSubUnitType Ship(string key, bool capital = false, bool submarine = false, string range = "800")
	{
		CSubUnitType type = Type(key, EUnitBranch.Naval, speed: "20");
		type.Stats.Values.Set(CUnitOrders.Range, CFixedPoint.Parse(range));
		type.Flags["capital"] = capital;
		type.Flags["is_sub"] = submarine;
		return type;
	}

	private static CSubUnitType Wing(string key, bool bomber, string range = "400")
	{
		CSubUnitType type = Type(key, EUnitBranch.Air, speed: "200");
		type.Stats.Values.Set(CUnitOrders.Range, CFixedPoint.Parse(range));
		type.Flags["is_bomber"] = bomber;
		return type;
	}

	[Fact]
	public void AnOrderSaysForItselfWhereItIsFor()
	{
		CGameState state = State(out CCountry country);
		CUnit fleet = Unit(country, EUnitKind.Navy, Berlin, Ship("destroyer"));
		fleet.Path.AddRange(new[] { Potsdam, Dessau });

		// With no order a fleet on its way reads as a land unit does: the next province.
		Assert.Equal("Move to Potsdam.", CUnitPanelText.Order(Localise, state, fleet));

		fleet.OrderProvince = Dessau;
		fleet.Order = EUnitOrder.Move;
		Assert.Equal("Move to Dessau.", CUnitPanelText.Order(Localise, state, fleet));
		fleet.Order = EUnitOrder.Rebase;
		Assert.Equal("Rebase :Dessau", CUnitPanelText.Order(Localise, state, fleet));
		fleet.Order = EUnitOrder.RebaseAir;
		Assert.Equal("Rebase(Dessau)", CUnitPanelText.Order(Localise, state, fleet));
	}

	[Fact]
	public void AFleetAndAnAirUnitNameTheirBaseAndHowFarTheyReachFromIt()
	{
		CGameState state = State(out CCountry country);
		CUnit fleet = Unit(country, EUnitKind.Navy, Berlin, Ship("destroyer", range: "1500"), Ship("submarine", range: "800.5"));
		fleet.Base = Potsdam;

		Assert.Equal("Supplied from: Potsdam(800km)", CUnitPanelText.SuppliedFrom(Localise, state, country, fleet));
		fleet.Base = 0;
		Assert.Equal("Supplied from: ???", CUnitPanelText.SuppliedFrom(Localise, state, country, fleet));
	}

	[Fact]
	public void TheHeadingCountsAFleetsShipsByKindAndAnAirUnitsWings()
	{
		State(out CCountry country);
		CUnit fleet = Unit(country, EUnitKind.Navy, Berlin, Ship("battleship", capital: true), Ship("destroyer"),
			Ship("light_cruiser"), Ship("submarine", submarine: true));
		CUnit air = Unit(country, EUnitKind.Air, Berlin, Wing("interceptor", bomber: false), Wing("tactical_bomber", bomber: true),
			Wing("cas", bomber: true));

		Assert.Equal("1 Capital Ships, 2 Screens & 1 others.", CUnitPanelText.BrigadesHeader(Localise, fleet));
		Assert.Equal("1 Fighters & 2 Bombers.", CUnitPanelText.BrigadesHeader(Localise, air));
	}
}
