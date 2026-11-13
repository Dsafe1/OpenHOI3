using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the screen shows of the carrier and carrying by air, against <c>docs/mechanics/transport.md</c>, section 13.6, on
/// a port with an airfield and its sea:
///
///     1 Kiel ==port== 2 Bight
/// </summary>
public class AirTransportTextTests
{
	private static readonly CCountryTag Ger = new("GER");

	private const int Kiel = 1;
	private const int Bight = 2;

	private static readonly CBuilding NavalBase = new(0, "naval_base", 10, CBombingOrders.NavalBaseLevel, CFixedPoint.One);
	private static readonly CBuilding AirBase = new(1, "air_base", 10, CBombingOrders.AirBaseLevel, CFixedPoint.One);

	private static readonly Dictionary<string, string> Texts = new()
	{
		["PARADROP_MISSION_SHORT_DESC"] = "Airborne Assault ($TYPE$}",
		["REBASE_TO_CAG_MISSION_SHORT_DESC"] = "Rebase to Carrier ($TYPE$)",
		["TRANS_SUP_MISSION_SHORT_DESC"] = "Air Supply ($TYPE$)",
		["CAG_DUTY_SHORT_DESC"] = "CAG Duty ($TYPE$)",
		["STRAT_BOMB_WAITING"] = "Resting",
		["STRAT_BOMB_RETURNING"] = "Returning",
		["PARA_FLYING"] = "Flying",
		["CAG_FIGHTING"] = "Fighting",
		["TS_WAITING"] = "Waiting",
		["TS_RETURNING"] = "Returning",
		["TS_SUPPLYING"] = "Flying",
	};

	private static string Localise(string key) => Texts.GetValueOrDefault(key, key);

	private static CProvinceEdge Edge(int to) => new(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.FromInt(10));

	private static CGameState State(out CCountry germany)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var kiel = new CProvince(Kiel, "Kiel", isSea: false)
		{
			Owner = Ger, Controller = Ger, PortSea = Bight, SeaIndex = -1, Edges = new[] { Edge(Bight) },
		};
		kiel.SetBuildingLevel(NavalBase, CFixedPoint.FromInt(1));
		kiel.SetBuildingLevel(AirBase, CFixedPoint.FromInt(2));
		var defines = new CDefines();
		void Military(string name, string value) => defines.Set(CManpower.MilitarySection, name, CFixedPoint.Parse(value));
		Military(CAirTransport.OrganisationRequired, "0.95");
		Military("DEFENSIVE_STRENGTH_LIMIT", "0.5");
		Military("DEFENSIVE_ORGANISATION_LIMIT", "0.3");
		return new CGameState(new CDate(1936, 6, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				kiel,
				new CProvince(Bight, "Bight", isSea: true) { SeaIndex = 0, Edges = new[] { Edge(Kiel) } },
			},
			new[] { germany }, buildings: new[] { NavalBase, AirBase }, defines: defines);
	}

	private static CUnit Unit(CGameState state, CCountry country, EUnitKind kind, int province, int parts, bool flag,
		params (string Name, string Value)[] stats)
	{
		var unit = new CUnit(kind, country.Tag) { Id = ++state.LastUnitId, Location = province, Name = kind.ToString() };
		EUnitBranch branch = kind switch
		{
			EUnitKind.Navy => EUnitBranch.Naval,
			EUnitKind.Air => EUnitBranch.Air,
			_ => EUnitBranch.Land,
		};
		var type = new CSubUnitType("part", branch);
		type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.FromInt(30));
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(3));
		foreach ((string name, string value) in stats)
		{
			type.Stats.Values.Set(name, CFixedPoint.Parse(value));
		}
		type.Flags[CAirTransport.CanParadrop] = flag;
		type.Flags[CAirFire.IsCag] = flag;
		for (int i = 0; i < parts; i++)
		{
			var part = new CSubUnit(type)
			{
				Id = ++state.LastUnitId, Unit = unit, Strength = CFixedPoint.FromInt(3), Organisation = CFixedPoint.FromInt(30),
			};
			part.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(part);
		}
		country.Units.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	private static CUnit Plane(CGameState state, CCountry germany) =>
		Unit(state, germany, EUnitKind.Air, Kiel, 1, false, (CTransport.TransportCapability, "15"));

	private static CUnit Paratroops(CGameState state, CCountry germany) =>
		Unit(state, germany, EUnitKind.Division, Kiel, 3, true, (CSubUnitType.TransportWeight, "5"));

	[Fact]
	public void ThePlanesLoadButtonFindsAnAirUnitAtItsBaseHereAndItsUnloadWantsTheBase()
	{
		CGameState state = State(out CCountry germany);
		CUnit plane = Plane(state, germany);
		plane.Base = Kiel;
		CUnit division = Paratroops(state, germany);

		CCarryButtons buttons = CTransportText.Buttons(state, germany, division);
		Assert.True(buttons.PlaneLoadShown && buttons.PlaneLoadEnabled && !buttons.PlaneUnloadShown);
		Assert.Equal(plane.Id, buttons.Plane);
		Assert.Equal("UW_LOAD_PLANE", CUnitOrderTooltips.Buttons(Localise, division, false, "load_plane_button", buttons).Text);

		division.SubUnits[0].Organisation = CFixedPoint.FromInt(20);
		buttons = CTransportText.Buttons(state, germany, division);
		Assert.False(buttons.PlaneLoadEnabled);
		Assert.Equal("UW_LOAD_PLANE_FAIL_LOW_ORG",
			CUnitOrderTooltips.Buttons(Localise, division, false, "load_plane_button", buttons).Text);
		division.SubUnits[0].Organisation = CFixedPoint.FromInt(30);

		CAirTransport.Board(plane, division);
		buttons = CTransportText.Buttons(state, germany, division);
		Assert.True(!buttons.PlaneLoadShown && !buttons.LoadShown && buttons.PlaneUnloadShown && buttons.PlaneUnloadEnabled);
		Assert.Equal("UW_UNLOAD_PLANE_BASE_OK",
			CUnitOrderTooltips.Buttons(Localise, division, false, "unload_plane_button", buttons).Text);

		plane.Location = Bight;
		division.Location = Bight;
		buttons = CTransportText.Buttons(state, germany, division);
		Assert.False(buttons.PlaneUnloadEnabled);
		Assert.Equal("UW_UNLOAD_PLANE_BASE_ONLY",
			CUnitOrderTooltips.Buttons(Localise, division, false, "unload_plane_button", buttons).Text);
		// A passenger aboard an air unit stands among the air units on the map.
		Assert.Equal(EMapStack.Air, CMapCounters.StackOf(state, division));
	}

	[Fact]
	public void TheAirWindowListsCarrierProtectionFirstAboardACarrierHereThenTheParadropAndTheRebaseToCarrier()
	{
		CGameState state = State(out CCountry germany);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Kiel, 1, false, (CShipCensus.CarrierSize, "2"));
		CUnit group = Unit(state, germany, EUnitKind.Air, Kiel, 1, true, (CAirMissions.AirAttack, "2"));
		CCarriers.BaseOn(state, group, fleet.SubUnits[0]);

		List<COrderRow> rows = COrdersText.Rows(state, germany, group, state.Province(Kiel));
		Assert.Equal(new[] { "CAG_DUTY", "AIRSUP", "INTERCEPT", "REBASE_TO_CAG_MISSION", "ORDER_AIR_RESERVE", "ORDER_AIR_REBASE" },
			rows.Select(row => row.NameKey));
		Assert.True(rows[0].MayGive);

		CUnit plane = Plane(state, germany);
		plane.Base = Kiel;
		CAirTransport.Board(plane, Paratroops(state, germany));
		rows = COrdersText.Rows(state, germany, plane, state.Province(Kiel));
		Assert.Equal(new[] { "PARADROP_MISSION", "REBASE_TO_CAG_MISSION", "TRANS_SUP_MISSION", "ORDER_AIR_RESERVE", "ORDER_AIR_REBASE" },
			rows.Select(row => row.NameKey));
		Assert.True(rows[0].MayGive);
		Assert.False(rows[1].MayGive);          // a transport plane is no carrier air group
		Assert.True(rows[2].MayGive);
		Assert.Equal("PARADROP_MISSION_DESC", CUnitOrderTooltips.DescriptionKey(EUnitOrder.Paradrop));
	}

	[Fact]
	public void ThePanelsOrderLineSaysWhatEachOfTheFourOrdersIsDoing()
	{
		CGameState state = State(out CCountry germany);
		CUnit plane = Plane(state, germany);
		plane.Base = Kiel;
		plane.Order = EUnitOrder.AirSupply;
		plane.OrderProvince = Kiel;
		Assert.Equal("Air Supply (Waiting)", CUnitPanelText.Order(Localise, state, plane));
		plane.Mission.ReturnToBase = true;
		Assert.Equal("Air Supply (Returning)", CUnitPanelText.Order(Localise, state, plane));

		plane.Mission = new CMission { StartHour = state.TotalHours + 24 };
		plane.Order = EUnitOrder.Paradrop;
		Assert.Equal("Airborne Assault (Resting}", CUnitPanelText.Order(Localise, state, plane));

		plane.Order = EUnitOrder.RebaseCarrier;
		plane.Location = Bight;
		Assert.Equal("Rebase to Carrier (Flying)", CUnitPanelText.Order(Localise, state, plane));
		plane.Order = EUnitOrder.CarrierProtection;
		Assert.Equal("CAG Duty (Flying)", CUnitPanelText.Order(Localise, state, plane));
	}
}
