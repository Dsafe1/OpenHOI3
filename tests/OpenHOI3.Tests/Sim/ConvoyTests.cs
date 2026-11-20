using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Convoys, against <c>docs/mechanics/convoys.md</c>. Each test lays out a home shore, a
/// strip of sea and a shore across it. A harbour lands 6 for each level of its naval
/// base; a convoy wants a transport for every three provinces of its way to a full
/// harbour, and never fewer than one; a province holds fifteen days of what is asked of it.
/// </summary>
public class ConvoyTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private static readonly CBuilding Infra = new(0, "infra", 10, CSupplyFigures.Infrastructure, CFixedPoint.Parse("0.1"));
	private static readonly CBuilding NavalBase = new(1, CSupplyNetworks.NavalBase, 10, "", CFixedPoint.Zero);
	private static readonly CBuilding Industry = new(2, CSupplyNetworks.Industry, 10, "", CFixedPoint.Zero);

	private readonly List<CProvince> _provinces = new() { new CProvince(0, "", isSea: false) };
	private readonly Dictionary<int, List<CProvinceEdge>> _edges = new();
	private readonly List<CCountry> _countries = new();
	private readonly List<CWar> _wars = new();
	private int _lastSea;

	private CCountry Country(CCountryTag tag, int capital)
	{
		var country = new CCountry { Tag = tag, Name = tag.Value, DefinitionFile = "", Capital = capital };
		_countries.Add(country);
		return country;
	}

	private int Sea(int x)
	{
		int id = _provinces.Count;
		_provinces.Add(new CProvince(id, $"S{id}", isSea: true) { SeaIndex = _lastSea++, CentreX = x, CentreY = 100 });
		_edges[id] = new List<CProvinceEdge>();
		return id;
	}

	private int Land(CCountryTag? holder, int x, int port = 0, int onto = 0, int industry = 0)
	{
		int id = _provinces.Count;
		var province = new CProvince(id, $"P{id}", isSea: false)
		{
			Owner = holder,
			Controller = holder,
			Span = 10,
			PortSea = port > 0 ? onto : 0,
			SeaIndex = port > 0 ? _lastSea++ : -1,
			CentreX = x,
			CentreY = 100,
		};
		province.SetBuildingLevel(Infra, CFixedPoint.FromInt(10));
		province.SetBuildingLevel(NavalBase, CFixedPoint.FromInt(port));
		province.SetBuildingLevel(Industry, CFixedPoint.FromInt(industry));
		province.Modifier.Set(CSupplyFigures.Infrastructure, CFixedPoint.One);
		if (holder is { } core)
		{
			province.Cores.Add(core);
		}
		_provinces.Add(province);
		_edges[id] = new List<CProvinceEdge>();
		if (onto != 0)
		{
			Join(id, onto);
		}
		return id;
	}

	private void Join(params int[] chain)
	{
		for (int at = 0; at + 1 < chain.Length; at++)
		{
			_edges[chain[at]].Add(new CProvinceEdge(chain[at + 1], EProvinceEdgeKind.Plain, 0, CFixedPoint.FromInt(10)));
			_edges[chain[at + 1]].Add(new CProvinceEdge(chain[at], EProvinceEdgeKind.Plain, 0, CFixedPoint.FromInt(10)));
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
		defines.Set(CSupplyFigures.Military, CSupplyDay.PoolDays, CFixedPoint.FromInt(15));
		defines.Set(CSupplyFigures.Military, CBaseCrowding.NavalBaseEfficiency, CFixedPoint.FromInt(6));
		defines.Set(CSupplyFigures.WeatherSection, CSupplyFigures.MudTax, CFixedPoint.Parse("0.25"));
		defines.Set(CConvoyRules.EconomySection, CConvoyRules.PathLengthMult, CFixedPoint.Parse("0.33"));
		defines.Set(CConvoyRules.EconomySection, CConvoyRules.TradeWeightMult, CFixedPoint.Parse("0.2"));
		return new CGameState(new CDate(1936, 1, 1), _provinces, _countries,
			new[] { Infra, NavalBase, Industry }, defines: defines, wars: _wars) { MapWidth = 10000 };
	}

	/// <summary>
	/// Kiel and Hamburg on one sea, three more seas in a row, and Tsingtao on the last
	/// with a province inland of it.
	/// </summary>
	private (int Berlin, int Kiel, int Hamburg, int[] Seas, int Tsingtao, int Inland) TwoShores(int tsingtaoPort = 10)
	{
		int near = Sea(100);
		int second = Sea(200);
		int third = Sea(300);
		int far = Sea(400);
		Join(near, second, third, far);
		int berlin = Land(Ger, 0, industry: 5);
		int kiel = Land(Ger, 90, port: 10, onto: near);
		int hamburg = Land(Ger, 80, port: 3, onto: near);
		Join(berlin, kiel);
		Join(berlin, hamburg);
		int tsingtao = Land(Ger, 410, port: tsingtaoPort, onto: far);
		int inland = Land(Ger, 420);
		Join(tsingtao, inland);
		return (berlin, kiel, hamburg, new[] { near, second, third, far }, tsingtao, inland);
	}

	// --- the heap ---

	[Fact]
	public void The_heap_gives_the_least_first_and_equals_in_its_own_order()
	{
		var heap = new CPathHeap();
		heap.Push(5, 1);
		heap.Push(3, 2);
		heap.Push(5, 3);
		heap.Push(3, 4);
		heap.Push(1, 5);

		var order = new List<int>();
		while (heap.Count > 0)
		{
			order.Add(heap.Pop());
		}

		// Of the two threes and of the two fives the one that went in later comes out
		// first: an entry sinking from the top stops above one that weighs the same.
		Assert.Equal(new[] { 5, 4, 2, 3, 1 }, order);
	}

	// --- the way ---

	[Fact]
	public void A_convoy_goes_from_harbour_to_harbour_through_the_sea_between()
	{
		var world = TwoShores();
		Country(Ger, world.Berlin);
		CGameState state = State();

		List<int>? path = CConvoyRoute.Find(state, world.Kiel, world.Tsingtao, Ger);

		Assert.Equal(new[] { world.Kiel, world.Seas[0], world.Seas[1], world.Seas[2], world.Seas[3], world.Tsingtao }, path);
	}

	[Fact]
	public void There_is_no_way_where_the_sea_does_not_join()
	{
		int near = Sea(100);
		int far = Sea(400);
		int berlin = Land(Ger, 0, industry: 5);
		int kiel = Land(Ger, 90, port: 10, onto: near);
		Join(berlin, kiel);
		int tsingtao = Land(Ger, 410, port: 10, onto: far);
		Country(Ger, berlin);
		CGameState state = State();

		Assert.Null(CConvoyRoute.Find(state, kiel, tsingtao, Ger));
	}

	// --- what a convoy wants ---

	[Fact]
	public void A_convoy_wants_more_transports_the_longer_its_way_and_the_larger_the_harbour_it_ends_in()
	{
		var world = TwoShores();
		CCountry germany = Country(Ger, world.Berlin);
		CGameState state = State();

		CConvoy toFull = CConvoyMaker.Create(state, germany, world.Kiel, world.Tsingtao, forSupplies: true);
		// Six provinces at 0.33 is 1.98, a full harbour counts whole, and one is added: 2.98.
		Assert.Equal(2, CConvoyRules.DesiredTransports(state, toFull));

		CConvoy toSmall = CConvoyMaker.Create(state, germany, world.Tsingtao, world.Hamburg, forSupplies: false);
		// The same way to a harbour of three levels: 1.98 x 0.3 and one, 1.594.
		Assert.Equal(1, CConvoyRules.DesiredTransports(state, toSmall));
	}

	[Fact]
	public void A_convoy_made_while_a_game_starts_is_manned_for_nothing_and_one_made_later_is_empty()
	{
		var world = TwoShores();
		CCountry germany = Country(Ger, world.Berlin);
		CGameState state = State();

		state.IsBeingMade = true;
		CConvoy first = CConvoyMaker.Create(state, germany, world.Kiel, world.Tsingtao, forSupplies: true);
		state.IsBeingMade = false;
		CConvoy second = CConvoyMaker.Create(state, germany, world.Kiel, world.Tsingtao, forSupplies: true);

		Assert.Equal((2, 1), (first.Transports, first.Escorts));
		Assert.Equal((0, 0), (second.Transports, second.Escorts));
		Assert.Equal(0, germany.Transports);
	}

	// --- which harbours ---

	[Fact]
	public void An_areas_first_harbour_is_its_largest()
	{
		var world = TwoShores();
		Country(Ger, world.Berlin);
		CSupplyNetwork network = CSupplyNetworks.Build(State());

		// Kiel was found before Hamburg and is the larger; Hamburg alone would be first
		// were they in the order they were found the other way about.
		Assert.Equal(new[] { world.Kiel, world.Hamburg }, network.AreaOf(world.Berlin)!.Ports);
	}

	[Fact]
	public void The_harbour_at_home_is_the_one_whose_water_is_nearest_and_the_first_of_two_alike()
	{
		var world = TwoShores();
		Country(Ger, world.Berlin);
		CGameState state = State();
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);

		// Both open onto the same water, so the first in the list - the larger - is taken.
		Assert.Equal(world.Kiel, CConvoyRules.NearestPort(state, state.Country(Ger)!, network.AreaOf(world.Berlin), world.Tsingtao));
	}

	[Fact]
	public void At_peace_every_harbour_across_the_sea_gets_a_supply_convoy()
	{
		var world = TwoShores();
		int second = Land(Ger, 430, port: 2, onto: world.Seas[3]);
		Join(world.Inland, second);
		CCountry germany = Country(Ger, world.Berlin);
		CGameState state = State();
		state.IsBeingMade = true;
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);

		CSupplyConvoys.Maintain(state, network, germany);

		// The depot first, then the harbour furthest from it - though nothing is asked for.
		Assert.Equal(2, germany.Convoys.Count);
		Assert.Equal((world.Kiel, world.Tsingtao), (germany.Convoys[0].Start, germany.Convoys[0].End));
		Assert.Equal((world.Kiel, second), (germany.Convoys[1].Start, germany.Convoys[1].End));
		Assert.True(germany.Convoys[0].IsSupply);

		// And a second look makes no more.
		CSupplyConvoys.Maintain(state, network, germany);
		Assert.Equal(2, germany.Convoys.Count);
	}

	[Fact]
	public void At_war_a_harbour_gets_a_convoy_only_while_the_depot_is_asked_for_more_than_is_landed()
	{
		var world = TwoShores();
		int second = Land(Ger, 430, port: 2, onto: world.Seas[3]);
		Join(world.Inland, second);
		CCountry germany = Country(Ger, world.Berlin);
		int warsaw = Land(Pol, 900);
		Country(Pol, warsaw);
		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		_wars.Add(war);
		CGameState state = State();
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);

		CSupplyConvoys.Maintain(state, network, germany);
		Assert.Empty(germany.Convoys);

		// A little is asked of the depot: one harbour of ten levels lands sixty, which is plenty.
		state.Province(world.Tsingtao).SupplyDrawn[EGoods.Supplies] = CFixedPoint.FromInt(5);
		CSupplyConvoys.Maintain(state, network, germany);
		Assert.Single(germany.Convoys);
		Assert.Equal(world.Tsingtao, germany.Convoys[0].End);
	}

	// --- a sailing ---

	[Fact]
	public void A_supply_convoy_lands_what_the_harbours_across_the_sea_have_room_for()
	{
		var world = TwoShores();
		CCountry germany = Country(Ger, world.Berlin);
		CGameState state = State();
		state.IsBeingMade = true;
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CConvoy convoy = CConvoyMaker.Create(state, germany, world.Kiel, world.Tsingtao, forSupplies: true);
		CProvince berlin = state.Province(world.Berlin);
		CProvince tsingtao = state.Province(world.Tsingtao);
		berlin.Pool[EGoods.Supplies] = CFixedPoint.FromInt(1000);
		berlin.Pool[EGoods.Metal] = CFixedPoint.FromInt(1000);
		tsingtao.SupplyDrawn[EGoods.Supplies] = CFixedPoint.FromInt(2);
		tsingtao.Pool[EGoods.Supplies] = CFixedPoint.FromInt(10);

		CConvoyDelivery.Run(state, network, convoy);

		// Fifteen days of the two asked for is thirty, and ten lie there already.
		Assert.Equal(CFixedPoint.FromInt(20), convoy.Daily[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.FromInt(30), tsingtao.Pool[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.FromInt(980), berlin.Pool[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.FromInt(20), germany.ConvoyedOut[EGoods.Supplies]);
		// It carries supplies and fuel and nothing else.
		Assert.Equal(CFixedPoint.FromInt(1000), berlin.Pool[EGoods.Metal]);
	}

	[Fact]
	public void A_convoy_short_of_transports_carries_that_much_less_and_one_with_none_does_not_sail()
	{
		var world = TwoShores();
		CCountry germany = Country(Ger, world.Berlin);
		CGameState state = State();
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CConvoy convoy = CConvoyMaker.Create(state, germany, world.Kiel, world.Tsingtao, forSupplies: true);
		CProvince berlin = state.Province(world.Berlin);
		CProvince tsingtao = state.Province(world.Tsingtao);
		berlin.Pool[EGoods.Supplies] = CFixedPoint.FromInt(40);
		tsingtao.SupplyDrawn[EGoods.Supplies] = CFixedPoint.FromInt(100);

		CConvoyDelivery.Run(state, network, convoy);
		Assert.Equal(CFixedPoint.Zero, tsingtao.Pool[EGoods.Supplies]);

		// One transport of the two it wants: half of what the stockpile holds.
		convoy.Transports = 1;
		CConvoyDelivery.Run(state, network, convoy);
		Assert.Equal(CFixedPoint.FromInt(20), tsingtao.Pool[EGoods.Supplies]);
	}

	[Fact]
	public void No_more_is_landed_than_the_harbour_can_take_in_a_day()
	{
		var world = TwoShores(tsingtaoPort: 1);
		CCountry germany = Country(Ger, world.Berlin);
		CGameState state = State();
		state.IsBeingMade = true;
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CConvoy convoy = CConvoyMaker.Create(state, germany, world.Kiel, world.Tsingtao, forSupplies: true);
		state.Province(world.Berlin).Pool[EGoods.Supplies] = CFixedPoint.FromInt(1000);
		state.Province(world.Tsingtao).SupplyDrawn[EGoods.Supplies] = CFixedPoint.FromInt(100);

		CConvoyDelivery.Run(state, network, convoy);

		// One level of naval base lands six.
		Assert.Equal(CFixedPoint.FromInt(6), state.Province(world.Tsingtao).Pool[EGoods.Supplies]);
	}

	// --- what the land across the sea yields ---

	[Fact]
	public void What_cut_off_land_yields_waits_in_its_largest_harbour_and_a_ship_is_sent_for_it()
	{
		var world = TwoShores();
		CCountry germany = Country(Ger, world.Berlin);
		CGameState state = State();
		state.IsBeingMade = true;
		state.Province(world.Inland).CurrentProducing[EGoods.Metal] = CFixedPoint.FromInt(3);
		state.Province(world.Berlin).CurrentProducing[EGoods.Metal] = CFixedPoint.FromInt(7);
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);

		CResources.GatherCutOff(state);
		CResources.UpdateHomeProduction(state, germany);

		Assert.Equal(CFixedPoint.FromInt(3), state.Province(world.Tsingtao).Pool[EGoods.Metal]);
		Assert.Equal(CFixedPoint.Zero, state.Province(world.Inland).Pool[EGoods.Metal]);
		Assert.Equal(CFixedPoint.FromInt(7), germany.HomeProduced[EGoods.Metal]);

		CResourceConvoys.Maintain(state, network, germany);
		CConvoy convoy = Assert.Single(germany.Convoys);
		Assert.Equal((world.Tsingtao, world.Kiel), (convoy.Start, convoy.End));
		Assert.False(convoy.IsSupply);

		// It lands in the stockpile, not in the harbour it comes to.
		CConvoyDelivery.Run(state, network, convoy);
		Assert.Equal(CFixedPoint.Zero, state.Province(world.Tsingtao).Pool[EGoods.Metal]);
		Assert.Equal(CFixedPoint.FromInt(3), state.Province(world.Berlin).Pool[EGoods.Metal]);
		Assert.Equal(CFixedPoint.FromInt(3), germany.ConvoyedIn[EGoods.Metal]);
	}

	// --- the ships ---

	[Fact]
	public void Transports_go_to_the_supply_convoys_first_and_a_convoy_is_filled_or_left_empty()
	{
		var world = TwoShores();
		CCountry germany = Country(Ger, world.Berlin);
		CGameState state = State();
		state.Province(world.Inland).CurrentProducing[EGoods.Metal] = CFixedPoint.FromInt(3);
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CConvoy resources = CConvoyMaker.Create(state, germany, world.Tsingtao, world.Kiel, forSupplies: false);
		CConvoy supplies = CConvoyMaker.Create(state, germany, world.Kiel, world.Tsingtao, forSupplies: true);
		germany.Transports = 3;

		CConvoyShips.Distribute(state, network, germany);

		// Each wants two; the supply convoy is served first and the other gets the one left.
		Assert.Equal(2, supplies.Transports);
		Assert.Equal(1, resources.Transports);
		Assert.Equal(0, germany.Transports);
		Assert.Equal(4, germany.TransportsNeeded);

		// A day later the run for resources, under strength, gives its one back - and
		// gets it again, there being nobody else to take it.
		CConvoyShips.Distribute(state, network, germany);
		Assert.Equal((2, 1, 0), (supplies.Transports, resources.Transports, germany.Transports));
	}

	[Fact]
	public void A_convoy_with_nothing_to_load_gives_its_ships_back_and_a_country_at_peace_keeps_no_escorts_out()
	{
		var world = TwoShores();
		CCountry germany = Country(Ger, world.Berlin);
		CGameState state = State();
		state.IsBeingMade = true;
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CConvoy resources = CConvoyMaker.Create(state, germany, world.Tsingtao, world.Kiel, forSupplies: false);
		CConvoy supplies = CConvoyMaker.Create(state, germany, world.Kiel, world.Tsingtao, forSupplies: true);

		CConvoyShips.Distribute(state, network, germany);

		// Nothing is yielded across the sea, so the run for resources is idle.
		Assert.Equal((0, 0), (resources.Transports, resources.Escorts));
		Assert.Equal((2, 0), (supplies.Transports, supplies.Escorts));
		Assert.Equal(2, germany.Transports);
		Assert.Equal(2, germany.Escorts);
	}

	// --- the day and the start ---

	[Fact]
	public void A_depot_across_the_sea_sends_its_surplus_home_only_as_far_as_its_convoys_carry()
	{
		var world = TwoShores(tsingtaoPort: 1);
		CCountry germany = Country(Ger, world.Berlin);
		CGameState state = State();
		state.Province(world.Tsingtao).Pool[EGoods.Supplies] = CFixedPoint.FromInt(100);

		CSupplyPass.RunDaily(state);
		Assert.Equal(CFixedPoint.FromInt(100), state.Province(world.Tsingtao).Pool[EGoods.Supplies]);

		// With a convoy at strength the harbour sends home what it lands in a day: six.
		state.IsBeingMade = true;
		CConvoyMaker.Create(state, germany, world.Kiel, world.Tsingtao, forSupplies: true);
		CSupplyPass.RunDaily(state);
		Assert.Equal(CFixedPoint.FromInt(94), state.Province(world.Tsingtao).Pool[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.FromInt(6), state.Province(world.Berlin).Pool[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.FromInt(6), germany.SentBack[EGoods.Supplies]);
	}

	[Fact]
	public void A_new_game_gives_a_country_its_convoys_manned_and_transports_to_half_its_base_IC()
	{
		var world = TwoShores();
		CCountry germany = Country(Ger, world.Berlin);
		CGameState state = State();
		germany.BaseIc = 9;
		state.Province(world.Berlin).Pool[EGoods.Supplies] = CFixedPoint.FromInt(500);
		state.IsBeingMade = true;

		CSupplyStart.Fill(state);

		// The convoy has its two transports out of nowhere; its escort went back, the
		// country being at peace; and four transports are free besides.
		CConvoy convoy = Assert.Single(germany.Convoys);
		Assert.Equal((world.Kiel, world.Tsingtao), (convoy.Start, convoy.End));
		Assert.Equal((2, 0), (convoy.Transports, convoy.Escorts));
		Assert.Equal(4, germany.Transports);
		Assert.Equal(1, germany.Escorts);
		Assert.Equal(CFixedPoint.FromInt(500), state.Province(world.Berlin).Pool[EGoods.Supplies]);
	}
}
