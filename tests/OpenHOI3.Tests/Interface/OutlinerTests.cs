using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the outliner lists and what its rows say, against <c>docs/mechanics/outliner.md</c> and three
/// screenshots of the original's, with Their Finest Hour's strings where the test needs their shape.
/// </summary>
public class OutlinerTests
{
	private static readonly CCountryTag Ita = new("ITA");
	private static readonly CCountryTag Eth = new("ETH");

	private const int Roma = 1;
	private const int Gurbo = 2;
	private const int Sea = 3;

	private static readonly Dictionary<string, string> Texts = new()
	{
		["OL_COMBATS"] = "Land Combat",
		["ARMIES"] = "Land Units",
		["NAVIES"] = "Naval Units",
		["OL_ARMY"] = "This army currently $COMMANDER$ and is standing in §Y$PROV$§W.",
		["OL_COMMANDER"] = "is under the command of $COMMANDER$",
		["OL_NOCOMMANDER"] = "has no commander",
		["OL_ARMY_MOVE"] = "It is on the move towards §Y$DEST$§W.",
		["OL_UNIT_MOVING"] = "Moving towards §Y$TARGET$§W.",
		["OL_LAND_COMBAT_ATTACK"] = "In our attack we have a progressed toward winning by §Y$PROGRESS$%§W. Our forces are led by "
			+ "§Y$OUR_LEAD$§W who has §Y$OUR_NUM$§W brigades committed, and have §Y$OUR_RES$§W brigades in reserves. We are "
			+ "facing $ENEMY$ forces led by §Y$THEIR_LEAD$§W who has §Y$THEIR_NUM$§W brigades committed, and have "
			+ "§Y$THEIR_RES$§W brigades in reserves.",
		["NO_COMMANDER"] = "(No Commander)",
		["ETH_ADJ"] = "Ethiopian",
		["AND"] = "and ",
		["OL_AO_THEY_REQUEST"] = " have requested that we",
		["OL_AO_WE_REQUEST"] = "We have requested that ",
		["OL_AO_DEFEND"] = " §Gdefend§W near ",
		["OL_AO_ATTACK"] = " §Rattack§W near ",
	};

	private static string Localise(string key) => Texts.GetValueOrDefault(key, key);

	private static readonly EOutlinerSection[] All = System.Enum.GetValues<EOutlinerSection>();

	private static CGameState State(out CCountry italy)
	{
		italy = new CCountry { Tag = Ita, Name = "Italy", DefinitionFile = "" };
		var ethiopia = new CCountry { Tag = Eth, Name = "Ethiopia", DefinitionFile = "" };
		var war = new CWar("war");
		war.Attackers.Add(Ita);
		war.Defenders.Add(Eth);
		return new CGameState(new CDate(1936, 1, 1), new[]
		{
			new CProvince(0, "", isSea: false),
			new CProvince(Roma, "Roma", isSea: false) { Owner = Ita, Controller = Ita },
			new CProvince(Gurbo, "Gurbo", isSea: false) { Owner = Eth, Controller = Eth },
			new CProvince(Sea, "Tyrrhenian Sea", isSea: true),
		}, new[] { italy, ethiopia }, wars: new[] { war });
	}

	private static CUnit Unit(CCountry owner, EUnitKind kind, string name, int id, int brigades = 1, string strength = "10")
	{
		var unit = new CUnit(kind, owner.Tag) { Name = name, Location = kind == EUnitKind.Navy ? Sea : Roma, Id = id };
		for (int i = 0; i < brigades; i++)
		{
			var type = new CSubUnitType("infantry_brigade", kind == EUnitKind.Navy ? EUnitBranch.Naval : EUnitBranch.Land);
			type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(10));
			var subUnit = new CSubUnit(type) { Unit = unit, Id = id * 10 + i, Strength = CFixedPoint.Parse(strength) };
			subUnit.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(subUnit);
		}
		return unit;
	}

	private static void Attach(CUnit parent, CUnit child)
	{
		parent.Children.Add(child);
		child.Parent = parent;
	}

	[Fact]
	public void SectionsComeInTheirOrderEachUnderItsTitleAndOnlyWithSomethingInThem()
	{
		CGameState state = State(out CCountry italy);
		CUnit army = Unit(italy, EUnitKind.Army, "Cdo. Sup. Africa", 1);
		CUnit fleet = Unit(italy, EUnitKind.Navy, "Regia Marina", 2, brigades: 18);
		italy.Units.Add(fleet);
		italy.Units.Add(army);
		var combat = new CCombat(Gurbo);
		combat.Attacker.Add(army);

		List<COutlinerRow> rows = COutlinerContents.Gather(state, Ita, All, _ => false);

		Assert.Equal(new[]
		{
			new COutlinerRow(EOutlinerSection.LandCombat, IsTitle: true),
			new COutlinerRow(EOutlinerSection.LandCombat, Combat: combat, Province: Gurbo),
			new COutlinerRow(EOutlinerSection.LandUnits, IsTitle: true),
			new COutlinerRow(EOutlinerSection.LandUnits, Unit: army),
			new COutlinerRow(EOutlinerSection.NavalUnits, IsTitle: true),
			new COutlinerRow(EOutlinerSection.NavalUnits, Unit: fleet),
		}, rows);
		// A section switched off is not there, title and all.
		Assert.DoesNotContain(COutlinerContents.Gather(state, Ita, new[] { EOutlinerSection.LandUnits }, _ => false),
			row => row.Section != EOutlinerSection.LandUnits);
	}

	[Fact]
	public void AnExpandedUnitShowsItsTreeAndAFleetUnderALandCommandIsListedTwice()
	{
		CGameState state = State(out CCountry italy);
		CUnit africa = Unit(italy, EUnitKind.ArmyGroup, "Cdo. Sup. Africa", 1);
		CUnit army = Unit(italy, EUnitKind.Army, "Armata dell'A.O.I.", 2);
		CUnit corps = Unit(italy, EUnitKind.Corps, "I Corpo d'Armata d'Eritrea", 3);
		CUnit squadron = Unit(italy, EUnitKind.Navy, "19a Squadra di Marina", 4, brigades: 2);
		Attach(africa, army);
		Attach(army, corps);
		Attach(africa, squadron);
		italy.Units.Add(africa);

		var expanded = new HashSet<CUnit> { africa, army };
		List<COutlinerRow> rows = COutlinerContents.Gather(state, Ita, All, expanded.Contains);

		Assert.Equal(new (CUnit, int)[] { (africa, 0), (army, 1), (corps, 2), (squadron, 1) },
			rows.Where(row => row.Section == EOutlinerSection.LandUnits && !row.IsTitle).Select(row => (row.Unit!, row.Depth)));
		Assert.Equal(new[] { squadron },
			rows.Where(row => row.Section == EOutlinerSection.NavalUnits && !row.IsTitle).Select(row => row.Unit));
		// Collapsed, the root alone.
		Assert.Single(COutlinerContents.Gather(state, Ita, new[] { EOutlinerSection.LandUnits }, _ => false), row => !row.IsTitle);
	}

	[Fact]
	public void ANameIsCutAtTwentyOneAndCarriesItsSelectionGroup()
	{
		CGameState state = State(out CCountry italy);
		CUnit corps = Unit(italy, EUnitKind.Corps, "I Corpo d'Armata d'Eritrea", 7);

		Assert.Equal("I Corpo d'Armata d...", COutlinerText.UnitName(italy, corps));
		italy.SelectionGroups[3].Add(7);
		Assert.Equal("I Corpo d'Armata d...(§Y3§W) ", COutlinerText.UnitName(italy, corps));
		Assert.Equal("Roma", COutlinerText.ShortName("Roma"));
	}

	[Fact]
	public void ALandUnitShowsItsStrengthAFleetItsShips()
	{
		CGameState state = State(out CCountry italy);

		Assert.Equal("100.0%", COutlinerText.UnitFigure(state, Unit(italy, EUnitKind.Division, "a", 1), landRow: true, made: false));
		Assert.Equal("99.9%",
			COutlinerText.UnitFigure(state, Unit(italy, EUnitKind.Division, "b", 2, strength: "9.999"), landRow: true, made: false));
		CUnit fleet = Unit(italy, EUnitKind.Navy, "Regia Marina", 3, brigades: 18);
		Assert.Equal("18", COutlinerText.UnitFigure(state, fleet, landRow: false, made: true));
		// A fleet in the land tree is a land row, and shows its strength (a screenshot of the original: 19a Squadra di Marina).
		Assert.Equal("100.0%", COutlinerText.UnitFigure(state, fleet, landRow: true, made: false));
	}

	[Fact]
	public void ALandBattleShowsTheAttackersShareGreenToTheAttackerAndNegatedRedToTheDefender()
	{
		var combat = new CCombat(Gurbo);                       // two empty sides: the share is a half

		Assert.Equal("§G50%§W", COutlinerText.CombatFigure(combat, playerAttacks: true));
		Assert.Equal("§R-50%§W", COutlinerText.CombatFigure(combat, playerAttacks: false));
	}

	[Fact]
	public void ASeaBattleShowsBothSidesShipsTheAttackersFirstAndThePlayersGreen()
	{
		State(out CCountry italy);
		var combat = new CCombat(Sea, ECombatKind.Naval);
		combat.Attacker.Add(Unit(italy, EUnitKind.Navy, "a", 1, brigades: 3));
		combat.Defender.Add(Unit(italy, EUnitKind.Navy, "b", 2, brigades: 2));

		Assert.Equal("§G3§W - §R2§W", COutlinerText.CombatFigure(combat, playerAttacks: true));
		Assert.Equal("§R3§W - §G2§W", COutlinerText.CombatFigure(combat, playerAttacks: false));
	}

	[Fact]
	public void AnArmysTooltipNamesItsCommanderAndWhereItStands()
	{
		CGameState state = State(out CCountry italy);
		CUnit army = Unit(italy, EUnitKind.Army, "Armata dell'A.O.I.", 1);
		army.Leader = new CLeader(1) { Name = "de Bono" };

		Assert.Equal("This army currently is under the command of de Bono and is standing in §YRoma§W.",
			COutlinerTooltips.Unit(Localise, state, army, "entry_text").Text);
		army.Leader = null;
		army.Path.Add(Gurbo);
		Assert.Equal("This army currently has no commander and is standing in §YRoma§W.\nIt is on the move towards §YGurbo§W.",
			COutlinerTooltips.Unit(Localise, state, army, "entry_text").Text);
		Assert.Equal("Moving towards §YGurbo§W.", COutlinerTooltips.Unit(Localise, state, army, "movment").Text);
	}

	[Fact]
	public void ALandBattlesTooltipSaysTheProgressLeadersAndBrigadesOfBothSides()
	{
		CGameState state = State(out CCountry italy);
		var ethiopia = state.Country(Eth)!;
		var combat = new CCombat(Gurbo);
		CUnit ours = Unit(italy, EUnitKind.Division, "a", 1, brigades: 3);
		ours.Leader = new CLeader(1) { Name = "Giorgis", Rank = 1 };
		CUnit theirs = Unit(ethiopia, EUnitKind.Division, "b", 2, brigades: 6);
		CUnit reserve = Unit(ethiopia, EUnitKind.Division, "c", 3, brigades: 1);
		combat.Attacker.Add(ours);
		combat.Defender.Add(theirs);
		combat.Defender.Add(reserve);
		combat.Attacker.Front.Add(ours);
		combat.Defender.Front.Add(theirs);
		combat.Defender.Reserves.Add(reserve);

		string text = COutlinerTooltips.Combat(Localise, state, combat, playerAttacks: true).Text;

		Assert.StartsWith("In our attack we have a progressed toward winning by §Y", text);
		Assert.EndsWith("Our forces are led by §YGiorgis§W who has §Y3§W brigades committed, and have §Y0§W brigades in "
			+ "reserves. We are facing Ethiopian forces led by §Y(No Commander)§W who has §Y6§W brigades committed, and have "
			+ "§Y1§W brigades in reserves.", text);
	}

	[Fact]
	public void ABombingIsOursWhereTheUnitOfOursInItIsAnAirUnit()
	{
		CGameState state = State(out CCountry italy);
		CUnit wing = Unit(italy, EUnitKind.Air, "4th Squadra Aerea", 1);
		CUnit division = Unit(italy, EUnitKind.Division, "a", 2);
		italy.Units.Add(wing);
		italy.Units.Add(division);
		var ours = new CCombat(Gurbo, ECombatKind.LandBombing);
		var theirs = new CCombat(Roma, ECombatKind.LandBombing);
		wing.Bombing = ours;
		division.Bombing = theirs;

		List<COutlinerRow> rows = COutlinerContents.Gather(state, Ita, All, _ => false);

		Assert.Contains(new COutlinerRow(EOutlinerSection.OurBombings, Combat: ours, Province: Gurbo), rows);
		Assert.Contains(new COutlinerRow(EOutlinerSection.HostileBombing, Combat: theirs, Province: Roma), rows);
		Assert.Equal("", COutlinerText.CombatFigure(ours, playerAttacks: true));
	}

	[Fact]
	public void AnAlliedObjectiveSaysWhoAskedAndWhetherToDefendOrAttack()
	{
		CGameState state = State(out _);
		state.AlliedObjectives.Set(Ita, Eth, Gurbo, active: true);
		state.AlliedObjectives.Set(Eth, Ita, Roma, active: true);

		List<COutlinerRow> rows = COutlinerContents.Gather(state, Ita, All, _ => false);

		Assert.Equal(new[]
		{
			new COutlinerRow(EOutlinerSection.AlliedObjectives, IsTitle: true),
			new COutlinerRow(EOutlinerSection.AlliedObjectives, Province: Gurbo, AskedOfPlayer: true),
			new COutlinerRow(EOutlinerSection.AlliedObjectives, Province: Roma),
		}, rows);
		Assert.Equal("§RGurbo", COutlinerText.ObjectiveText(state, Gurbo, askedOfPlayer: true));
		// Two countries in no faction are one faction to the original: Ethiopia's Gurbo is to be defended.
		Assert.Equal("§YEthiopia§W have requested that we §Gdefend§W near §YGurbo§W.",
			COutlinerTooltips.Objective(Localise, state, Ita, Gurbo, askedOfPlayer: true).Text);
		Assert.Equal("§YItaly§W, §YEthiopia§W and  §YPoland§W", COutlinerTooltips.List(Localise, new[] { "Italy", "Ethiopia", "Poland" }));
	}
}
