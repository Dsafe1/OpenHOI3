using System.Collections.Generic;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What a selected unit's panel says in a tooltip, against <c>docs/mechanics/unitpanel.md</c>,
/// section 10, with Their Finest Hour's own strings where the test needs their shape.
/// </summary>
public class UnitPanelTooltipTests
{
	private static readonly CCountryTag Ger = new("GER");

	private const int Berlin = 1;
	private const int Potsdam = 2;

	private static readonly Dictionary<string, string> Texts = new()
	{
		["LAND_RANK2"] = "Lt. General",
		["SEA_RANK1"] = "Commander",
		["INACTIVE"] = "(inactive)",
		["LEADER_COMMAND_SIZE"] = "Max command",
		["SHIPS_LABEL"] = "ships",
		["LEADER_POSITIONING"] = "Positioning Bonus",
		["LEADER_SKILL"] = "Skill",
		["LEADER_SKILL_AT_MAX"] = "Max",
		["LEADER_XP"] = "Experience",
		["ACTUAL_TRAITS_DESC"] = "Actual traits:",
		["TO_GAIN_TRAITS_DESC"] = "Traits to gain:",
		["TRAIT_GAIN_PROGRESS"] = "§G$PROG$%§! progress toward trait §Y$TRAIT$§!",
		["panzer_leader"] = "Panzer Leader",
		["winter_specialist"] = "Winter Specialist",
		["January"] = "January",
		["UW_ATTACK_DELAY"] = "Delay $HOURS$ until$DATE$.",
		["ESTIMATE_ARIVAL"] = "Will arrive in $PROV$ on$DATE$",
		["UV_NO_STRENGHT"] = "Strenght $STR$ of $NEED$.",
		["UV_ATTACKING"] = "This unit is attacking",
		["UNIT_DIVISION_NAME"] = "Division",
		["UNIT_DIVISION_DESC"] = "The prime land combat unit.",
		["UNIT_BRIGADE_NAME"] = "Brigade",
		["UNIT_CORP_NAME"] = "Corps",
		["UW_TOTAL_STR_IRO"] = "Maximum strength is $VALUE$.\n",
		["UW_FOLLOWING_HURT"] = "The following units are damaged.\n",
		["UW_ALL_FIT"] = "All units are 100% fit.",
		["CURR_COMB_STR"] = "Current Strength at ",
		["MOVE_ORDER_DESC"] = "Move.",
		["ORDER_PATROL_DESC"] = "Patrol.",
		["UNIT_COMBAT_WIDTH"] = "Combat Width",
		["UW_DIS_OUT_OF_RANGE"] = "Out of range.",
		["UW_EFFECTIVE_RADIO_STRENGTH"] = "This $HQ$ reaches $DISTANCE$km.",
		["UW_DIST_TO"] = "$HQ$ $NAME$ at $LOCATION$, $DISTANCE$ km from $LOWER$.",
		["CA_BONUS_DETAILS_TOTAL"] = "Combined Arms Bonus: $VAL$%",
		["CA_BONUS_DETAILS_BASE"] = "Base: $VAL$%",
		["infantry_brigade"] = "Infantry",
		["infantry_activation"] = "Infantry Activation",
		["MODEL_TAG"] = "Model",
		["NO"] = "No",
		["YES"] = "Yes",
		["POS_STACK_PENALTY"] = "Above $MAX$, $EACH$% each, $TOTAL$% in all.",
		["UW_TURN_ON_PRIO"] = "Give it priority.",
		["UW_TURN_OFF_PRIO"] = "Take its priority away.",
		["SUBTT_STRENGTH"] = "Strength",
		["SUBTT_EXP"] = "Experience",
		["MAXIMUM_SPEED"] = "Max Speed",
		["KPH"] = "kph",
		["RANGE"] = "Range",
		["KM"] = "km",
	};

	private static string Localise(string key) => Texts.GetValueOrDefault(key, key);

	private static string? None(string key) => null;

	private static CGameState State(out CCountry country, params CTrait[] traits)
	{
		country = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin };
		var defines = new CDefines();
		defines.Set(CManpower.MilitarySection, "NAVAL_RANK_1", CFixedPoint.FromInt(10));
		defines.Set(CManpower.MilitarySection, "RADIO_CORPS_LEADER_DISTANCE", CFixedPoint.FromInt(100));
		defines.Set(CManpower.MilitarySection, CAttackDelay.DelayPeriod, CFixedPoint.FromInt(24));
		defines.Set(CManpower.MilitarySection, CNavalBattle.HullLimit, CFixedPoint.FromInt(12));
		defines.Set(CManpower.MilitarySection, CNavalBattle.HullPenalty, CFixedPoint.Parse("0.05"));
		defines.Set(CManpower.MilitarySection, CNavalBattle.MostPenalty, CFixedPoint.Parse("0.5"));
		var activation = new CTechnology("infantry_activation", 12, new CModifier("infantry_activation"))
		{
			StartYear = 1918, FirstOffset = 1934, AdditionalOffset = 2,
		};
		return new CGameState(new CDate(1936, 1, 1), new[]
		{
			new CProvince(0, "", isSea: false),
			new CProvince(Berlin, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
			new CProvince(Potsdam, "Potsdam", isSea: false) { Owner = Ger, Controller = Ger, CentreX = 100 },
		}, new[] { country }, technologies: new[] { activation }, defines: defines)
		{
			Traits = traits,
		};
	}

	private static CSubUnitType Type(string key, EUnitBranch branch = EUnitBranch.Land, string strength = "30")
	{
		var type = new CSubUnitType(key, branch);
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.Parse(strength));
		type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.FromInt(30));
		type.Stats.Values.Set(CSubUnitType.CombatWidth, CFixedPoint.FromInt(1));
		type.Stats.Values.Set(CMovementSpeed.MaximumSpeed, CFixedPoint.FromInt(4));
		return type;
	}

	private static CUnit Unit(CCountry country, EUnitKind kind, int location, string name, params CSubUnitType[] types)
	{
		var unit = new CUnit(kind, country.Tag) { Id = country.Units.Count + 1, Location = location, Name = name };
		foreach (CSubUnitType type in types)
		{
			var subUnit = new CSubUnit(type) { Unit = unit, Strength = type.Stats[CSubUnitType.MaxStrength], Name = "Brigade " + unit.SubUnits.Count };
			subUnit.Stats.CopyFrom(type.Stats);
			subUnit.Organisation = type.Stats[CSubUnitType.DefaultOrganisation];
			unit.SubUnits.Add(subUnit);
		}
		country.Units.Add(unit);
		return unit;
	}

	// ---- the leader

	[Fact]
	public void AGeneralsTooltipIsHisRankSkillExperienceTraitsAndWhatHeIsOnHisWayTo()
	{
		var panzer = new CTrait("panzer_leader");
		var winter = new CTrait("winter_specialist");
		var trickster = new CTrait("trickster");
		CGameState state = State(out CCountry country, panzer, winter, trickster);
		CUnit army = Unit(country, EUnitKind.Army, Berlin, "2. Armee");
		var leader = new CLeader(1)
		{
			Name = "Heinz Guderian", Rank = 2, Skill = 3, MaxSkill = 3, Experience = CFixed15.FromInt(6000),
		};
		leader.Traits.Add(panzer);
		leader.TraitProgress.Progress[new OpenHOI3.Sim.Leaders.CGainableTrait(OpenHOI3.Sim.Combat.ECombatKind.Land, trickster)] = CTraitProgress.Whole;    // earned: no line
		leader.TraitProgress.Progress[new OpenHOI3.Sim.Leaders.CGainableTrait(OpenHOI3.Sim.Combat.ECombatKind.Land, winter)] = 2500;
		CLeaders.SetLeader(state, army, leader, false, false);

		Assert.Equal(
			"Lt. General §YHeinz Guderian§W\n"
			+ "Skill §Y3 (Max)§W\n"
			+ "Experience §Y50.00%§W\n"
			+ "\nActual traits:\n- §YPanzer Leader§!"
			+ "\nTraits to gain:\n- §G2.50%§! progress toward trait §YWinter Specialist§!",
			CLeaderTooltip.For(Localise, state, leader));
	}

	[Fact]
	public void AnAdmiralSaysHowManyShipsHeCommandsAndHisPositioning()
	{
		CGameState state = State(out CCountry country);
		CSubUnitType destroyer = Type("destroyer", EUnitBranch.Naval, "100");
		CUnit fleet = Unit(country, EUnitKind.Navy, Berlin, "1. Flotte", destroyer, destroyer, destroyer);
		var admiral = new CLeader(2)
		{
			Name = "Erich Raeder", Type = ELeaderType.Sea, Rank = 1, Skill = 2, MaxSkill = 4, Experience = CFixed15.FromInt(2000),
		};
		CLeaders.SetLeader(state, fleet, admiral, false, false);

		Assert.Equal(
			"Commander §YErich Raeder§W\n"
			+ "Max command §G 10 ships§W\n"
			+ "Positioning Bonus §Y20%§W\n"
			+ "Skill §Y2§W\n"
			+ "Experience §Y0.00%§W\n",
			CLeaderTooltip.For(Localise, state, admiral));
	}

	[Theory]
	[InlineData(1, 1500, 500)]       // half way from 1000 to 2000
	[InlineData(3, 4001, 0)]         // a point of 4000 is under a thousandth
	[InlineData(10, 80000, 0)]       // 60000 to 100000000: a sliver
	[InlineData(11, 100000000, 0)]   // the next threshold is the stray 1 after the table
	[InlineData(12, 100000000, 0)]
	public void HisExperienceIsHowFarHeIsFromOneSkillsThresholdToTheNext(int skill, long experience, long share)
	{
		var leader = new CLeader(3) { Skill = skill, Experience = CFixed15.FromInt(experience) };

		Assert.Equal(share, CLeaderTooltip.ExperienceShare(leader));
	}

	[Fact]
	public void ARankOfNoneIsInactive()
	{
		Assert.Equal("(inactive)", CLeaderTooltip.RankName(Localise, new CLeader(4) { Rank = 0 }));
	}

	// ---- the header

	[Fact]
	public void ADateIsWrittenWithASpaceInFront()
	{
		long hour = (long)new CDate(1936, 1, 2).DayNumber * CGameState.HoursPerDay + 6;

		Assert.Equal(" 6:00, 2 January, 1936", CUnitHeaderTooltips.Date(Localise, hour));
	}

	[Fact]
	public void TheActivityStripSaysWhatHoldsFirst()
	{
		CGameState state = State(out CCountry country);
		CUnit division = Unit(country, EUnitKind.Division, Berlin, "1. Inf", Type("infantry_brigade"));

		Assert.Equal("", CUnitHeaderTooltips.Activity(Localise, state, country, division));

		division.SubUnits[0].Strength = CFixedPoint.Parse("0.05");
		Assert.Equal("Strenght 0.0 of 0.1.", CUnitHeaderTooltips.Activity(Localise, state, country, division));

		// An attack delay comes first, and is cut by the hours attacked out of the period.
		division.AttackDelay = CFixedPoint.FromInt(30);
		Assert.Equal("Delay 30 until 6:00, 2 January, 1936.", CUnitHeaderTooltips.Activity(Localise, state, country, division));
		division.HoursAttacking = 6;
		Assert.Equal(7, CUnitHeaderTooltips.DelayHours(state, division));
		division.HoursAttacking = 1;
		Assert.Equal(1, CUnitHeaderTooltips.DelayHours(state, division));     // never under one

		var combat = new CCombat(Potsdam);
		combat.Attacker.Add(division);
		Assert.Equal("This unit is attacking", CUnitHeaderTooltips.Activity(Localise, state, country, division));
	}

	[Fact]
	public void TheCountersListEachBrigadeAndALoneBrigadeIsCalledOne()
	{
		State(out CCountry country);
		CSubUnitType infantry = Type("infantry_brigade");
		CUnit division = Unit(country, EUnitKind.Division, Berlin, "1. Inf", infantry, infantry);
		CUnit lone = Unit(country, EUnitKind.Division, Berlin, "2. Inf", infantry);

		Assert.Equal("£infantry_brigade Brigade 0\n£infantry_brigade Brigade 1\n", CUnitHeaderTooltips.Brigades(Localise, division));
		CTooltip level = CUnitHeaderTooltips.Level(Localise, division);
		Assert.Equal("Division", level.Text);
		Assert.Equal("The prime land combat unit.", level.Delayed);
		Assert.Equal("Brigade", CUnitHeaderTooltips.Level(Localise, lone).Text);
		Assert.Equal("UNIT_BRIGADE_DESC", CUnitHeaderTooltips.Level(Localise, lone).Delayed);
	}

	[Fact]
	public void ALandUnitsStrengthListsEveryBrigadeAndAFleetsOnlyTheHurt()
	{
		State(out CCountry country);
		CSubUnitType infantry = Type("infantry_brigade");
		CUnit division = Unit(country, EUnitKind.Division, Berlin, "1. Inf", infantry, infantry, infantry);
		division.SubUnits[1].Strength = CFixedPoint.FromInt(20);
		division.SubUnits[2].Strength = CFixedPoint.FromInt(10);

		Assert.Equal(
			"Maximum strength is 9,000.\n"
			+ "£infantry_brigade Brigade 0 - §W3,000§W/3,000\n"
			+ "£infantry_brigade Brigade 1 - §Y2,000§W/3,000\n"
			+ "£infantry_brigade Brigade 2 - §R1,000§W/3,000\n",
			CUnitHeaderTooltips.Strength(Localise, division));

		CSubUnitType destroyer = Type("destroyer", EUnitBranch.Naval, "100");
		CUnit fleet = Unit(country, EUnitKind.Navy, Berlin, "1. Flotte", destroyer, destroyer, destroyer);
		Assert.Equal("All units are 100% fit.", CUnitHeaderTooltips.Strength(Localise, fleet));
		fleet.SubUnits[1].Strength = CFixedPoint.FromInt(90);
		fleet.SubUnits[2].Strength = CFixedPoint.FromInt(65);
		Assert.Equal(
			"The following units are damaged.\n£destroyer Brigade 1 §G90§W\n£destroyer Brigade 2 §R65§W\n",
			CUnitHeaderTooltips.Strength(Localise, fleet));
	}

	[Fact]
	public void TheStrengthBarSaysItsShareToOnePlace()
	{
		State(out CCountry country);
		CSubUnitType infantry = Type("infantry_brigade");
		CUnit division = Unit(country, EUnitKind.Division, Berlin, "1. Inf", infantry, infantry, infantry);
		division.SubUnits[0].Strength = CFixedPoint.FromInt(20);

		Assert.Equal("Current Strength at §Y88.8%§W\n", CUnitHeaderTooltips.Header(Localise, null!, country, division, "str_bar").Text);
	}

	[Fact]
	public void TheOrganisationBarSaysWhatABrigadeOfTheMeanMoraleRegainsAnHour()
	{
		CGameState state = State(out CCountry country);
		CSubUnitType infantry = Type("infantry_brigade");
		infantry.Stats.Values.Set(COrganisation.Morale, CFixedPoint.Parse("0.3"));
		CUnit division = Unit(country, EUnitKind.Division, Berlin, "1. Inf", infantry, infantry);
		division.SubUnits[0].Organisation = CFixedPoint.FromInt(20);
		division.SuppliesReceived = CFixedPoint.One;
		country.OfficerRatio = CFixedPoint.One;

		// Berlin has no roads here, so a hundredth: 1 x 0.01 x 1 x 1 x 0.3 x 0.2 = 0.0006, cut to 0.
		Assert.Equal("Current Organisation at §Y25.0§W\n+§Y0.000",
			CUnitHeaderTooltips.Header(key => key == "CURR_COMB_ORG" ? "Current Organisation at " : Localise(key), state, country, division,
				"org_bar").Text);
		state.Province(Berlin).Modifier.Set(COrganisation.Infrastructure, CFixedPoint.One);
		Assert.Equal("+§Y0.060", CUnitHeaderTooltips.Regain(state, country, division));
		// Given nothing, it regains nothing, where the hour takes organisation away.
		division.SuppliesReceived = CFixedPoint.Zero;
		Assert.Equal("+§Y0.000", CUnitHeaderTooltips.Regain(state, country, division));
	}

	[Fact]
	public void TheDugInCapIsTheDefinesAndWhatTechnologyAdds()
	{
		CGameState state = State(out CCountry country);
		state.Defines.Set(CManpower.MilitarySection, CDigIn.Cap, CFixedPoint.FromInt(10));
		country.Technology.Set(CDigIn.TechnologyCap, CFixedPoint.FromInt(2));
		CUnit division = Unit(country, EUnitKind.Division, Berlin, "1. Inf", Type("infantry_brigade"));
		division.DigIn = CFixedPoint.FromInt(4);

		Assert.Equal("4 of 12", CUnitStatusTooltips.For(key => key == "UW_DUGIN" ? "$CURR$ of $CAP$" : Localise(key), state, country, division,
			"unitstatus_dugin", "", "").Text);
	}

	[Fact]
	public void AStrategicRedeploymentsWaitIsNotCalledAnAttackDelay()
	{
		CGameState state = State(out CCountry country);
		CUnit division = Unit(country, EUnitKind.Division, Berlin, "1. Inf", Type("infantry_brigade"));
		division.AttackDelay = CFixedPoint.FromInt(5);
		string Texts(string key) => key == "UW_STRATDEPLOYMENT_DELAY" ? "Redeployed, $HOURS$." : Localise(key);

		Assert.StartsWith("Delay 5", CUnitStatusTooltips.For(Texts, state, country, division, "unitstatus_delay", "", "").Text);
		division.Order = EUnitOrder.StrategicRedeployment;
		Assert.Equal("Redeployed, 5.", CUnitStatusTooltips.For(Texts, state, country, division, "unitstatus_delay", "", "").Text);
	}

	[Fact]
	public void AFactionMembersModelTakesItsLeadersNameBeforeTheGames()
	{
		CGameState state = State(out CCountry germany);
		var italy = new CCountry { Tag = new CCountryTag("ITA"), Name = "Italy", DefinitionFile = "" };
		var withItaly = new CGameState(state.Date, new[] { new CProvince(0, "", isSea: false) }, new[] { germany, italy });
		germany.Faction = "axis";
		italy.Faction = "axis";
		CSubUnitType infantry = Type("infantry_brigade");
		string? Find(string key) => key switch { "GER_infantry_brigade_1" => "Infanterie 1", "infantry_brigade_1" => "Infantry 1", _ => null };

		// Germany leads, being first: its own. Italy has none of its own and takes its leader's.
		Assert.Equal("Infanterie 1", CBrigadeTooltips.ModelName(Localise, Find, withItaly, germany, infantry, 1));
		Assert.Equal("Infanterie 1", CBrigadeTooltips.ModelName(Localise, Find, withItaly, italy, infantry, 1));
		italy.Faction = "";
		Assert.Equal("Infantry 1", CBrigadeTooltips.ModelName(Localise, Find, withItaly, italy, infantry, 1));
		Assert.Equal("Infantry (III)", CBrigadeTooltips.ModelName(Localise, None, withItaly, italy, infantry, 2));
	}

	// ---- the order and the buttons

	[Fact]
	public void AnIdleUnitsOrderSaysNothingAndOneOnTheMarchMoves()
	{
		State(out CCountry country);
		CUnit division = Unit(country, EUnitKind.Division, Berlin, "1. Inf", Type("infantry_brigade"));

		Assert.Equal("", CUnitOrderTooltips.Description(Localise, division));
		division.Path.Add(Potsdam);
		Assert.Equal("Move.", CUnitOrderTooltips.Description(Localise, division));
		division.Order = EUnitOrder.Patrol;
		Assert.Equal("Patrol.", CUnitOrderTooltips.Description(Localise, division));
	}

	[Fact]
	public void ACheckboxSaysWhatAClickWouldDo()
	{
		State(out CCountry country);
		CUnit division = Unit(country, EUnitKind.Division, Berlin, "1. Inf", Type("infantry_brigade"));

		Assert.Equal("Give it priority.", CUnitOrderTooltips.Buttons(Localise, division, false, "prio_checkbox").Text);
		division.IsPrioritised = true;
		Assert.Equal("Take its priority away.", CUnitOrderTooltips.Buttons(Localise, division, false, "prio_checkbox").Text);
		Assert.Equal("UW_NOT_POSSIBLE_TO_ATTACH", CUnitOrderTooltips.Buttons(Localise, division, false, "attach_button").Text);
	}

	[Theory]
	[InlineData(EUnitKind.Division, "CANT_AUTO_DIVISIONS")]
	[InlineData(EUnitKind.Theatre, "CANT_AUTO_NO_AREA")]
	[InlineData(EUnitKind.Navy, "ONLY_AUTO_ARMIES")]
	[InlineData(EUnitKind.Army, "PLAYER_CONTROLLED")]
	public void AHeadquartersIsNotHandedToAnAiForAReasonOfItsLevel(EUnitKind kind, string key)
	{
		Assert.Equal(key, CUnitOrderTooltips.Automate(new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false) }, new[] { new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" } }), new CUnit(kind, Ger)));
	}

	// ---- the status

	[Fact]
	public void TheDistanceToAHeadquartersOutOfReachIsYellowAndNeverRed()
	{
		CGameState state = State(out CCountry country);
		CSubUnitType radio = Type("infantry_brigade");
		radio.Stats.Values.Set(CCommandReach.RadioStrength, CFixedPoint.One);
		CUnit corps = Unit(country, EUnitKind.Corps, Potsdam, "XIII. AK", radio);
		CUnit division = Unit(country, EUnitKind.Division, Berlin, "1. Inf", radio);
		division.Parent = corps;
		corps.Children.Add(division);

		// 400 km apart, and the corps reaches 100 in full and 200 in part.
		Assert.Equal("Out of range.\nCorps XIII. AK at Potsdam, §Y400 km from 1. Inf.\n",
			CUnitStatusTooltips.Distance(Localise, state, division));
		Assert.Equal("This Corps reaches 100km.\n", CUnitStatusTooltips.Distance(Localise, state, corps));
	}

	[Fact]
	public void TheWidthAndTheCombinedArmsLinesAreTheOriginals()
	{
		Assert.Equal("Combat Width: §Y2.0§W\n", CUnitStatusTooltips.Width(Localise, CFixedPoint.FromInt(2)));
		Assert.Equal("§G125.00%§!", CUnitStatusTooltips.Multiplier(1250));
		Assert.Equal("§R80.00%§!", CUnitStatusTooltips.Multiplier(800));
	}

	// ---- the brigades

	[Fact]
	public void ALevelOfEquipmentIsTheCountrysTextTheGamesOrAModelYear()
	{
		CGameState state = State(out _);
		CTechnology activation = state.Technologies["infantry_activation"];

		Assert.Equal("No", CBrigadeTooltips.LevelName(Localise, None, Ger, activation, 0));
		Assert.Equal("Model 1918", CBrigadeTooltips.LevelName(Localise, None, Ger, activation, 1));
		Assert.Equal("Model 1934", CBrigadeTooltips.LevelName(Localise, None, Ger, activation, 2));
		Assert.Equal("Model 1936", CBrigadeTooltips.LevelName(Localise, None, Ger, activation, 3));
		Assert.Equal("Kar98k", CBrigadeTooltips.LevelName(Localise, key => key == "GER_infantry_activation_3" ? "Kar98k" : null,
			Ger, activation, 3));
		Assert.Equal("Mauser", CBrigadeTooltips.LevelName(Localise, key => key == "infantry_activation_3" ? "Mauser" : null,
			Ger, activation, 3));
	}

	[Fact]
	public void ABrigadesSummaryNamesItsModelAndListsItsEquipment()
	{
		CGameState state = State(out CCountry country);
		state.Defines.Set(CHistoricalModels.MilitarySection, CHistoricalModels.MaxDefine, CFixedPoint.FromInt(10));
		CSubUnitType infantry = Type("infantry_brigade");
		infantry.Technologies.Add(state.Technologies["infantry_activation"]);
		CUnit division = Unit(country, EUnitKind.Division, Berlin, "1. Inf", infantry);
		CSubUnit brigade = division.SubUnits[0];
		// The order of battle's model is not what it is shown as: its levels are.
		brigade.HistoricalModel = 0;
		brigade.TechnologyLevels["infantry_activation"] = 2;
		brigade.TechnologyProgress["infantry_activation"] = new CFixedPoint(250);

		Assert.Equal("Infantry (III)", CBrigadeTooltips.ModelLabel(Localise, None, state, country, brigade));
		Assert.Equal("Infanterie-Brigade 36", CBrigadeTooltips.ModelLabel(Localise,
			key => key == "GER_infantry_brigade_2" ? "Infanterie-Brigade 36" : null, state, country, brigade));

		string summary = CBrigadeTooltips.Summary(Localise, None, state, country, brigade);
		Assert.StartsWith("(§YInfantry (III)§W)\nStrength: §Y3,000§W/3,000\n", summary);
		Assert.Contains("Experience: §Y0.000§W%\n", summary);
		Assert.EndsWith("Infantry Activation : §YModel 1934§W(+25.00%)§W\n", summary);
	}

	[Fact]
	public void AShipsSpeedAndRangeAreYellowLinesAndNothingForNothing()
	{
		Assert.Equal("Max Speed §Y28.50§W kph \n", CBrigadeTooltips.StatLine(Localise, "MAXIMUM_SPEED", CFixedPoint.Parse("28.5"), 2, "KPH"));
		Assert.Equal("Range §Y2000§W km \n", CBrigadeTooltips.StatLine(Localise, "RANGE", CFixedPoint.FromInt(2000), 0, "KM"));
		Assert.Equal("", CBrigadeTooltips.StatLine(Localise, "RANGE", CFixedPoint.Zero, 0, "KM"));
	}

	[Fact]
	public void AFleetsHullsOverTheLimitCostItPositioning()
	{
		CGameState state = State(out CCountry country);
		CSubUnitType cruiser = Type("heavy_cruiser", EUnitBranch.Naval, "100");
		cruiser.Stats.Values.Set(CNavalBattle.Hull, CFixedPoint.FromInt(10));
		CUnit fleet = Unit(country, EUnitKind.Navy, Berlin, "1. Flotte", cruiser, cruiser);

		Assert.Equal("Above 12, 5% each, 40.0% in all.", CBrigadeTooltips.Window(Localise, state, fleet, "positioning").Text);
		Assert.Equal("", CBrigadeTooltips.Window(Localise, state, fleet, "header").Text);
	}
}
