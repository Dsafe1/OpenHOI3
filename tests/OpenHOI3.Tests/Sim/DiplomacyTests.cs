using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What countries hold of each other, against <c>docs/mechanics/diplomacy.md</c>, on
/// <see cref="ScriptWorld"/>'s strip of ground: Germany owns three provinces beside Poland's one,
/// Italy one by itself, Slovakia and REB none; Germany and Italy are in the Axis.
/// </summary>
public class DiplomacyTests
{
	private static readonly CCountryTag Ger = ScriptWorld.Ger;
	private static readonly CCountryTag Pol = ScriptWorld.Pol;
	private static readonly CCountryTag Ita = ScriptWorld.Ita;
	private static readonly CCountryTag Slo = ScriptWorld.Slo;
	private static readonly CCountryTag Reb = ScriptWorld.Reb;

	/// <summary>Answers every pair with the same figure, and counts what it was asked.</summary>
	private sealed class FixedScripts : IAiScripts
	{
		public FixedScripts(int answer)
		{
			Answer = answer;
		}

		public int Answer { get; }

		public List<(CCountryTag, CCountryTag)> Asked { get; } = new();

		public int? NonHistoricalRelation(CGameState state, CCountry country, CCountry other)
		{
			Asked.Add((country.Tag, other.Tag));
			return Answer;
		}

		public int DiploScore(CGameState state, string function, CCountry actor, CCountry recipient, CCountry observer)
		{
			return Answer;
		}
	}

	private static CCountry C(CGameState state, CCountryTag tag) => state.Country(tag)!;

	private static CFixedPoint Relation(CGameState state, CCountryTag holder, CCountryTag other) =>
		CRelations.Of(C(state, holder), C(state, other));

	private static CFixedPoint N(string text) => CFixedPoint.Parse(text);

	[Fact]
	public void Every_country_holds_a_record_towards_every_country_itself_included()
	{
		CGameState state = ScriptWorld.State();

		foreach (CCountry country in state.Countries)
		{
			Assert.Equal(state.Countries.Count, country.Diplomacy.Length);
			Assert.Equal(country.Tag, country.Diplomacy[country.Index].Other);
		}
	}

	[Fact]
	public void A_change_writes_both_sides_and_is_held_between_minus_and_plus_200()
	{
		CGameState state = ScriptWorld.State();
		CRelations.SetOneSide(C(state, Pol), C(state, Ger), CFixedPoint.FromInt(30));

		CRelations.Change(C(state, Ger), C(state, Pol), CFixedPoint.FromInt(190));

		Assert.Equal(CFixedPoint.FromInt(190), Relation(state, Ger, Pol));
		Assert.Equal(CFixedPoint.FromInt(190), Relation(state, Pol, Ger));
		CRelations.Change(C(state, Pol), C(state, Ger), CFixedPoint.FromInt(-500));
		Assert.Equal(CFixedPoint.FromInt(-200), Relation(state, Ger, Pol));
	}

	[Fact]
	public void The_historys_relation_sets_one_side_and_an_ended_one_sets_it_back_to_nought()
	{
		CGameState state = ScriptWorld.State();
		var entries = new[]
		{
			new CHistoryRelation(Ger, Ita, CFixedPoint.FromInt(200), new CDate(1936, 1, 1), null),
			new CHistoryRelation(Ger, Pol, CFixedPoint.FromInt(50), new CDate(1930, 1, 1), new CDate(1935, 1, 1)),
			new CHistoryRelation(Ger, Slo, CFixedPoint.FromInt(70), new CDate(1937, 1, 1), null),
		};

		CRelations.SetFromHistory(state, entries, new CDate(1936, 6, 1));

		Assert.Equal(CFixedPoint.FromInt(200), Relation(state, Ger, Ita));
		Assert.Equal(CFixedPoint.Zero, Relation(state, Ita, Ger));
		Assert.Equal(CFixedPoint.Zero, Relation(state, Ger, Pol));
		Assert.Equal(CFixedPoint.Zero, Relation(state, Ger, Slo));
	}

	[Fact]
	public void A_new_game_asks_the_script_for_every_owning_pair_and_fills_only_what_is_still_nought()
	{
		CGameState state = ScriptWorld.State();
		var scripts = new FixedScripts(75);
		state.AiScripts = scripts;
		CRelations.SetOneSide(C(state, Ger), C(state, Ita), CFixedPoint.FromInt(200));
		var reports = new List<string>();

		CRelations.SetAtStart(state, reports.Add);

		Assert.Equal(CFixedPoint.FromInt(200), Relation(state, Ger, Ita));    // kept
		Assert.Equal(CFixedPoint.FromInt(75), Relation(state, Ita, Ger));     // one side each
		Assert.Equal(CFixedPoint.FromInt(75), Relation(state, Ger, Ger));     // itself included
		Assert.Equal(CFixedPoint.FromInt(25), Relation(state, Ger, Slo));     // owns nothing
		Assert.Equal(CFixedPoint.FromInt(25), Relation(state, Ger, Reb));     // the rebels
		Assert.Equal(CFixedPoint.Zero, Relation(state, Slo, Ger));            // owns nothing: not filled
		Assert.Equal(CFixedPoint.Zero, Relation(state, Reb, Ger));
		Assert.DoesNotContain(scripts.Asked, pair => pair.Item2 == Slo || pair.Item2 == Reb || pair.Item1 == Slo);
		Assert.Empty(reports);
	}

	[Fact]
	public void Without_the_script_a_new_games_relations_are_left_and_reported()
	{
		CGameState state = ScriptWorld.State();
		var reports = new List<string>();

		CRelations.SetAtStart(state, reports.Add);

		Assert.Equal(CFixedPoint.Zero, Relation(state, Ger, Pol));
		Assert.Equal(CFixedPoint.FromInt(25), Relation(state, Ger, Slo));
		Assert.Single(reports);
	}

	[Fact]
	public void A_month_moves_neighbours_at_peace_by_the_whole_drift_half_from_each_end()
	{
		CGameState state = ScriptWorld.State();

		CRelations.RunMonth(state);

		Assert.Equal(N("0.050"), Relation(state, Ger, Pol));
		Assert.Equal(N("0.050"), Relation(state, Pol, Ger));
		Assert.Equal(CFixedPoint.Zero, Relation(state, Ger, Ita));             // not neighbours
	}

	[Fact]
	public void At_war_a_month_takes_a_tenth_and_being_neighbours_gives_nothing()
	{
		CGameState state = ScriptWorld.State(atWar: true);

		CRelations.RunMonth(state);

		Assert.Equal(N("-0.100"), Relation(state, Ger, Pol));
	}

	[Fact]
	public void A_guarantee_drifts_both_ways_and_a_pair_above_100_gains_nothing()
	{
		CGameState state = ScriptWorld.State();
		state.Guarantees.Add(new CGuarantee(Ger, Ita));
		CRelations.Change(C(state, Ger), C(state, Pol), CFixedPoint.FromInt(150));

		CRelations.RunMonth(state);

		Assert.Equal(N("0.080"), Relation(state, Ger, Ita));
		Assert.Equal(CFixedPoint.FromInt(150), Relation(state, Ger, Pol));
	}

	[Fact]
	public void A_puppet_and_its_master_nobody_plays_gain_at_least_two_a_month()
	{
		CGameState state = ScriptWorld.State();
		C(state, Ita).Overlord = Ger;

		CRelations.RunMonth(state);

		// 250 + 2000 from each end, halved: 1125 each visit.
		Assert.Equal(N("2.250"), Relation(state, Ger, Ita));
	}

	[Fact]
	public void A_friend_of_the_owner_on_the_winning_side_gains_5_with_the_owner_and_the_owner_nothing()
	{
		CGameState state = ScriptWorld.State(atWar: true);
		var combat = new CCombat(2, ECombatKind.Land);
		combat.Attacker.Standing.AddRange(new[] { Ita, Ger });
		combat.Defender.Standing.Add(Pol);

		CCombats.Conclude(state, combat, attackerLost: false);

		Assert.Equal(CFixedPoint.FromInt(5), Relation(state, Ita, Ger));
		Assert.Equal(CFixedPoint.FromInt(5), Relation(state, Ger, Ita));
		Assert.Equal(CFixedPoint.Zero, Relation(state, Ger, Ger));
	}

	[Fact]
	public void No_gain_where_the_owner_is_not_the_losers_enemy()
	{
		CGameState state = ScriptWorld.State();
		var combat = new CCombat(2, ECombatKind.Land);
		combat.Attacker.Standing.Add(Ita);
		combat.Defender.Standing.Add(Pol);

		CCombats.Conclude(state, combat, attackerLost: false);

		Assert.Equal(CFixedPoint.Zero, Relation(state, Ita, Ger));
	}

	[Fact]
	public void A_day_holds_neutrality_within_100_and_moves_it_by_the_countrys_neutrality_change()
	{
		CGameState state = ScriptWorld.State();
		CCountry germany = C(state, Ger);
		germany.Neutrality = CFixedPoint.FromInt(130);
		germany.Modifier.Add(CNeutrality.ChangeModifier, N("-0.2"));

		CNeutrality.RunDaily(state);

		Assert.Equal(N("99.8"), germany.Neutrality);
		Assert.Equal(N("99.8"), germany.EffectiveNeutrality);                 // made again by the move
	}

	[Fact]
	public void Effective_neutrality_is_less_the_threat_from_the_most_threatening_country_and_never_below_nought()
	{
		CGameState state = ScriptWorld.State();
		CCountry germany = C(state, Ger);
		germany.Neutrality = CFixedPoint.FromInt(60);
		germany.MostThreatening = Pol;
		CGameState.Status(germany, C(state, Pol)).Threat = CFixedPoint.FromInt(15);

		CNeutrality.UpdateEffective(state, germany);
		Assert.Equal(CFixedPoint.FromInt(45), germany.EffectiveNeutrality);

		CGameState.Status(germany, C(state, Pol)).Threat = CFixedPoint.FromInt(80);
		CNeutrality.UpdateEffective(state, germany);
		Assert.Equal(CFixedPoint.Zero, germany.EffectiveNeutrality);
	}

	[Fact]
	public void The_relation_effect_changes_both_sides_and_THIS_is_the_scope_itself()
	{
		CGameState state = ScriptWorld.State();

		CEffects.Run(state, ScriptWorld.Effects("relation = { who = POL value = 12.5 }"), CEventScope.Start(Ger, 0));
		CEffects.Run(state, ScriptWorld.Effects("relation = { who = THIS value = 3 }"), CEventScope.Start(Ger, 0));

		Assert.Equal(N("12.5"), Relation(state, Pol, Ger));
		Assert.Equal(N("12.5"), Relation(state, Ger, Pol));
		Assert.Equal(CFixedPoint.FromInt(3), Relation(state, Ger, Ger));
	}

	[Fact]
	public void The_neutrality_effect_adds_floored_at_nought_and_uncapped()
	{
		CGameState state = ScriptWorld.State();
		CCountry germany = C(state, Ger);
		germany.Neutrality = CFixedPoint.FromInt(95);

		CEffects.Run(state, ScriptWorld.Effects("neutrality = 10"), CEventScope.Start(Ger, 0));
		Assert.Equal(CFixedPoint.FromInt(105), germany.Neutrality);
		Assert.Equal(CFixedPoint.FromInt(100), germany.EffectiveNeutrality);

		CEffects.Run(state, ScriptWorld.Effects("neutrality = -200"), CEventScope.Start(Ger, 0));
		Assert.Equal(CFixedPoint.Zero, germany.Neutrality);
	}

	[Fact]
	public void The_conditions_ask_the_scopes_relation_and_threat_and_both_neutralities()
	{
		CGameState state = ScriptWorld.State();
		CCountry germany = C(state, Ger);
		CRelations.Change(germany, C(state, Pol), CFixedPoint.FromInt(40));
		CGameState.Status(germany, C(state, Pol)).Threat = CFixedPoint.FromInt(20);
		germany.Neutrality = CFixedPoint.FromInt(70);
		germany.EffectiveNeutrality = CFixedPoint.FromInt(50);

		bool Holds(string text) => CConditions.Holds(state, ScriptWorld.Condition(text), CEventScope.Start(Ger, 0));

		Assert.True(Holds("relation = { who = POL value = 40 }"));
		Assert.False(Holds("relation = { who = POL value = 41 }"));
		Assert.True(Holds("threat = { which = POL value = 20 }"));
		Assert.False(Holds("threat = { which = POL value = 21 }"));
		Assert.True(Holds("neutrality = 50"));
		Assert.False(Holds("neutrality = 51"));
		Assert.True(Holds("base_neutrality = 70"));
		Assert.True(Holds("is_threatend = 20"));          // Poland is Germany's neighbour
		Assert.False(Holds("is_threatend = 21"));
	}
}
