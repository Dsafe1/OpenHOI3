using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>The order of battle browser's tree and rules, against <c>docs/mechanics/oobbrowser.md</c>, and the card's, against <c>brigadecard.md</c>.</summary>
public class OobBrowserTests
{
	private static readonly CCountryTag Eng = new("ENG");

	private static readonly CSubUnitType Infantry = new("infantry_brigade", EUnitBranch.Land);

	private static readonly CSubUnitType Ship = MakeShip();

	private static CSubUnitType MakeShip()
	{
		var type = new CSubUnitType("battleship", EUnitBranch.Naval);
		type.Stats.Values.Add("hull", CFixedPoint.FromInt(1));
		return type;
	}

	private static CCountry England() => new() { Tag = Eng, Name = "England", DefinitionFile = "" };

	private static CGameState World(CCountry country)
	{
		var provinces = new[] { new CProvince(0, "", isSea: false), new CProvince(1, "London", isSea: false) };
		return new CGameState(new CDate(1936, 1, 1), provinces, new[] { country });
	}

	private static int _next = 100;

	private static CUnit Unit(CCountry country, EUnitKind kind, CUnit? parent = null, int brigades = 0, CSubUnitType? type = null)
	{
		var unit = new CUnit(kind, Eng) { Id = ++_next, Location = 1, Name = "Unit " + _next, Parent = parent };
		for (int i = 0; i < brigades; i++)
		{
			var subUnit = new CSubUnit(type ?? Infantry) { Id = ++_next, Unit = unit, Name = "Sub " + _next };
			unit.SubUnits.Add(subUnit);
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

	[Fact]
	public void TheRootsAreTheTopLevelUnits()
	{
		CCountry england = England();
		CUnit theatre = Unit(england, EUnitKind.Theatre);
		Unit(england, EUnitKind.Army, theatre);
		var tree = new COobTree();
		tree.FillRoots(england);
		Assert.Equal(theatre, Assert.Single(tree.Rows).Unit);
		Assert.False(tree.AnyIndented);
	}

	[Fact]
	public void OpeningAddsDirectChildrenAndClosingTakesAllBelow()
	{
		CCountry england = England();
		CUnit theatre = Unit(england, EUnitKind.Theatre);
		CUnit army = Unit(england, EUnitKind.Army, theatre);
		Unit(england, EUnitKind.Corps, army);
		var tree = new COobTree();
		tree.FillRoots(england);
		tree.Open(0);
		Assert.Equal(new[] { 0, 1 }, tree.Rows.Select(row => row.Level));
		Assert.True(tree.IsOpen(0));
		Assert.False(tree.IsOpen(1));
		tree.Open(1);
		Assert.Equal(3, tree.Rows.Count);
		tree.Toggle(0);
		Assert.Single(tree.Rows);
		Assert.False(tree.IsOpen(0));
	}

	[Fact]
	public void ExpandingAllOpensEveryLevel()
	{
		CCountry england = England();
		CUnit theatre = Unit(england, EUnitKind.Theatre);
		CUnit army = Unit(england, EUnitKind.Army, theatre);
		Unit(england, EUnitKind.Corps, army);
		var tree = new COobTree();
		tree.ExpandAll(england);
		Assert.Equal(new[] { 0, 1, 2 }, tree.Rows.Select(row => row.Level));
	}

	[Fact]
	public void AMovedUnitGoesLastUnderAnOpenCommander()
	{
		CCountry england = England();
		CUnit theatre = Unit(england, EUnitKind.Theatre);
		CUnit first = Unit(england, EUnitKind.Army, theatre);
		CUnit second = Unit(england, EUnitKind.Army, theatre);
		CUnit corps = Unit(england, EUnitKind.Corps, first);
		CUnit other = Unit(england, EUnitKind.Corps, second);
		var tree = new COobTree();
		tree.ExpandAll(england);
		first.Children.Remove(corps);
		corps.Parent = second;
		second.Children.Add(corps);
		tree.Reposition(corps);
		Assert.Equal(new[] { theatre, first, second, other, corps }, tree.Rows.Select(row => row.Unit));
		Assert.Equal(2, tree.Rows[4].Level);
	}

	[Fact]
	public void AMovedUnitIsLeftOutWhereItsCommanderIsClosed()
	{
		CCountry england = England();
		CUnit theatre = Unit(england, EUnitKind.Theatre);
		CUnit first = Unit(england, EUnitKind.Army, theatre);
		CUnit second = Unit(england, EUnitKind.Army, theatre);
		CUnit corps = Unit(england, EUnitKind.Corps, first);
		var tree = new COobTree();
		tree.ExpandAll(england);
		first.Children.Remove(corps);
		corps.Parent = second;
		second.Children.Add(corps);
		tree.Reposition(corps);
		Assert.Equal(-1, tree.IndexOf(corps));
	}

	[Fact]
	public void EnsuringARowOpensThoseAbove()
	{
		CCountry england = England();
		CUnit theatre = Unit(england, EUnitKind.Theatre);
		CUnit army = Unit(england, EUnitKind.Army, theatre);
		CUnit corps = Unit(england, EUnitKind.Corps, army);
		var tree = new COobTree();
		tree.FillRoots(england);
		Assert.Equal(2, tree.EnsureRow(corps));
	}

	[Theory]
	[InlineData(0, 0, 5, 0)]
	[InlineData(9, 0, 5, 5)]
	[InlineData(9, 2, 5, 7)]
	[InlineData(9, 8, 5, 8)]
	public void TheScrollPutsTheRowLastAndMakesRoomForChildren(int index, int children, int visible, int first)
	{
		CCountry england = England();
		var tree = new COobTree();
		CUnit root = Unit(england, EUnitKind.Theatre);
		for (int i = 0; i < 20; i++)
		{
			Unit(england, EUnitKind.Theatre);
		}
		tree.FillRoots(england);
		CUnit parent = tree.Rows[index].Unit;
		for (int i = 0; i < children; i++)
		{
			Unit(england, EUnitKind.Army, parent);
		}
		if (children > 0)
		{
			tree.Open(index);
		}
		Assert.Equal(first, tree.ScrollFor(index, visible));
		Assert.NotNull(root);
	}

	[Fact]
	public void ACopyIsRowForRow()
	{
		CCountry england = England();
		CUnit theatre = Unit(england, EUnitKind.Theatre);
		Unit(england, EUnitKind.Army, theatre);
		var one = new COobTree();
		one.ExpandAll(england);
		var two = new COobTree();
		two.CopyFrom(one);
		Assert.Equal(one.Rows, two.Rows);
	}

	[Theory]
	[InlineData(EUnitKind.Theatre, 0, 6)]
	[InlineData(EUnitKind.ArmyGroup, 0, 5)]
	[InlineData(EUnitKind.Army, 0, 4)]
	[InlineData(EUnitKind.Corps, 0, 3)]
	[InlineData(EUnitKind.Division, 1, 1)]
	[InlineData(EUnitKind.Division, 3, 2)]
	[InlineData(EUnitKind.Navy, 1, 2)]
	[InlineData(EUnitKind.Air, 1, 1)]
	public void TheSizeFrameByLevel(EUnitKind kind, int brigades, int frame)
	{
		Assert.Equal(frame, COobBrowserText.SizeFrame(Unit(England(), kind, null, brigades)));
	}

	[Fact]
	public void TheRowShowsFiveBrigadesAndDots()
	{
		CUnit unit = Unit(England(), EUnitKind.Division, null, 6);
		string info = COobBrowserText.UnitInfo(unit);
		Assert.Equal(5, info.Count(character => character == '£'));
		Assert.EndsWith("...", info);
		Assert.DoesNotContain("...", COobBrowserText.UnitInfo(Unit(England(), EUnitKind.Division, null, 5)));
	}

	[Fact]
	public void TheDelayedHalfCountsOnlyLevelsThatHaveUnits()
	{
		CCountry england = England();
		CUnit corps = Unit(england, EUnitKind.Corps);
		Unit(england, EUnitKind.Division, corps, 2);
		Unit(england, EUnitKind.Division, corps, 1);
		CGameState state = World(england);
		string delayed = COobBrowserText.Tooltip(key => key, state, corps, "unit_name").Delayed;
		Assert.Equal("OOB_UNDER_COMMAND2 NUM_DIVISONS\n\n3 £infantry_brigade  ", delayed);
	}

	[Fact]
	public void AUnitOnlyGoesUnderAHigherRankAndNotItself()
	{
		CCountry england = England();
		CUnit corps = Unit(england, EUnitKind.Corps);
		CUnit division = Unit(england, EUnitKind.Division);
		Assert.True(COobBrowserText.MayTakeUnit(corps, division));
		Assert.False(COobBrowserText.MayTakeUnit(division, corps));
		Assert.False(COobBrowserText.MayTakeUnit(corps, corps));
	}

	[Fact]
	public void ALeaderDroppedOnALedUnitSwaps()
	{
		CCountry england = England();
		CUnit left = Unit(england, EUnitKind.Corps);
		CUnit right = Unit(england, EUnitKind.Corps);
		var first = new CLeader(1) { Rank = 2, Unit = left };
		var second = new CLeader(2) { Rank = 2, Unit = right };
		left.Leader = first;
		right.Leader = second;
		Assert.Equal(2, COobBrowserText.LeaderDrop(Eng, right, first).Count());
		right.Leader = null;
		Assert.Single(COobBrowserText.LeaderDrop(Eng, right, first));
	}

	[Fact]
	public void APrideIsFoundAndMovedForTheDissent()
	{
		CCountry england = England();
		CGameState state = World(england);
		state.Defines.Set(CManpower.MilitarySection, CPride.RedesignationDissent, CFixedPoint.One);
		CUnit fleet = Unit(england, EUnitKind.Navy, null, 2, Ship);
		CSubUnit oldShip = fleet.SubUnits[0];
		CSubUnit newShip = fleet.SubUnits[1];
		oldShip.IsPride = true;
		Assert.Equal(oldShip, CPride.Find(england));
		new CMakePrideCommand(Eng, newShip.Id).Execute(state);
		Assert.False(oldShip.IsPride);
		Assert.True(newShip.IsPride);
		Assert.Equal(CFixedPoint.One, england.Dissent);
	}

	[Fact]
	public void TheFirstPrideCostsNothing()
	{
		CCountry england = England();
		CGameState state = World(england);
		state.Defines.Set(CManpower.MilitarySection, CPride.RedesignationDissent, CFixedPoint.One);
		CUnit fleet = Unit(england, EUnitKind.Navy, null, 1, Ship);
		new CMakePrideCommand(Eng, fleet.SubUnits[0].Id).Execute(state);
		Assert.Equal(CFixedPoint.Zero, england.Dissent);
	}

	[Fact]
	public void TheDissentIsCutByNewerTechnologyAndHalvedByALargerHull()
	{
		var defines = new CDefines();
		defines.Set(CManpower.MilitarySection, CPride.RedesignationDissent, CFixedPoint.One);
		var older = new CSubUnit(Ship);
		var newer = new CSubUnit(Ship);
		older.Stats.CopyFrom(Ship.Stats);
		newer.Stats.CopyFrom(Ship.Stats);
		Assert.Equal(CFixedPoint.One, CPride.Dissent(defines, newer, older));
		newer.Stats.Values.Add("hull", CFixedPoint.One);
		Assert.Equal(new CFixedPoint(500), CPride.Dissent(defines, newer, older));
		Assert.Equal(CFixedPoint.Zero, CPride.Dissent(defines, newer, null));
	}

	[Fact]
	public void ARenameGivesUpTheOldNameAndTakesTheNew()
	{
		CCountry england = England();
		CGameState state = World(england);
		CUnit fleet = Unit(england, EUnitKind.Navy, null, 1, Ship);
		CSubUnit ship = fleet.SubUnits[0];
		string old = ship.Name;
		england.UnitNamesInUse.Add(old);
		new CRenameSubUnitCommand(Eng, ship.Id, "Hood").Execute(state);
		Assert.Equal("Hood", ship.Name);
		Assert.Contains("Hood", england.UnitNamesInUse);
		Assert.DoesNotContain(old, england.UnitNamesInUse);
	}

	[Fact]
	public void ARenameIsPostedOnlyForAChangedName()
	{
		var ship = new CSubUnit(Ship) { Id = 7, Name = "Hood" };
		Assert.Null(CBrigadeCardText.Rename(Eng, ship, " Hood "));
		Assert.Null(CBrigadeCardText.Rename(Eng, ship, "   "));
		Assert.NotNull(CBrigadeCardText.Rename(Eng, ship, "Rodney"));
	}

	[Fact]
	public void AUnitsRenameGivesUpTheOldNameAndTakesTheNew()
	{
		CCountry england = England();
		CGameState state = World(england);
		CUnit fleet = Unit(england, EUnitKind.Navy, null, 1, Ship);
		fleet.Name = "Home Fleet";
		england.UnitNamesInUse.Add("Home Fleet");
		var rename = new CRenameUnitCommand(Eng, fleet.Id, "Grand Fleet");
		Assert.True(rename.MayExecute(state));
		rename.Execute(state);
		Assert.Equal("Grand Fleet", fleet.Name);
		Assert.Contains("Grand Fleet", england.UnitNamesInUse);
		Assert.DoesNotContain("Home Fleet", england.UnitNamesInUse);
		// A unit gone is renamed by nobody.
		Assert.False(new CRenameUnitCommand(Eng, 99_999, "Ghost").MayExecute(state));
	}

	[Fact]
	public void AUnitsNameIsSentTrimmedAndOnlyWhereItChangedByteForByte()
	{
		CCountry england = England();
		CUnit fleet = Unit(england, EUnitKind.Navy, null, 1, Ship);
		fleet.Name = "Home Fleet";
		Assert.Null(CUnitPanelText.Rename(Eng, fleet, " Home Fleet\t"));
		Assert.Null(CUnitPanelText.Rename(Eng, fleet, " \r\n "));
		// A change of case is a rename.
		Assert.NotNull(CUnitPanelText.Rename(Eng, fleet, "home fleet"));
		Assert.NotNull(CUnitPanelText.Rename(Eng, fleet, "Grand Fleet"));
	}

	[Fact]
	public void AModelsPicturesAreTriedCountryFirst()
	{
		var ship = new CSubUnit(Ship) { HistoricalModel = 2 };
		Assert.Equal(new[] { "gfx/models/ENG_battleship_2.tga", "gfx/models/battleship_2.tga", "gfx/models/battleship_0.tga" }, CBrigadeCardText.Pictures(Eng, ship));
		Assert.Equal(new[] { "gfx/models/battleship_0.tga" }, CBrigadeCardText.Pictures(Eng, new CSubUnit(Ship)));
	}

	[Fact]
	public void TheSunkListIsThisShipsVictimsInOrder()
	{
		CCountry england = England();
		CGameState state = World(england);
		var ship = new CSubUnit(Ship) { Id = 7 };
		var first = new CSubUnit(Ship) { Id = 8, SunkById = 7 };
		var other = new CSubUnit(Ship) { Id = 9, SunkById = 3 };
		var second = new CSubUnit(Ship) { Id = 10, SunkById = 7 };
		foreach (CSubUnit each in new[] { first, other, second })
		{
			state.SunkShips.Add(new CSunkShip(Eng, each));
		}
		Assert.Equal(new[] { first, second }, CBrigadeCardText.SunkBy(state, ship).Select(entry => entry.Ship));
		Assert.True(CBrigadeCardText.HasSunk(state, ship));
		Assert.False(CBrigadeCardText.HasSunk(state, new CSubUnit(Ship) { Id = 11 }));
	}

	[Fact]
	public void ThePrideDialogNamesTheOldShipAndTheDissent()
	{
		CCountry england = England();
		CGameState state = World(england);
		state.Defines.Set(CManpower.MilitarySection, CPride.RedesignationDissent, CFixedPoint.One);
		CUnit fleet = Unit(england, EUnitKind.Navy, null, 2, Ship);
		fleet.SubUnits[0].IsPride = true;
		(string title, string text) = CBrigadeCardText.PrideDialog(key => key switch
		{
			"CONFIRM_MAKE_PRIDE_DESC" => "$EFFECTS$",
			"MAKE_PRIDE_PREVIOUS_EFFECT" => "old $OLD_NAME$;",
			"MAKE_PRIDE_DISSENT_EFFECT" => "dissent $DISSENT$;",
			_ => key,
		}, state, england, fleet.SubUnits[1]);
		Assert.Equal("CONFIRM_MAKE_PRIDE_TITLE", title);
		Assert.Contains("old " + fleet.SubUnits[0].Name + ";", text);
		Assert.Contains("dissent 1.00;", text);
	}
}
