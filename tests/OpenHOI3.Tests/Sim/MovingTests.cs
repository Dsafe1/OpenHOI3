using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The AI moving and attacking, against <c>docs/mechanics/moving.md</c>: a garrison's rating, the position cap from the
/// stacking define, committing by the powers, the area's peacetime scores, a long way made a strategic redeployment and
/// its speed, a support attack that never advances, a halt where a unit is sent to where it stands, and the odds' ends.
/// Germany owns 1 to 12 in a row, Poland 21 to 30, 21 beside 12. Every province is fully roaded.
/// </summary>
public class MovingTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private static CSubUnitType Type(string key, params (string Stat, int Value)[] stats)
	{
		var type = new CSubUnitType(key, EUnitBranch.Land);
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(10));
		type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.FromInt(30));
		type.Stats.Values.Set("maximum_speed", CFixedPoint.FromInt(4));
		foreach ((string stat, int value) in stats)
		{
			type.Stats.Values.Set(stat, CFixedPoint.FromInt(value));
		}
		return type;
	}

	private static CGameState World()
	{
		CCountry Country(CCountryTag tag, int capital) => new()
		{
			Tag = tag, Name = tag.Value, DefinitionFile = "", Capital = capital, Manpower = CFixedPoint.FromInt(100),
		};
		var countries = new List<CCountry> { Country(Ger, 6), Country(Pol, 25) };
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
		var provinces = new List<CProvince>();
		for (int id = 0; id <= 30; id++)
		{
			CCountryTag? owner = id switch
			{
				>= 1 and <= 12 => Ger,
				>= 21 and <= 30 => Pol,
				_ => null,
			};
			provinces.Add(new CProvince(id, $"p{id}", isSea: false)
			{
				Owner = owner,
				Controller = owner,
				Neighbours = neighbours.GetValueOrDefault(id)?.ToArray() ?? System.Array.Empty<int>(),
				CentreX = id * 10,
			});
		}
		TestRoads.Pave(provinces);
		foreach (CProvince province in provinces)
		{
			province.Modifier.Set(OpenHOI3.Sim.Supply.CSupplyFigures.Infrastructure, CFixedPoint.One);
		}
		var state = new CGameState(new CDate(1936, 1, 1), provinces.ToArray(), countries.ToArray(), buildings: new[] { TestRoads.Infra })
		{
			MapWidth = 100000,
			SubUnitTypes = new Dictionary<string, CSubUnitType>
			{
				["infantry_brigade"] = Type("infantry_brigade", ("soft_attack", 3), ("defensiveness", 5), ("toughness", 3),
					("softness", 1)),
				["garrison_brigade"] = Type("garrison_brigade", ("soft_attack", 1), ("defensiveness", 3)),
				["hq_brigade"] = Type("hq_brigade"),
			},
		};
		state.Defines.Set(CManpower.MilitarySection, CPositionCap.BaseStackingPenalty, CFixedPoint.Parse("-0.1"));
		state.Defines.Set(CManpower.MilitarySection, CMovementSpeed.StratRedeployBaseSpeed, CFixedPoint.FromInt(20));
		state.Defines.Set(CManpower.MilitarySection, CMovementSpeed.LandSpeedModifier, CFixedPoint.One);
		return state;
	}

	private static int _nextId = 1000;

	private static CUnit Division(CGameState state, CCountryTag owner, int location, string typeKey, int brigades = 3)
	{
		var unit = new CUnit(EUnitKind.Division, owner) { Id = ++_nextId, Location = location, Name = $"u{_nextId}" };
		for (int index = 0; index < brigades; index++)
		{
			var brigade = new CSubUnit(state.SubUnitTypes[typeKey]) { Id = ++_nextId, Unit = unit };
			CUnits.RebuildStats(brigade);
			brigade.Strength = CFixedPoint.FromInt(10);
			brigade.Organisation = CFixedPoint.FromInt(30);
			unit.SubUnits.Add(brigade);
		}
		state.Country(owner)!.Units.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	[Fact]
	public void A_lone_garrison_brigade_is_a_whole_garrison_and_three_infantry_brigades_hardly_one()
	{
		CGameState state = World();
		CUnit garrison = Division(state, Ger, 1, "garrison_brigade", brigades: 1);
		CUnit infantry = Division(state, Ger, 1, "infantry_brigade", brigades: 3);
		// (100 + 20) x (2 - 1), held to 100; (20 + -20) x 1, held up to 1.
		Assert.Equal(100_000, CUnitRatings.Garrison(state, garrison));
		Assert.Equal(1_000, CUnitRatings.Garrison(state, infantry));
		// At half strength a two-brigade garrison rates higher than at full: (100 + 0) x 1.5, held to 100, against 100.
		CUnit worn = Division(state, Ger, 1, "garrison_brigade", brigades: 2);
		worn.SubUnits.ForEach(brigade => brigade.Strength = CFixedPoint.FromInt(5));
		Assert.Equal(100_000, CUnitRatings.Garrison(state, worn));
	}

	[Fact]
	public void The_stack_a_position_holds_is_half_over_the_stacking_penalty_less_two_thirds_of_its_fort()
	{
		CGameState state = World();
		Assert.Equal(5, CPositionCap.Stack(state));
		var agent = new CAgent(Ger, 1);
		agent.Area.Add(12);
		CProvince border = state.Province(12);
		border.Modifier.Set(CCombatModifiers.FortLevel, CFixedPoint.FromInt(3));
		state.Country(Ger)!.Faction = "";
		// Not beside an enemy, but in the area: 5 - 2.
		Assert.Equal(3, CPositionCap.Of(state, state.Country(Ger)!, agent, border, CPositionCap.Stack(state)));
		Assert.Equal(0, CPositionCap.Of(state, state.Country(Ger)!, agent, state.Province(5), CPositionCap.Stack(state)));
	}

	[Fact]
	public void At_peace_a_division_is_committed_and_at_war_a_weak_one_is_held_back()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		var headquarters = new CUnit(EUnitKind.Theatre, Ger) { Id = 1, Location = 6 };
		var agent = new CAgent(Ger, 1) { Unit = headquarters };
		CUnit fit = Division(state, Ger, 3, "infantry_brigade");
		CUnit weak = Division(state, Ger, 4, "infantry_brigade");
		weak.SubUnits.ForEach(brigade => brigade.Strength = CFixedPoint.FromInt(1));
		agent.Land.AddRange(new[] { fit, weak });

		CCommitment.Run(state, germany, agent);
		Assert.Equal(new[] { fit, weak }, agent.Committed);

		agent.FrontierAtWar = true;
		CCommitment.Run(state, germany, agent);
		Assert.Equal(new[] { fit }, agent.Committed);
		Assert.Equal(new[] { weak }, agent.HeldBack);
		Assert.True(weak.IsHeldBack);
	}

	[Fact]
	public void At_peace_a_province_scores_ten_a_fort_level_and_ten_and_the_odd_divisions_add_twenty_spread_out()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		var headquarters = new CUnit(EUnitKind.Corps, Ger) { Id = 1, Location = 6 };
		var agent = new CAgent(Ger, 1) { Unit = headquarters };
		agent.Area.AddRange(new[] { 1, 2, 3, 4 });
		state.Province(2).Modifier.Set(CCombatModifiers.FortLevel, CFixedPoint.FromInt(2));
		// Six divisions over four provinces: 6 mod 4 = 2 of them get the 20, at the second and the fourth.
		for (int index = 0; index < 6; index++)
		{
			agent.Committed.Add(Division(state, Ger, 1, "infantry_brigade"));
		}
		CAreaScores.Score(state, germany, agent, CUnitPlan.Prepare);
		var scores = agent.Positions.ToDictionary(p => p.Province, p => p.Score);
		Assert.Equal(10_000, scores[1]);
		Assert.Equal(50_000, scores[2]);
		Assert.Equal(10_000, scores[3]);
		Assert.Equal(30_000, scores[4]);
		Assert.Equal(100_000, agent.PositionSum);
		Assert.Equal(2, agent.Positions[0].Province);
	}

	[Fact]
	public void A_long_way_asked_for_safely_is_a_strategic_redeployment_and_goes_at_the_roads_speed()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CUnit division = Division(state, Ger, 1, "infantry_brigade");
		// 110 apart at a top speed of 4: at peace 4 / 4 x 600 = 600 hours' worth is not reached...
		Assert.False(CAiMoves.ShouldRedeploy(state, germany, division, 12));
		// ...while a neighbour never is.
		Assert.False(CAiMoves.ShouldRedeploy(state, germany, division, 2));
		division.SubUnits.ForEach(brigade => brigade.Stats.Values.Set("maximum_speed", CFixedPoint.Parse("0.1")));
		Assert.True(CAiMoves.ShouldRedeploy(state, germany, division, 12));
		Assert.True(CAiMoves.Move(state, germany, division, 12, safe: true));
		CSetUnitOrderCommand order = Assert.IsType<CSetUnitOrderCommand>(Assert.Single(state.Outbox));
		Assert.Equal(12, division.AiBound);
		Assert.True(order.MayExecute(state));
		order.Execute(state);
		Assert.Equal(EUnitOrder.StrategicRedeployment, division.Order);
		// 20 x the full roads.
		Assert.Equal(CFixedPoint.FromInt(20), CMovementSpeed.Speed(state, germany, division));
	}

	[Fact]
	public void Sent_to_where_it_stands_while_it_moves_a_unit_is_halted_and_a_support_attack_never_advances()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CUnit division = Division(state, Ger, 3, "infantry_brigade");
		CMovement.SetPath(division, new[] { 4, 5 }, clear: true);
		Assert.True(CAiMoves.Move(state, germany, division, 3, safe: true));
		Assert.IsType<CCancelMovementCommand>(Assert.Single(state.Outbox));
		Assert.Equal(0, division.AiBound);

		division.Order = EUnitOrder.SupportAttack;
		division.OrderProvince = 4;
		division.MovementProgress = CFixedPoint.Zero;
		CMovement.Advance(state, germany, division, addSpeed: true);
		Assert.Equal(CFixedPoint.Zero, division.MovementProgress);
	}

	[Fact]
	public void A_unit_marching_into_a_battle_is_neither_halted_nor_sent_anywhere_by_the_AI()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CUnit division = Division(state, Ger, 3, "infantry_brigade");
		CMovement.SetPath(division, new[] { 4, 5 }, clear: true);
		var combat = new OpenHOI3.Sim.Combat.CCombat(4);
		state.Combats.Add(combat);
		combat.Attacker.Add(division);

		// The click's test refuses every province to it, its own included, and the AI's move asks that first.
		Assert.False(CAiMoves.Move(state, germany, division, 3, safe: true));
		Assert.False(CAiMoves.Move(state, germany, division, 2, safe: false));
		Assert.Empty(state.Outbox);
		Assert.Equal(new[] { 4, 5 }, division.Path);
	}

	[Fact]
	public void A_beach_counts_its_enemy_brigades_by_strength_a_headquarters_a_third_and_a_fifth_more_a_coastal_fort_level()
	{
		CGameState state = World();
		CUnit infantry = Division(state, Pol, 21, "infantry_brigade", brigades: 2);
		infantry.SubUnits[1].Strength = CFixedPoint.FromInt(5);
		Division(state, Pol, 21, "hq_brigade", brigades: 1);
		CWarDeclarations(state);
		CCountry germany = state.Country(Ger)!;
		// 1 + 0.5 + a third: 1.833.
		Assert.Equal(1_833, CLandingPicker.Defence(state, germany, state.Province(21)));
		state.Province(21).Modifier.Set(CCombatModifiers.CoastalFortLevel, CFixedPoint.FromInt(5));
		Assert.Equal(3_666, CLandingPicker.Defence(state, germany, state.Province(21)));
	}

	[Fact]
	public void A_country_carries_in_whole_units_and_at_peace_every_sea_is_open()
	{
		CGameState state = World();
		var transport = new CSubUnitType("transport_ship", EUnitBranch.Naval);
		transport.Stats.Values.Set(OpenHOI3.Sim.Transport.CTransport.TransportCapability, CFixedPoint.Parse("2.5"));
		var fleet = new CUnit(EUnitKind.Navy, Ger) { Id = ++_nextId, Location = 1, Name = "fleet" };
		for (int index = 0; index < 2; index++)
		{
			var ship = new CSubUnit(transport) { Id = ++_nextId, Unit = fleet };
			CUnits.RebuildStats(ship);
			fleet.SubUnits.Add(ship);
		}
		state.Country(Ger)!.Units.Add(fleet);
		// Two ships of 2.5: 5 whole, and the country no more.
		Assert.Equal(5, CSealift.SumCapacity(state.Country(Ger)!));
		Assert.True(CSealiftPickers.IsSeaRouteOpen(state, state.Country(Ger)!, 1, 30));
	}

	/// <summary>Germany and Poland put at war with each other.</summary>
	private static void CWarDeclarations(CGameState state)
	{
		var war = new OpenHOI3.Sim.Wars.CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		state.Wars.Add(war);
	}

	[Fact]
	public void The_odds_are_nought_with_no_attacker_and_a_hundred_with_no_defender()
	{
		CGameState state = World();
		CUnit ours = Division(state, Ger, 12, "infantry_brigade");
		CUnit theirs = Division(state, Pol, 21, "infantry_brigade");
		CProvince target = state.Province(21);
		Assert.Equal(0, CAttackOdds.Of(state, new List<CUnit>(), new List<CUnit> { theirs }, target));
		Assert.Equal(100_000, CAttackOdds.Of(state, new List<CUnit> { ours }, new List<CUnit>(), target));
		// Like against like: the same damage each way.
		Assert.Equal(1_000, CAttackOdds.Of(state, new List<CUnit> { ours }, new List<CUnit> { theirs }, target));
	}

	private static CUnit AirGroup(CGameState state, CCountryTag owner, int airBase, string typeKey)
	{
		var type = new CSubUnitType(typeKey, EUnitBranch.Air);
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(10));
		type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.FromInt(30));
		type.Stats.Values.Set(CUnitOrders.Range, CFixedPoint.FromInt(5000));
		var unit = new CUnit(EUnitKind.Air, owner) { Id = ++_nextId, Location = airBase, Base = airBase, Name = $"a{_nextId}" };
		var wing = new CSubUnit(type) { Id = ++_nextId, Unit = unit };
		CUnits.RebuildStats(wing);
		wing.Strength = CFixedPoint.FromInt(10);
		wing.Organisation = CFixedPoint.FromInt(30);
		unit.SubUnits.Add(wing);
		state.Country(owner)!.Units.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	[Fact]
	public void The_AI_flies_close_support_over_its_invasions_beach_till_sunset_and_calls_off_a_worn_group()
	{
		CGameState state = World();
		CWarDeclarations(state);
		CCountry germany = state.Country(Ger)!;
		var headquarters = new CUnit(EUnitKind.Theatre, Ger) { Id = 1, Location = 6 };
		headquarters.Plan.AirStance = CUnitPlan.Offensive;
		var agent = new CAgent(Ger, 1) { Unit = headquarters, RegroupAirPending = false };
		Division(state, Pol, 21, "infantry_brigade");
		agent.InvasionTargets.Add(21);
		CUnit bombers = AirGroup(state, Ger, 6, "tactical_bomber");
		CUnit worn = AirGroup(state, Ger, 6, "cas");
		worn.SubUnits[0].Strength = CFixedPoint.FromInt(1);
		worn.Order = EUnitOrder.GroundAttack;
		agent.Air.AddRange(new[] { bombers, worn });

		CAirBody.Run(state, germany, agent, full: true);
		Assert.Single(state.Outbox.OfType<CSetUnitOrderCommand>());
		Assert.Single(state.Outbox.OfType<CDropOrderCommand>());

		// No map height, so no latitude: q is six and sunset eighteen, which on the stroke of midnight leaves eighteen hours.
		Assert.Equal(18, OpenHOI3.Sim.Weather.CDaylight.SunsetHour(state, state.Province(21)));
	}

	[Fact]
	public void A_province_no_way_leads_to_is_out_of_reach_and_sending_a_unit_there_posts_nothing()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CUnit division = Division(state, Ger, 12, "infantry_brigade");
		// 15 belongs to nobody and has no neighbours; 5 is down the road.
		Assert.False(CAiMoves.CanReach(state, germany, division, 15));
		Assert.True(CAiMoves.CanReach(state, germany, division, 5));
		Assert.False(CAiMoves.Move(state, germany, division, 15, safe: false));
		Assert.Empty(state.Outbox);
		Assert.True(CAiMoves.Move(state, germany, division, 5, safe: false));
		Assert.Single(state.Outbox);
	}

	[Fact]
	public void The_power_ratio_is_a_thousand_with_the_enemy_below_nought_and_minus_one_at_nought()
	{
		var plan = new CUnitPlan { OurPower = 500, TheirPower = 1000 };
		Assert.Equal(500, CAirBody.PowerRatio(plan));
		plan.TheirPower = 0;
		Assert.Equal(-1, CAirBody.PowerRatio(plan));
		plan.TheirPower = -1;
		Assert.Equal(1000, CAirBody.PowerRatio(plan));
	}

	[Fact]
	public void A_support_attack_goes_only_beside_an_enemy_and_waits_there_for_a_battle_without_a_way()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CUnit division = Division(state, Ger, 12, "infantry_brigade");
		// At peace nobody is an enemy; at war only the province beside (landorders.md, section 6).
		Assert.False(CUnitOrders.MayGive(state, germany, division, EUnitOrder.SupportAttack, state.Province(21)));
		CWarDeclarations(state);
		Assert.True(CUnitOrders.MayGive(state, germany, division, EUnitOrder.SupportAttack, state.Province(21)));
		Assert.False(CUnitOrders.MayGive(state, germany, division, EUnitOrder.SupportAttack, state.Province(22)));
		Assert.False(CUnitOrders.MayGive(state, germany, division, EUnitOrder.SupportAttack, state.Province(11)));

		// Given on its way, it is stopped where it stands and routed nowhere (section 7).
		CMovement.SetPath(division, new[] { 11 }, clear: true);
		CUnitOrders.Give(state, germany, division, EUnitOrder.SupportAttack, 21);
		Assert.False(division.IsMoving);
		// With no battle there it waits, as long as need be.
		CUnitOrders.RunHour(state, germany, division);
		Assert.Equal(EUnitOrder.SupportAttack, division.Order);
		Assert.False(division.IsMoving);
		// A way into a province where nobody fights is taken away with the order.
		CMovement.SetPath(division, new[] { 21 }, clear: true);
		CUnitOrders.RunHour(state, germany, division);
		Assert.Equal(EUnitOrder.None, division.Order);
		Assert.False(division.IsMoving);
	}

	[Fact]
	public void A_strategic_redeployment_goes_anywhere_not_held_by_an_enemy_and_starts_its_attack_delay_every_hour()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CWarDeclarations(state);
		CUnit division = Division(state, Ger, 1, "infantry_brigade");
		// No distance is asked, a neighbour will do; an enemy's province will not (landorders.md, section 6).
		Assert.True(CUnitOrders.MayGive(state, germany, division, EUnitOrder.StrategicRedeployment, state.Province(2)));
		Assert.True(CUnitOrders.MayGive(state, germany, division, EUnitOrder.StrategicRedeployment, state.Province(12)));
		Assert.False(CUnitOrders.MayGive(state, germany, division, EUnitOrder.StrategicRedeployment, state.Province(21)));

		CUnitOrders.Give(state, germany, division, EUnitOrder.StrategicRedeployment, 6);
		Assert.True(division.IsMoving);
		Assert.Equal(CFixedPoint.Zero, division.AttackDelay);
		// Each hour of the journey starts the delay again - twelve hours at the least (section 8).
		CUnitOrders.RunHour(state, germany, division);
		Assert.Equal(CFixedPoint.FromInt(12), division.AttackDelay);
		Assert.Equal(EUnitOrder.StrategicRedeployment, division.Order);
		division.AttackDelay = CFixedPoint.FromInt(3);
		CUnitOrders.RunHour(state, germany, division);
		Assert.Equal(CFixedPoint.FromInt(12), division.AttackDelay);
		// In the province, it is done.
		division.Location = 6;
		CUnitOrders.RunHour(state, germany, division);
		Assert.Equal(EUnitOrder.None, division.Order);
	}

	[Fact]
	public void An_order_with_a_start_hour_does_not_move_before_it_and_one_with_an_arrival_hour_waits_at_full_progress()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CUnit division = Division(state, Ger, 1, "infantry_brigade");
		// The window's Move Order goes only beside (landorders.md, section 6).
		Assert.True(CUnitOrders.MayGive(state, germany, division, EUnitOrder.Move, state.Province(2)));
		Assert.False(CUnitOrders.MayGive(state, germany, division, EUnitOrder.Move, state.Province(3)));

		var later = new CSetUnitOrderCommand(Ger, division.Id, EUnitOrder.Move, 2, startHour: state.TotalHours + 5);
		Assert.True(later.MayExecute(state));
		later.Execute(state);
		Assert.True(division.IsMoving);
		CMovement.RunHour(state, germany, division);
		Assert.Equal(CFixedPoint.Zero, division.MovementProgress);

		// Not to arrive before a far hour: it covers the way and waits at its end (section 5).
		var synchronised = new CSetUnitOrderCommand(Ger, division.Id, EUnitOrder.Move, 2, arriveHour: state.TotalHours + 10_000);
		synchronised.Execute(state);
		CFixedPoint distance = CMovement.Distance(state, division);
		division.MovementProgress = distance + CFixedPoint.FromInt(1);
		CMovement.RunHour(state, germany, division);
		Assert.Equal(1, division.Location);
		Assert.Equal(distance, division.MovementProgress);

		// A halt empties both hours.
		CMovement.Halt(state, division);
		Assert.Null(division.NotBeforeHour);
		Assert.Null(division.ArriveNotBeforeHour);
	}
}
