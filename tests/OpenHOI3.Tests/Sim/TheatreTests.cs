using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Fronts and theatres, against <c>docs/mechanics/theatres.md</c>: an area's fronts and a country's, the front-worthy
/// table, a theatre of a front's own, the rest of an area placed and the fronts left given, the merging, the old theatres
/// kept by overlap, a front's chain, the headquarters adopted, made and disbanded, the readiness for a prepared war, the
/// names a country keeps, and what marks theatres. Germany, in the axis, owns 1 to 12 in a row and, apart, 13 to 15; Poland
/// owns 21 to 30 in a row, 21 beside 12 and 13; Holland owns 31 and 32, 31 beside 1. Every province is fully roaded.
/// </summary>
public class TheatreTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Hol = new("HOL");
	private static readonly CCountryTag Den = new("DEN");
	private static readonly CCountryTag Swe = new("SWE");
	private static readonly CCountryTag Fin = new("FIN");
	private static readonly CCountryTag Sov = new("SOV");
	private static readonly CCountryTag Nor = new("NOR");

	private static CSubUnitType HqBrigade()
	{
		var type = new CSubUnitType(CTheatreHeadquarters.HqBrigade, EUnitBranch.Land);
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(10));
		type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.FromInt(30));
		type.Stats.Values.Set(CSubUnitStats.BuildCostManpower, CFixedPoint.FromInt(3));
		return type;
	}

	private static CGameState World(bool eastPrussia = false)
	{
		CCountry Country(CCountryTag tag, int capital) => new() { Tag = tag, Name = tag.Value, DefinitionFile = "", Capital = capital };
		CCountry germany = Country(Ger, 6);
		germany.Faction = "axis";
		var countries = new List<CCountry> { germany, Country(Pol, 25), Country(Hol, 32), Country(Den, 0), Country(Swe, 0),
			Country(Fin, 0), Country(Sov, 0), Country(Nor, 0) };

		var neighbours = new Dictionary<int, List<int>>();
		void Join(int a, int b)
		{
			if (!neighbours.TryGetValue(a, out List<int>? fromA))
			{
				neighbours[a] = fromA = new List<int>();
			}
			if (!neighbours.TryGetValue(b, out List<int>? fromB))
			{
				neighbours[b] = fromB = new List<int>();
			}
			fromA.Add(b);
			fromB.Add(a);
		}
		for (int id = 1; id < 12; id++)
		{
			Join(id, id + 1);
		}
		for (int id = 21; id < 30; id++)
		{
			Join(id, id + 1);
		}
		Join(12, 21);
		Join(1, 31);
		Join(31, 32);
		if (eastPrussia)
		{
			Join(13, 14);
			Join(14, 15);
			Join(13, 22);
		}

		var provinces = new List<CProvince>();
		for (int id = 0; id <= 32; id++)
		{
			CCountryTag? owner = id switch
			{
				>= 1 and <= 12 => Ger,
				>= 13 and <= 15 when eastPrussia => Ger,
				>= 21 and <= 30 => Pol,
				31 or 32 => Hol,
				_ => null,
			};
			provinces.Add(new CProvince(id, $"p{id}", isSea: false)
			{
				Owner = owner,
				Controller = owner,
				Neighbours = neighbours.GetValueOrDefault(id)?.ToArray() ?? System.Array.Empty<int>(),
				CentreX = id * 10,
				Points = id == 6 ? 5 : 0,
			});
		}
		TestRoads.Pave(provinces);
		foreach (CProvince province in provinces)
		{
			province.Modifier.Set(CSupplyFigures.Infrastructure, CFixedPoint.One);
		}
		var fascism = new CIdeologyGroup("fascism", 200000, 200000) { Faction = "axis" };
		var democracy = new CIdeologyGroup("democracy", 0, -146000) { Faction = "allies" };
		return new CGameState(new CDate(1936, 1, 1), provinces.ToArray(), countries.ToArray(), buildings: new[] { TestRoads.Infra })
		{
			IdeologyGroups = new[] { fascism, democracy },
			MapWidth = 100000,
			SubUnitTypes = new Dictionary<string, CSubUnitType> { [CTheatreHeadquarters.HqBrigade] = HqBrigade() },
			NameTexts = new CUnitNameTexts { NamedTheatre = "$PROV$ HQ", Theatre = "$NUM$$ORDER$ Theatre", Brigade = "$NUM$$ORDER$ $TYPE$ Brigade",
				Ordinals = new[] { "th", "st", "nd", "rd" } },
		};
	}

	private static CSupplyArea AreaOf(CGameState state, int province) => CSupplyNetworks.Refresh(state).AreaOf(province)!;

	private static string Fronts(IEnumerable<CFront> fronts) => string.Join(" | ", fronts.Select(front => front.ToString()));

	// ---- fronts

	[Fact]
	public void An_areas_fronts_are_its_provinces_on_a_land_border_each_run_flooded_through_the_area()
	{
		CGameState state = World();
		IReadOnlyList<List<int>> fronts = CAreaFronts.Of(state, AreaOf(state, 1));
		Assert.Equal(new[] { "1", "12" }, fronts.Select(front => string.Join(" ", front)));
		// Made once and kept on the area.
		Assert.Same(fronts, CAreaFronts.Of(state, AreaOf(state, 5)));
	}

	[Fact]
	public void A_countrys_fronts_face_an_enemy_and_are_hot_at_war()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		Assert.Equal("HOL: 1 | POL: 12", Fronts(CCountryFronts.Of(state, germany, AreaOf(state, 1))));

		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		state.Wars.Add(war);
		Assert.Equal("HOL: 1 | POL hot: 12", Fronts(CCountryFronts.Of(state, germany, AreaOf(state, 1))));
	}

	[Fact]
	public void The_front_worthy_table_is_the_cached_slots_and_a_country_in_no_faction_measures_the_x_twice()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		Assert.True(CFrontWorthy.Is(state, germany, Pol));
		Assert.True(CFrontWorthy.Is(state, germany, Hol));
		Assert.True(CFrontWorthy.Is(state, germany, Den));
		Assert.False(CFrontWorthy.Is(state, germany, Swe));

		// Finland in no faction: Norway and Germany never, the Soviet Union always.
		CCountry finland = state.Country(Fin)!;
		Assert.False(CFrontWorthy.Is(state, finland, Nor));
		Assert.False(CFrontWorthy.Is(state, finland, Ger));
		Assert.True(CFrontWorthy.Is(state, finland, Sov));

		// The distance on the triangle counts the x difference twice and never the y.
		CCountry sweden = state.Country(Swe)!;
		CCountry poland = state.Country(Pol)!;
		sweden.AlignmentY = 900000;
		Assert.Equal(0, CFrontWorthy.AlignmentDistance(poland, sweden));
		sweden.AlignmentX = 127300;
		Assert.True(CFrontWorthy.AlignmentDistance(poland, sweden) >= 180000);
		Assert.True(CFrontWorthy.Is(state, poland, Swe));
	}

	// ---- making

	[Fact]
	public void A_front_with_enough_of_its_enemy_beside_it_is_a_theatre_and_the_rest_goes_to_it()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CTheatreBuilder.Build(state, germany);

		CTheatre theatre = Assert.Single(germany.Strategy.Theatres);
		Assert.Equal(1, theatre.Id);
		Assert.Equal(2, state.NextTheatreId);
		// Poland's ten provinces spill over 8000: Poland's front is its own; Holland's two do not, and its front is given after.
		Assert.Equal("POL: 12 | HOL: 1", Fronts(theatre.Fronts));
		// The rest of the area placed outward from the front, and the front provinces kept off the theatre's own.
		Assert.Equal(new[] { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 }, theatre.Provinces);
		Assert.All(Enumerable.Range(1, 12), id => Assert.Equal(1, state.Province(id).TheatreId));
		Assert.False(germany.Strategy.TheatresDirty);
	}

	[Fact]
	public void A_small_theatre_merges_into_its_nearest_and_the_merge_keeps_the_fronts_as_they_are()
	{
		CGameState state = World(eastPrussia: true);
		CCountry germany = state.Country(Ger)!;
		CTheatreBuilder.Build(state, germany);

		// Two theatres made, one for each Polish front; the small one went into the large.
		CTheatre theatre = Assert.Single(germany.Strategy.Theatres);
		Assert.Equal(1, theatre.Id);
		Assert.Equal(3, state.NextTheatreId);
		Assert.Equal("POL: 12 | HOL: 1 | POL: 13", Fronts(theatre.Fronts));
		Assert.Contains(14, theatre.Provinces);
	}

	[Fact]
	public void A_new_theatre_overlapping_half_of_an_old_one_is_the_old_one_under_its_number()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CTheatreBuilder.Build(state, germany);
		CTheatreBuilder.Build(state, germany);

		CTheatre theatre = Assert.Single(germany.Strategy.Theatres);
		Assert.Equal(1, theatre.Id);
		// The second making took a number of its own all the same.
		Assert.Equal(3, state.NextTheatreId);
		Assert.Equal(new[] { 1 }, state.Theatres.Keys);
	}

	[Fact]
	public void A_front_is_put_back_as_a_chain_from_an_end()
	{
		CGameState state = World();
		var theatre = new CTheatre(9, Ger);
		var front = new CFront { Enemy = Pol };
		front.Provinces.AddRange(new[] { 4, 5, 3 });
		CTheatreProvinces.AddFront(state, theatre, front, reorder: true);
		// The first province of the front with fewer than two of the others beside it starts the chain.
		Assert.Equal(new[] { 5, 4, 3 }, front.Provinces);
		Assert.Equal(new[] { 5, 4, 3 }, theatre.AllProvinces);
		Assert.Equal(9, state.Province(4).TheatreId);
	}

	// ---- headquarters

	[Fact]
	public void A_theatre_level_unit_standing_in_a_theatre_becomes_its_headquarters()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		var command = new CUnit(EUnitKind.Theatre, Ger) { Id = 50, Location = 4, Name = "OB West" };
		germany.Units.Add(command);
		CTheatreBuilder.Build(state, germany);

		Assert.Equal(50, germany.Strategy.Theatres[0].Headquarters);
		Assert.Single(germany.Units);
	}

	[Fact]
	public void A_theatre_with_no_headquarters_is_given_one_behind_its_front_with_a_fifth_of_a_brigade()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		state.LastUnitId = 99;
		var orphan = new CUnit(EUnitKind.Division, Ger) { Id = 60, Location = 3, IsOrphaned = true };
		germany.Units.Add(orphan);
		CTheatreBuilder.Build(state, germany);

		CTheatre theatre = germany.Strategy.Theatres[0];
		Assert.Equal(100, theatre.Headquarters);
		Assert.Equal(101, state.LastUnitId);
		CUnit headquarters = germany.Units.Single(unit => unit.Kind == EUnitKind.Theatre);
		Assert.Equal(100, headquarters.Id);
		// Named after the theatre's province with most victory points.
		Assert.Equal("p6 HQ", headquarters.Name);
		CSubUnit brigade = Assert.Single(headquarters.SubUnits);
		Assert.Equal(101, brigade.Id);
		Assert.Equal(CFixedPoint.FromInt(2), brigade.Strength);
		Assert.Equal("1st hq_brigade_short Brigade", brigade.Name);
		// The orphan is put under it.
		Assert.Same(headquarters, orphan.Parent);
		Assert.False(orphan.IsOrphaned);
		// Of the theatre's provinces with roads enough, the score falls nearest to 150 behind the front: here the farthest
		// from 1 and 12 is nearest, and the capital's third favours it.
		Assert.Equal(6, headquarters.Location);
	}

	[Fact]
	public void A_theatre_level_unit_heading_no_theatre_is_disbanded_in_the_countrys_hour_before_the_game_runs()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		var first = new CUnit(EUnitKind.Theatre, Ger) { Id = 50, Location = 4 };
		var second = new CUnit(EUnitKind.Theatre, Ger) { Id = 51, Location = 5 };
		second.SubUnits.Add(new CSubUnit(HqBrigade()) { Id = 52, Unit = second, Strength = CFixedPoint.FromInt(5) });
		germany.Units.Add(first);
		germany.Units.Add(second);
		CTheatreBuilder.Build(state, germany);

		var disband = Assert.IsType<CDisbandCommand>(Assert.Single(state.Outbox));
		germany.Manpower = CFixedPoint.Zero;
		disband.Execute(state);
		Assert.True(second.IsRemoved);
		Assert.DoesNotContain(second, germany.Units);
		// Half its strength, half the men it cost back.
		Assert.Equal(CFixedPoint.Parse("1.5"), germany.Manpower);
	}

	// ---- a pact's border (agreements.md, section 6)

	private static void Brigades(CGameState state, CCountryTag owner, int province, int brigades, int id)
	{
		var type = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		var unit = new CUnit(EUnitKind.Army, owner) { Id = id, Location = province, Name = "Unit" };
		for (int i = 0; i < brigades; i++)
		{
			unit.SubUnits.Add(new CSubUnit(type));
		}
		state.Country(owner)!.Units.Add(unit);
		state.ForgetPositions();
	}

	[Fact]
	public void A_pact_counts_brigades_along_the_border_across_every_block_of_the_first_party()
	{
		CGameState state = World(eastPrussia: true);
		Brigades(state, Ger, 12, 3, 901);
		Brigades(state, Pol, 21, 2, 902);
		Brigades(state, Pol, 22, 2, 903);
		var nap = new OpenHOI3.Sim.Agreements.CNap(Ger, Pol, state.TotalHours);
		OpenHOI3.Sim.Agreements.CNapBalance.Measure(state, nap);
		Assert.Equal(3, nap.OurPower);
		Assert.Equal(4, nap.TheirPower);
		Assert.Equal(750, nap.Balance);
		Assert.Equal(1333, OpenHOI3.Sim.Agreements.CNapBalance.As(nap, Pol));
	}

	[Fact]
	public void A_pact_with_nothing_of_theirs_on_the_border_is_a_million_and_a_nought_turned_round_is_minus_one()
	{
		CGameState state = World();
		var nap = new OpenHOI3.Sim.Agreements.CNap(Ger, Pol, state.TotalHours);
		OpenHOI3.Sim.Agreements.CNapBalance.Measure(state, nap);
		Assert.Equal(1000000, nap.Balance);
		nap.Balance = 0;
		Assert.Equal(-1, OpenHOI3.Sim.Agreements.CNapBalance.As(nap, Pol));
		Assert.Equal(0, OpenHOI3.Sim.Agreements.CNapBalance.As(nap, Ger));
	}

	[Fact]
	public void A_pact_may_be_broken_in_a_window_only_on_the_balance_and_after_them_always()
	{
		CGameState state = World();
		foreach ((string key, int value) in new[] { ("NAP_UNBREAKABLE_MONTHS", 6), ("NAP_FORCE_BALANCE_RULE_MONTHS", 6), ("NAP_BREAK_FORCE_BALANCE_1", 2), ("NAP_BREAK_FORCE_BALANCE_2", 1), ("NAP_BREAK_FORCE_BALANCE_3", 1) })
		{
			state.Defines.Set("diplomacy", key, CFixedPoint.FromInt(value));
		}
		CCountry germany = state.Country(Ger)!;
		CCountry poland = state.Country(Pol)!;
		var nap = new OpenHOI3.Sim.Agreements.CNap(Ger, Pol, OpenHOI3.Sim.Agreements.COfferRules.AddMonths(state.TotalHours, -7)) { Balance = 2000 };
		state.Naps.Add(nap);
		Assert.True(OpenHOI3.Sim.Agreements.COfferRules.CanBreakNap(state, germany, poland));
		Assert.False(OpenHOI3.Sim.Agreements.COfferRules.CanBreakNap(state, poland, germany));
		nap.Balance = 1999;
		Assert.False(OpenHOI3.Sim.Agreements.COfferRules.CanBreakNap(state, germany, poland));
		state.Naps.Clear();
		state.Naps.Add(new OpenHOI3.Sim.Agreements.CNap(Ger, Pol, OpenHOI3.Sim.Agreements.COfferRules.AddMonths(state.TotalHours, -24)));
		Assert.True(OpenHOI3.Sim.Agreements.COfferRules.CanBreakNap(state, germany, poland));
	}

	// ---- readiness

	[Fact]
	public void A_prepared_war_is_ready_with_no_front_and_waits_on_an_empty_one_until_fit_units_outweigh_the_enemy()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		Assert.True(CWarReadiness.IsReady(state, germany, new CWarTarget(new CCountryTag("SWE"), null, false, state.TotalHours)));
		var poland = new CWarTarget(Pol, null, false, state.TotalHours);
		Assert.False(CWarReadiness.IsReady(state, germany, poland));

		var division = new CUnit(EUnitKind.Division, Ger) { Id = 70, Location = 12 };
		division.SubUnits.Add(new CSubUnit(HqBrigade()) { Id = 71, Unit = division, Strength = CFixedPoint.FromInt(10),
			Organisation = CFixedPoint.FromInt(30) });
		germany.Units.Add(division);
		state.ForgetPositions();
		Assert.True(CWarReadiness.IsReady(state, germany, poland));
	}

	// ---- what marks them, and names

	[Fact]
	public void A_change_of_hands_marks_the_old_and_new_holder_and_every_owner_beside()
	{
		CGameState state = World();
		foreach (CCountry country in state.Countries)
		{
			country.Strategy.TheatresDirty = false;
		}
		CProvinceHands.SetController(state, state.Province(12), Pol);
		Assert.True(state.Country(Ger)!.Strategy.TheatresDirty);
		Assert.True(state.Country(Pol)!.Strategy.TheatresDirty);
		Assert.False(state.Country(Hol)!.Strategy.TheatresDirty);
	}

	[Fact]
	public void A_name_is_taken_only_within_its_country()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CCountry poland = state.Country(Pol)!;
		CUnitNames.Register(germany, "p6 HQ");
		Assert.Equal("2nd Theatre", CUnitNames.ForTheatre(state, germany, "p6"));
		Assert.Equal("p6 HQ", CUnitNames.ForTheatre(state, poland, "p6"));
		Assert.Equal("1st Theatre", CUnitNames.ForTheatre(state, poland, null));
	}
}
