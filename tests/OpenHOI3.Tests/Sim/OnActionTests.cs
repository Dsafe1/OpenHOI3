using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Events;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The on_actions, against <c>docs/mechanics/events.md</c>, section 8, on <see cref="ScriptWorld"/>'s
/// strip of ground.
/// </summary>
public class OnActionTests
{
	private static readonly CCountryTag Ger = ScriptWorld.Ger;
	private static readonly CCountryTag Pol = ScriptWorld.Pol;
	private static readonly CCountryTag Ita = ScriptWorld.Ita;

	private static CEvent Event(int id, string trigger = "", bool once = false)
	{
		var made = new CEvent(isProvinceEvent: false, "events/test.txt", id) { Id = id, FireOnlyOnce = once };
		made.Trigger.Children.AddRange(ScriptWorld.Condition(trigger).Children);
		made.Options.Add(new CEventOption { Name = "OK" });
		return made;
	}

	/// <summary>Who each event fired for, in order, as (id, country, FROM).</summary>
	private static List<(int Id, CCountryTag? Country, CCountryTag? From, int Province)> Listen(CGameState state)
	{
		var fired = new List<(int, CCountryTag?, CCountryTag?, int)>();
		state.EventFired += (shown, scope) => fired.Add((shown.Id, scope.Country, scope.FromCountry, scope.Province));
		return fired;
	}

	private static CCombat Battle(ECombatKind kind, CCountryTag[] attackers, CCountryTag[] defenders, bool convoy = false)
	{
		var combat = new CCombat(2, kind) { IsConvoyBattle = convoy };
		combat.Attacker.Standing.AddRange(attackers);
		combat.Defender.Standing.AddRange(defenders);
		return combat;
	}

	[Fact]
	public void The_first_event_whose_trigger_holds_fires_and_an_id_that_is_no_event_is_passed_over()
	{
		CGameState state = ScriptWorld.State();
		state.Events = new[] { Event(1, "tag = POL"), Event(2), Event(3) };
		state.OnActions = new Dictionary<string, IReadOnlyList<int>> { ["on_battle_won"] = new[] { 99, 1, 2, 3 } };
		var fired = Listen(state);

		COnActions.Fire(state, "on_battle_won", CEventScope.Start(Ger));

		Assert.Equal(new[] { 2 }, fired.ConvertAll(f => f.Id));
	}

	[Fact]
	public void A_fire_once_event_that_has_fired_stands_in_front_of_those_after_it()
	{
		CGameState state = ScriptWorld.State();
		state.Events = new[] { Event(619, once: true), Event(620) };
		state.OnActions = new Dictionary<string, IReadOnlyList<int>> { ["on_battle_won"] = new[] { 619, 620 } };
		var fired = Listen(state);

		COnActions.Fire(state, "on_battle_won", CEventScope.Start(Ger));
		COnActions.Fire(state, "on_battle_won", CEventScope.Start(Ger));

		Assert.Equal(new[] { 619 }, fired.ConvertAll(f => f.Id));
	}

	[Fact]
	public void A_land_battle_fires_for_every_winner_then_every_loser_each_country_counting_it()
	{
		CGameState state = ScriptWorld.State();
		state.Events = new[] { Event(1), Event(2) };
		state.OnActions = new Dictionary<string, IReadOnlyList<int>>
		{
			["on_battle_won"] = new[] { 1 },
			["on_battle_lost"] = new[] { 2 },
		};
		var fired = Listen(state);

		CCombats.Conclude(state, Battle(ECombatKind.Land, new[] { Ger, Ita }, new[] { Pol }), attackerLost: false);

		Assert.Equal(new[] { (1, (CCountryTag?)Ger, (CCountryTag?)Pol, 2), (1, Ita, Pol, 2), (2, Pol, Ger, 2) }, fired);
		Assert.Equal(1, state.Country(Ger)!.LandBattlesFought);
		Assert.Equal(1, state.Country(Pol)!.LandBattlesFought);
		Assert.True(CConditions.Holds(state, ScriptWorld.Condition("land_battles_fought = 1"), CEventScope.Start(Ita)));
	}

	[Fact]
	public void A_raid_on_a_convoy_fires_nothing_and_a_land_battle_with_an_empty_side_neither()
	{
		CGameState state = ScriptWorld.State();
		state.Events = new[] { Event(1) };
		state.OnActions = new Dictionary<string, IReadOnlyList<int>> { ["on_naval_battle_won"] = new[] { 1 }, ["on_battle_won"] = new[] { 1 } };
		var fired = Listen(state);

		CCombats.Conclude(state, Battle(ECombatKind.Naval, new[] { Ger }, new[] { Pol }, convoy: true), attackerLost: true);
		CCombats.Conclude(state, Battle(ECombatKind.Land, new[] { Ger }, System.Array.Empty<CCountryTag>()), attackerLost: true);

		Assert.Empty(fired);
		Assert.Equal(0, state.Country(Ger)!.NavalBattlesFought);
	}

	[Fact]
	public void A_battle_at_sea_fires_even_where_a_side_has_no_country()
	{
		CGameState state = ScriptWorld.State();
		state.Events = new[] { Event(1) };
		state.OnActions = new Dictionary<string, IReadOnlyList<int>> { ["on_naval_battle_won"] = new[] { 1 } };
		var fired = Listen(state);

		CCombats.Conclude(state, Battle(ECombatKind.Naval, new[] { Ger }, System.Array.Empty<CCountryTag>()), attackerLost: false);

		Assert.Equal(new[] { (1, (CCountryTag?)Ger, (CCountryTag?)null, 2) }, fired);
	}

	[Fact]
	public void A_pride_sunk_tells_the_shooter_then_the_owner_and_is_remembered()
	{
		CGameState state = ScriptWorld.State();
		state.Events = new[] { Event(1), Event(2) };
		state.OnActions = new Dictionary<string, IReadOnlyList<int>>
		{
			["on_enemy_pride_sunk"] = new[] { 1 },
			["on_our_pride_sunk"] = new[] { 2 },
		};
		var fired = Listen(state);
		var type = new CSubUnitType("battleship", EUnitBranch.Naval);
		var pride = new CSubUnit(type) { Name = "Bismarck", IsPride = true, Unit = new CUnit(EUnitKind.Navy, Ger) };
		var shooter = new CSubUnit(type) { Name = "Rodney", Experience = CFixedPoint.FromInt(98), Unit = new CUnit(EUnitKind.Navy, Pol) };

		COnActions.PrideSunk(state, pride, shooter);

		Assert.Equal(new[] { (1, (CCountryTag?)Pol, (CCountryTag?)Ger, 0), (2, Ger, Pol, 0) }, fired);
		Assert.Equal(CFixedPoint.FromInt(100), shooter.Experience);
		Assert.Same(pride, state.LastPrideSunk);
		Assert.Same(shooter, state.LastPrideSinker);
	}
}
