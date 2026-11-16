using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Events;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A country's war record, its <c>lost_IC</c> and the world's last-battle losses, against
/// <c>docs/mechanics/triggers.md</c>, sections 5.1 and 5.2, on <see cref="ScriptWorld"/>'s strip of ground.
/// </summary>
public class WarRecordTests
{
	private static readonly CCountryTag Ger = ScriptWorld.Ger;
	private static readonly CCountryTag Pol = ScriptWorld.Pol;
	private static readonly CCountryTag Ita = ScriptWorld.Ita;
	private static readonly CCountryTag Reb = ScriptWorld.Reb;

	private static bool Holds(CGameState state, string text, CCountryTag country)
	{
		return CConditions.Holds(state, ScriptWorld.Condition(text), CEventScope.Start(country));
	}

	private static CUnit Unit(CCountry country, EUnitKind kind, EUnitOrder order = EUnitOrder.None, CUnit? under = null)
	{
		var unit = new CUnit(kind, country.Tag) { Order = order };
		if (under is null)
		{
			country.Units.Add(unit);
		}
		else
		{
			unit.Parent = under;
			under.Children.Add(unit);
		}
		return unit;
	}

	[Fact]
	public void The_record_counts_battles_once_by_who_holds_their_ground_fleets_fighting_and_air_units_under_a_bombing_order()
	{
		CGameState state = ScriptWorld.State(atWar: true);
		CCountry germany = state.Country(Ger)!;
		state.Province(6).Controller = Reb;
		var warsaw = new CCombat(4);       // Poland holds it, at war with Germany
		var border = new CCombat(2);       // Germany's own
		var rome = new CCombat(7);         // Italy's, at peace
		var wilds = new CCombat(6);        // the rebels'
		var sea = new CCombat(5, ECombatKind.Naval);
		CUnit corps = Unit(germany, EUnitKind.Corps);
		Unit(germany, EUnitKind.Division, under: corps).Combats.Add(warsaw);
		Unit(germany, EUnitKind.Division, under: corps).Combats.Add(warsaw);  // the same battle: once
		CUnit both = Unit(germany, EUnitKind.Division);
		both.Combats.Add(border);
		both.Combats.Add(rome);
		corps.Combats.Add(wilds);
		Unit(germany, EUnitKind.Navy).Combats.Add(sea);
		Unit(germany, EUnitKind.Navy);
		Unit(germany, EUnitKind.Air, EUnitOrder.StrategicBombing);
		Unit(germany, EUnitKind.Air, EUnitOrder.CarrierProtection);
		Unit(germany, EUnitKind.Air, EUnitOrder.Interdiction);
		Unit(germany, EUnitKind.Air, EUnitOrder.AirConvoyRaid);
		Unit(germany, EUnitKind.Air, EUnitOrder.AirSuperiority).Combats.Add(new CCombat(4, ECombatKind.Air));

		// Nothing is seen until the count.
		Assert.False(Holds(state, "total_offensives = 1", Ger));
		CWarRecord.CountDaily(state);

		Assert.Equal(2, germany.TotalOffensives);
		Assert.Equal(2, germany.TotalDefensives);
		Assert.Equal(1, germany.TotalSeaBattles);
		Assert.Equal(3, germany.TotalWeBomb);
		Assert.True(Holds(state, "total_offensives = 2.9", Ger));       // the value cut to whole
		Assert.False(Holds(state, "total_offensives = 3", Ger));
		Assert.True(Holds(state, "total_defensives = 2", Ger));
		Assert.True(Holds(state, "total_sea_battles = 1", Ger));
		Assert.False(Holds(state, "total_sea_battles = 2", Ger));
		Assert.True(Holds(state, "total_we_bomb = 3", Ger));
		Assert.False(Holds(state, "total_we_bomb = 4", Ger));
		Assert.True(Holds(state, "total_offensives = 0", Pol));
		Assert.False(Holds(state, "total_offensives = 1", Pol));

		// A battle over is not seen until the next count.
		both.Combats.Clear();
		Assert.True(Holds(state, "total_defensives = 2", Ger));
		CWarRecord.Count(state, germany);
		Assert.False(Holds(state, "total_defensives = 1", Ger));
	}

	[Fact]
	public void Lost_ic_is_the_share_of_the_owned_provinces_local_ic_held_and_the_held_part_itself_where_the_whole_is_not_above_nought()
	{
		CGameState state = ScriptWorld.State();
		CCountry germany = state.Country(Ger)!;
		germany.Technology.Add("ic_modifier", CFixedPoint.Parse("0.1"));
		// Penalties, as the base game's local modifiers all are: Border and Posen at -0.5, Poland holding Posen.
		state.Province(2).Modifier.Add("local_ic", CFixedPoint.Parse("-0.5"));
		state.Province(3).Modifier.Add("local_ic", CFixedPoint.Parse("-0.5"));
		state.Province(3).Controller = Pol;
		CWarRecord.CountDaily(state);

		// Each weighs -0.5 x 1.1 = -0.55; the whole is under nought, so the figure is Border's alone.
		Assert.Equal(CFixedPoint.Parse("-0.55"), germany.LostIc);
		Assert.False(Holds(state, "lost_IC = 0", Ger));
		Assert.True(Holds(state, "lost_IC = -0.55", Ger));
		Assert.False(Holds(state, "lost_IC = -0.549", Ger));

		// A mod's bonuses: 0.22 each, half of it held - the share held, not lost.
		state.Province(2).Modifier.Set("local_ic", CFixedPoint.Parse("0.2"));
		state.Province(3).Modifier.Set("local_ic", CFixedPoint.Parse("0.2"));
		CWarRecord.CountDaily(state);
		Assert.Equal(CFixedPoint.Parse("0.5"), germany.LostIc);
		state.Province(3).Controller = Ger;
		CWarRecord.CountDaily(state);
		Assert.True(Holds(state, "lost_IC = 1", Ger));

		// No local modifier anywhere: nought.
		Assert.Equal(CFixedPoint.Zero, state.Country(Pol)!.LostIc);
	}

	private static CCombat Decided(ECombatKind kind, string attackerLosses, string defenderLosses, bool convoy = false,
		bool emptyDefender = false)
	{
		var combat = new CCombat(2, kind) { IsConvoyBattle = convoy };
		combat.Attacker.Standing.Add(Ger);
		if (!emptyDefender)
		{
			combat.Defender.Standing.Add(Pol);
		}
		combat.Attacker.Losses = CFixedPoint.Parse(attackerLosses);
		combat.Defender.Losses = CFixedPoint.Parse(defenderLosses);
		return combat;
	}

	[Fact]
	public void A_decided_battle_sets_the_worlds_figures_for_its_kind_strictly_more_than_the_whole_number_written()
	{
		CGameState state = ScriptWorld.State();

		CCombats.Conclude(state, Decided(ECombatKind.Land, "2500.7", "900.999"), attackerLost: true);
		Assert.Equal(2500, state.LastBattleLosses.LandLoser);
		Assert.Equal(900, state.LastBattleLosses.LandWinner);
		Assert.True(Holds(state, "last_battle_loser_losses = 2499", Ita));    // any scope: the world's figure
		Assert.False(Holds(state, "last_battle_loser_losses = 2500", Ita));
		Assert.True(Holds(state, "last_battle_winner_losses = 899.9", Ita));  // the value cut to whole
		Assert.False(Holds(state, "last_battle_winner_losses = 900", Ita));
		Assert.False(Holds(state, "last_air_battle_loser_losses = 0", Ita));

		CCombats.Conclude(state, Decided(ECombatKind.Air, "3", "12"), attackerLost: false);
		CCombats.Conclude(state, Decided(ECombatKind.Naval, "2", "0"), attackerLost: true);
		Assert.Equal((12L, 3L), (state.LastBattleLosses.AirLoser, state.LastBattleLosses.AirWinner));
		Assert.Equal((2L, 0L), (state.LastBattleLosses.NavalLoser, state.LastBattleLosses.NavalWinner));
		Assert.True(Holds(state, "last_naval_battle_loser_losses = 1", Ger));
		Assert.Equal(2500, state.LastBattleLosses.LandLoser);             // the land figures stand

		// A bombing, a raid on a convoy and a battle with a side of no country write nothing.
		CCombats.Conclude(state, Decided(ECombatKind.GroundBombing, "50", "50"), attackerLost: true);
		CCombats.Conclude(state, Decided(ECombatKind.Naval, "7", "7", convoy: true), attackerLost: true);
		CCombats.Conclude(state, Decided(ECombatKind.Land, "7", "7", emptyDefender: true), attackerLost: true);
		Assert.Equal(2500, state.LastBattleLosses.LandLoser);
		Assert.Equal(2, state.LastBattleLosses.NavalLoser);
	}

	[Fact]
	public void The_figures_are_set_before_the_battles_on_actions_ask_them()
	{
		CGameState state = ScriptWorld.State();
		var lost = new CEvent(isProvinceEvent: false, "events/test.txt", 1) { Id = 1 };
		lost.Trigger.Children.AddRange(ScriptWorld.Condition("last_battle_loser_losses = 1000").Children);
		lost.Options.Add(new CEventOption { Name = "OK" });
		state.Events = new[] { lost };
		state.OnActions = new Dictionary<string, IReadOnlyList<int>> { ["on_battle_lost"] = new[] { 1 } };
		var fired = new List<int>();
		state.EventFired += (shown, scope) => fired.Add(shown.Id);

		CCombats.Conclude(state, Decided(ECombatKind.Land, "1200", "300"), attackerLost: true);

		Assert.Equal(new[] { 1 }, fired);
	}
}
