using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Land in no area handed to the enemy beside it, in each province's hour, against <c>docs/mechanics/war.md</c>, section
/// 11. The map is Cyp's at the Day of Infamy: a Soviet province of one level of infrastructure on the coast, and beside it
/// Finland's own ground and Soviet ground Finland holds, each of two levels.
/// </summary>
public class StrandedLandTests
{
	private static readonly CCountryTag Sov = new("SOV");
	private static readonly CCountryTag Fin = new("FIN");
	private static readonly CCountryTag Swe = new("SWE");

	private const int Cyp = 1;
	private const int Sea = 2;
	private const int Pecenga = 3;
	private const int Titovka = 4;
	private const int Murmansk = 5;

	private static CProvince Land(int id, CCountryTag owner, CCountryTag controller, int infrastructure, params int[] neighbours)
	{
		var province = new CProvince(id, $"p{id}", isSea: false) { Owner = owner, Controller = controller, Continent = "europe" };
		province.SetBuildingLevel(CTestRoads.Infrastructure, CFixedPoint.FromInt(infrastructure));
		var edges = new List<CProvinceEdge>();
		foreach (int neighbour in neighbours)
		{
			edges.Add(new CProvinceEdge(neighbour, EProvinceEdgeKind.Plain, 0, CFixedPoint.Zero));
		}
		province.Edges = edges.ToArray();
		return province;
	}

	/// <summary>Cyp beside the sea, Pecenga and Titovka; Murmansk beside nothing until a test joins it.</summary>
	private static CGameState World(bool atWar = true, int neighbourInfrastructure = 2)
	{
		CCountry Country(CCountryTag tag, int capital) => new() { Tag = tag, Name = tag.Value, DefinitionFile = "", Capital = capital };
		var war = new CWar("continuation");
		war.Attackers.Add(Fin);
		war.Defenders.Add(Sov);
		var provinces = new List<CProvince>
		{
			new(0, "", isSea: false),
			Land(Cyp, Sov, Sov, 1, Sea, Pecenga, Titovka),
			new(Sea, "sea", isSea: true),
			Land(Pecenga, Fin, Fin, neighbourInfrastructure, Cyp, Titovka),
			Land(Titovka, Sov, Fin, neighbourInfrastructure, Cyp, Pecenga),
			Land(Murmansk, Sov, Sov, 2),
		};
		return new CGameState(new CDate(1941, 12, 11), provinces,
			new[] { Country(Sov, Murmansk), Country(Fin, Pecenga), Country(Swe, 0) },
			buildings: CTestRoads.With(), wars: atWar ? new[] { war } : null);
	}

	private static void Join(CGameState state, int from, int to)
	{
		CProvince province = state.Province(from);
		var edges = new List<CProvinceEdge>(province.Edges) { new(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.Zero) };
		province.Edges = edges.ToArray();
	}

	[Fact]
	public void A_province_in_no_area_goes_to_the_enemy_holding_the_ground_in_an_area_beside_it()
	{
		CGameState state = World();
		Assert.True(CStrandedLand.Run(state, state.Province(Cyp)));
		Assert.Equal(Fin, state.Province(Cyp).Controller);
		Assert.Equal(Sov, state.Province(Cyp).Owner);
	}

	[Fact]
	public void Nothing_changes_at_peace_nor_in_an_area()
	{
		CGameState state = World(atWar: false);
		Assert.False(CStrandedLand.Run(state, state.Province(Cyp)));
		Assert.Equal(Sov, state.Province(Cyp).Controller);
		// Two levels put it in an area of its own.
		state = World();
		state.Province(Cyp).SetBuildingLevel(CTestRoads.Infrastructure, CFixedPoint.FromInt(2));
		Assert.False(CStrandedLand.Run(state, state.Province(Cyp)));
		Assert.Equal(Sov, state.Province(Cyp).Controller);
	}

	[Fact]
	public void Ground_of_its_own_in_an_area_beside_it_keeps_it()
	{
		CGameState state = World();
		Join(state, Cyp, Murmansk);
		Assert.False(CStrandedLand.Run(state, state.Province(Cyp)));
		Assert.Equal(Sov, state.Province(Cyp).Controller);
	}

	[Fact]
	public void An_impassable_way_is_not_looked_at()
	{
		CGameState state = World();
		CProvince cyp = state.Province(Cyp);
		var edges = new List<CProvinceEdge>(cyp.Edges) { new(Murmansk, EProvinceEdgeKind.Impassable, 0, CFixedPoint.Zero) };
		cyp.Edges = edges.ToArray();
		Assert.True(CStrandedLand.Run(state, cyp));
		Assert.Equal(Fin, cyp.Controller);
	}

	[Fact]
	public void With_every_neighbour_in_no_area_it_goes_to_the_enemy_holding_the_first_where_every_enemy_is_that_one()
	{
		CGameState state = World(neighbourInfrastructure: 1);
		Assert.True(CStrandedLand.Run(state, state.Province(Cyp)));
		Assert.Equal(Fin, state.Province(Cyp).Controller);
	}

	[Fact]
	public void With_every_neighbour_in_no_area_its_own_side_or_another_first_keeps_it()
	{
		// A neighbour of its own holder's, last in the walk.
		CGameState state = World(neighbourInfrastructure: 1);
		state.Province(Titovka).Controller = Sov;
		Assert.False(CStrandedLand.Run(state, state.Province(Cyp)));
		Assert.Equal(Sov, state.Province(Cyp).Controller);
		// A neutral holds the first: the enemy beside it is not that one.
		state = World(neighbourInfrastructure: 1);
		state.Province(Pecenga).Controller = Swe;
		Assert.False(CStrandedLand.Run(state, state.Province(Cyp)));
		Assert.Equal(Sov, state.Province(Cyp).Controller);
	}

	[Fact]
	public void The_sea_and_land_nobody_owns_are_left_alone()
	{
		CGameState state = World();
		Assert.False(CStrandedLand.Run(state, state.Province(Sea)));
		state.Province(Cyp).Owner = null;
		Assert.False(CStrandedLand.Run(state, state.Province(Cyp)));
	}
}
