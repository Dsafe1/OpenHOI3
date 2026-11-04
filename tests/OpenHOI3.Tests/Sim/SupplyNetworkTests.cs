using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Who draws supplies from where, against <c>docs/mechanics/supply.md</c>, sections 2 to
/// 8. Each test lays out a few provinces of its own. The defines are the base game's.
/// </summary>
public class SupplyNetworkTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Ita = new("ITA");
	private static readonly CCountryTag Man = new("MAN");
	private static readonly CCountryTag Pol = new("POL");

	private static readonly CBuilding Infra = new(0, "infra", 10, CSupplyFigures.Infrastructure, CFixedPoint.Parse("0.1"));
	private static readonly CBuilding NavalBase = new(1, CSupplyNetworks.NavalBase, 10, "", CFixedPoint.Zero);
	private static readonly CBuilding Industry = new(2, CSupplyNetworks.Industry, 10, "", CFixedPoint.Zero);

	private readonly List<CProvince> _provinces = new() { new CProvince(0, "", isSea: false) };
	private readonly Dictionary<int, List<CProvinceEdge>> _edges = new();
	private readonly List<CCountry> _countries = new();
	private readonly List<CWar> _wars = new();

	private CCountry Country(CCountryTag tag, int capital, CCountryTag? overlord = null, string faction = "")
	{
		var country = new CCountry { Tag = tag, Name = tag.Value, DefinitionFile = "", Capital = capital, Overlord = overlord, Faction = faction };
		_countries.Add(country);
		return country;
	}

	/// <summary>A land province, numbered in the order they are made.</summary>
	private int Land(CCountryTag? holder, int infra = 10, int port = 0, int industry = 0, int span = 10,
		CCountryTag? owner = null)
	{
		int id = _provinces.Count;
		var province = new CProvince(id, $"P{id}", isSea: false)
		{
			Owner = owner ?? holder,
			Controller = holder,
			Span = span,
			// A sea to open onto, which is what makes a naval base a port.
			PortSea = port > 0 ? 9999 : 0,
		};
		province.SetBuildingLevel(Infra, CFixedPoint.FromInt(infra));
		province.SetBuildingLevel(NavalBase, CFixedPoint.FromInt(port));
		province.SetBuildingLevel(Industry, CFixedPoint.FromInt(industry));
		province.Modifier.Set(CSupplyFigures.Infrastructure, new CFixedPoint(infra * 100));
		_provinces.Add(province);
		_edges[id] = new List<CProvinceEdge>();
		return id;
	}

	private void Join(params int[] chain)
	{
		for (int at = 0; at + 1 < chain.Length; at++)
		{
			_edges[chain[at]].Add(new CProvinceEdge(chain[at + 1], EProvinceEdgeKind.Plain, 0, CFixedPoint.One));
			_edges[chain[at + 1]].Add(new CProvinceEdge(chain[at], EProvinceEdgeKind.Plain, 0, CFixedPoint.One));
		}
	}

	private CGameState State()
	{
		foreach (CProvince province in _provinces)
		{
			if (_edges.TryGetValue(province.Id, out List<CProvinceEdge>? edges))
			{
				province.Edges = edges.ToArray();
			}
		}
		var defines = new CDefines();
		defines.Set(CSupplyFigures.Military, CSupplyFigures.ThroughputImpact, CFixedPoint.Parse("4"));
		defines.Set(CSupplyFigures.Military, CSupplyFigures.OwnedThroughputBonus, CFixedPoint.Parse("2"));
		defines.Set(CSupplyFigures.Military, CSupplyFigures.SupplyTax, CFixedPoint.Parse("0.1"));
		defines.Set(CSupplyFigures.Military, CSupplyFigures.PartisanTax, CFixedPoint.Parse("0.1"));
		defines.Set(CSupplyFigures.WeatherSection, CSupplyFigures.MudTax, CFixedPoint.Parse("0.25"));
		return new CGameState(new CDate(1936, 1, 1), _provinces, _countries,
			new[] { Infra, NavalBase, Industry }, defines: defines, wars: _wars);
	}

	// --- areas and who is in none ---

	[Fact]
	public void Land_with_industry_around_the_capital_draws_on_the_capital()
	{
		int berlin = Land(Ger, industry: 5);
		int potsdam = Land(Ger);
		int stettin = Land(Ger);
		Join(berlin, potsdam, stettin);
		Country(Ger, berlin);

		CSupplyNetwork network = CSupplyNetworks.Build(State());

		Assert.Equal(berlin, network.DepotOf(berlin));
		Assert.Equal(berlin, network.DepotOf(potsdam));
		Assert.Equal(berlin, network.DepotOf(stettin));
		Assert.True(network.IsDepot(berlin));
		Assert.False(network.IsDepot(potsdam));
		Assert.Single(network.Areas);
		Assert.Equal(new[] { berlin, potsdam, stettin }, network.Areas[0].Provinces);
	}

	[Fact]
	public void A_province_with_one_level_of_infrastructure_or_less_is_in_no_area()
	{
		int berlin = Land(Ger, industry: 5);
		int marsh = Land(Ger, infra: 1);
		int beyond = Land(Ger, infra: 2);
		Join(berlin, marsh, beyond);
		Country(Ger, berlin);

		CSupplyNetwork network = CSupplyNetworks.Build(State());

		Assert.Null(network.AreaOf(marsh));
		Assert.Equal(0, network.DepotOf(marsh));
		// And what lies behind it is cut off from the capital: an area of its own, with
		// no port to be supplied through.
		Assert.NotSame(network.AreaOf(berlin), network.AreaOf(beyond));
		Assert.Equal(0, network.DepotOf(beyond));
	}

	[Fact]
	public void Land_nobody_holds_draws_on_nothing()
	{
		int berlin = Land(Ger, industry: 5);
		int empty = Land(null);
		Join(berlin, empty);
		Country(Ger, berlin);

		CSupplyNetwork network = CSupplyNetworks.Build(State());

		Assert.Null(network.AreaOf(empty));
		Assert.Equal(0, network.DepotOf(empty));
		Assert.Equal(0, network.DistanceOf(empty));
	}

	[Fact]
	public void The_capitals_land_draws_on_the_capital_even_with_no_industry_and_no_port()
	{
		int capital = Land(Ger);
		int other = Land(Ger);
		Join(capital, other);
		Country(Ger, capital);

		CSupplyNetwork network = CSupplyNetworks.Build(State());

		Assert.False(network.Areas[0].MayHoldDepot);
		Assert.Equal(capital, network.DepotOf(other));
	}

	[Fact]
	public void The_area_around_a_province_is_what_its_holder_controls_as_far_as_the_roads_go()
	{
		int berlin = Land(Ger);
		int near = Land(Ger);
		int marsh = Land(Ger, infra: 1);
		int beyond = Land(Ger);
		int taken = Land(Ger, owner: Pol);
		int posen = Land(Pol);
		Join(berlin, near, marsh, beyond);
		Join(near, taken, posen);
		Country(Ger, berlin);
		Country(Pol, posen);

		CGameState state = State();
		List<int> area = CSupplyNetworks.AreaAround(state, berlin);
		area.Sort();

		// Land taken from another counts, being held; land behind a marsh does not.
		Assert.Equal(new[] { berlin, near, taken }, area);
		Assert.Empty(CSupplyNetworks.AreaAround(state, marsh));
		Assert.Equal(new[] { beyond }, CSupplyNetworks.AreaAround(state, beyond));
		// It is the same cut the whole network makes.
		CSupplyNetwork network = CSupplyNetworks.Build(state);
		List<int> whole = new(network.AreaOf(berlin)!.Provinces);
		whole.Sort();
		Assert.Equal(area, whole);
	}

	// --- land away from the capital ---

	[Fact]
	public void Land_across_the_sea_with_a_port_has_a_depot_of_its_own()
	{
		int roma = Land(Ita, industry: 5);
		int inland = Land(Ita, infra: 4);
		int tripoli = Land(Ita, infra: 4, port: 3);
		Join(inland, tripoli);
		Country(Ita, roma);

		CSupplyNetwork network = CSupplyNetworks.Build(State());

		Assert.Equal(roma, network.DepotOf(roma));
		Assert.Equal(tripoli, network.DepotOf(tripoli));
		Assert.Equal(tripoli, network.DepotOf(inland));
		Assert.False(network.AreaOf(tripoli)!.IsLandlocked);
		Assert.True(network.AreaOf(tripoli)!.MayHoldDepot);
	}

	[Fact]
	public void Land_across_the_sea_with_no_port_draws_on_nothing()
	{
		int roma = Land(Ita, industry: 5);
		int oasis = Land(Ita, infra: 4);
		int next = Land(Ita, infra: 4);
		Join(oasis, next);
		Country(Ita, roma);

		CSupplyNetwork network = CSupplyNetworks.Build(State());

		Assert.True(network.AreaOf(oasis)!.IsLandlocked);
		Assert.Equal(0, network.DepotOf(oasis));
		Assert.Equal(0, network.DepotOf(next));
	}

	[Fact]
	public void A_naval_base_with_no_sea_to_open_onto_is_no_port()
	{
		int roma = Land(Ita, industry: 5);
		int lake = Land(Ita, infra: 4, port: 3);
		_provinces[lake] = Copy(_provinces[lake], portSea: 0);
		Country(Ita, roma);

		CSupplyNetwork network = CSupplyNetworks.Build(State());

		Assert.True(network.AreaOf(lake)!.IsLandlocked);
		Assert.Equal(0, network.DepotOf(lake));
	}

	private static CProvince Copy(CProvince from, int portSea)
	{
		var province = new CProvince(from.Id, from.Name, from.IsSea)
		{
			Owner = from.Owner, Controller = from.Controller, Span = from.Span, PortSea = portSea,
		};
		province.SetBuildingLevel(Infra, from.BuildingLevel(Infra));
		province.SetBuildingLevel(NavalBase, from.BuildingLevel(NavalBase));
		province.SetBuildingLevel(Industry, from.BuildingLevel(Industry));
		province.Modifier.Set(CSupplyFigures.Infrastructure, from.Modifier[CSupplyFigures.Infrastructure]);
		return province;
	}

	[Fact]
	public void The_depot_across_the_sea_is_the_province_with_the_larger_port()
	{
		int roma = Land(Ita, industry: 5);
		int small = Land(Ita, infra: 6, port: 1);
		int middle = Land(Ita, infra: 6);
		int large = Land(Ita, infra: 4, port: 4);
		Join(small, middle, large);
		Country(Ita, roma);

		CSupplyNetwork network = CSupplyNetworks.Build(State());

		Assert.Equal(large, network.DepotOf(small));
		Assert.Equal(large, network.DepotOf(middle));
	}

	// --- puppets, friends and strangers ---

	[Fact]
	public void A_puppet_beside_its_master_draws_on_the_masters_capital()
	{
		int tokyo = Land(new CCountryTag("JAP"), industry: 5);
		int korea = Land(new CCountryTag("JAP"));
		int hsinking = Land(Man, industry: 2);
		int harbin = Land(Man);
		Join(tokyo, korea, hsinking, harbin);
		Country(new CCountryTag("JAP"), tokyo);
		Country(Man, hsinking, overlord: new CCountryTag("JAP"));

		CSupplyNetwork network = CSupplyNetworks.Build(State());

		Assert.Single(network.Groups);
		Assert.Equal(2, network.Groups[0].Areas.Count);
		Assert.Equal(tokyo, network.DepotOf(hsinking));
		Assert.Equal(tokyo, network.DepotOf(harbin));
		Assert.Equal(tokyo, network.DepotOf(korea));
	}

	/// <summary>
	/// Korea as the maintainer watched it in the original on 2026-10-09: the master's land
	/// with a port, beside a puppet whose own port is the larger.
	/// </summary>
	private (int Pusan, int Pyongyang, int Hamhung) Korea(int pusanPort)
	{
		var jap = new CCountryTag("JAP");
		var prk = new CCountryTag("PRK");
		int tokyo = Land(jap, industry: 5);
		int pusan = Land(jap, infra: 6, port: pusanPort);
		int taegu = Land(jap, infra: 6);
		int pyongyang = Land(prk, infra: 6, industry: 2);
		int hamhung = Land(prk, infra: 6, port: 6);
		Join(pusan, taegu, pyongyang, hamhung);
		Country(jap, tokyo);
		Country(prk, pyongyang, overlord: jap);
		return (pusan, pyongyang, hamhung);
	}

	[Fact]
	public void A_port_in_a_countrys_own_home_counts_for_nothing_and_its_masters_port_beside_it_is_the_depot()
	{
		(int pusan, int pyongyang, int hamhung) = Korea(pusanPort: 4);

		CSupplyNetwork network = CSupplyNetworks.Build(State());

		// The puppet's port is the larger, and it is at home; its capital is worth two
		// levels of port; the master's four levels, away from the master's home, win.
		Assert.Equal(pusan, network.DepotOf(pyongyang));
		Assert.Equal(pusan, network.DepotOf(hamhung));
		Assert.Equal(pusan, network.DepotOf(pusan));
	}

	[Fact]
	public void With_no_port_of_the_masters_to_beat_it_a_puppets_capital_is_the_depot()
	{
		(int pusan, int pyongyang, int hamhung) = Korea(pusanPort: 0);

		CSupplyNetwork network = CSupplyNetworks.Build(State());

		Assert.Equal(pyongyang, network.DepotOf(pusan));
		Assert.Equal(pyongyang, network.DepotOf(hamhung));
	}

	[Fact]
	public void A_puppet_its_master_does_not_touch_draws_on_its_own_capital()
	{
		int tokyo = Land(new CCountryTag("JAP"), industry: 5);
		int hsinking = Land(Man, industry: 2);
		int harbin = Land(Man);
		Join(hsinking, harbin);
		Country(new CCountryTag("JAP"), tokyo);
		Country(Man, hsinking, overlord: new CCountryTag("JAP"));

		CSupplyNetwork network = CSupplyNetworks.Build(State());

		Assert.Equal(2, network.Groups.Count);
		Assert.Equal(hsinking, network.DepotOf(harbin));
	}

	[Fact]
	public void Two_countries_of_one_faction_keep_a_depot_each()
	{
		int berlin = Land(Ger, industry: 5);
		int munich = Land(Ger);
		int milano = Land(Ita);
		int roma = Land(Ita, industry: 5);
		Join(berlin, munich, milano, roma);
		Country(Ger, berlin, faction: "axis");
		Country(Ita, roma, faction: "axis");

		CSupplyNetwork network = CSupplyNetworks.Build(State());

		Assert.Equal(2, network.Groups.Count);
		Assert.Equal(berlin, network.DepotOf(munich));
		Assert.Equal(roma, network.DepotOf(milano));
	}

	[Fact]
	public void Land_with_no_port_beside_a_friends_draws_on_the_friends_depot()
	{
		int berlin = Land(Ger, industry: 5);
		int roma = Land(Ita, industry: 5);
		int milano = Land(Ita);
		int pocket = Land(Ger, infra: 5);
		Join(roma, milano, pocket);
		Country(Ger, berlin, faction: "axis");
		Country(Ita, roma, faction: "axis");

		CSupplyNetwork network = CSupplyNetworks.Build(State());

		Assert.Equal(roma, network.DepotOf(pocket));
		Assert.Equal(roma, network.DepotOf(milano));
	}

	[Fact]
	public void Land_with_no_port_beside_a_strangers_draws_on_nothing()
	{
		int berlin = Land(Ger, industry: 5);
		int warsaw = Land(Pol, industry: 5);
		int pocket = Land(Ger, infra: 5);
		Join(warsaw, pocket);
		Country(Ger, berlin);
		Country(Pol, warsaw);

		CSupplyNetwork network = CSupplyNetworks.Build(State());

		Assert.Equal(0, network.DepotOf(pocket));
		Assert.Equal(warsaw, network.DepotOf(warsaw));
	}

	// --- how far ---

	[Fact]
	public void A_province_is_its_own_step_further_than_the_nearest_beside_it()
	{
		int berlin = Land(Ger, industry: 5);
		int good = Land(Ger, infra: 10, span: 10);          // good roads, small: 1
		int poor = Land(Ger, infra: 5, span: 45);           // poor roads, middling: 2 x 2
		int bad = Land(Ger, infra: 2, span: 95);            // bad roads, large: 3 x 4
		Join(berlin, good, poor, bad);
		Country(Ger, berlin);

		CGameState state = State();
		CSupplyNetwork network = CSupplyNetworks.Build(state);

		Assert.Equal(1, CSupplyFigures.Step(state.Province(good)));
		Assert.Equal(4, CSupplyFigures.Step(state.Province(poor)));
		Assert.Equal(12, CSupplyFigures.Step(state.Province(bad)));
		Assert.Equal(0, network.DistanceOf(berlin));
		Assert.Equal(1, network.DistanceOf(good));
		Assert.Equal(5, network.DistanceOf(poor));
		Assert.Equal(17, network.DistanceOf(bad));
		Assert.Equal(3, network.HopsOf(bad));
	}

	[Fact]
	public void The_distance_is_by_the_cheapest_way_and_not_by_the_fewest_provinces()
	{
		int berlin = Land(Ger, industry: 5);
		int swamp = Land(Ger, infra: 2, span: 95);          // 12
		int roadOne = Land(Ger);
		int roadTwo = Land(Ger);
		int far = Land(Ger);
		Join(berlin, swamp, far);
		Join(berlin, roadOne, roadTwo, far);
		Country(Ger, berlin);

		CSupplyNetwork network = CSupplyNetworks.Build(State());

		Assert.Equal(3, network.DistanceOf(far));
		Assert.Equal(3, network.HopsOf(far));
	}

	[Theory]
	[InlineData(29, 1)]
	[InlineData(30, 2)]
	[InlineData(59, 2)]
	[InlineData(60, 3)]
	[InlineData(89, 3)]
	[InlineData(90, 4)]
	public void A_province_is_one_of_four_sizes_by_its_box_on_the_map(int span, int size)
	{
		int id = Land(Ger, span: span);

		Assert.Equal(size, CSupplyFigures.SizeClass(_provinces[id]));
	}

	// --- what a province can pass and what it takes ---

	[Fact]
	public void What_a_province_can_pass_goes_by_the_square_of_its_infrastructure()
	{
		int berlin = Land(Ger, industry: 5);
		int beside = Land(Ger);
		int full = Land(Ger, infra: 10);
		int half = Land(Ger, infra: 5);
		int held = Land(Ger, infra: 5, owner: Pol);
		Join(berlin, beside, full, half, held);
		Country(Ger, berlin);
		Country(Pol, 0);

		CGameState state = State();

		// 10 levels squared is 100, four times that and doubled for its own ground.
		Assert.Equal(CFixedPoint.Parse("800"), CSupplyFigures.Capacity(state, state.Province(full)));
		Assert.Equal(CFixedPoint.Parse("200"), CSupplyFigures.Capacity(state, state.Province(half)));
		Assert.Equal(CFixedPoint.Parse("100"), CSupplyFigures.Capacity(state, state.Province(held)));
		// The capital and what touches it pass without limit.
		Assert.Equal(CSupplyFigures.Unlimited, CSupplyFigures.Capacity(state, state.Province(berlin)));
		Assert.Equal(CSupplyFigures.Unlimited, CSupplyFigures.Capacity(state, state.Province(beside)));
		Assert.True(CSupplyFigures.IsBesideCapital(state, state.Province(beside)));
		Assert.False(CSupplyFigures.IsBesideCapital(state, state.Province(full)));
	}

	[Fact]
	public void Technology_and_the_countrys_modifier_raise_what_a_province_can_pass()
	{
		int berlin = Land(Ger, industry: 5);
		int beside = Land(Ger);
		int full = Land(Ger, infra: 10);
		Join(berlin, beside, full);
		CCountry germany = Country(Ger, berlin);
		germany.Technology.Set(CSupplyFigures.Throughput, CFixedPoint.Parse("0.05"));
		germany.Modifier.Set(CSupplyFigures.Throughput, CFixedPoint.Parse("0.1"));

		CGameState state = State();

		Assert.Equal(CFixedPoint.Parse("920"), CSupplyFigures.Capacity(state, state.Province(full)));
	}

	[Fact]
	public void A_load_loses_half_as_much_on_a_countrys_own_ground()
	{
		int berlin = Land(Ger, industry: 5);
		int own = Land(Ger);
		int held = Land(Ger, owner: Pol);
		Join(berlin, own, held);
		CCountry germany = Country(Ger, berlin);
		Country(Pol, 0);

		CGameState state = State();

		Assert.Equal(CFixedPoint.Parse("0.05"), CSupplyFigures.Loss(state, state.Province(own)));
		Assert.Equal(CFixedPoint.Parse("0.1"), CSupplyFigures.Loss(state, state.Province(held)));

		germany.Technology.Set(CSupplyFigures.TransferCost, CFixedPoint.Parse("-0.01"));
		Assert.Equal(CFixedPoint.Parse("0.04"), CSupplyFigures.Loss(state, state.Province(own)));
		germany.Technology.Set(CSupplyFigures.TransferCost, CFixedPoint.Parse("-1"));
		Assert.Equal(CFixedPoint.Zero, CSupplyFigures.Loss(state, state.Province(own)));
	}

	[Fact]
	public void Mud_and_partisans_take_more()
	{
		int berlin = Land(Ger, industry: 5);
		int muddy = Land(Ger);
		int frozen = Land(Ger);
		int taken = Land(Ger, owner: Pol);
		Join(berlin, muddy, frozen, taken);
		Country(Ger, berlin);
		Country(Pol, 0);
		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		_wars.Add(war);

		CGameState state = State();
		state.Province(muddy).Weather.Muddyness = CFixedPoint.Parse("0.8");
		state.Province(frozen).Weather.Muddyness = CFixedPoint.Parse("0.8");
		state.Province(frozen).Weather.Frozen = CFixedPoint.One;
		state.Province(taken).Modifier.Set(CSupplyFigures.PartisanSupport, CFixedPoint.Parse("0.5"));

		// 0.05 x (1 + 0.8 x 0.25), and the mud of frozen ground counts for nothing.
		Assert.Equal(CFixedPoint.Parse("0.06"), CSupplyFigures.Loss(state, state.Province(muddy)));
		Assert.Equal(CFixedPoint.Parse("0.05"), CSupplyFigures.Loss(state, state.Province(frozen)));
		// 0.1 x (1 + 0.1 x 0.5) on ground taken from an enemy.
		Assert.Equal(CFixedPoint.Parse("0.105"), CSupplyFigures.Loss(state, state.Province(taken)));
	}

	// --- keeping it ---

	[Fact]
	public void The_network_is_made_again_when_a_province_changes_hands()
	{
		int berlin = Land(Ger, industry: 5);
		int posen = Land(Pol);
		int warsaw = Land(Pol, industry: 5);
		Join(berlin, posen, warsaw);
		Country(Ger, berlin);
		Country(Pol, warsaw);

		CGameState state = State();
		CSupplyNetwork first = CSupplyNetworks.Refresh(state);
		Assert.Same(first, CSupplyNetworks.Refresh(state));
		Assert.Equal(warsaw, first.DepotOf(posen));

		state.Province(posen).Controller = Ger;
		CSupplyNetwork second = CSupplyNetworks.Refresh(state);

		Assert.NotSame(first, second);
		Assert.Equal(berlin, second.DepotOf(posen));
		Assert.NotSame(second, CSupplyNetworks.Refresh(state, force: true));
	}
}
