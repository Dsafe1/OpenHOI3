using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Map;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The selection, the drag box, the selection groups and the multi-unit panel, against
/// <c>docs/mechanics/selection.md</c>, on a port, its sea and a province inland:
///
///     1 Kiel (10, 10) ==port== 2 Bight (20, 10)      3 Berlin (100, 50)
///
/// Kiel's naval base stands at (12, 8) and its air base at (8, 12) in the positions file.
/// </summary>
public class SelectionTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Kiel = 1;
	private const int Bight = 2;
	private const int Berlin = 3;
	private const int MapWidth = 200;

	private static readonly CBuilding NavalBase = new(0, "naval_base", 10, "", CFixedPoint.Zero);

	private static readonly CMapPosition[] Points = { new(0, 0), new(10, 10), new(20, 10), new(100, 50) };

	private static readonly CBuildingPosition[][] Placed =
	{
		Array.Empty<CBuildingPosition>(),
		new[] { new CBuildingPosition("naval_base", 12f, 8f), new CBuildingPosition("air_base", 8f, 12f) },
		Array.Empty<CBuildingPosition>(),
		Array.Empty<CBuildingPosition>(),
	};

	private static readonly Dictionary<string, string> Texts = new()
	{
		["ARMIES"] = "Land Units",
		["NAVIES"] = "Naval Units",
		["MULTI_SEL_PRIO_DESC"] = "§G$NUM$§! out of §G$MAX$§! selected units have priority for upgrades and reinforcements §YENABLED§!.",
		["MUL_LOAD_ONTO"] = "Load these units onto",
		["NO_MULTILOAD"] = "There is no possible fleet to load these units onto.",
		["TOTAL_LOAD_WILL_BE"] = "The units weigh a total of",
		["UNMOUNT_MULTILOAD"] = "Unmount §G$NUM$§! units from air and naval transports.",
		["UNMOUNT_MULTILOAD_NONE"] = "There are no units to unload from air or naval transports.",
		["REORGANISE_UNITS"] = "Reorganise these two units",
		["NO_REORGANISE_UNITS"] = "You can only reorganise when 2 units are selected.",
	};

	private static string Localise(string key) => Texts.GetValueOrDefault(key, key);

	private static CProvinceEdge Edge(int to) => new(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.FromInt(10));

	private static CGameState State(out CCountry germany, out CCountry poland)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		var kiel = new CProvince(Kiel, "Kiel", isSea: false)
		{
			Owner = Ger, Controller = Ger, PortSea = Bight, SeaIndex = -1, Edges = new[] { Edge(Bight) },
		};
		kiel.SetBuildingLevel(NavalBase, CFixedPoint.FromInt(1));
		return new CGameState(new CDate(1936, 6, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				kiel,
				new CProvince(Bight, "Bight", isSea: true) { SeaIndex = 0, Edges = new[] { Edge(Kiel) } },
				new CProvince(Berlin, "Berlin", isSea: false) { Owner = Ger, Controller = Ger, SeaIndex = -1 },
			},
			new[] { germany, poland }, buildings: new[] { NavalBase });
	}

	private static CUnit Unit(CGameState state, CCountry country, EUnitKind kind, int province, string stat = "x",
		string value = "1", int parts = 1)
	{
		var unit = new CUnit(kind, country.Tag) { Id = ++state.LastUnitId, Location = province, Name = kind + " " + state.LastUnitId };
		var type = new CSubUnitType("part", kind == EUnitKind.Navy ? EUnitBranch.Naval
			: kind == EUnitKind.Air ? EUnitBranch.Air : EUnitBranch.Land);
		type.Stats.Values.Set(stat, CFixedPoint.Parse(value));
		for (int i = 0; i < parts; i++)
		{
			var part = new CSubUnit(type) { Id = ++state.LastUnitId, Unit = unit, Strength = CFixedPoint.FromInt(3), Organisation = CFixedPoint.FromInt(30) };
			part.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(part);
		}
		country.Units.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	private static void Command(CUnit above, CUnit below)
	{
		below.Parent = above;
		above.Children.Add(below);
	}

	[Fact]
	public void ASelectionIsThePlayersOwnUnitsOfOneKindInTheOrderSelected()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit first = Unit(state, germany, EUnitKind.Division, Berlin);
		CUnit corps = Unit(state, germany, EUnitKind.Corps, Berlin);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Bight);
		CUnit foreign = Unit(state, poland, EUnitKind.Division, Berlin);
		var selection = new CSelection();

		Assert.True(selection.Add(first, Ger));
		// Land units of every level are one kind.
		Assert.True(selection.Add(corps, Ger));
		// A fleet never joins land units, another country's unit never joins at all, nor one twice.
		Assert.False(selection.Add(fleet, Ger));
		Assert.False(selection.Add(foreign, Ger));
		Assert.False(selection.Add(first, Ger));
		Assert.Equal(new[] { first, corps }, selection.Units);
		Assert.Same(first, selection.First);
		Assert.Null(selection.Single);

		// Taken out, the next is first and its kind the rule.
		selection.Remove(first);
		selection.Remove(corps);
		Assert.True(selection.Add(fleet, Ger));
		Assert.Same(fleet, selection.Single);
	}

	[Fact]
	public void CtrlBringsTheWholeCommandEachUnitFollowedByItsOwn()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit army = Unit(state, germany, EUnitKind.Army, Berlin);
		CUnit corpsA = Unit(state, germany, EUnitKind.Corps, Berlin);
		CUnit corpsB = Unit(state, germany, EUnitKind.Corps, Berlin);
		CUnit a1 = Unit(state, germany, EUnitKind.Division, Berlin);
		CUnit a2 = Unit(state, germany, EUnitKind.Division, Berlin);
		CUnit b1 = Unit(state, germany, EUnitKind.Division, Berlin);
		Command(army, corpsA);
		Command(army, corpsB);
		Command(corpsA, a1);
		Command(corpsA, a2);
		Command(corpsB, b1);
		var selection = new CSelection();
		selection.Add(army, Ger);
		selection.AddCommand(army);

		// Each unit followed by its own command before its next sibling, as the original walks it.
		Assert.Equal(new[] { army, corpsA, a1, a2, corpsB, b1 }, selection.Units);
	}

	[Fact]
	public void ADragIsTwoMapUnitsOrMore()
	{
		Assert.False(CBoxSelection.IsDrag(10f, 10f, 11.9f, 10f));
		Assert.True(CBoxSelection.IsDrag(10f, 10f, 12f, 10f));        // exactly 2.0 is a drag
		Assert.True(CBoxSelection.IsDrag(10f, 10f, 11.5f, 11.5f));
	}

	[Fact]
	public void ABoxTakesLandUnitsBeforeFleetsAndFleetsBeforeAirUnits()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit division = Unit(state, germany, EUnitKind.Division, Kiel);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Bight);
		CUnit air = Unit(state, germany, EUnitKind.Air, Kiel);
		air.Base = Kiel;
		Unit(state, poland, EUnitKind.Division, Kiel);
		var selection = new CSelection();

		// Over Kiel and the Bight: the division alone.
		Assert.Equal(1, CBoxSelection.Take(state, germany, Points, Placed, 0f, 0f, 30f, 20f, MapWidth, selection));
		Assert.Equal(new[] { division }, selection.Units);

		// Over the Bight alone: the fleet.
		selection.Clear();
		CBoxSelection.Take(state, germany, Points, Placed, 15f, 5f, 25f, 15f, MapWidth, selection);
		Assert.Equal(new[] { fleet }, selection.Units);

		// The air unit at its base stands at the air base, (8, 12): a box short of it by two still takes it - if no
		// land unit is in the box. The division's point is (10, 10), outside.
		selection.Clear();
		CBoxSelection.Take(state, germany, Points, Placed, 2f, 14f, 6.5f, 16f, MapWidth, selection);
		Assert.Equal(new[] { air }, selection.Units);
		selection.Clear();
		CBoxSelection.Take(state, germany, Points, Placed, 2f, 14.5f, 5.5f, 16f, MapWidth, selection);
		Assert.Empty(selection.Units);
	}

	[Fact]
	public void AFleetInPortStandsAtTheNavalBaseAndALandUnitAboardAtSeaIsLeft()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Kiel, CTransport.TransportCapability, "40");
		CUnit aboard = Unit(state, germany, EUnitKind.Division, Bight);
		var selection = new CSelection();

		// The naval base is (12, 8); Kiel's own point (10, 10) is outside this box, which is the port's alone.
		CBoxSelection.Take(state, germany, Points, Placed, 11f, 7f, 13f, 7.5f, MapWidth, selection);
		Assert.Equal(new[] { fleet }, selection.Units);

		// A land unit standing in a sea province is in no walk: the box over the Bight takes nothing of it.
		selection.Clear();
		CBoxSelection.Take(state, germany, Points, Placed, 18f, 8f, 22f, 12f, MapWidth, selection);
		Assert.DoesNotContain(aboard, selection.Units);
	}

	[Fact]
	public void WithUnitsSelectedTheirKindWinsAndTheMapWraps()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Bight);
		CUnit division = Unit(state, germany, EUnitKind.Division, Kiel);
		var selection = new CSelection();
		selection.Add(fleet, Ger);

		// Shift and a box over Kiel: the division may not join the fleet.
		CBoxSelection.Take(state, germany, Points, Placed, 5f, 5f, 15f, 15f, MapWidth, selection);
		Assert.Equal(new[] { fleet }, selection.Units);

		// Ours: a box across the seam, on the copy to the east, takes Kiel's division.
		selection.Clear();
		CBoxSelection.Take(state, germany, Points, Placed, MapWidth + 5f, 5f, MapWidth + 15f, 15f, MapWidth, selection);
		Assert.Equal(new[] { division }, selection.Units);
	}

	[Fact]
	public void ASelectionGroupIsKeptByACommandAndRecalledInItsOrder()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit first = Unit(state, germany, EUnitKind.Division, Berlin);
		CUnit second = Unit(state, germany, EUnitKind.Division, Kiel);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Bight);
		CUnit gone = Unit(state, germany, EUnitKind.Division, Kiel);

		var command = new CSetSelectionGroupCommand(Ger, 3, new[] { second.Id, gone.Id, fleet.Id, first.Id });
		Assert.True(command.MayExecute(state));
		command.Execute(state);
		Assert.False(new CSetSelectionGroupCommand(Ger, 10, new[] { first.Id }).MayExecute(state));
		germany.Units.Remove(gone);

		var selection = new CSelection();
		selection.Add(fleet, Ger);
		CSelectionGroups.Recall(germany, 3, selection);
		// The selection let go; one gone skipped; the fleet not of the first one's kind.
		Assert.Equal(new[] { second, first }, selection.Units);

		CSelectionGroups.Recall(germany, 4, selection);
		Assert.Empty(selection.Units);
	}

	[Fact]
	public void ThePanelsLineIsTheCountAndTheFirstUnitsKind()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit a = Unit(state, germany, EUnitKind.Division, Berlin);
		CUnit b = Unit(state, germany, EUnitKind.Corps, Berlin);
		CUnit c = Unit(state, germany, EUnitKind.Division, Berlin);
		Assert.Equal("3 Land Units", CMultiUnitText.Desc(Localise, new[] { a, b, c }));
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Bight);
		Assert.Equal("2 Naval Units", CMultiUnitText.Desc(Localise, new[] { fleet, fleet }));
	}

	[Fact]
	public void ACheckboxIsTickedWhereAnyUnitHasTheFlag()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit a = Unit(state, germany, EUnitKind.Division, Berlin);
		CUnit b = Unit(state, germany, EUnitKind.Division, Berlin);
		CUnit c = Unit(state, germany, EUnitKind.Division, Berlin);
		b.IsPrioritised = true;
		CUnit[] units = { a, b, c };

		Assert.Equal(1, CMultiUnitText.FlagCount(units, EUnitSupport.Priority));
		Assert.Equal("§G1§! out of §G3§! selected units have priority for upgrades and reinforcements §YENABLED§!.",
			CMultiUnitTooltips.For(Localise, state, units, "prio_checkbox").Text);
		Assert.Equal(3, CMultiUnitText.FlagCount(units, EUnitSupport.Reinforcement));
	}

	[Fact]
	public void TheRowsFitTheScreenAndTheSliderAndTheWheelMoveThem()
	{
		// Their Finest Hour's unitlist_start (0, 144) and unitlist_offset (16, 40) on 1080 lines.
		Assert.Equal(22, CMultiUnitText.RowsThatFit(1080, 144, 40));

		Assert.Equal(0, CMultiUnitText.FirstRow(20, 22, 50f));
		// 30 rows, 22 fit: 8 more, the slider's value out of its 100.
		Assert.Equal(0, CMultiUnitText.FirstRow(30, 22, 12f));
		Assert.Equal(4, CMultiUnitText.FirstRow(30, 22, 50f));
		Assert.Equal(8, CMultiUnitText.FirstRow(30, 22, 100f));

		// A notch of the wheel is a row: 100 / 8 of the slider.
		Assert.Equal(12.5f, CMultiUnitText.Wheel(25f, 1, 30, 22));
		Assert.Equal(1, CMultiUnitText.FirstRow(30, 22, CMultiUnitText.Wheel(25f, 1, 30, 22)));
		Assert.Equal(0f, CMultiUnitText.Wheel(5f, 1, 30, 22));
		Assert.Equal(100f, CMultiUnitText.Wheel(95f, -1, 30, 22));
	}

	[Fact]
	public void LoadFindsAFleetForTheWholeSelectionInOnePort()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit small = Unit(state, germany, EUnitKind.Navy, Kiel, CTransport.TransportCapability, "15");
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Kiel, CTransport.TransportCapability, "20");
		CUnit a = Unit(state, germany, EUnitKind.Division, Kiel, CSubUnitType.TransportWeight, "10");
		CUnit b = Unit(state, germany, EUnitKind.Division, Kiel, CSubUnitType.TransportWeight, "10");
		CUnit[] units = { a, b };

		// The first fleet that takes the whole selection's 20, not the first that takes one unit.
		Assert.Same(fleet, CMultiUnitText.FleetFor(state, units));
		Assert.Equal("Load these units onto §Y" + fleet.Name + "§W", CMultiUnitTooltips.For(Localise, state, units, "load_multiunit").Text);
		Assert.Equal("The units weigh a total of §Y20§W", CMultiUnitTooltips.For(Localise, state, units, "load_multiunit").Delayed);
		Assert.Null(CMultiUnitText.FleetFor(state, new[] { a }));                // two or more

		// One standing elsewhere fails the search.
		b.Location = Berlin;
		Assert.Null(CMultiUnitText.FleetFor(state, units));
		Assert.Equal("There is no possible fleet to load these units onto.",
			CMultiUnitTooltips.For(Localise, state, units, "load_multiunit").Text);

		// Unmount counts the units aboard.
		Assert.Equal("There are no units to unload from air or naval transports.",
			CMultiUnitTooltips.For(Localise, state, units, "unmount_multiunit").Text);
		CTransport.Board(small, a);
		Assert.Equal("Unmount §G1§! units from air and naval transports.",
			CMultiUnitTooltips.For(Localise, state, units, "unmount_multiunit").Text);
		Assert.True(CMultiUnitText.CarryShown(units));
	}

	[Fact]
	public void ReorganiseSaysItIsForTwoUnits()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit[] units = Enumerable.Range(0, 3).Select(_ => Unit(state, germany, EUnitKind.Division, Berlin)).ToArray();
		Assert.Equal("Reorganise these two units", CMultiUnitTooltips.For(Localise, state, units[..2], "reorganizebutton").Text);
		Assert.Equal("You can only reorganise when 2 units are selected.",
			CMultiUnitTooltips.For(Localise, state, units, "reorganizebutton").Text);
		Assert.Equal("", CMultiUnitTooltips.For(Localise, state, units, "close_multiunit").Text);
	}
}
