using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The engine's side of the tech and production ministers, against <c>docs/mechanics/ministers.md</c>: their hours, a
/// finished level taken off an AI's queue, the AI's commands written as given, desperation, the roles a finished
/// brigade is placed by, the placing itself and the build queue's priority. Germany holds Berlin (1), Kiel (2) and Lodz
/// (3), all paved, Berlin its capital; Poland holds Posen (4), beside Berlin.
/// </summary>
public class MinisterTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private static CSubUnitType Type(string key, string width = "1", string softness = "1", string speed = "4")
	{
		var type = new CSubUnitType(key, EUnitBranch.Land);
		type.Stats.Values.Set(CSubUnitType.CombatWidth, CFixedPoint.Parse(width));
		type.Stats.Values.Set(CUnitTotals.Softness, CFixedPoint.Parse(softness));
		type.Stats.Values.Set("maximum_speed", CFixedPoint.Parse(speed));
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(3));
		type.Stats.Values.Set(CSubUnitStats.BuildCostManpower, CFixedPoint.FromInt(3));
		return type;
	}

	private static readonly CSubUnitType Infantry = Type("infantry_brigade");
	private static readonly CSubUnitType Armour = Type("armor_brigade", softness: "0.2", speed: "6");
	private static readonly CSubUnitType Artillery = Type("artillery_brigade", width: "0");

	private static CGameState World(bool atWar = false)
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1, Manpower = CFixedPoint.FromInt(100) };
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "", Capital = 4 };
		CProvince Land(int id, CCountryTag owner, params int[] beside) =>
			new(id, $"p{id}", isSea: false) { Owner = owner, Controller = owner, Neighbours = beside, Continent = "europe" };
		CProvince berlin = Land(1, Ger, 2, 4);
		CProvince kiel = Land(2, Ger, 1, 3);
		CProvince lodz = Land(3, Ger, 2);
		CProvince posen = Land(4, Pol, 1);
		var tsingtao = new CProvince(5, "p5", isSea: false) { Owner = Ger, Controller = Ger, Continent = "asia" };
		foreach (CProvince province in new[] { berlin, kiel, lodz })
		{
			province.Cores.Add(Ger);
		}
		TestRoads.Pave(new[] { berlin, kiel, lodz, posen, tsingtao });
		var wars = new List<CWar>();
		if (atWar)
		{
			var war = new CWar("war");
			war.Attackers.Add(Ger);
			war.Defenders.Add(Pol);
			wars.Add(war);
		}
		var types = new Dictionary<string, CSubUnitType>(System.StringComparer.OrdinalIgnoreCase)
		{
			[Infantry.Key] = Infantry, [Armour.Key] = Armour, [Artillery.Key] = Artillery,
		};
		var defines = new CDefines();
		defines.Set(CManpower.MilitarySection, CUnitAvailability.BrigadesInDivision, CFixedPoint.FromInt(4));
		return new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false), berlin, kiel, lodz, posen, tsingtao },
			new[] { germany, poland }, buildings: new[] { TestRoads.Infra }, wars: wars, defines: defines)
		{
			SubUnitTypes = types,
		};
	}

	private static CUnit Division(CGameState state, CCountry owner, int province, params CSubUnitType[] brigades)
	{
		var unit = new CUnit(EUnitKind.Division, owner.Tag) { Id = ++state.LastUnitId, Location = province };
		foreach (CSubUnitType type in brigades)
		{
			var brigade = new CSubUnit(type) { Id = ++state.LastUnitId, Unit = unit, Strength = type.Stats[CSubUnitType.MaxStrength] };
			brigade.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(brigade);
		}
		owner.Units.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	// ---- the ministers' hours and research

	[Fact]
	public void Each_minister_works_in_its_countrys_number_and_its_own_offset()
	{
		CCountry germany = World().Country(Ger)!;

		// The number counts the original's placeholder country: the first is 1, and the tech minister adds 3.
		Assert.Equal((germany.Index + 1 + 3) % 24, CResearchAi.Hour(germany, 3));
		Assert.Equal((germany.Index + 1 + 5) % 24, CResearchAi.Hour(germany, 5));
	}

	[Fact]
	public void A_finished_level_is_taken_off_the_queue_of_a_country_with_a_tech_minister_and_left_on_anothers()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CCountry poland = state.Country(Pol)!;
		var technology = new CTechnology("smallarms", 10, new CModifier("smallarms")) { AdditionalOffset = 1 };
		germany.ResearchQueue.Add("smallarms");
		poland.ResearchQueue.Add("smallarms");
		germany.TechCountdown = 1;

		CResearchAi.LevelResearched(state, germany, new[] { technology });
		CResearchAi.LevelResearched(state, poland, new[] { technology });

		CCommand stop = Assert.Single(state.Outbox);
		Assert.IsType<CStopResearchCommand>(stop);
		stop.Execute(state);
		Assert.Empty(germany.ResearchQueue);
		Assert.Single(poland.ResearchQueue);
		// Taken off already, a second is refused.
		Assert.False(stop.MayExecute(state));
	}

	[Fact]
	public void A_research_start_goes_to_the_end_and_refuses_only_a_technology_queued_already()
	{
		var technologies = new[] { new CTechnology("a", 1, new CModifier("a")), new CTechnology("b", 1, new CModifier("b")) };
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1 };
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false) }, new[] { germany },
			technologies: technologies);
		germany.ResearchQueue.Add("a");

		var start = new CStartResearchCommand(Ger, "b");
		Assert.True(start.MayExecute(state));
		start.Execute(state);
		Assert.Equal(new[] { "a", "b" }, germany.ResearchQueue);
		Assert.False(new CStartResearchCommand(Ger, "a").MayExecute(state));
	}

	// ---- the AI's commands

	[Fact]
	public void The_AIs_shares_are_written_as_given_even_a_little_over_the_whole()
	{
		CGameState state = World();
		var shares = new[] { new CFixed15(2), new CFixed15(2), new CFixed15(16384), new CFixed15(16384), new CFixed15(0), new CFixed15(0) };

		var command = new CChangeInvestmentCommand(Ger, shares);
		Assert.True(command.MayExecute(state));
		command.Execute(state);

		Assert.Equal(shares, state.Country(Ger)!.ProductionShares);
	}

	[Fact]
	public void The_AIs_unit_order_refuses_only_no_brigades_and_a_run_of_none_takes_no_men()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;

		Assert.False(new CConstructUnitCommand(Ger, System.Array.Empty<string>(), 1, false).MayExecute(state));
		Assert.False(new CConstructUnitCommand(Ger, new[] { "" }, 1, false).MayExecute(state));

		var none = new CConstructUnitCommand(Ger, new[] { "infantry_brigade", "infantry_brigade" }, 0, false);
		Assert.True(none.MayExecute(state));
		none.Execute(state);
		CConstruction line = Assert.Single(germany.Constructions);
		Assert.Equal(0, line.Size);
		Assert.Equal(CFixedPoint.FromInt(100), germany.Manpower);

		// A run of two takes both units' men at once.
		new CConstructUnitCommand(Ger, new[] { "infantry_brigade" }, 2, false).Execute(state);
		Assert.Equal(CFixedPoint.FromInt(94), germany.Manpower);
	}

	// ---- desperation

	[Fact]
	public void Desperation_is_nought_at_peace_and_at_war_the_share_of_cores_near_the_capital_not_held()
	{
		Assert.Equal(0, CDesperation.Of(World(), World().Country(Ger)!));

		CGameState state = World(atWar: true);
		CCountry germany = state.Country(Ger)!;
		Assert.Equal(0, CDesperation.Of(state, germany));

		// Lodz falls: one of three like provinces, a third desperate - and Lodz's area is now Poland's, beside Germany's.
		state.Province(3).Controller = Pol;
		Assert.Equal(1000 - 2 * 1000 * 1000 / (3 * 1000), CDesperation.Of(state, germany));
	}

	[Fact]
	public void A_province_weighs_its_manpower_and_one_and_a_fifth_of_it_away_from_its_owners_capital()
	{
		CGameState state = World();
		CProvince kiel = state.Province(2);
		kiel.Manpower = CFixedPoint.FromInt(9);
		Assert.Equal(10, CDesperation.Weight(state, kiel));

		// Neither in the capital's area nor on its continent: a fifth, at least one.
		CProvince tsingtao = state.Province(5);
		tsingtao.Manpower = CFixedPoint.FromInt(9);
		Assert.Equal(2, CDesperation.Weight(state, tsingtao));
		tsingtao.Manpower = CFixedPoint.Zero;
		Assert.Equal(1, CDesperation.Weight(state, tsingtao));
	}

	// ---- roles and placing

	[Fact]
	public void A_brigade_and_a_division_have_the_originals_roles()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;

		Assert.Equal(CArmyRole.Infantry, CArmyRole.Of(Infantry));
		Assert.Equal(CArmyRole.Armour, CArmyRole.Of(Armour));
		Assert.Equal(CArmyRole.Support, CArmyRole.Of(Artillery));

		Assert.Equal(CArmyRole.Empty, CArmyRole.Of(Division(state, germany, 1)));
		Assert.Equal(CArmyRole.Infantry, CArmyRole.Of(Division(state, germany, 1, Infantry, Infantry, Artillery)));
		// Armour on average wins.
		Assert.Equal(CArmyRole.Armour, CArmyRole.Of(Division(state, germany, 1, Armour, Armour, Infantry)));
		// Half or more narrow is support.
		Assert.Equal(CArmyRole.Support, CArmyRole.Of(Division(state, germany, 1, Infantry, Artillery)));

		Assert.Equal(1, CArmyRole.FactorInHalves(CArmyRole.Armour, CArmyRole.Armour));
		Assert.Equal(6, CArmyRole.FactorInHalves(CArmyRole.Armour, CArmyRole.Fast));
		Assert.Equal(0, CArmyRole.FactorInHalves(CArmyRole.Headquarters, CArmyRole.Infantry));
	}

	[Fact]
	public void A_lone_brigade_joins_the_emptiest_fitting_division_near_the_capital()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CUnit three = Division(state, germany, 1, Infantry, Infantry, Infantry);
		CUnit two = Division(state, germany, 2, Infantry, Infantry);
		Division(state, germany, 1, Infantry, Infantry, Infantry, Infantry);
		var waiting = new CUnit(EUnitKind.Division, Ger) { Id = ++state.LastUnitId };
		waiting.SubUnits.Add(new CSubUnit(Infantry) { Unit = waiting });
		germany.Deployments.Add(waiting);

		CAiDeployment.Deploy(state, germany);

		// Two brigades score 20, three 30; the full one takes none.
		CDeployUnitCommand deploy = Assert.IsType<CDeployUnitCommand>(Assert.Single(state.Outbox));
		Assert.True(deploy.MayExecute(state));
		deploy.Execute(state);
		Assert.Equal(3, two.SubUnits.Count);
		Assert.Equal(3, three.SubUnits.Count);
	}

	[Fact]
	public void A_unit_that_joins_nothing_goes_to_the_capital_s_area_s_best_province_the_capital_first()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		var waiting = new CUnit(EUnitKind.Division, Ger) { Id = ++state.LastUnitId };
		waiting.SubUnits.Add(new CSubUnit(Infantry) { Unit = waiting });
		waiting.SubUnits.Add(new CSubUnit(Infantry) { Unit = waiting });
		germany.Deployments.Add(waiting);

		Assert.Equal(1, CAiDeployment.Place(state, germany, waiting));
	}

	[Fact]
	public void A_played_countrys_placed_unit_is_put_under_the_headquarters_of_its_provinces_theatre()
	{
		(CGameState State, CUnit Waiting, CUnit Headquarters) Made()
		{
			CGameState state = World();
			CCountry germany = state.Country(Ger)!;
			var headquarters = new CUnit(EUnitKind.Theatre, Ger) { Id = ++state.LastUnitId, Location = 1, Name = "OB West" };
			germany.Units.Add(headquarters);
			var theatre = new OpenHOI3.Sim.Theatres.CTheatre(7, Ger) { Headquarters = headquarters.Id };
			state.Theatres[theatre.Id] = theatre;
			OpenHOI3.Sim.Theatres.CTheatreProvinces.Add(state, theatre, 1);
			var waiting = new CUnit(EUnitKind.Division, Ger) { Id = ++state.LastUnitId };
			waiting.SubUnits.Add(new CSubUnit(Infantry) { Unit = waiting });
			waiting.SubUnits.Add(new CSubUnit(Infantry) { Unit = waiting });
			germany.Deployments.Add(waiting);
			state.ForgetPositions();
			return (state, waiting, headquarters);
		}

		// A country nobody plays: placed, and its agents see to the rest.
		(CGameState ai, _, _) = Made();
		CAiDeployment.Deploy(ai, ai.Country(Ger)!);
		Assert.IsType<CDeployUnitCommand>(Assert.Single(ai.Outbox));

		// The player's: placed in the capital, and put under the headquarters of the capital's theatre.
		(CGameState state, CUnit waiting, CUnit headquarters) = Made();
		state.Played.Add(Ger);
		CAiDeployment.Deploy(state, state.Country(Ger)!);
		Assert.Equal(new[] { typeof(CDeployUnitCommand), typeof(CAttachUnitCommand) }, state.Outbox.Select(command => command.GetType()));
		foreach (CCommand command in state.Outbox.ToList())
		{
			Assert.True(command.MayExecute(state));
			command.Execute(state);
		}
		Assert.Equal(1, waiting.Location);
		Assert.Same(headquarters, waiting.Parent);
	}

	// ---- the queue

	[Fact]
	public void A_land_line_due_soon_goes_to_the_front_when_the_country_is_desperate()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CConstruction Line(int id, string status, string left)
		{
			var line = new CConstruction(Ger) { Id = id, Status = CFixedPoint.Parse(status), Duration = CFixedPoint.Parse(left) };
			line.Brigades.Add(new CBrigadeOrder(Infantry));
			germany.Constructions.Add(line);
			return line;
		}
		Line(1, "1", "10");      // paid in full: never moved
		Line(2, "0.5", "100");   // 200 days: too far
		Line(3, "0.5", "50");    // 100 days: moved
		Line(4, "0", "1000");    // paid nothing: counts as due

		germany.MinisterDesperation = 600;
		CProductionAi.PrioritizeBuildQueue(state, germany);
		Assert.Empty(state.Outbox);

		germany.MinisterDesperation = 601;
		CProductionAi.PrioritizeBuildQueue(state, germany);
		foreach (CCommand command in state.Outbox)
		{
			command.Execute(state);
		}
		// Moved in turn, so the last moved is first.
		Assert.Equal(new[] { 4, 3, 1, 2 }, germany.Constructions.Select(line => line.Id));
	}

	// ---- manpower

	[Fact]
	public void The_spare_manpower_charges_a_quarter_of_build_manpower_for_every_brigade_and_what_it_lacks()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CUnit division = Division(state, germany, 1, Infantry, Infantry);
		division.SubUnits[0].Strength = CFixedPoint.FromInt(2);

		CSpareManpower.Update(state, germany);

		// 100, less (0.333 + 0.25) x 3 and 0.25 x 3; the defines give no half year.
		Assert.Equal(100000 - (333 + 250) * 3 - 250 * 3, germany.SpareManpower.Raw);
	}
}
