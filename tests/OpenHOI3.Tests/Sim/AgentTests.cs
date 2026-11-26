using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The AI's unit agents, against <c>docs/mechanics/agents.md</c>: an agent made with its theatre and bound by the pass,
/// commands made a level at a time and filled five to a command, a new command's brigade and name, fleets and air groups
/// split and merged, the stances, the area of operations, the objectives, a unit's power and the aircraft wanted.
/// Germany, in the axis and run by the AI, owns 1 to 12 in a row; Poland owns 21 to 30, 21 beside 12; Holland 31 and 32,
/// 31 beside 1. Every province is fully roaded.
/// </summary>
public class AgentTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Hol = new("HOL");

	private static CSubUnitType Type(string key, EUnitBranch branch, params (string Stat, int Value)[] stats)
	{
		var type = new CSubUnitType(key, branch);
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(10));
		type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.FromInt(30));
		type.Stats.Values.Set(CSubUnitStats.BuildCostManpower, CFixedPoint.FromInt(1));
		foreach ((string stat, int value) in stats)
		{
			type.Stats.Values.Set(stat, CFixedPoint.FromInt(value));
		}
		return type;
	}

	private static CGameState World(bool withRebels = false)
	{
		CCountry Country(CCountryTag tag, int capital) => new()
		{
			Tag = tag, Name = tag.Value, DefinitionFile = "", Capital = capital, Manpower = CFixedPoint.FromInt(100),
		};
		CCountry germany = Country(Ger, 6);
		germany.Faction = "axis";
		var countries = new List<CCountry> { germany, Country(Pol, 25), Country(Hol, 32) };
		if (withRebels)
		{
			// The rebels are a country with no land of its own and no capital.
			countries.Add(Country(new CCountryTag(OpenHOI3.Sim.Revolts.CRebelFactions.Rebels), 0));
		}
		var neighbours = new Dictionary<int, List<int>>();
		void Join(int a, int b)
		{
			(neighbours.TryGetValue(a, out List<int>? fromA) ? fromA : neighbours[a] = new List<int>()).Add(b);
			(neighbours.TryGetValue(b, out List<int>? fromB) ? fromB : neighbours[b] = new List<int>()).Add(a);
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
		var provinces = new List<CProvince>();
		for (int id = 0; id <= 32; id++)
		{
			CCountryTag? owner = id switch
			{
				>= 1 and <= 12 => Ger,
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
				Points = id == 25 ? 5 : 0,
			});
		}
		TestRoads.Pave(provinces);
		foreach (CProvince province in provinces)
		{
			province.Modifier.Set(OpenHOI3.Sim.Supply.CSupplyFigures.Infrastructure, CFixedPoint.One);
		}
		var fascism = new CIdeologyGroup("fascism", 200000, 200000) { Faction = "axis" };
		var democracy = new CIdeologyGroup("democracy", 0, -146000) { Faction = "allies" };
		return new CGameState(new CDate(1936, 1, 1), provinces.ToArray(), countries.ToArray(), buildings: new[] { TestRoads.Infra })
		{
			IdeologyGroups = new[] { fascism, democracy },
			MapWidth = 100000,
			SubUnitTypes = new Dictionary<string, CSubUnitType>
			{
				[CTheatreHeadquarters.HqBrigade] = Type(CTheatreHeadquarters.HqBrigade, EUnitBranch.Land),
				["infantry_brigade"] = Type("infantry_brigade", EUnitBranch.Land, ("soft_attack", 3), ("defensiveness", 5),
					("toughness", 3), ("softness", 1)),
				["destroyer"] = Type("destroyer", EUnitBranch.Naval),
				["battleship"] = Type("battleship", EUnitBranch.Naval),
				["interceptor"] = Type("interceptor", EUnitBranch.Air),
				["tactical_bomber"] = Type("tactical_bomber", EUnitBranch.Air),
			},
			NameTexts = new CUnitNameTexts
			{
				NamedTheatre = "$PROV$ HQ", Theatre = "$NUM$$ORDER$ Theatre", Brigade = "$NUM$$ORDER$ $TYPE$ Brigade",
				Corps = "$NUM$$ORDER$ Corps", Army = "$NUM$$ORDER$ Army", ArmyGroup = "$NUM$$ORDER$ Army Group",
				Navy = "$NUM$$ORDER$ Navy", Air = "$NUM$$ORDER$ Air wing", Ordinals = new[] { "th", "st", "nd", "rd" },
			},
		};
	}

	private static int _nextId = 1000;

	private static CUnit Unit(CGameState state, EUnitKind kind, int location, string typeKey, int brigades = 1, CUnit? parent = null)
	{
		CCountry germany = state.Country(Ger)!;
		var unit = new CUnit(kind, Ger) { Id = ++_nextId, Location = location, Name = $"u{_nextId}" };
		if (kind is EUnitKind.Navy or EUnitKind.Air)
		{
			unit.Base = location;
		}
		for (int index = 0; index < brigades; index++)
		{
			var brigade = new CSubUnit(state.SubUnitTypes[typeKey]) { Id = ++_nextId, Unit = unit };
			CUnits.RebuildStats(brigade);
			brigade.Strength = CFixedPoint.FromInt(10);
			brigade.Organisation = CFixedPoint.FromInt(30);
			unit.SubUnits.Add(brigade);
		}
		if (parent is null)
		{
			germany.Units.Add(unit);
		}
		else
		{
			unit.Parent = parent;
			parent.Children.Add(unit);
		}
		state.ForgetPositions();
		return unit;
	}

	/// <summary>The theatre's agent, bound to a theatre-level unit standing at 4 that the theatre adopts.</summary>
	private static (CAgent Agent, CUnit Headquarters) Bound(CGameState state)
	{
		CCountry germany = state.Country(Ger)!;
		state.LastUnitId = 5000;
		CUnit headquarters = Unit(state, EUnitKind.Theatre, 4, CTheatreHeadquarters.HqBrigade);
		CTheatreBuilder.Build(state, germany);
		CAgent agent = Assert.Single(germany.UnitAi.Agents);
		CAgents.BindAndAttachOrphans(state, germany);
		Assert.Same(headquarters, agent.Unit);
		Drain(state);
		return (agent, headquarters);
	}

	/// <summary>Every command posted executed, and those they post, in order.</summary>
	private static void Drain(CGameState state)
	{
		while (state.Outbox.Count > 0)
		{
			var posted = state.Outbox.ToList();
			state.Outbox.Clear();
			foreach (CCommand command in posted)
			{
				if (command.MayExecute(state))
				{
					command.Execute(state);
				}
			}
		}
	}

	// ---- the agents

	[Fact]
	public void The_expeditions_pass_lends_a_unit_on_a_friends_land_and_an_enhanced_games_define_stops_the_lending_alone()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		// Holland is Germany's puppet and in its faction: a friend whose land Germany may operate in. No way leads there
		// from Germany's, so it is no part of the land Germany's theatre looks after.
		state.Country(Hol)!.Faction = "axis";
		state.Country(Hol)!.Overlord = Ger;
		foreach ((int from, int to) in new[] { (1, 31), (31, 1) })
		{
			CProvince province = state.Province(from);
			province.Neighbours = province.Neighbours.Where(neighbour => neighbour != to).ToArray();
			province.Edges = province.Edges.Where(edge => edge.To != to).ToArray();
		}
		state.ForgetPositions();
		(CAgent agent, _) = Bound(state);
		// A German division on Holland's land, outside Germany's theatre; and one Poland lent Germany, in Germany, where
		// Poland - no friend - may not operate.
		CUnit abroad = Unit(state, EUnitKind.Division, 31, "infantry_brigade", 2);
		CUnit onLoan = Unit(state, EUnitKind.Division, 5, "infantry_brigade", 2);
		onLoan.Lender = Pol;
		foreach (CUnit unit in new[] { abroad, onLoan })
		{
			if (!agent.Land.Contains(unit))
			{
				agent.Land.Add(unit);
			}
		}
		state.Outbox.Clear();
		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(System.Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
		state.Defines.Set(CAiExpeditions.MilitarySection, CAiExpeditions.LendsDefine, CFixedPoint.Zero);

		CAiExpeditions.Run(state, germany, agent);

		// The unit on loan is sent home all the same; the one abroad stays Germany's.
		Assert.Single(state.Outbox.OfType<CSendExpeditionCommand>());
		Assert.DoesNotContain(onLoan, agent.Land);
		Assert.Contains(abroad, agent.Land);

		// Without the define - the original's - it is lent to Holland.
		state.Outbox.Clear();
		state.Defines.Set(CAiExpeditions.MilitarySection, CAiExpeditions.LendsDefine, CFixedPoint.One);
		CAiExpeditions.Run(state, germany, agent);
		Assert.Single(state.Outbox.OfType<CSendExpeditionCommand>());
		Assert.DoesNotContain(abroad, agent.Land);
	}

	[Fact]
	public void A_theatre_gets_an_unbound_agent_and_the_pass_binds_it_and_switches_its_plan_on()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		Unit(state, EUnitKind.Theatre, 4, CTheatreHeadquarters.HqBrigade);
		CTheatreBuilder.Build(state, germany);
		CAgent agent = Assert.Single(germany.UnitAi.Agents);
		Assert.Null(agent.Unit);
		Assert.True(agent.AreaDirty);

		CAgents.BindAndAttachOrphans(state, germany);
		Assert.NotNull(agent.Unit);
		Drain(state);
		Assert.True(agent.Unit!.Plan.Active);
		// Its area, set as it is bound: the fronts against Poland and Holland, both front-worthy and the theatre cold.
		Assert.Equal(new[] { 12, 1 }, agent.Unit.Plan.Area);
	}

	[Fact]
	public void A_top_level_unit_is_attached_under_its_theatre_once_every_agent_has_posted_its_plan()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		(CAgent agent, CUnit headquarters) = Bound(state);
		CUnit stray = Unit(state, EUnitKind.Division, 7, "infantry_brigade");
		CAgents.BindAndAttachOrphans(state, germany);
		Drain(state);
		Assert.Null(stray.Parent);

		agent.PlanPosted = true;
		CAgents.BindAndAttachOrphans(state, germany);
		Drain(state);
		Assert.Same(headquarters, stray.Parent);
	}

	[Fact]
	public void The_gate_sorts_every_top_level_unit_under_the_theatre_it_stands_in_and_ends_the_pass()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CUnit headquarters = Unit(state, EUnitKind.Theatre, 4, CTheatreHeadquarters.HqBrigade);
		CTheatreBuilder.Build(state, germany);
		CUnit army = Unit(state, EUnitKind.Army, 9, CTheatreHeadquarters.HqBrigade);
		CUnit division = Unit(state, EUnitKind.Division, 8, "infantry_brigade", parent: army);
		CUnit fleet = Unit(state, EUnitKind.Navy, 3, "destroyer");
		CTheatreSort.Sort(state, germany);
		Drain(state);
		// The army and the fleet, at the top, go under the headquarters; the division keeps its army, which stands in the
		// same theatre.
		Assert.Same(headquarters, army.Parent);
		Assert.Same(headquarters, fleet.Parent);
		Assert.Same(army, division.Parent);
	}

	// ---- commands made and filled

	[Fact]
	public void Nine_divisions_want_three_corps_made_over_them_and_the_next_day_an_army_over_those()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		(CAgent agent, CUnit headquarters) = Bound(state);
		for (int index = 0; index < 9; index++)
		{
			Unit(state, EUnitKind.Division, 2 + index % 5, "infantry_brigade", parent: headquarters);
		}
		CSubordinates.Sort(state, germany, agent);
		CCommandLadder.Organise(state, germany, agent);
		Assert.Equal(3, state.Outbox.OfType<CCreateHigherCommand>().Count());
		Drain(state);

		List<CUnit> corps = headquarters.Children.Where(u => u.Kind == EUnitKind.Corps).ToList();
		Assert.Equal(new[] { "1st Corps", "2nd Corps", "3rd Corps" }, corps.Select(c => c.Name));
		CSubUnit brigade = Assert.Single(corps[0].SubUnits);
		// A twentieth of its strength, and three ids taken for each.
		Assert.Equal(CFixedPoint.Parse("0.5"), brigade.Strength);
		Assert.Equal(corps[0].Id + 1, brigade.Id);
		Assert.Equal(corps[1].Id, corps[0].Id + 3);

		CSubordinates.Sort(state, germany, agent);
		CCommandLadder.Organise(state, germany, agent);
		Drain(state);
		CUnit army = Assert.Single(headquarters.Children, u => u.Kind == EUnitKind.Army);
		Assert.Equal("1st Army", army.Name);
	}

	[Fact]
	public void A_new_command_brings_its_days_out_of_the_stockpile_and_holds_no_level_of_its_countrys_technology()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CSubUnitType type = state.SubUnitTypes[CTheatreHeadquarters.HqBrigade];
		type.Stats.Values.Set(CUnitSupply.SupplyStatistic, CFixedPoint.One);
		type.Technologies.Add(new OpenHOI3.Sim.Research.CTechnology("mass_assault", 12, new OpenHOI3.Sim.Modifiers.CModifier("mass_assault")));
		germany.TechnologyLevels["mass_assault"] = 2;
		state.Defines.Set(OpenHOI3.Sim.Supply.CSupplyFigures.Military, OpenHOI3.Sim.Supply.CSupplyDay.PoolDays, CFixedPoint.FromInt(15));
		(CAgent _, CUnit headquarters) = Bound(state);
		CUnit division = Unit(state, EUnitKind.Division, 2, "infantry_brigade", parent: headquarters);
		state.Province(6).Pool[OpenHOI3.Sim.Economy.EGoods.Supplies] = CFixedPoint.FromInt(100);

		var command = new CCreateHigherCommand(state, Ger, division.Id);
		Assert.True(command.MayExecute(state));
		command.Execute(state);

		// A twentieth of a brigade using one a day, fifteen days of it out of the capital's pool into the division's
		// province, at no level of mass_assault.
		CUnit corps = division.Parent!;
		Assert.Equal(EUnitKind.Corps, corps.Kind);
		Assert.Equal(0, Assert.Single(corps.SubUnits).TechnologyLevels.GetValueOrDefault("mass_assault"));
		Assert.Equal(CFixedPoint.Parse("0.75"), state.Province(2).Pool[OpenHOI3.Sim.Economy.EGoods.Supplies]);
		Assert.Equal(CFixedPoint.Parse("99.25"), state.Province(6).Pool[OpenHOI3.Sim.Economy.EGoods.Supplies]);
	}

	[Fact]
	public void A_division_goes_under_the_nearest_corps_with_room_for_five()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		(CAgent agent, CUnit headquarters) = Bound(state);
		CUnit near = Unit(state, EUnitKind.Corps, 10, CTheatreHeadquarters.HqBrigade, parent: headquarters);
		CUnit far = Unit(state, EUnitKind.Corps, 2, CTheatreHeadquarters.HqBrigade, parent: headquarters);
		var divisions = new List<CUnit>();
		for (int index = 0; index < 6; index++)
		{
			divisions.Add(Unit(state, EUnitKind.Division, 11, "infantry_brigade", parent: headquarters));
		}
		CSubordinates.Sort(state, germany, agent);
		CCommandLadder.Organise(state, germany, agent);
		Drain(state);
		// Five fill the nearer; the sixth goes to the other.
		Assert.Equal(5, near.Children.Count);
		Assert.Single(far.Children);
	}

	[Fact]
	public void A_unit_the_sealift_means_for_somewhere_and_one_under_such_a_unit_keep_their_commander()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		(CAgent agent, CUnit headquarters) = Bound(state);
		CUnit corps = Unit(state, EUnitKind.Corps, 10, CTheatreHeadquarters.HqBrigade, parent: headquarters);
		CUnit bound = Unit(state, EUnitKind.Division, 11, "infantry_brigade", parent: headquarters);
		bound.SealiftTarget = 25;
		CUnit army = Unit(state, EUnitKind.Army, 11, CTheatreHeadquarters.HqBrigade, parent: headquarters);
		army.SealiftTarget = 25;
		CUnit under = Unit(state, EUnitKind.Corps, 11, CTheatreHeadquarters.HqBrigade, parent: army);
		CUnit free = Unit(state, EUnitKind.Division, 11, "infantry_brigade", parent: headquarters);
		CSubordinates.Sort(state, germany, agent);
		CCommandLadder.Organise(state, germany, agent);
		Drain(state);
		// FINDINGS-liveread.md, section 82: Italy's 2nd Army and 2a Armata, each with a target, stayed under the theatre.
		Assert.Same(headquarters, bound.Parent);
		Assert.Same(army, under.Parent);
		// A command under such a unit still takes others: the nearest, in the division's own province.
		Assert.Same(under, free.Parent);
		Assert.Empty(corps.Children);
	}

	// ---- fleets and air groups

	[Fact]
	public void A_fleet_over_its_screens_sheds_the_first_into_fleets_of_one()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		(CAgent agent, CUnit headquarters) = Bound(state);
		CUnit fleet = Unit(state, EUnitKind.Navy, 3, "destroyer", 6, parent: headquarters);
		var first = fleet.SubUnits.Take(2).ToList();
		CSubordinates.Sort(state, germany, agent);
		CFleetRegroup.Run(state, germany, agent);
		Drain(state);

		Assert.Equal(4, fleet.SubUnits.Count);
		List<CUnit> made = headquarters.Children.Where(u => u.Kind == EUnitKind.Navy && !ReferenceEquals(u, fleet)).ToList();
		Assert.Equal(new[] { "1st Navy", "2nd Navy" }, made.Select(f => f.Name));
		Assert.Equal(first, made.Select(f => Assert.Single(f.SubUnits)));
		Assert.All(made, f => Assert.Equal(3, f.Base));
	}

	[Fact]
	public void A_lone_capital_ship_is_ordered_to_join_a_screen_and_is_merged_into_it_on_arrival()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		(CAgent agent, CUnit headquarters) = Bound(state);
		state.SubUnitTypes["battleship"].Flags["capital"] = true;
		CUnit battleship = Unit(state, EUnitKind.Navy, 3, "battleship", parent: headquarters);
		CUnit destroyer = Unit(state, EUnitKind.Navy, 3, "destroyer", parent: headquarters);
		CSubordinates.Sort(state, germany, agent);
		CFleetRegroup.Run(state, germany, agent);
		Assert.Single(state.Outbox.OfType<CJoinUnitCommand>());
		Drain(state);
		Assert.Equal(EUnitOrder.JoinFleet, battleship.Order);

		Assert.False(CJoinOrders.RunHour(state, germany, battleship));
		Assert.True(battleship.IsRemoved);
		Assert.Equal(2, destroyer.SubUnits.Count);
	}

	[Fact]
	public void An_interceptor_beside_a_tactical_bomber_is_walked_first_and_given_an_air_unit_of_its_own()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		(CAgent agent, CUnit headquarters) = Bound(state);
		CUnit group = Unit(state, EUnitKind.Air, 3, "interceptor", parent: headquarters);
		var bomber = new CSubUnit(state.SubUnitTypes["tactical_bomber"]) { Id = ++_nextId, Unit = group };
		CUnits.RebuildStats(bomber);
		bomber.Strength = CFixedPoint.FromInt(10);
		bomber.Organisation = CFixedPoint.FromInt(30);
		group.SubUnits.Add(bomber);
		CSubordinates.Sort(state, germany, agent);
		CAirRegroup.Run(state, germany, agent, force: true, new HashSet<CUnit>(), agent.Air);
		Drain(state);
		CUnit made = Assert.Single(headquarters.Children, u => u.Kind == EUnitKind.Air && !ReferenceEquals(u, group));
		Assert.Same(group.SubUnits.Count == 1 ? group.SubUnits[0] : null, bomber);
		Assert.Equal("interceptor", Assert.Single(made.SubUnits).Type.Key);
		Assert.Equal("1st Air wing", made.Name);
	}

	[Fact]
	public void An_air_unit_put_under_an_agent_asks_it_to_regroup_and_a_division_does_not()
	{
		CGameState state = World();
		(CAgent agent, CUnit headquarters) = Bound(state);
		agent.RegroupAirPending = false;
		CUnit division = Unit(state, EUnitKind.Division, 3, "infantry_brigade");
		Assert.True(CAgentUnits.Attach(state, division, headquarters));
		Assert.False(agent.RegroupAirPending);
		CUnit group = Unit(state, EUnitKind.Air, 3, "tactical_bomber");
		Assert.True(CAgentUnits.Attach(state, group, headquarters));
		Assert.True(agent.RegroupAirPending);
	}

	[Fact]
	public void At_peace_a_worn_group_at_its_base_is_still_ordered_to_join_another()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		(CAgent agent, CUnit headquarters) = Bound(state);
		CUnit worn = Unit(state, EUnitKind.Air, 3, "tactical_bomber", parent: headquarters);
		CUnit other = Unit(state, EUnitKind.Air, 3, "tactical_bomber", parent: headquarters);
		worn.SubUnits[0].Organisation = CFixedPoint.FromInt(1);
		other.SubUnits[0].Organisation = CFixedPoint.FromInt(1);
		CSubordinates.Sort(state, germany, agent);
		agent.RegroupAirPending = true;
		CAirBody.Run(state, germany, agent, full: false);
		Assert.Single(state.Outbox.OfType<CJoinUnitCommand>());
	}

	// ---- the plan

	[Fact]
	public void At_peace_a_plan_with_an_area_is_prepared_and_air_and_naval_offensive()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		(CAgent agent, CUnit headquarters) = Bound(state);
		CPlanStances.Update(state, germany, force: true);
		Drain(state);
		Assert.Equal(CUnitPlan.Prepare, headquarters.Plan.Stance);
		Assert.Equal(CUnitPlan.Offensive, headquarters.Plan.AirStance);
		Assert.Equal(CUnitPlan.Offensive, headquarters.Plan.NavalStance);
		Assert.True(germany.UnitAi.StancesSettled);
	}

	[Fact]
	public void A_theatre_build_marks_the_theatre_agent_of_a_friend_fighting_beside_it_whose_headquarters_it_can_reach()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CCountry holland = state.Country(Hol)!;
		Bound(state);
		var hq = new CUnit(EUnitKind.Theatre, Hol) { Id = ++_nextId, Location = 32, Name = "Den Haag HQ" };
		var brigade = new CSubUnit(state.SubUnitTypes[CTheatreHeadquarters.HqBrigade]) { Id = ++_nextId, Unit = hq };
		CUnits.RebuildStats(brigade);
		hq.SubUnits.Add(brigade);
		holland.Units.Add(hq);
		state.ForgetPositions();
		CTheatreBuilder.Build(state, holland);
		CAgent dutch = Assert.Single(holland.UnitAi.Agents);
		CAgents.BindAndAttachOrphans(state, holland);
		Drain(state);
		Assert.Same(hq, dutch.Unit);

		// At peace with nobody beside it: nothing.
		dutch.AreaDirty = false;
		CTheatreBuilder.Build(state, germany);
		Assert.False(dutch.AreaDirty);

		// Fighting Poland beside Germany, and reached by Germany's headquarters overland (FINDINGS-liveread.md, section 83).
		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Attackers.Add(Hol);
		war.Defenders.Add(Pol);
		state.Wars.Add(war);
		CTheatreBuilder.Build(state, germany);
		Assert.True(dutch.AreaDirty);
	}

	[Fact]
	public void A_hot_theatre_aims_at_every_province_with_points_beyond_its_front_at_twenty_a_point()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		state.Wars.Add(war);
		(CAgent agent, CUnit headquarters) = Bound(state);
		Assert.True(CAgents.TheatreOf(state, germany, agent)!.IsHot);
		CPlanObjectives.Generate(state, germany, agent);
		Drain(state);
		CObjective objective = Assert.Single(headquarters.Plan.Objectives);
		Assert.Equal(new CObjective(25, 100), objective);
		Assert.True(agent.FrontierAtWar);
	}

	[Fact]
	public void A_frontier_beside_the_rebels_land_is_at_war_for_a_country_at_peace()
	{
		CGameState quiet = World(withRebels: true);
		(CAgent idle, _) = Bound(quiet);
		CAgentArea.Set(quiet, quiet.Country(Ger)!, idle);
		Assert.False(idle.FrontierAtWar);

		// The rebels hold a province inside the country: no war was declared, and the frontier is at war all the same.
		CGameState state = World(withRebels: true);
		CProvinceHands.SetController(state, state.Province(8), new CCountryTag(OpenHOI3.Sim.Revolts.CRebelFactions.Rebels));
		(CAgent agent, _) = Bound(state);
		CAgentArea.Set(state, state.Country(Ger)!, agent);
		Assert.Contains(7, agent.Area);
		Assert.Contains(9, agent.Area);
		Assert.True(agent.FrontierAtWar);
	}

	[Fact]
	public void A_land_brigade_is_worth_its_attacks_a_tenth_of_its_defences_and_two_less_its_softness()
	{
		CGameState state = World();
		CUnit division = Unit(state, EUnitKind.Division, 3, "infantry_brigade");
		// (3 + (5 + 3)/10) x (2 - 1) = 3.8; at 10 strength and 30 organisation, (10000/100) x 3800/1000 x (30000/100)/1000.
		Assert.Equal(3800, CUnitPower.Value(division.SubUnits[0]));
		Assert.Equal(100 * 3800 / 1000 * 300 / 1000, CUnitPower.Of(division));
	}

	[Fact]
	public void The_aircraft_wanted_are_counted_by_the_types_in_the_files_order()
	{
		CGameState state = World();
		// destroyer, hq_brigade, infantry_brigade, interceptor, tactical_bomber, battleship - by their names in capitals.
		Assert.Equal(new[] { "battleship", "destroyer", "hq_brigade", "infantry_brigade", "interceptor", "tactical_bomber" },
			CAircraftWants.Order(state).Select(t => t.Key));
		Assert.Equal(5, CAircraftWants.IndexOf(state, "interceptor"));
		var plan = new CUnitPlan();
		CAircraftWants.Pad(state, plan);
		Assert.Equal(7, plan.Min.Count);
	}
}
