using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Wars;
using Xunit;

namespace OpenHOI3.Tests.Sim;

/// <summary>A puppet's dues and a war nobody declared, against <c>stockpile.md</c>, section 7, and <c>war.md</c>, section 15.</summary>
public class TributeTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Slo = new("SLO");

	private static CGameState World(out CCountry germany, out CCountry slovakia)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1, ActingCapital = 1 };
		slovakia = new CCountry { Tag = Slo, Name = "Slovakia", DefinitionFile = "", Capital = 2, ActingCapital = 2, Overlord = Ger };
		CProvinceEdge Edge(int to) => new(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.FromInt(10));
		var provinces = new[]
		{
			new CProvince(0, "", isSea: false),
			new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger, Edges = new[] { Edge(2) } },
			new CProvince(2, "Bratislava", isSea: false) { Owner = Slo, Controller = Slo, Edges = new[] { Edge(1) } },
		};
		var roads = new CBuilding(0, "infra", 10, OpenHOI3.Sim.Missions.CBombingOrders.Infrastructure, CFixedPoint.Parse("0.1"));
		provinces[1].SetBuildingLevel(roads, CFixedPoint.FromInt(5));
		provinces[2].SetBuildingLevel(roads, CFixedPoint.FromInt(5));
		return new CGameState(new CDate(1936, 1, 1), provinces, new[] { germany, slovakia }, buildings: new[] { roads });
	}

	[Fact]
	public void A_puppet_pays_what_it_holds_above_two_thousand_but_never_supplies()
	{
		CGameState state = World(out CCountry germany, out CCountry slovakia);
		state.Stockpile(slovakia)![EGoods.Metal] = CFixedPoint.FromInt(2500);
		state.Stockpile(slovakia)![EGoods.Energy] = CFixedPoint.FromInt(100);
		state.Stockpile(slovakia)![EGoods.Supplies] = CFixedPoint.FromInt(9000);

		CTribute.RunDaily(state);

		Assert.Equal(CFixedPoint.FromInt(2000), state.Stockpile(slovakia)![EGoods.Metal]);
		Assert.Equal(CFixedPoint.FromInt(500), state.Stockpile(germany)![EGoods.Metal]);
		Assert.Equal(CFixedPoint.FromInt(500), germany.TributeReceived[EGoods.Metal]);
		Assert.Equal(CFixedPoint.FromInt(-500), slovakia.TributeSent[EGoods.Metal]);
		Assert.Equal(CFixedPoint.FromInt(100), state.Stockpile(slovakia)![EGoods.Energy]);
		Assert.Equal(CFixedPoint.FromInt(9000), state.Stockpile(slovakia)![EGoods.Supplies]);
	}

	[Fact]
	public void A_puppet_whose_government_has_fled_pays_nothing()
	{
		CGameState state = World(out CCountry germany, out CCountry slovakia);
		state.Stockpile(slovakia)![EGoods.Metal] = CFixedPoint.FromInt(2500);
		slovakia.Capital = 1;

		CTribute.RunDaily(state);

		Assert.Equal(CFixedPoint.Zero, germany.TributeReceived[EGoods.Metal]);
	}

	[Fact]
	public void A_declaration_ends_an_undeclared_war_and_it_was_hostile_only_in_its_regions()
	{
		CGameState state = World(out _, out _);
		state.Province(2).Regions.Add(0);
		var world = new CGameState(new CDate(1936, 1, 1), new[] { state.Province(0), state.Province(1), state.Province(2) }, new[] { state.Country(Ger)!, state.Country(Slo)! })
		{
			Regions = new[] { new CRegion("atlantic", new[] { 2 }, false) },
		};
		CUndeclaredWars.AddRegion(world, Ger, Slo, "atlantic");

		Assert.True(OpenHOI3.Sim.Movement.CMovementRules.IsEnemyIn(world, Ger, Slo, 2));
		Assert.False(OpenHOI3.Sim.Movement.CMovementRules.IsEnemyIn(world, Ger, Slo, 1));
		Assert.False(OpenHOI3.Sim.Movement.CMovementRules.IsEnemy(world, Ger, Slo));
		CUndeclaredWars.End(world, Slo, Ger);
		Assert.Empty(world.UndeclaredWars);
	}
}
