using System.Collections.Generic;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the deploy menu's lines and the two orders windows say in a tooltip, against
/// <c>docs/mechanics/mapunits.md</c>, section 10, and <c>docs/mechanics/fleetsandair.md</c>,
/// section 8. Kiel has a port onto Kiel Bay; the Baltic is 126 by sea from it.
/// </summary>
public class OrdersTooltipTests
{
	private static readonly CCountryTag Ger = new("GER");

	private const int Kiel = 1;
	private const int KielBay = 2;
	private const int Baltic = 3;

	private static readonly Dictionary<string, string> Texts = new()
	{
		["OV_NO_VALID_ORDER"] = "No valid order is selected.",
		["OV_NO_VALID_PROVINCES"] = "No valid provinces selected",
		["OV_STANCE"] = "This determies how agressively the unit will perform its mission.",
		["OV_PRIO"] = "This determines the priority given to reinforcing this unit.",
		["OV_CONT"] = "Checking the continuous box will start the mission immediately.",
		["AirOrdersBtn_Circle"] = "Targets a circular area.",
		["ORDER_PATROL_DESC"] = "Patrol.",
		["ORDER_REBASE_DESC"] = "Rebase.",
		["ORDER_TRANSPORT_DESC"] = "Transport land units on board the fleet.",
		["OUT_OF_RANGE"] = "Target is out of range. Your fleet can only reach $MYRANGE$ km rather than the required $RANGE$kms",
		["ORDER_DAY_TIME_DESC"] = "Estimated local day time in §Y$PROV$§! when the mission begins is: §Y$TIME$§!",
		["ORDER_DAY"] = "Day",
		["ORDER_NIGHT"] = "Night",
	};

	private static string Localise(string key) => Texts.GetValueOrDefault(key, key);

	private static CProvinceEdge Edge(int to)
	{
		return new CProvinceEdge(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.FromInt(10));
	}

	private static CGameState State(out CCountry germany)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		return new CGameState(new CDate(1936, 1, 1), new[]
		{
			new CProvince(0, "", isSea: false),
			new CProvince(Kiel, "Kiel", isSea: false)
			{
				Owner = Ger, Controller = Ger, CentreX = 10, CentreY = 10, PortSea = KielBay, SeaIndex = 0,
				Edges = new[] { Edge(KielBay), Edge(Baltic) },
			},
			new CProvince(KielBay, "Kiel Bay", isSea: true) { CentreX = 10, CentreY = 20, SeaIndex = 1, Edges = new[] { Edge(Kiel), Edge(Baltic) } },
			new CProvince(Baltic, "Baltic", isSea: true) { CentreX = 40, CentreY = 20, SeaIndex = 2, Edges = new[] { Edge(KielBay), Edge(Kiel) } },
		}, new[] { germany })
		{
			MapWidth = 1000,
		};
	}

	private static CUnit Unit(CCountry country, EUnitKind kind, string range, params string[] types)
	{
		var unit = new CUnit(kind, country.Tag) { Id = country.Units.Count + 1, Location = Kiel, Base = Kiel, Name = "Unit" };
		foreach (string key in types)
		{
			var type = new CSubUnitType(key, kind switch
			{
				EUnitKind.Navy => EUnitBranch.Naval,
				EUnitKind.Air => EUnitBranch.Air,
				_ => EUnitBranch.Land,
			});
			type.Stats.Values.Set(CUnitOrders.Range, CFixedPoint.Parse(range));
			var subUnit = new CSubUnit(type) { Unit = unit, Name = key + " " + unit.SubUnits.Count };
			subUnit.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(subUnit);
		}
		country.Units.Add(unit);
		return unit;
	}

	// ---- the deploy menu

	[Fact]
	public void AFinishedLandUnitsLineListsItsBrigadesAndAFleetsSaysNothing()
	{
		State(out CCountry germany);
		CUnit division = Unit(germany, EUnitKind.Division, "0", "infantry_brigade", "artillery_brigade");
		CUnit fleet = Unit(germany, EUnitKind.Navy, "1000", "destroyer");

		Assert.Equal("£infantry_brigade infantry_brigade 0\n£artillery_brigade artillery_brigade 1\n",
			CDeployMenuTooltips.Entry(Localise, division).Text);
		Assert.Equal("", CDeployMenuTooltips.Entry(Localise, fleet).Text);
		// A unit the new one may join is listed whatever it is.
		Assert.Equal("£destroyer destroyer 0\n", CDeployMenuTooltips.Target(Localise, fleet).Text);
	}

	// ---- the fleet's window

	[Fact]
	public void AcceptSaysWhyOnlyWhileItCannotBePressed()
	{
		CGameState state = State(out CCountry germany);
		CUnit fleet = Unit(germany, EUnitKind.Navy, "1000", "destroyer");
		CProvince baltic = state.Province(Baltic);

		Assert.Equal("No valid order is selected.",
			COrdersTooltips.Naval(Localise, state, fleet, baltic, true, EUnitOrder.None, "accept").Text);
		Assert.Equal("", COrdersTooltips.Naval(Localise, state, fleet, baltic, false, EUnitOrder.Patrol, "accept").Text);
	}

	[Fact]
	public void ThePriorityButtonsSayNothingAndTheRestOfTheStanceAndPrioritySayTheirLine()
	{
		CGameState state = State(out CCountry germany);
		CUnit fleet = Unit(germany, EUnitKind.Navy, "1000", "destroyer");
		CProvince baltic = state.Province(Baltic);

		Assert.StartsWith("This determies", COrdersTooltips.Naval(Localise, state, fleet, baltic, false, EUnitOrder.None, "increase_stance_button").Text);
		Assert.StartsWith("This determines", COrdersTooltips.Naval(Localise, state, fleet, baltic, false, EUnitOrder.None, "priority_value").Text);
		Assert.Equal("", COrdersTooltips.Naval(Localise, state, fleet, baltic, false, EUnitOrder.None, "increase_priority_button").Text);
		Assert.Equal("", COrdersTooltips.Naval(Localise, state, fleet, baltic, false, EUnitOrder.None, "decrease_priority_button").Text);
	}

	[Fact]
	public void ARowOutOfTheFleetsReachSaysHowFarItReachesAndHowFarItWouldHaveTo()
	{
		CGameState state = State(out CCountry germany);
		CUnit fleet = Unit(germany, EUnitKind.Navy, "100", "destroyer");
		CProvince baltic = state.Province(Baltic);

		CTooltip patrol = COrdersTooltips.Row(Localise, state, fleet, baltic, new COrderRow("ORDER_PATROL", EUnitOrder.Patrol, false));
		Assert.Equal("Patrol.", patrol.Text);
		Assert.Equal("Target is out of range. Your fleet can only reach 100.0 km rather than the required 126kms", patrol.Delayed);

		// A change of base is never out of reach; a row of an order not built has its own description.
		Assert.Equal("", COrdersTooltips.Row(Localise, state, fleet, baltic, new COrderRow("ORDER_REBASE", EUnitOrder.Rebase, true)).Delayed);
		Assert.Equal("Transport land units on board the fleet.",
			COrdersTooltips.Row(Localise, state, fleet, baltic, new COrderRow("ORDER_TRANSPORT", EUnitOrder.None, false)).Text);
	}

	// ---- the air window

	[Fact]
	public void TheAirWindowsAcceptShapesContinuousBoxAndDayReadout()
	{
		CGameState state = State(out _);
		CProvince kiel = state.Province(Kiel);
		long noon = state.TotalHours + 12;

		Assert.Equal("No valid order is selected.", COrdersTooltips.Air(Localise, state, kiel, true, false, false, noon, "accept").Text);
		Assert.Equal("No valid provinces selected", COrdersTooltips.Air(Localise, state, kiel, true, true, false, noon, "accept").Text);
		Assert.Equal("", COrdersTooltips.Air(Localise, state, kiel, true, true, true, noon, "accept").Text);
		Assert.Equal("Targets a circular area.", COrdersTooltips.Air(Localise, state, kiel, false, true, true, noon, "circle_target_button").Text);
		Assert.StartsWith("Checking", COrdersTooltips.Air(Localise, state, kiel, false, true, true, noon, "continous_checkbox").Text);
		Assert.Equal("", COrdersTooltips.Air(Localise, state, kiel, false, true, true, noon, "decrease_priority_button").Text);

		string day = CAirOrdersText.IsNightAtStart(state, kiel, noon) ? "Night" : "Day";
		Assert.Equal("Estimated local day time in §YKiel§! when the mission begins is: §Y" + day + "§!",
			COrdersTooltips.Air(Localise, state, kiel, false, true, true, noon, "daytime_icon").Text);
	}
}
