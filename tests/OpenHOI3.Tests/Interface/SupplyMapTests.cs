using System.Collections.Generic;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The supply map mode, against <c>docs/mechanics/supplymap.md</c>: Berlin, a province
/// beside it and one beyond that, all Germany's, with Warsaw beside the last.
/// </summary>
public class SupplyMapTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private static readonly CBuilding Infra = new(0, "infra", 10, CSupplyFigures.Infrastructure, CFixedPoint.Parse("0.1"));
	private static readonly CBuilding NavalBase = new(1, CSupplyNetworks.NavalBase, 10, "", CFixedPoint.Zero);
	private static readonly CBuilding Industry = new(2, CSupplyNetworks.Industry, 10, "", CFixedPoint.Zero);

	private readonly List<CProvince> _provinces = new() { new CProvince(0, "", isSea: false) };
	private readonly Dictionary<int, List<CProvinceEdge>> _edges = new();

	private int Land(CCountryTag holder, string name, int infrastructure = 10, int port = 0, int onto = 0)
	{
		int id = _provinces.Count;
		var province = new CProvince(id, name, isSea: false)
		{
			Owner = holder,
			Controller = holder,
			Span = 10,
			PortSea = onto,
			SeaIndex = port > 0 ? 1 : -1,
		};
		province.SetBuildingLevel(Infra, CFixedPoint.FromInt(infrastructure));
		province.SetBuildingLevel(NavalBase, CFixedPoint.FromInt(port));
		province.SetBuildingLevel(Industry, CFixedPoint.FromInt(id == 1 ? 5 : 0));
		province.Modifier.Set(CSupplyFigures.Infrastructure, CFixedPoint.Parse("0.1") * CFixedPoint.FromInt(infrastructure));
		province.Cores.Add(holder);
		_provinces.Add(province);
		_edges[id] = new List<CProvinceEdge>();
		return id;
	}

	private void Join(int a, int b)
	{
		_edges[a].Add(new CProvinceEdge(b, EProvinceEdgeKind.Plain, 0, CFixedPoint.One));
		_edges[b].Add(new CProvinceEdge(a, EProvinceEdgeKind.Plain, 0, CFixedPoint.One));
	}

	private (CGameState State, int Berlin, int Near, int Far, int Warsaw) World(int farInfrastructure = 10)
	{
		int berlin = Land(Ger, "Berlin");
		int near = Land(Ger, "Near");
		int far = Land(Ger, "Far", farInfrastructure);
		int warsaw = Land(Pol, "Warsaw");
		Join(berlin, near);
		Join(near, far);
		Join(far, warsaw);
		return (State(berlin, warsaw), berlin, near, far, warsaw);
	}

	private CGameState State(int berlin, int warsaw)
	{
		foreach (CProvince province in _provinces)
		{
			if (_edges.TryGetValue(province.Id, out List<CProvinceEdge>? edges))
			{
				province.Edges = edges;
			}
		}
		var defines = new CDefines();
		defines.Set(CSupplyFigures.Military, CSupplyFigures.ThroughputImpact, CFixedPoint.Parse("4"));
		defines.Set(CSupplyFigures.Military, CSupplyFigures.OwnedThroughputBonus, CFixedPoint.Parse("2"));
		defines.Set(CSupplyFigures.Military, CSupplyFigures.SupplyTax, CFixedPoint.Parse("0.1"));
		defines.Set(CSupplyFigures.Military, CSupplyDay.PoolDays, CFixedPoint.FromInt(15));
		defines.Set(CSupplyFigures.Military, CBaseCrowding.NavalBaseEfficiency, CFixedPoint.FromInt(6));
		var countries = new[]
		{
			new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = berlin },
			new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "", Capital = warsaw },
		};
		return new CGameState(new CDate(1936, 1, 1), _provinces, countries, new[] { Infra, NavalBase, Industry },
			defines: defines);
	}

	private static void Set(CProvince province, string lying, string asked, string layThere = "0", string passed = "0",
		string need = "0")
	{
		province.Pool[EGoods.Supplies] = CFixedPoint.Parse(lying);
		province.SupplyDrawn[EGoods.Supplies] = CFixedPoint.Parse(asked);
		province.LastPool[EGoods.Supplies] = CFixedPoint.Parse(layThere);
		province.SupplyThroughput[EGoods.Supplies] = CFixedPoint.Parse(passed);
		province.SupplyNeed[EGoods.Supplies] = CFixedPoint.Parse(need);
	}

	private static readonly CColor Grey = new(0.4f, 0.4f, 0.4f);

	// --- the colours ---

	[Fact]
	public void Land_the_player_does_not_hold_is_a_dim_grey_whatever_lies_there()
	{
		var world = World();
		CProvince warsaw = world.State.Province(world.Warsaw);
		Set(warsaw, lying: "10", asked: "5");

		(CColor ground, CColor stripes) = CSupplyMapColours.Of(world.State, warsaw, Ger);

		Assert.Equal(new CColor(0.2f, 0.2f, 0.2f), ground);
		Assert.Equal(ground, stripes);
	}

	[Fact]
	public void Land_with_under_two_levels_of_infrastructure_is_black()
	{
		var world = World(farInfrastructure: 1);

		(CColor ground, CColor stripes) = CSupplyMapColours.Of(world.State, world.State.Province(world.Far), Ger);

		Assert.Equal(new CColor(0f, 0f, 0f), ground);
		Assert.Equal(ground, stripes);
	}

	[Fact]
	public void A_province_holding_what_was_asked_of_it_is_green()
	{
		var world = World();
		CProvince far = world.State.Province(world.Far);
		Set(far, lying: "10", asked: "5");

		Assert.Equal(new CColor(0.4f, 0.8f, 0.4f), CSupplyMapColours.Of(world.State, far, Ger).Ground);
	}

	[Fact]
	public void A_province_with_nothing_there_and_nothing_coming_whose_units_need_some_is_red()
	{
		var world = World();
		CProvince far = world.State.Province(world.Far);
		Set(far, lying: "0", asked: "5", need: "1");

		Assert.Equal(new CColor(0.6f, 0.4f, 0.4f), CSupplyMapColours.Of(world.State, far, Ger).Ground);
	}

	[Fact]
	public void A_province_behind_but_being_supplied_is_yellow_and_brighter_where_its_roads_are_down()
	{
		var world = World();
		CProvince far = world.State.Province(world.Far);
		Set(far, lying: "2", asked: "5", need: "1");
		Assert.Equal(new CColor(0.5f, 0.5f, 0.4f), CSupplyMapColours.Of(world.State, far, Ger).Ground);

		// Nothing lying there, but something came through today: still yellow, not red.
		Set(far, lying: "0", asked: "5", passed: "1", need: "1");
		Assert.Equal(new CColor(0.5f, 0.5f, 0.4f), CSupplyMapColours.Of(world.State, far, Ger).Ground);

		far.Modifier.Set(CSupplyFigures.LocalInfrastructure, CFixedPoint.Parse("-0.5"));
		Assert.Equal(new CColor(0.8f, 0.8f, 0.4f), CSupplyMapColours.Of(world.State, far, Ger).Ground);
	}

	[Fact]
	public void A_province_with_supplies_nobody_asked_for_is_blue_green_and_one_with_none_is_grey()
	{
		var world = World();
		CProvince far = world.State.Province(world.Far);
		Set(far, lying: "10", asked: "0");
		Assert.Equal(new CColor(0.4f, 0.6f, 0.6f), CSupplyMapColours.Of(world.State, far, Ger).Ground);

		Set(far, lying: "0", asked: "0");
		Assert.Equal(Grey, CSupplyMapColours.Of(world.State, far, Ger).Ground);
	}

	[Fact]
	public void A_province_asked_for_more_than_it_can_pass_is_striped_red()
	{
		var world = World();
		CProvince far = world.State.Province(world.Far);
		// Ten levels on its owner's ground pass 800 a day.
		Set(far, lying: "1000", asked: "800");
		(CColor ground, CColor stripes) = CSupplyMapColours.Of(world.State, far, Ger);
		Assert.Equal(ground, stripes);

		Set(far, lying: "1000", asked: "801");
		(ground, stripes) = CSupplyMapColours.Of(world.State, far, Ger);
		Assert.Equal(new CColor(0.4f, 0.8f, 0.4f), ground);
		Assert.Equal(new CColor(0.7f, 0.4f, 0.4f), stripes);
	}

	// --- the lines ---

	/// <summary>Each key in brackets, and the templates with their places kept.</summary>
	private static string Localise(string key) => key switch
	{
		"SUPPLY_INFRA_EFFECT_INFO" => "[INFRA $INFRA$/$MAX$ $THRU$]",
		"SUPPLY_INFRA_CAPITAL_EFFECT_INFO" => "[CAPITAL $THRU$]",
		"CONV_SHIP_IRO" => "[SHIP $VALUE$ $NAME$]",
		_ => $"[{key}]",
	};

	[Fact]
	public void A_provinces_text_says_what_its_roads_pass_where_it_is_supplied_from_and_what_the_day_made_of_it()
	{
		var world = World();
		CProvince far = world.State.Province(world.Far);
		Set(far, lying: "2.5", asked: "5", passed: "1.25", need: "3");
		far.Pool[EGoods.Fuel] = CFixedPoint.Parse("7");
		CSupplyNetwork network = CSupplyNetworks.Refresh(world.State);

		string text = CSupplyMapText.Text(Localise, world.State, network, far, Ger);

		Assert.Equal(
			"\n[INFRA §G10.00§!/10.00 800]\n"
			+ "\n[LOGTT_1]§YBerlin§W (2 [LOGTT_2])"
			+ "\n[LOGTT_3]§R2.50§W[LOGTT_4]§G7.00§W"
			+ "\n[LOGTT_5]§Y5.00§W[LOGTT_6]§Y0.00§W"
			+ "\n[LOGTT_7]§Y1.25§W[LOGTT_6]§Y0.00§W", text);
	}

	[Fact]
	public void Asked_for_less_than_passed_is_red_and_passed_at_the_most_it_can_is_red()
	{
		var world = World();
		CProvince far = world.State.Province(world.Far);
		Set(far, lying: "1", asked: "1", passed: "800");

		string text = CSupplyMapText.Text(Localise, world.State, CSupplyNetworks.Refresh(world.State), far, Ger);

		Assert.Contains("\n[LOGTT_5]§R1.00§W", text);
		Assert.Contains("\n[LOGTT_7]§R800.00§W", text);
	}

	[Fact]
	public void Infrastructure_below_its_most_is_red_and_beside_the_capital_it_passes_without_limit()
	{
		var world = World();
		CProvince far = world.State.Province(world.Far);
		far.Modifier.Set(CSupplyFigures.Infrastructure, CFixedPoint.Parse("0.5"));
		CSupplyNetwork network = CSupplyNetworks.Refresh(world.State);

		Assert.StartsWith("\n[INFRA §R5.00§!/10.00 ", CSupplyMapText.Text(Localise, world.State, network, far, Ger));
		Assert.StartsWith("\n[CAPITAL [VALUE_MAX]]\n",
			CSupplyMapText.Text(Localise, world.State, network, world.State.Province(world.Near), Ger));
	}

	[Fact]
	public void Of_a_province_the_player_knows_nothing_of_nothing_is_told_and_of_a_roadless_one_only_that()
	{
		var world = World();
		CSupplyNetwork network = CSupplyNetworks.Refresh(world.State);
		Assert.Equal("", CSupplyMapText.Text(Localise, world.State, network, world.State.Province(world.Berlin), Pol));

		_provinces.Clear();
		_provinces.Add(new CProvince(0, "", isSea: false));
		_edges.Clear();
		var roadless = World(farInfrastructure: 1);
		Assert.Equal("\n[TOOLOWINFRA]", CSupplyMapText.Text(Localise, roadless.State,
			CSupplyNetworks.Refresh(roadless.State), roadless.State.Province(roadless.Far), Ger));
	}

	[Fact]
	public void On_the_sea_the_players_convoys_that_go_by_are_named_with_what_each_ships()
	{
		var world = World();
		var sea = new CProvince(99, "Baltic", isSea: true);
		CCountry germany = world.State.Country(Ger)!;
		var convoy = new CConvoy { Id = 1, Owner = Ger, Start = world.Berlin, End = world.Far };
		convoy.Path.AddRange(new[] { world.Berlin, 99, world.Far });
		convoy.Daily[EGoods.Fuel] = CFixedPoint.Parse("12.5");
		germany.Convoys.Add(convoy);

		string text = CSupplyMapText.Text(Localise, world.State, CSupplyNetworks.Refresh(world.State), sea, Ger);

		Assert.Equal("\n[LOGTT_8]Berlin[LOGTT_9]Far[LOGTT_10]\n   [SHIP 12.5 [FUEL_IRO]]", text);
		Assert.Equal("", CSupplyMapText.Text(Localise, world.State, CSupplyNetworks.Refresh(world.State), sea, Pol));
	}
}
