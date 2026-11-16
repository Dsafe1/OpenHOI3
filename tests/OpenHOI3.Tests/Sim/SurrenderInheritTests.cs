using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// <c>surrender_inherit</c>'s surrender mode, against <c>docs/mechanics/war.md</c>, section 13
/// (<c>reversing/findings/FINDINGS-surrender.md</c>, A.3 steps 2, 4 and 8). The map, west to east:
///
///     5 Sea   1 Anx --- 2 Anx --- 3 Par
///               |
///             4 Two --- 6 Ger
///
/// Germany is at war with Anx, Par and Two, who fight side by side; Neu fights nobody. Anx's two provinces are one area,
/// with Par's area beside 2 and Two's beside 1 - Two's nearer to 1, Par's to 2. Anx is Italy, Par Britain and Two
/// Romania, tags the script fixture knows.
/// </summary>
public class SurrenderInheritTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Anx = new("ITA");
	private static readonly CCountryTag Par = new("ENG");
	private static readonly CCountryTag Two = new("ROM");
	private static readonly CCountryTag Neu = new("NEU");

	private static CProvince Land(int id, CCountryTag owner, int x, params int[] neighbours)
	{
		var province = new CProvince(id, $"p{id}", isSea: false)
		{
			Owner = owner, Controller = owner, Continent = "europe", CentreX = x, CentreY = id == 4 || id == 6 ? 10 : 0,
		};
		province.SetBuildingLevel(CTestRoads.Infrastructure, CFixedPoint.FromInt(3));
		var edges = new List<CProvinceEdge>();
		foreach (int neighbour in neighbours)
		{
			edges.Add(new CProvinceEdge(neighbour, EProvinceEdgeKind.Plain, 0, CFixedPoint.Zero));
		}
		province.Edges = edges.ToArray();
		return province;
	}

	private static CGameState World(bool atWar = true)
	{
		CCountry Country(CCountryTag tag, int capital) => new() { Tag = tag, Name = tag.Value, DefinitionFile = "", Capital = capital };
		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Anx);
		war.Defenders.Add(Par);
		war.Defenders.Add(Two);
		var provinces = new List<CProvince>
		{
			new(0, "", isSea: false),
			Land(1, Anx, 0, 2, 4, 5),
			Land(2, Anx, 10, 1, 3),
			Land(3, Par, 20, 2),
			Land(4, Two, 0, 1, 6),
			new(5, "Sea", isSea: true) { Edges = new[] { new CProvinceEdge(1, EProvinceEdgeKind.Plain, 0, CFixedPoint.Zero) } },
			Land(6, Ger, 10, 4),
		};
		return new CGameState(new CDate(1940, 6, 1), provinces,
			new[] { Country(Ger, 6), Country(Anx, 1), Country(Par, 3), Country(Two, 4), Country(Neu, 0) },
			buildings: CTestRoads.With(), wars: atWar ? new[] { war } : null);
	}

	private static CUnit Unit(CGameState state, int id, EUnitKind kind, int location, int baseProvince = 0)
	{
		var unit = new CUnit(kind, Anx) { Id = id, Location = location, Base = baseProvince };
		unit.SubUnits.Add(new CSubUnit(new CSubUnitType("x", EUnitBranch.Land)) { Id = id + 1000, Unit = unit });
		state.Country(Anx)!.Units.Add(unit);
		return unit;
	}

	[Fact]
	public void The_candidates_are_the_annexed_countrys_partners_at_war_with_the_annexer_in_the_countries_order()
	{
		CGameState state = World();

		Assert.Equal(new[] { Par, Two },
			CAnnexation.Candidates(state, state.Country(Ger)!, state.Country(Anx)!).ConvertAll(c => c.Tag));
		Assert.Empty(CAnnexation.Candidates(World(atWar: false), state.Country(Ger)!, state.Country(Anx)!));
	}

	[Fact]
	public void The_annexer_owns_the_land_and_the_candidate_nearest_each_province_holds_it()
	{
		CGameState state = World();

		CEffects.Run(state, ScriptWorld.Effects("surrender_inherit = ITA"), CEventScope.Start(Ger));

		Assert.Equal(Ger, state.Province(1).Owner);
		Assert.Equal(Two, state.Province(1).Controller);
		Assert.Equal(Ger, state.Province(2).Owner);
		Assert.Equal(Par, state.Province(2).Controller);
	}

	[Fact]
	public void Inherit_and_a_surrender_with_nobody_to_take_over_give_the_annexer_everything()
	{
		CGameState state = World();
		CUnit unit = Unit(state, 50, EUnitKind.Division, 3);

		CEffects.Run(state, ScriptWorld.Effects("inherit = ITA"), CEventScope.Start(Ger));

		Assert.Equal(Ger, state.Province(1).Controller);
		Assert.Equal(Ger, state.Province(2).Controller);
		Assert.True(unit.IsRemoved);
		Assert.Equal(LandDraws(surrender: false), state.Random.Draws);
	}

	/// <summary>
	/// The draws an annexation of the bare map makes - the land changing hands while at war draws for the buildings taken -
	/// so that a test can tell the units' from them.
	/// </summary>
	private static long LandDraws(bool surrender, uint seed = 1)
	{
		CGameState bare = World();
		bare.Random.Seed(seed);
		CAnnexation.Annex(bare, bare.Country(Ger)!, bare.Country(Anx)!, surrender);
		return bare.Random.Draws;
	}

	/// <summary>Whether each of the draws after the land's is even.</summary>
	private static bool[] UnitDrawsEven(int count, uint seed = 1)
	{
		var probe = new CRandom(seed);
		probe.Skip(LandDraws(surrender: true, seed));
		var even = new bool[count];
		for (int i = 0; i < count; i++)
		{
			even[i] = (probe.Next() & 1) == 0;
		}
		return even;
	}

	[Fact]
	public void A_unit_with_a_candidate_goes_over_on_an_even_draw_and_one_without_takes_no_draw()
	{
		CGameState state = World();
		// On a candidate's ground; on the annexer's; an air unit over its own land but based on Two's; a fleet at sea. Its
		// own land is a candidate's by the time the units are judged, so a unit there would draw too.
		CUnit onPar = Unit(state, 50, EUnitKind.Division, 3);
		CUnit onGerman = Unit(state, 51, EUnitKind.Division, 6);
		CUnit air = Unit(state, 52, EUnitKind.Air, 1, baseProvince: 4);
		CUnit fleet = Unit(state, 53, EUnitKind.Navy, 5);
		bool[] even = UnitDrawsEven(3);

		CEffects.Run(state, ScriptWorld.Effects("surrender_inherit = ITA"), CEventScope.Start(Ger));

		Assert.Equal(LandDraws(surrender: true) + 3, state.Random.Draws);
		Assert.True(onGerman.IsRemoved);
		(CUnit Unit, CCountryTag To)[] judged = { (onPar, Par), (air, Two), (fleet, Par) };
		for (int i = 0; i < judged.Length; i++)
		{
			Assert.Equal(!even[i], judged[i].Unit.IsRemoved);
			if (even[i])
			{
				Assert.Equal(judged[i].To, judged[i].Unit.Owner);
				Assert.Contains(judged[i].Unit, state.Country(judged[i].To)!.Units);
			}
		}
	}

	[Fact]
	public void A_unit_going_over_leaves_the_units_under_it_behind_each_judged_in_its_turn()
	{
		CGameState state = World();
		CUnit corps = Unit(state, 50, EUnitKind.Corps, 3);
		var division = new CUnit(EUnitKind.Division, Anx) { Id = 60, Location = 1, Parent = corps };
		division.SubUnits.Add(new CSubUnit(new CSubUnitType("x", EUnitBranch.Land)) { Id = 61, Unit = division });
		corps.Children.Add(division);
		// Find a seed whose first draw is even, so the corps goes over.
		uint seed = 1;
		while (!UnitDrawsEven(1, seed)[0])
		{
			seed++;
		}
		state.Random.Seed(seed);

		CAnnexation.Annex(state, state.Country(Ger)!, state.Country(Anx)!, surrender: true);

		Assert.Equal(Par, corps.Owner);
		Assert.Empty(corps.Children);
		// The division stood on Anx's own ground, now Two's: it is judged too, after the corps.
		Assert.Null(division.Parent);
		Assert.Equal(LandDraws(surrender: true, seed) + 2, state.Random.Draws);
	}
}
