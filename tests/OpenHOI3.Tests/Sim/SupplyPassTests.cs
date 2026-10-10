using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The carrying of supplies and what units draw, against
/// <c>docs/mechanics/supply.md</c>, sections 11 to 17. Each test lays out a few
/// provinces of its own. A division here uses one unit of supplies a day and, where it
/// is said to have motors, two of fuel; a load loses 0.05 on arriving on a country's
/// own ground; a province holds fifteen days.
/// </summary>
public class SupplyPassTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Ita = new("ITA");
	private static readonly CCountryTag Pol = new("POL");

	private static readonly CBuilding Infra = new(0, "infra", 10, CSupplyFigures.Infrastructure, CFixedPoint.Parse("0.1"));
	private static readonly CBuilding NavalBase = new(1, CSupplyNetworks.NavalBase, 10, "", CFixedPoint.Zero);
	private static readonly CBuilding Industry = new(2, CSupplyNetworks.Industry, 10, "", CFixedPoint.Zero);

	private readonly List<CProvince> _provinces = new() { new CProvince(0, "", isSea: false) };
	private readonly Dictionary<int, List<CProvinceEdge>> _edges = new();
	private readonly List<CCountry> _countries = new();
	private readonly List<CWar> _wars = new();
	private int _lastUnit;

	private CCountry Country(CCountryTag tag, int capital, string faction = "")
	{
		var country = new CCountry { Tag = tag, Name = tag.Value, DefinitionFile = "", Capital = capital, Faction = faction };
		_countries.Add(country);
		return country;
	}

	private int Land(CCountryTag? holder, int port = 0, int industry = 0)
	{
		int id = _provinces.Count;
		var province = new CProvince(id, $"P{id}", isSea: false)
		{
			Owner = holder,
			Controller = holder,
			Span = 10,
			PortSea = port > 0 ? 9999 : 0,
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
				province.Edges = edges;
			}
		}
		var defines = new CDefines();
		defines.Set(CSupplyFigures.Military, CSupplyFigures.ThroughputImpact, CFixedPoint.Parse("4"));
		defines.Set(CSupplyFigures.Military, CSupplyFigures.OwnedThroughputBonus, CFixedPoint.Parse("2"));
		defines.Set(CSupplyFigures.Military, CSupplyFigures.SupplyTax, CFixedPoint.Parse("0.1"));
		defines.Set(CSupplyFigures.Military, CSupplyFigures.PartisanTax, CFixedPoint.Parse("0.1"));
		defines.Set(CSupplyFigures.Military, CSupplyDay.PoolDays, CFixedPoint.FromInt(15));
		defines.Set(CSupplyFigures.WeatherSection, CSupplyFigures.MudTax, CFixedPoint.Parse("0.25"));
		return new CGameState(new CDate(1936, 1, 1), _provinces, _countries,
			new[] { Infra, NavalBase, Industry }, defines: defines, wars: _wars);
	}

	/// <summary>A division of one brigade in a province, using one of supplies a day and so much fuel.</summary>
	private CUnit Division(CCountry country, int province, string fuel = "0")
	{
		var type = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(30));
		type.Stats.Values.Set(CUnitSupply.SupplyStatistic, CFixedPoint.One);
		type.Stats.Values.Set(CUnitSupply.FuelStatistic, CFixedPoint.Parse(fuel));
		var unit = new CUnit(EUnitKind.Division, country.Tag) { Id = ++_lastUnit, Location = province };
		var brigade = new CSubUnit(type) { Unit = unit, Strength = CFixedPoint.FromInt(30), Highest = CFixedPoint.FromInt(30) };
		unit.SubUnits.Add(brigade);
		country.Units.Add(unit);
		return unit;
	}

	private static CFixedPoint Supplies(CGameState state, int province)
	{
		return state.Province(province).Pool[EGoods.Supplies];
	}

	// --- the order of a pass ---

	[Fact]
	public void The_furthest_province_comes_first_and_equals_come_as_the_originals_sort_leaves_them()
	{
		int berlin = Land(Ger, industry: 5);
		int near = Land(Ger);
		int farA = Land(Ger);
		int farB = Land(Ger);
		Join(berlin, near, farA);
		Join(near, farB);
		Country(Ger, berlin);

		int[] order = CSupplyOrder.FarthestFirst(CSupplyNetworks.Build(State()));

		// Province 0 and the capital are no way from a depot, the next is one province
		// out and the last two are two out - and of those two the later one comes first.
		Assert.Equal(new[] { farA, farB, near, berlin, 0 }, order);
	}

	// --- a day's pass ---

	[Fact]
	public void A_province_takes_from_the_one_nearer_the_depot_and_loses_a_little_on_the_way()
	{
		int berlin = Land(Ger, industry: 5);
		int near = Land(Ger);
		int far = Land(Ger);
		Join(berlin, near, far);
		CCountry germany = Country(Ger, berlin);
		Division(germany, far);
		CGameState state = State();
		state.Province(berlin).Pool[EGoods.Supplies] = CFixedPoint.FromInt(100);

		// The first day nothing moves: every province further out comes before the one it
		// would take from, and none has yet been seen to hold anything.
		CSupplyPass.RunDaily(state);
		Assert.Equal(CFixedPoint.Zero, Supplies(state, near));
		// What the far province wants is asked of the near one all the same: two days'
		// worth at the most, and what a load loses on top.
		Assert.Equal(CFixedPoint.Parse("2"), state.Province(far).SupplyDrawn[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.Parse("2.05"), state.Province(near).SupplyDrawn[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.Parse("2.1"), state.Province(berlin).SupplyDrawn[EGoods.Supplies]);

		// The second day it reaches the province beside the capital, less what is lost.
		CSupplyPass.RunDaily(state);
		Assert.Equal(CFixedPoint.Parse("2.05"), Supplies(state, near));
		Assert.Equal(CFixedPoint.Parse("97.9"), Supplies(state, berlin));
		Assert.Equal(CFixedPoint.Parse("2.1"), germany.UnitDrawn[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.Parse("2.1"), state.Province(berlin).SupplyThroughput[EGoods.Supplies]);

		// And the third day it reaches the division's province.
		CSupplyPass.RunDaily(state);
		Assert.Equal(CFixedPoint.Parse("2"), Supplies(state, far));
	}

	[Fact]
	public void What_a_province_holds_beyond_its_days_goes_one_province_inward_a_day()
	{
		int berlin = Land(Ger, industry: 5);
		int near = Land(Ger);
		int far = Land(Ger);
		Join(berlin, near, far);
		CCountry germany = Country(Ger, berlin);
		CGameState state = State();
		state.Province(far).Pool[EGoods.Supplies] = CFixedPoint.FromInt(3);

		CSupplyPass.RunDaily(state);
		Assert.Equal(CFixedPoint.Zero, Supplies(state, far));
		Assert.Equal(CFixedPoint.FromInt(3), Supplies(state, near));
		Assert.Equal(CFixedPoint.Zero, germany.SentBack[EGoods.Supplies]);

		CSupplyPass.RunDaily(state);
		Assert.Equal(CFixedPoint.FromInt(3), Supplies(state, berlin));
		Assert.Equal(CFixedPoint.FromInt(3), germany.SentBack[EGoods.Supplies]);
	}

	[Fact]
	public void A_province_with_units_keeps_its_fifteen_days_and_sends_back_only_the_rest()
	{
		int berlin = Land(Ger, industry: 5);
		int far = Land(Ger);
		Join(berlin, far);
		CCountry germany = Country(Ger, berlin);
		Division(germany, far);
		CGameState state = State();
		state.Province(far).Pool[EGoods.Supplies] = CFixedPoint.FromInt(20);

		CSupplyPass.RunDaily(state);

		Assert.Equal(CFixedPoint.FromInt(15), Supplies(state, far));
		Assert.Equal(CFixedPoint.FromInt(5), Supplies(state, berlin));
	}

	// --- what a unit draws ---

	[Fact]
	public void A_unit_draws_on_the_province_it_stands_in_and_not_on_the_stockpile()
	{
		int berlin = Land(Ger, industry: 5);
		int far = Land(Ger);
		Join(berlin, far);
		CCountry germany = Country(Ger, berlin);
		CUnit division = Division(germany, far);
		CGameState state = State();
		state.Province(berlin).Pool[EGoods.Supplies] = CFixedPoint.FromInt(100);

		CSupplyDraw.Draw(state, germany, division);
		Assert.Equal(CFixedPoint.Zero, division.SuppliesReceived);
		Assert.Equal(CFixedPoint.FromInt(100), Supplies(state, berlin));

		state.Province(far).Pool[EGoods.Supplies] = CFixedPoint.FromInt(4);
		CSupplyDraw.Draw(state, germany, division);
		Assert.Equal(CFixedPoint.One, division.SuppliesReceived);
		Assert.Equal(CFixedPoint.FromInt(3), Supplies(state, far));
		// It is counted as asked for by the country that holds the ground.
		Assert.Equal(CFixedPoint.FromInt(2), germany.UnitDemand[EGoods.Supplies]);
	}

	[Fact]
	public void Units_in_a_province_that_holds_too_little_share_what_there_is()
	{
		int berlin = Land(Ger, industry: 5);
		int far = Land(Ger);
		Join(berlin, far);
		CCountry germany = Country(Ger, berlin);
		CUnit first = Division(germany, far);
		CUnit second = Division(germany, far);
		CGameState state = State();
		CSupplyPass.RunDaily(state);                            // which counts what the province's units ask for
		state.Province(far).Pool[EGoods.Supplies] = CFixedPoint.One;

		CSupplyDraw.Draw(state, germany, first);

		// Two ask for one each and there is one: each has a half, whoever draws first.
		Assert.Equal(CFixedPoint.Parse("0.5"), first.SuppliesReceived);
		Assert.Equal(CFixedPoint.Parse("0.5"), Supplies(state, far));
		CSupplyDraw.Draw(state, germany, second);
		Assert.Equal(CFixedPoint.Parse("0.25"), second.SuppliesReceived);
	}

	[Fact]
	public void Fuel_is_given_to_a_unit_standing_still_and_burnt_only_on_the_move()
	{
		int berlin = Land(Ger, industry: 5);
		int far = Land(Ger);
		Join(berlin, far);
		CCountry germany = Country(Ger, berlin);
		CUnit tanks = Division(germany, far, fuel: "2");
		CGameState state = State();
		state.Province(far).Pool[EGoods.Supplies] = CFixedPoint.FromInt(10);
		state.Province(far).Pool[EGoods.Fuel] = CFixedPoint.FromInt(10);

		CSupplyDraw.Draw(state, germany, tanks);
		Assert.Equal(CFixedPoint.One, tanks.FuelReceived);
		Assert.Equal(CFixedPoint.FromInt(10), state.Province(far).Pool[EGoods.Fuel]);

		tanks.Path.Add(berlin);
		CSupplyDraw.Draw(state, germany, tanks);
		Assert.Equal(CFixedPoint.FromInt(8), state.Province(far).Pool[EGoods.Fuel]);
	}

	[Fact]
	public void On_a_friends_depot_a_units_country_pays_the_friend_for_what_it_asks()
	{
		int berlin = Land(Ger, industry: 5);
		int far = Land(Ger);
		Join(berlin, far);
		int rome = Land(Ita, industry: 5);
		CCountry germany = Country(Ger, berlin, faction: "axis");
		CCountry italy = Country(Ita, rome, faction: "axis");
		CUnit division = Division(italy, far);
		CGameState state = State();
		state.Province(rome).Pool[EGoods.Supplies] = CFixedPoint.FromInt(50);
		state.Province(far).Pool[EGoods.Supplies] = CFixedPoint.FromInt(15);
		CSupplyPass.RunDaily(state);                            // the province now knows it is asked for one a day

		CSupplyDraw.Draw(state, italy, division);

		// The province holds its fifteen days, so the price is what was asked and no more.
		Assert.Equal(CFixedPoint.One, division.SuppliesReceived);
		Assert.Equal(CFixedPoint.FromInt(49), Supplies(state, rome));
		Assert.Equal(CFixedPoint.One, Supplies(state, berlin));
		Assert.Equal(CFixedPoint.One, italy.TradedAway[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.One, germany.TradedFor[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.One, germany.UnitDemand[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.Zero, italy.UnitDemand[EGoods.Supplies]);
	}

	[Fact]
	public void On_a_depot_held_by_nobodys_friend_a_unit_is_given_nothing()
	{
		int berlin = Land(Ger, industry: 5);
		int far = Land(Ger);
		Join(berlin, far);
		int warsaw = Land(Pol, industry: 5);
		CCountry germany = Country(Ger, berlin);
		CCountry poland = Country(Pol, warsaw);
		CUnit division = Division(poland, far);
		CGameState state = State();
		state.Province(far).Pool[EGoods.Supplies] = CFixedPoint.FromInt(15);

		CSupplyDraw.Draw(state, poland, division);

		Assert.Equal(CFixedPoint.Zero, division.SuppliesReceived);
		Assert.Equal(CFixedPoint.FromInt(15), Supplies(state, far));
		Assert.Equal(CFixedPoint.Zero, germany.UnitDemand[EGoods.Supplies]);
	}

	// --- what a unit brings ---

	[Fact]
	public void A_unit_takes_its_days_of_supplies_with_it_out_of_the_province_it_leaves()
	{
		int berlin = Land(Ger, industry: 5);
		int near = Land(Ger);
		int far = Land(Ger);
		Join(berlin, near, far);
		CCountry germany = Country(Ger, berlin);
		CUnit division = Division(germany, near);
		CGameState state = State();
		state.Province(near).Pool[EGoods.Supplies] = CFixedPoint.FromInt(20);

		CSupplyCarry.Bring(state, germany, division, from: near, into: far);

		Assert.Equal(CFixedPoint.FromInt(5), Supplies(state, near));
		Assert.Equal(CFixedPoint.FromInt(15), Supplies(state, far));
	}

	[Fact]
	public void A_unit_from_nowhere_brings_its_days_out_of_the_stockpile()
	{
		int berlin = Land(Ger, industry: 5);
		int near = Land(Ger);
		Join(berlin, near);
		CCountry germany = Country(Ger, berlin);
		CUnit division = Division(germany, near);
		CGameState state = State();
		state.Province(berlin).Pool[EGoods.Supplies] = CFixedPoint.FromInt(10);

		CSupplyCarry.Bring(state, germany, division, from: 0, into: near);

		// No more than there is.
		Assert.Equal(CFixedPoint.Zero, Supplies(state, berlin));
		Assert.Equal(CFixedPoint.FromInt(10), Supplies(state, near));
	}

	// --- where supplies are made ---

	[Fact]
	public void A_days_supplies_are_made_where_the_factories_are()
	{
		int berlin = Land(Ger, industry: 5);
		int essen = Land(Ger, industry: 1);
		Join(berlin, essen);
		CCountry germany = Country(Ger, berlin);
		CGameState state = State();
		state.Province(berlin).Modifier.Set(CIndustrialCapacity.ProvinceIc, CFixedPoint.FromInt(5));
		state.Province(essen).Modifier.Set(CIndustrialCapacity.ProvinceIc, CFixedPoint.FromInt(1));
		germany.CoreHomeIc = 6000;

		CSupplyMaking.ShareOut(state, germany, state.Stockpile(germany)!, EGoods.Supplies, CFixedPoint.FromInt(6));

		// A sixth is 0.166 once it is cut to the thousandth, and five sixths 0.833; the
		// six thousandths nobody got stay with the stockpile.
		Assert.Equal(CFixedPoint.Parse("0.996"), Supplies(state, essen));
		Assert.Equal(CFixedPoint.Parse("5.004"), Supplies(state, berlin));
		Assert.Equal(CFixedPoint.Parse("0.996"), germany.SentTo[EGoods.Supplies]);
	}

	// The sea between a stockpile and a depot that is no capital is in ConvoyTests.

	// --- a new game ---

	[Fact]
	public void A_new_game_fills_the_provinces_and_leaves_the_stockpile_as_it_was()
	{
		int berlin = Land(Ger, industry: 5);
		int near = Land(Ger);
		int far = Land(Ger);
		Join(berlin, near, far);
		CCountry germany = Country(Ger, berlin);
		CUnit division = Division(germany, far);
		CGameState state = State();
		state.Province(berlin).Pool[EGoods.Supplies] = CFixedPoint.FromInt(100);

		CSupplyStart.Fill(state);

		// Two provinces out, so four passes: two before anything reaches the division,
		// and two of it taking two days' worth and eating one.
		Assert.Equal(CFixedPoint.One, division.SuppliesReceived);
		Assert.Equal(CFixedPoint.FromInt(2), Supplies(state, far));
		Assert.Equal(CFixedPoint.Parse("2.05"), Supplies(state, near));
		Assert.Equal(CFixedPoint.FromInt(100), Supplies(state, berlin));
		Assert.Equal(CFixedPoint.Zero, germany.UnitDemand[EGoods.Supplies]);
	}

	[Fact]
	public void A_unit_the_filling_could_not_reach_is_given_its_days_out_of_the_stockpile()
	{
		int berlin = Land(Ger, industry: 5);
		int island = Land(Ger);                                 // no port, and joined to nothing
		CCountry germany = Country(Ger, berlin);
		CUnit division = Division(germany, island);
		CGameState state = State();
		state.Province(berlin).Pool[EGoods.Supplies] = CFixedPoint.FromInt(100);

		CSupplyStart.Fill(state);

		Assert.Equal(CFixedPoint.Zero, division.SuppliesReceived);
		Assert.Equal(CFixedPoint.FromInt(15), Supplies(state, island));
		Assert.Equal(CFixedPoint.FromInt(85), Supplies(state, berlin));
	}
}
