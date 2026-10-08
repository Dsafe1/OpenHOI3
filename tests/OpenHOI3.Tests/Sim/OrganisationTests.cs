using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The officer ratio, the most organisation a brigade may have, and what brings it back
/// each hour, against <c>docs/mechanics/organisation.md</c>. The figures marked as the
/// original's are out of its start savegame: Germany's reserves at 28, a Hungarian
/// headquarters at 29.310, and three Waffen-SS brigades at 55.107.
/// </summary>
public class OrganisationTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Eng = new("ENG");

	private const int Berlin = 1;
	private const int Kiel = 2;
	private const int Baltic = 3;

	private static readonly CBuilding Infra = new(0, "infra", 10, "infrastructure", CFixedPoint.Parse("0.1"));
	private static readonly CBuilding NavalBase = new(1, "naval_base", 10, "", CFixedPoint.Zero);
	private static readonly CBuilding AirBase = new(2, "air_base", 10, "", CFixedPoint.Zero);

	private static CSubUnitType Type(string key, EUnitBranch branch, string organisation, string morale, string officers = "100")
	{
		var type = new CSubUnitType(key, branch);
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(30));
		type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.Parse(organisation));
		type.Stats.Values.Set(COrganisation.Morale, CFixedPoint.Parse(morale));
		type.Stats.Values.Set(CSubUnitType.Officers, CFixedPoint.Parse(officers));
		type.Stats.Values.Set(CSubUnitStats.BuildCostManpower, CFixedPoint.Parse("3.33"));
		type.Stats.Values.Set(CSubUnitStats.BuildCostIc, CFixedPoint.Parse("2.33"));
		type.Stats.Values.Set(CSubUnitStats.BuildTime, CFixedPoint.FromInt(95));
		return type;
	}

	private static CSubUnitType Infantry() => Type("infantry_brigade", EUnitBranch.Land, "40", "0.3");

	private static CSubUnitType Destroyer() => Type("destroyer", EUnitBranch.Naval, "30", "0.3");

	private static CSubUnitType Fighter() => Type("interceptor", EUnitBranch.Air, "40", "0.3");

	/// <summary>Germany as its start save has it: a two-year draft, and more officers than it can use.</summary>
	private static CCountry Germany()
	{
		var country = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin };
		country.Modifier.Set(CUnits.ReservesPenaltySize, CFixedPoint.Parse("-0.5"));
		country.OfficerRatio = CFixedPoint.Parse("1.4");
		country.Manpower = CFixedPoint.FromInt(500);
		return country;
	}

	private static CGameState State(IEnumerable<CCountry> countries, IEnumerable<CWar>? wars = null,
		IEnumerable<CTechnology>? technologies = null)
	{
		var defines = new CDefines();
		defines.Set(CManpower.MilitarySection, COfficers.MostRatio, CFixedPoint.Parse("1.4"));
		defines.Set(CManpower.MilitarySection, COrganisation.LowRegainBonus, CFixedPoint.Parse("0.3"));
		defines.Set(CManpower.MilitarySection, CBaseCrowding.NavalBaseEfficiency, CFixedPoint.FromInt(6));
		defines.Set(CManpower.MilitarySection, CUpgrades.LandSpeed, CFixedPoint.One);

		var berlin = new CProvince(Berlin, "Berlin", isSea: false) { Owner = Ger, Controller = Ger };
		berlin.SetBuildingLevel(Infra, CFixedPoint.FromInt(10));
		var kiel = new CProvince(Kiel, "Kiel", isSea: false) { Owner = Ger, Controller = Ger };
		kiel.SetBuildingLevel(Infra, CFixedPoint.FromInt(6));
		var baltic = new CProvince(Baltic, "Baltic", isSea: true);

		var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false), berlin, kiel, baltic },
			countries, buildings: new[] { Infra, NavalBase, AirBase }, technologies: technologies, defines: defines, wars: wars);
		CProvinceModifiers.Rebuild(state);
		return state;
	}

	private static CUnit Unit(CCountry country, EUnitKind kind = EUnitKind.Division, int location = Berlin, CUnit? parent = null)
	{
		var unit = new CUnit(kind, country.Tag) { Location = location, Parent = parent };
		if (kind == EUnitKind.Navy || kind == EUnitKind.Air)
		{
			unit.Base = location;
		}
		if (parent is null)
		{
			country.Units.Add(unit);
		}
		else
		{
			parent.Children.Add(unit);
		}
		return unit;
	}

	private static CSubUnit Brigade(CSubUnitType type, CUnit unit, string? organisation = null, bool reserve = false)
	{
		var brigade = new CSubUnit(type) { Unit = unit, IsReserve = reserve };
		brigade.Strength = type.Stats[CSubUnitType.MaxStrength];
		brigade.Highest = brigade.Strength;
		brigade.Organisation = organisation is null ? type.Stats[CSubUnitType.DefaultOrganisation] : CFixedPoint.Parse(organisation);
		unit.SubUnits.Add(brigade);
		return brigade;
	}

	private static long Factor(CGameState state, CCountry country, CUnit unit)
	{
		return COrganisation.RegainFactor(state, country, unit, new CBaseCrowding(state));
	}

	// ---- the officer ratio

	[Fact]
	public void TheRatioIsTheOfficersACountryHasOverWhatItsUnitsCallFor()
	{
		CGameState state = State(new[] { Germany() });

		// Britain in the original's start save: 0.9859, and what is under a thousandth is dropped.
		Assert.Equal(CFixedPoint.Parse("0.985"),
			COfficers.Ratio(state, CFixedPoint.Parse("11939.444"), CFixedPoint.FromInt(12110)));
	}

	[Fact]
	public void TheRatioIsNeverAboveTheDefine()
	{
		CGameState state = State(new[] { Germany() });

		// Germany's history gives it half as many again as it needs, and the original shows 140%.
		Assert.Equal(CFixedPoint.Parse("1.4"), COfficers.Ratio(state, CFixedPoint.FromInt(1500), CFixedPoint.FromInt(1000)));
	}

	[Fact]
	public void ACountryWhoseUnitsCallForNoOfficersHasAllItNeeds()
	{
		CGameState state = State(new[] { Germany() });

		Assert.Equal(CFixedPoint.One, COfficers.Ratio(state, CFixedPoint.Zero, CFixedPoint.Zero));
		Assert.Equal(CFixedPoint.One, COfficers.Ratio(state, CFixedPoint.FromInt(50), CFixedPoint.Zero));
	}

	[Fact]
	public void EveryBrigadeCallsForItsOfficersWhateverStateItIsIn()
	{
		CCountry country = Germany();
		CUnit corps = Unit(country, EUnitKind.Corps);
		CUnit division = Unit(country, EUnitKind.Division, parent: corps);
		Brigade(Infantry(), division);
		Brigade(Infantry(), division);
		CSubUnit headquarters = Brigade(Type("hq_brigade", EUnitBranch.Land, "30", "0.4", officers: "50"), corps);
		headquarters.Strength = CFixedPoint.Zero;                   // too weak to add anything to its unit, and still counted

		Assert.Equal(CFixedPoint.FromInt(250), COfficers.Needed(country));
	}

	[Fact]
	public void TheUpdateCountsTheNeedAndWorksOutTheRatio()
	{
		CCountry country = Germany();
		Brigade(Infantry(), Unit(country));
		Brigade(Infantry(), Unit(country));
		country.Officers = CFixedPoint.FromInt(150);
		CGameState state = State(new[] { country });

		COfficers.Update(state);

		Assert.Equal(CFixedPoint.FromInt(200), country.OfficersNeeded);
		Assert.Equal(CFixedPoint.Parse("0.75"), country.OfficerRatio);
	}

	// ---- the most organisation a brigade may have

	[Fact]
	public void ARegularsCeilingIsItsOwnFigureTimesTheOfficerRatio()
	{
		CCountry country = Germany();
		CSubUnit brigade = Brigade(Infantry(), Unit(country));
		CGameState state = State(new[] { country });

		Assert.Equal(CFixedPoint.FromInt(56), COrganisation.Ceiling(state, country, brigade));
	}

	[Fact]
	public void AReserveIsKeptShortByTheShareItsConscriptionLawHoldsBack()
	{
		CCountry country = Germany();
		CSubUnit brigade = Brigade(Infantry(), Unit(country), reserve: true);
		CGameState state = State(new[] { country });

		// The original's own: Germany's reserve infantry stand at 28.000 on the first screen.
		Assert.Equal(CFixedPoint.FromInt(28), COrganisation.Ceiling(state, country, brigade));
	}

	[Fact]
	public void AMobilisedCountrysReservesHaveARegularsCeiling()
	{
		CCountry country = Germany();
		country.IsMobilised = true;
		CSubUnit brigade = Brigade(Infantry(), Unit(country), reserve: true);
		CGameState state = State(new[] { country });

		Assert.Equal(CFixedPoint.FromInt(56), COrganisation.Ceiling(state, country, brigade));
	}

	[Fact]
	public void ACountryAtWarThatHasNotMobilisedKeepsItsReservesShortOnlyWhereAsked()
	{
		CCountry country = Germany();
		CSubUnit brigade = Brigade(Infantry(), Unit(country), reserve: true);
		var war = new CWar("a war", new CDate(1935, 10, 3));
		war.Attackers.Add(Ger);
		war.Defenders.Add(Eng);
		CGameState state = State(new[] { country }, wars: new[] { war });

		Assert.Equal(CFixedPoint.FromInt(56), COrganisation.Ceiling(state, country, brigade));
		Assert.Equal(CFixedPoint.FromInt(28), COrganisation.Ceiling(state, country, brigade, penaltyAtWar: true));
		country.IsMobilised = true;
		Assert.Equal(CFixedPoint.FromInt(28), COrganisation.Ceiling(state, country, brigade, penaltyAlways: true));
	}

	[Fact]
	public void TooFewOfficersBringTheCeilingBelowTheBrigadesOwnFigure()
	{
		CCountry country = Germany();
		country.OfficerRatio = CFixedPoint.Parse("0.977");
		CSubUnit headquarters = Brigade(Type("hq_brigade", EUnitBranch.Land, "30", "0.4"), Unit(country, EUnitKind.Theatre));
		CGameState state = State(new[] { country });

		// The original's own: Hungary's headquarters, with 3617.698 officers of 3700.
		Assert.Equal(CFixedPoint.Parse("29.310"), COrganisation.Ceiling(state, country, headquarters));
	}

	[Fact]
	public void EachBranchHasItsOwnModifier()
	{
		CCountry country = Germany();
		country.OfficerRatio = CFixedPoint.One;
		country.Modifier.Set(COrganisation.LandOrganisation, CFixedPoint.Parse("0.1"));
		country.Modifier.Set(COrganisation.NavalOrganisation, CFixedPoint.Parse("0.05"));
		country.Modifier.Set(COrganisation.AirOrganisation, CFixedPoint.Parse("-0.25"));
		CSubUnit brigade = Brigade(Infantry(), Unit(country));
		CSubUnit ship = Brigade(Destroyer(), Unit(country, EUnitKind.Navy, Kiel));
		CSubUnit wing = Brigade(Fighter(), Unit(country, EUnitKind.Air));
		CGameState state = State(new[] { country });

		Assert.Equal(CFixedPoint.FromInt(44), COrganisation.Ceiling(state, country, brigade));
		Assert.Equal(CFixedPoint.Parse("31.5"), COrganisation.Ceiling(state, country, ship));
		Assert.Equal(CFixedPoint.FromInt(30), COrganisation.Ceiling(state, country, wing));
	}

	[Fact]
	public void TheArmyWhoseCommanderCountsIsTheOneADivisionOrACorpsAnswersTo()
	{
		CCountry country = Germany();
		CUnit theatre = Unit(country, EUnitKind.Theatre);
		CUnit army = Unit(country, EUnitKind.Army, parent: theatre);
		CUnit corps = Unit(country, EUnitKind.Corps, parent: army);
		CUnit division = Unit(country, EUnitKind.Division, parent: corps);
		CUnit direct = Unit(country, EUnitKind.Division, parent: army);
		CUnit loose = Unit(country, EUnitKind.Division, parent: theatre);
		CUnit looseCorps = Unit(country, EUnitKind.Corps, parent: theatre);
		CUnit underLooseCorps = Unit(country, EUnitKind.Division, parent: looseCorps);

		Assert.Same(army, COrganisation.ArmyAbove(army));
		Assert.Same(army, COrganisation.ArmyAbove(corps));
		Assert.Same(army, COrganisation.ArmyAbove(division));
		Assert.Same(army, COrganisation.ArmyAbove(direct));
		Assert.Null(COrganisation.ArmyAbove(loose));
		Assert.Null(COrganisation.ArmyAbove(underLooseCorps));
		Assert.Null(COrganisation.ArmyAbove(theatre));
		Assert.Null(COrganisation.ArmyAbove(Unit(country, EUnitKind.Navy, Kiel)));
	}

	// ---- how fast it comes back

	[Fact]
	public void AUnitWellSuppliedOnGoodRoadsGainsByItsCountrysOfficerRatio()
	{
		CCountry country = Germany();
		CUnit unit = Unit(country);
		CGameState state = State(new[] { country });

		Assert.Equal(1400, Factor(state, country, unit));
	}

	[Fact]
	public void SuppliesRoadsAndTheCountrysModifierEachScaleIt()
	{
		CCountry country = Germany();
		CUnit unit = Unit(country, location: Kiel);
		CGameState state = State(new[] { country });

		Assert.Equal(840, Factor(state, country, unit));          // infrastructure 6 of 10

		unit.SuppliesReceived = CFixedPoint.Parse("0.5");
		Assert.Equal(420, Factor(state, country, unit));

		country.Modifier.Set(COrganisation.RegainModifier, CFixedPoint.Parse("0.1"));
		Assert.Equal(462, Factor(state, country, unit));
	}

	[Fact]
	public void TheProvincesOwnModifiersScaleItsInfrastructureAndTheResultIsHeldBetweenAHundredthAndOne()
	{
		CCountry country = Germany();
		country.OfficerRatio = CFixedPoint.One;
		CUnit unit = Unit(country, location: Kiel);
		CGameState state = State(new[] { country });
		CProvince kiel = state.Province(Kiel);

		kiel.Modifier.Set(COrganisation.LocalInfrastructure, CFixedPoint.Parse("0.25"));
		Assert.Equal(750, Factor(state, country, unit));
		kiel.Modifier.Set(COrganisation.GlobalInfrastructure, CFixedPoint.Parse("0.5"));
		Assert.Equal(1000, Factor(state, country, unit));         // 1.05 is held to 1

		kiel.Modifier.Set(COrganisation.Infrastructure, CFixedPoint.Zero);
		Assert.Equal(10, Factor(state, country, unit));           // no roads at all still gives a hundredth
	}

	[Fact]
	public void AtSeaThereAreNoRoadsToCount()
	{
		CCountry country = Germany();
		country.OfficerRatio = CFixedPoint.One;
		CUnit fleet = Unit(country, EUnitKind.Navy, Kiel);
		Brigade(Destroyer(), fleet);
		CGameState state = State(new[] { country });
		state.Province(Kiel).SetBuildingLevel(NavalBase, CFixedPoint.FromInt(1));

		Assert.Equal(600, Factor(state, country, fleet));         // in port, on the port's roads
		fleet.Location = Baltic;
		Assert.Equal(1000, Factor(state, country, fleet));
	}

	[Fact]
	public void AUnitGivenNoSuppliesAtAllLosesOrganisation()
	{
		CCountry country = Germany();
		CUnit unit = Unit(country);
		unit.SuppliesReceived = CFixedPoint.Zero;
		CGameState state = State(new[] { country });

		Assert.Equal(-1000, Factor(state, country, unit));
	}

	// ---- one hour of one brigade

	[Fact]
	public void AnHourBringsBackAFifthOfABrigadesMoraleTimesTheFactor()
	{
		CCountry country = Germany();
		CSubUnit brigade = Brigade(Infantry(), Unit(country), organisation: "30");

		COrganisation.Regain(brigade, 1000, ceiling: 56000, lowBonus: 300);

		Assert.Equal(CFixedPoint.Parse("30.060"), brigade.Organisation);
	}

	[Fact]
	public void ABrigadeLowOnOrganisationGainsFasterAndOneNearlyFullSlower()
	{
		CCountry country = Germany();
		CSubUnit low = Brigade(Infantry(), Unit(country), organisation: "13.9");       // under a quarter of 56
		CSubUnit full = Brigade(Infantry(), Unit(country), organisation: "42.1");      // over three quarters

		COrganisation.Regain(low, 1000, ceiling: 56000, lowBonus: 300);
		COrganisation.Regain(full, 1000, ceiling: 56000, lowBonus: 300);

		Assert.Equal(CFixedPoint.Parse("13.978"), low.Organisation);
		Assert.Equal(CFixedPoint.Parse("42.142"), full.Organisation);
	}

	[Fact]
	public void TheOriginalsWaffenBrigadesGainAHundredAndSevenThousandthsInTheirOneHour()
	{
		// Morale 0.55 on Berlin's roads with Germany's officers: a factor of 1.4. At 55 of the
		// 60 its new doctrine allows it is nearly full, so 0.7 of that, and 0.55 x 0.98 x 0.2.
		CCountry country = Germany();
		CUnit unit = Unit(country);
		CSubUnit brigade = Brigade(Type("waffen_brigade", EUnitBranch.Land, "60", "0.55"), unit, organisation: "55");
		CGameState state = State(new[] { country });

		COrganisation.Regain(brigade, Factor(state, country, unit), ceiling: 60000, lowBonus: 300);

		Assert.Equal(CFixedPoint.Parse("55.107"), brigade.Organisation);
	}

	[Fact]
	public void ABrigadeIsNeverLeftAboveItsCeilingOrBelowNothing()
	{
		CCountry country = Germany();
		CSubUnit nearly = Brigade(Infantry(), Unit(country), organisation: "55.99");
		CSubUnit over = Brigade(Infantry(), Unit(country), organisation: "40");
		CSubUnit starved = Brigade(Infantry(), Unit(country), organisation: "0.05");

		COrganisation.Regain(nearly, 1400, ceiling: 56000, lowBonus: 300);
		COrganisation.Regain(over, 1400, ceiling: 28000, lowBonus: 300);
		COrganisation.Regain(starved, -1000, ceiling: 56000, lowBonus: 300);

		Assert.Equal(CFixedPoint.FromInt(56), nearly.Organisation);
		Assert.Equal(CFixedPoint.FromInt(28), over.Organisation);      // brought down in the one hour
		Assert.Equal(CFixedPoint.Zero, starved.Organisation);
	}

	[Fact]
	public void WithNoSuppliesABrigadeLosesAFifthOfItsMoraleAnHour()
	{
		CCountry country = Germany();
		CSubUnit brigade = Brigade(Infantry(), Unit(country), organisation: "30");

		COrganisation.Regain(brigade, -1000, ceiling: 56000, lowBonus: 300);

		Assert.Equal(CFixedPoint.Parse("29.940"), brigade.Organisation);
	}

	// ---- the hour

	[Fact]
	public void AnHourOfTheGameHoldsReservesDownAndBringsRegularsUp()
	{
		CCountry country = Germany();
		CUnit unit = Unit(country);
		CSubUnit regular = Brigade(Infantry(), unit);
		CSubUnit reserve = Brigade(Infantry(), unit, reserve: true);
		country.Officers = CFixedPoint.FromInt(300);                 // of the 200 its two brigades call for
		CGameState state = State(new[] { country });

		CTick.RunHour(state);

		// 0.3 x 1.4 x 0.2, at 40 of 56; and the reserve is held to 28 whatever it gained.
		Assert.Equal(CFixedPoint.Parse("40.084"), regular.Organisation);
		Assert.Equal(CFixedPoint.FromInt(28), reserve.Organisation);
		Assert.Equal(CFixedPoint.Parse("1.4"), country.OfficerRatio);
	}

	[Fact]
	public void AnHoursOrganisationIsWorkedOutWithTheRatioTheHourBeforeLeft()
	{
		CCountry country = Germany();
		CSubUnit brigade = Brigade(Infantry(), Unit(country));
		country.Officers = CFixedPoint.FromInt(140);
		CGameState state = State(new[] { country });
		country.OfficerRatio = CFixedPoint.One;                      // as if its officers had only just arrived

		CTick.RunHour(state);
		Assert.Equal(CFixedPoint.FromInt(40), brigade.Organisation);
		Assert.Equal(CFixedPoint.Parse("1.4"), country.OfficerRatio);

		CTick.RunHour(state);
		Assert.Equal(CFixedPoint.Parse("40.084"), brigade.Organisation);
	}

	[Fact]
	public void EnteringAGameWorksOutTheRatioBeforeAnyHourPasses()
	{
		CCountry country = Germany();
		Brigade(Infantry(), Unit(country));
		country.Officers = CFixedPoint.FromInt(50);
		country.OfficerRatio = CFixedPoint.One;
		CGameState state = State(new[] { country });

		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.Parse("0.5"), country.OfficerRatio);
	}

	// ---- a crowded base

	[Fact]
	public void APortHasRoomForSoManyShipsALevel()
	{
		CCountry country = Germany();
		CUnit fleet = Unit(country, EUnitKind.Navy, Kiel);
		for (int i = 0; i < 12; i++)
		{
			Brigade(Destroyer(), fleet);
		}
		CGameState state = State(new[] { country });

		Assert.Equal(250, new CBaseCrowding(state).Of(fleet));     // no base at all, and never under a quarter

		state.Province(Kiel).SetBuildingLevel(NavalBase, CFixedPoint.FromInt(1));
		Assert.Equal(500, new CBaseCrowding(state).Of(fleet));     // room for 6

		state.Province(Kiel).SetBuildingLevel(NavalBase, CFixedPoint.FromInt(3));
		Assert.Equal(1000, new CBaseCrowding(state).Of(fleet));    // room to spare
	}

	[Fact]
	public void WhoeverHoldsThePortRunsItByItsOwnModifierAndTechnology()
	{
		CCountry country = Germany();
		CUnit fleet = Unit(country, EUnitKind.Navy, Kiel);
		for (int i = 0; i < 12; i++)
		{
			Brigade(Destroyer(), fleet);
		}
		CGameState state = State(new[] { country });
		state.Province(Kiel).SetBuildingLevel(NavalBase, CFixedPoint.FromInt(1));

		country.Modifier.Set(CBaseCrowding.NavalBaseEfficiencyModifier, CFixedPoint.Parse("0.25"));
		Assert.Equal(625, new CBaseCrowding(state).Of(fleet));

		country.Technology.Set(CBaseCrowding.NavalBaseEfficiencyModifier, CFixedPoint.Parse("0.2"));
		Assert.Equal(725, new CBaseCrowding(state).Of(fleet));
	}

	[Fact]
	public void EveryFleetBasedInAPortSharesItWhoeverItBelongsTo()
	{
		CCountry germany = Germany();
		var britain = new CCountry { Tag = Eng, Name = "Britain", DefinitionFile = "" };
		CUnit ours = Unit(germany, EUnitKind.Navy, Kiel);
		CUnit theirs = Unit(britain, EUnitKind.Navy, Kiel);
		for (int i = 0; i < 6; i++)
		{
			Brigade(Destroyer(), ours);
			Brigade(Destroyer(), theirs);
		}
		CGameState state = State(new[] { germany, britain });
		state.Province(Kiel).SetBuildingLevel(NavalBase, CFixedPoint.FromInt(1));

		Assert.Equal(500, new CBaseCrowding(state).Of(ours));
	}

	[Fact]
	public void AnAirfieldHasRoomForAWingALevelAndShipsDoNotCrowdIt()
	{
		CCountry country = Germany();
		CUnit group = Unit(country, EUnitKind.Air, Kiel);
		for (int i = 0; i < 4; i++)
		{
			Brigade(Fighter(), group);
		}
		CUnit fleet = Unit(country, EUnitKind.Navy, Kiel);
		Brigade(Destroyer(), fleet);
		CGameState state = State(new[] { country });
		state.Province(Kiel).SetBuildingLevel(AirBase, CFixedPoint.FromInt(2));
		state.Province(Kiel).SetBuildingLevel(NavalBase, CFixedPoint.FromInt(1));

		var crowding = new CBaseCrowding(state);

		Assert.Equal(500, crowding.Of(group));
		Assert.Equal(1000, crowding.Of(fleet));
	}

	[Fact]
	public void AnArmyAndAnAirGroupAboardItsCarrierAreNeverCrowded()
	{
		CCountry country = Germany();
		CUnit division = Unit(country);
		CUnit fleet = Unit(country, EUnitKind.Navy, Kiel);
		CSubUnit carrier = Brigade(Destroyer(), fleet);
		var aboard = new CUnit(EUnitKind.Air, Ger) { Location = Kiel, Base = Kiel, CarriedBy = carrier };
		carrier.Carried.Add(aboard);
		Brigade(Fighter(), aboard);
		CGameState state = State(new[] { country });

		var crowding = new CBaseCrowding(state);

		Assert.Equal(1000, crowding.Of(division));
		Assert.Equal(1000, crowding.Of(aboard));
	}

	[Fact]
	public void ACrowdedBaseSlowsOrganisationAndReinforcementAlike()
	{
		CCountry country = Germany();
		country.OfficerRatio = CFixedPoint.One;
		CUnit fleet = Unit(country, EUnitKind.Navy, Kiel);
		for (int i = 0; i < 12; i++)
		{
			Brigade(Destroyer(), fleet);
		}
		fleet.Location = Baltic;
		CGameState state = State(new[] { country });
		state.Province(Kiel).SetBuildingLevel(NavalBase, CFixedPoint.FromInt(1));

		Assert.Equal(500, Factor(state, country, fleet));
		Assert.Equal(500, CReinforcement.Speed(country, fleet, new CBaseCrowding(state).Of(fleet)));
		Assert.Equal(1000, CReinforcement.Speed(country, fleet));
	}

	// ---- what else holds a brigade to its ceiling

	[Fact]
	public void ABrigadeThatGainsALevelIsHeldToWhatItsNewFiguresAllow()
	{
		CCountry country = Germany();
		country.OfficerRatio = CFixedPoint.Parse("0.8");
		country.TotalIc = 100;
		CSubUnitType type = Infantry();
		var doctrine = new CTechnology("infantry_warfare", 12, new CModifier("infantry_warfare")) { Change = false, CanUpgrade = true };
		var effect = new CSubUnitStats();
		effect.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.FromInt(5));
		doctrine.UnitEffects[type.Key] = effect;
		type.Technologies.Add(doctrine);
		CSubUnit brigade = Brigade(type, Unit(country));
		country.TechnologyLevels[doctrine.Name] = 1;
		CGameState state = State(new[] { country }, technologies: new[] { doctrine });

		CUpgrades.Run(state, country, CFixedPoint.Zero, apply: true);

		// 45 now, of which its country's officers can hold together 36.
		Assert.Equal(CFixedPoint.FromInt(45), brigade.Stats[CSubUnitType.DefaultOrganisation]);
		Assert.Equal(CFixedPoint.FromInt(36), brigade.Organisation);
	}

	[Fact]
	public void HoldingACountrysBrigadesGivesBackTheMenOfTheStrengthTakenOff()
	{
		CCountry country = Germany();
		CUnit unit = Unit(country);
		CSubUnit reserve = Brigade(Infantry(), unit, reserve: true);
		CSubUnit regular = Brigade(Infantry(), unit);
		CGameState state = State(new[] { country });

		COrganisation.HoldToCeilings(state, country);

		// Half a brigade of 3.33 men, with what is under a thousandth dropped.
		Assert.Equal(CFixedPoint.FromInt(15), reserve.Strength);
		Assert.Equal(CFixedPoint.FromInt(15), reserve.Highest);
		Assert.Equal(CFixedPoint.FromInt(28), reserve.Organisation);
		Assert.Equal(CFixedPoint.Parse("501.665"), country.Manpower);
		Assert.Equal(CFixedPoint.FromInt(30), regular.Strength);
		Assert.Equal(CFixedPoint.FromInt(40), regular.Organisation);
	}
}
