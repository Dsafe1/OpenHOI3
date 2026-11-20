using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The diplomacy screen's actions, against <c>docs/mechanics/agreements.md</c>, section 7, on
/// <see cref="ScriptWorld"/>: Germany and Italy the Axis, Poland in no faction.
/// </summary>
public class DiplomacyActionTests
{
	private static readonly Dictionary<string, string> Table = new()
	{
		["ENROUTE"] = "We have a diplomat enroute, and he will arrive on $DATE$",
		["DIP_NO_INFL"] = "You do not have the §Y$INF$§W diplomatic influence required",
		["DIP_INFLUENCECOST"] = "This will cost us §Y$COST$§W diplomatic influence points.",
		["DIP_THREAT"] = "This will generate §Y$AMOUNT$§W threat in §Y$PROV$§W",
		["BREAKNAPNEWDESC"] = "Do you wish to cancel the Non-Aggression Pact we have with $COUNTRY$?",
		["CANCELNAP_TOO_SOON_DESC"] = "Non-Aggression Pacts cannot be broken for the first §Y$MONTHS$§W months.",
		["CANCELNAP_TOO_LOW_BALANCE"] = "We need §Y$LIMIT_PERC$%§W of their brigades. We currently have §Y$CURR_PERC$%§W.",
		["June"] = "June",
	};

	private static string Localise(string key) => Table.GetValueOrDefault(key, key);

	private sealed class FixedScripts : IAiScripts
	{
		public int Score { get; init; }

		public int? NonHistoricalRelation(CGameState state, CCountry country, CCountry other) => null;

		public int DiploScore(CGameState state, string function, CCountry actor, CCountry recipient, CCountry observer) => Score;
	}

	private static CGameState World()
	{
		CGameState state = ScriptWorld.State();
		void Set(string name, string value) => state.Defines.Set(COfferRules.Section, name, CFixedPoint.Parse(value));
		Set("JOIN_ALLIANCE_INFLUENCE_COST", "5");
		Set("ALLIANCE_NEUTRALITY_LIMIT", "25");
		Set("NAP_JOIN_INFLUENCE_COST", "5");
		Set("LEAVE_NAP_INFLUENCE_COST", "1");
		Set("LEAVE_NAP_THREAT_COST", "2");
		Set("NAP_UNBREAKABLE_MONTHS", "6");
		Set("NAP_FORCE_BALANCE_RULE_MONTHS", "6");
		Set("NAP_BREAK_FORCE_BALANCE_1", "2");
		Set("INFLUENCE_INFLUENCE_COST", "3");
		foreach (CCountry country in state.Countries)
		{
			country.DiplomaticInfluence = CFixedPoint.FromInt(10);
		}
		state.Country(ScriptWorld.Ger)!.BaseIc = 101;
		return state;
	}

	private static CCountry C(CGameState state, CCountryTag tag) => state.Country(tag)!;

	[Fact]
	public void The_rows_are_in_the_originals_order_and_only_those_that_may_be_made()
	{
		CGameState state = World();

		List<COffer> toPoland = CDiplomacyActions.Rows(state, C(state, ScriptWorld.Ger), C(state, ScriptWorld.Pol));
		// Declare War first, at peace (war.md, section 10); no Declare Limited War, the Axis here having no rules.
		Assert.Equal(new[]
		{
			EOfferKind.DeclareWar,
			EOfferKind.Alliance, EOfferKind.NonAggressionPact, EOfferKind.Guarantee, EOfferKind.AskMilitaryAccess,
			EOfferKind.OfferMilitaryAccess, EOfferKind.Faction, EOfferKind.Embargo, EOfferKind.Licence, EOfferKind.Debt, EOfferKind.Influence,
			EOfferKind.Trade,
			EOfferKind.RequestLendLease, EOfferKind.OfferLendLease,
		}, toPoland.Select(row => row.Kind));
		// Invite to Faction is the faction row's clear value (factions.md, section 3).
		Assert.All(toPoland, row => Assert.Equal(row.Kind != EOfferKind.Faction, row.Value));

		// One faction: no alliance, no transit rights, no influence; and Germany, in one, aligns with nobody.
		List<COffer> toItaly = CDiplomacyActions.Rows(state, C(state, ScriptWorld.Ger), C(state, ScriptWorld.Ita));
		Assert.Equal(new[]
		{
			EOfferKind.Expedition, EOfferKind.NonAggressionPact, EOfferKind.Guarantee, EOfferKind.Embargo, EOfferKind.Licence, EOfferKind.Debt,
			EOfferKind.Trade,
			EOfferKind.RequestLendLease, EOfferKind.OfferLendLease,
		}, toItaly.Select(row => row.Kind));

		// Poland, in no faction, may align with the Axis's leader, and ask to join it.
		List<COffer> toGermany = CDiplomacyActions.Rows(state, C(state, ScriptWorld.Pol), C(state, ScriptWorld.Ger));
		Assert.Contains(toGermany, row => row.Kind == EOfferKind.Align && row.Value);
		Assert.Contains(toGermany, row => row.Kind == EOfferKind.Faction && row.Value);
		// Italy, a member that does not lead, is not asked to join and asks nobody.
		Assert.DoesNotContain(CDiplomacyActions.Rows(state, C(state, ScriptWorld.Pol), C(state, ScriptWorld.Ita)),
			row => row.Kind == EOfferKind.Faction);
	}

	[Fact]
	public void A_pact_standing_turns_its_row_round_and_a_guarantee_given_takes_its_row_away()
	{
		CGameState state = World();
		state.Naps.Add(new CNap(ScriptWorld.Ger, ScriptWorld.Pol, state.TotalHours));
		state.Guarantees.Add(new CGuarantee(ScriptWorld.Ger, ScriptWorld.Pol, state.TotalHours));

		List<COffer> rows = CDiplomacyActions.Rows(state, C(state, ScriptWorld.Ger), C(state, ScriptWorld.Pol));
		Assert.False(rows.Single(row => row.Kind == EOfferKind.NonAggressionPact).Value);
		Assert.DoesNotContain(rows, row => row.Kind == EOfferKind.Guarantee);
		Assert.Equal("CANCELNAPTITLE", CDiplomacyActions.Title(Localise, rows[2]));
		// Poland, guaranteed by Germany, is offered the guarantee and told why not.
		COffer back = CDiplomacyActions.Rows(state, C(state, ScriptWorld.Pol), C(state, ScriptWorld.Ger))
			.Single(row => row.Kind == EOfferKind.Guarantee);
		Assert.Equal("THEYGUARANT", CDiplomacyActions.Tooltip(Localise, state, back));
	}

	[Fact]
	public void A_diplomat_on_his_way_greys_every_row_and_says_when_he_arrives()
	{
		CGameState state = World();
		new OpenHOI3.Sim.Commands.COfferCommand(new COffer(EOfferKind.NonAggressionPact, ScriptWorld.Ger, ScriptWorld.Pol,
			EOfferStage.Proposed, true, state.TotalHours)).Execute(state);

		List<COffer> rows = CDiplomacyActions.Rows(state, C(state, ScriptWorld.Ger), C(state, ScriptWorld.Pol));
		Assert.All(rows, row => Assert.True(CDiplomacyActions.IsGreyed(state, row)));
		// Seven days on from 1 June 1936, at the same hour.
		Assert.Equal("We have a diplomat enroute, and he will arrive on  0:00, 8 June, 1936",
			CDiplomacyActions.Tooltip(Localise, state, rows[0]));
	}

	[Fact]
	public void Too_little_influence_is_said_whole_for_an_alliance_and_to_two_places_for_an_influence()
	{
		CGameState state = World();
		C(state, ScriptWorld.Ger).DiplomaticInfluence = CFixedPoint.FromInt(1);
		List<COffer> rows = CDiplomacyActions.Rows(state, C(state, ScriptWorld.Ger), C(state, ScriptWorld.Pol));

		Assert.Equal("You do not have the §Y5§W diplomatic influence required",
			CDiplomacyActions.Tooltip(Localise, state, rows.Single(row => row.Kind == EOfferKind.Alliance)));
		Assert.Equal("You do not have the §Y3.00§W diplomatic influence required",
			CDiplomacyActions.Tooltip(Localise, state, rows.Single(row => row.Kind == EOfferKind.Influence)));
	}

	[Fact]
	public void Revoking_a_pact_too_soon_says_so_and_then_asks_for_its_balance()
	{
		CGameState state = World();
		state.Naps.Add(new CNap(ScriptWorld.Ger, ScriptWorld.Pol, state.TotalHours));
		COffer revoke = new(EOfferKind.NonAggressionPact, ScriptWorld.Ger, ScriptWorld.Pol, EOfferStage.Proposed, false,
			state.TotalHours);

		Assert.Equal("Non-Aggression Pacts cannot be broken for the first §Y6§W months.",
			CDiplomacyActions.Tooltip(Localise, state, revoke));
		state.Naps[0] = state.Naps[0] with { Start = COfferRules.AddMonths(state.TotalHours, -7), Balance = 750 };
		Assert.Equal("We need §Y200%§W of their brigades. We currently have §Y75%§W.",
			CDiplomacyActions.Tooltip(Localise, state, revoke));
	}

	[Fact]
	public void The_dialog_says_what_revoking_costs_line_by_line_and_the_chance_only_where_the_AI_answers()
	{
		CGameState state = World();
		state.Naps.Add(new CNap(ScriptWorld.Ger, ScriptWorld.Pol, state.TotalHours));
		COffer revoke = new(EOfferKind.NonAggressionPact, ScriptWorld.Ger, ScriptWorld.Pol, EOfferStage.Proposed, false,
			state.TotalHours);
		Assert.Equal("Do you wish to cancel the Non-Aggression Pact we have with POL?\n"
			+ "This will generate §Y2.00§W threat in §Yp4§W\n"
			+ "This will cost us §Y1.00§W diplomatic influence points.",
			CDiplomacyActions.Description(Localise, state, revoke));

		state.AiScripts = new FixedScripts { Score = 59 };
		COffer offer = new(EOfferKind.Alliance, ScriptWorld.Ger, ScriptWorld.Pol, EOfferStage.Proposed, true, state.TotalHours);
		Assert.Equal("DIPCHANCE_MAYBE", CDiplomacyActions.Chance(Localise, state, offer));
		Assert.Equal("", CDiplomacyActions.Chance(Localise, state, offer with { Kind = EOfferKind.Guarantee }));
		Assert.Equal("", CDiplomacyActions.Chance(Localise, state, revoke));
	}

	private sealed class PostingScripts : IAiScripts
	{
		public int? NonHistoricalRelation(CGameState state, CCountry country, CCountry other) => null;

		public int DiploScore(CGameState state, string function, CCountry actor, CCountry recipient, CCountry observer)
		{
			state.Outbox.Add(new OpenHOI3.Sim.Commands.CChangeLawCommand(recipient.Tag, "free_press", "press_laws",
				payCost: false, postMessage: false));
			state.Random.Next(100);
			return 85;
		}
	}

	[Fact]
	public void TheChanceIsAskedOfTheScriptsKeptForLookingAndChangesNothing()
	{
		// docs/multiplayer.md, section 12: the display's scripts before the AI's own, and what a script posts or rolls
		// while it is asked is put back.
		CGameState state = World();
		state.AiScripts = new FixedScripts { Score = 5 };
		int made = 0;
		var display = new PostingScripts();
		state.DisplayScripts = () =>
		{
			made++;
			return display;
		};
		COffer offer = new(EOfferKind.Alliance, ScriptWorld.Ger, ScriptWorld.Pol, EOfferStage.Proposed, true, state.TotalHours);
		long draws = state.Random.Draws;

		Assert.Equal("DIPCHANCE_VLIKELY", CDiplomacyActions.Chance(Localise, state, offer));
		Assert.Equal(1, made);
		Assert.Empty(state.Outbox);
		Assert.Equal(draws, state.Random.Draws);

		// With none to be had - a failure to make one answers null - the game's own are asked, as before.
		state.DisplayScripts = () => null;
		Assert.Equal("DIPCHANCE_VUNLIKELY", CDiplomacyActions.Chance(Localise, state, offer));
	}
}
