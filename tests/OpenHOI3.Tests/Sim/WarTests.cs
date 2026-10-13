using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Declaring a war and who joins it, against <c>docs/mechanics/war.md</c>. The world, in a row:
///
///     1 Berlin --- 2 Warsaw --- 3 Rome --- 4 Paris --- 5 London --- 6 Bratislava --- 7 Vilnius
///
/// Germany leads the axis, with Italy in it; Slovakia is Germany's puppet and Lithuania Poland's. France
/// guarantees Poland and Britain is allied to it. Two casus belli types, both <c>always</c>: conquer and
/// puppet.
/// </summary>
public class WarTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Ita = new("ITA");
	private static readonly CCountryTag Fra = new("FRA");
	private static readonly CCountryTag Eng = new("ENG");
	private static readonly CCountryTag Slo = new("SLO");
	private static readonly CCountryTag Lit = new("LIT");

	internal static CGameState World(IReadOnlyList<OpenHOI3.Sim.Diplomacy.CIdeologyGroup>? groups = null)
	{
		CCountry Country(CCountryTag tag, int capital) =>
			new() { Tag = tag, Name = tag.Value, Adjective = tag.Value + "ish", DefinitionFile = "", Capital = capital };
		CCountry germany = Country(Ger, 1);
		germany.Faction = "axis";
		CCountry italy = Country(Ita, 3);
		italy.Faction = "axis";
		CCountry slovakia = Country(Slo, 6);
		slovakia.Overlord = Ger;
		CCountry lithuania = Country(Lit, 7);
		lithuania.Overlord = Pol;
		CCountryTag[] owners = { Ger, Pol, Ita, new("FRA"), new("ENG"), Slo, Lit };
		var provinces = new List<CProvince> { new(0, "", isSea: false) };
		for (int id = 1; id <= 7; id++)
		{
			var province = new CProvince(id, $"p{id}", isSea: false) { Owner = owners[id - 1], Controller = owners[id - 1], Continent = "europe" };
			var edges = new List<CProvinceEdge>();
			if (id > 1)
			{
				edges.Add(new CProvinceEdge(id - 1, EProvinceEdgeKind.Plain, 0, CFixedPoint.Zero));
			}
			if (id < 7)
			{
				edges.Add(new CProvinceEdge(id + 1, EProvinceEdgeKind.Plain, 0, CFixedPoint.Zero));
			}
			province.Edges = edges.ToArray();
			provinces.Add(province);
		}
		var mobilisation = new CModifier(CWarSides.Mobilisation);
		mobilisation.Add("reinforcement_bonus", CFixedPoint.One);
		var state = new CGameState(new CDate(1936, 6, 1), provinces,
			new[] { germany, Country(Pol, 2), italy, Country(Fra, 4), Country(Eng, 5), slovakia, lithuania },
			staticModifiers: new[] { mobilisation })
		{
			IdeologyGroups = groups ?? System.Array.Empty<OpenHOI3.Sim.Diplomacy.CIdeologyGroup>(),
		};
		void Set(string name, string value) => state.Defines.Set(COfferRules.Section, name, CFixedPoint.Parse(value));
		Set(CDeclarations.Belligerency, "33");
		Set(CDeclarations.RelationHit, "100");
		Set(CDeclarations.InfluenceCost, "0");
		Set(CCallsToArms.InfluenceCost, "1");
		Set("ALLIANCE_REJECT_RELATION_CHANGE", "-50");
		Set(CWarGoals.Cooldown, "1");
		state.Defines.Set("country", "MOBILIZATION_BONUS_DAYS", CFixedPoint.FromInt(30));
		state.CasusBelliTypes = new[]
		{
			CCasusBelliType.Null,
			new CCasusBelliType("conquer", 1) { Always = true, Rank = 1 },
			new CCasusBelliType("puppet", 2) { Always = true, Rank = 0, SpriteIndex = 3 },
		};
		state.Guarantees.Add(new CGuarantee(Fra, Pol));
		state.Alliances.Add(new CAlliance(Pol, Eng));
		foreach (CCountry country in state.Countries)
		{
			country.DiplomaticInfluence = CFixedPoint.FromInt(10);
		}
		return state;
	}

	private static CCountry C(CGameState state, CCountryTag tag) => state.Country(tag)!;

	private static CWarGoal Conquer(CGameState state, CCountryTag actor, CCountryTag recipient) =>
		new(state.CasusBelliTypes[1], actor, recipient);

	private static COffer Declaration(CGameState state, CCountryTag actor, CCountryTag recipient, bool value = true) =>
		new(EOfferKind.DeclareWar, actor, recipient, EOfferStage.Accepted, value, state.TotalHours)
		{
			WarGoal = Conquer(state, actor, recipient),
		};

	private static void Run(CGameState state, COffer offer) => new COfferCommand(offer).Execute(state);

	[Fact]
	public void A_declaration_makes_a_war_brings_in_puppets_guarantors_and_factions_and_asks_the_allies()
	{
		CGameState state = World();
		var told = new List<CPactMessage>();
		state.PactMessaged += told.Add;

		Run(state, Declaration(state, Ger, Pol));

		CWar war = Assert.Single(state.Wars);
		// Germany first; its puppet; its faction, after the loop. Poland first; then, in one loop in the
		// countries' order, its guarantor by an accepted call and its puppet.
		Assert.Equal(new[] { Ger, Slo, Ita }, war.Attackers);
		Assert.Equal(new[] { Pol, Fra, Lit }, war.Defenders);
		Assert.Equal(new CWarName(CWarName.Aggression, "GERish", "POLish", 1), war.MadeName);
		Assert.False(war.IsLimited);
		Assert.Equal("conquer", Assert.Single(war.GoalsOf(Ger)).Type.Name);
		// Britain, Poland's ally, is only asked - a call whose value is the war's limited flag.
		COffer call = Assert.Single(C(state, Eng).PendingOffers);
		Assert.Equal((EOfferKind.CallAlly, Pol, false), (call.Kind, call.Actor, call.Value));
		Assert.Equal(Ger, call.Versus);
		Assert.Contains(Eng, war.CallsWaiting);
		// Relation -100, stamped for a truce, mobilised.
		Assert.Equal(CFixedPoint.FromInt(-100), CGameState.Status(C(state, Ger), C(state, Pol)).Relation);
		Assert.Equal(state.TotalHours, CGameState.Status(C(state, Ger), C(state, Pol)).LastWar);
		Assert.True(C(state, Ger).Mobilised);
		Assert.Contains(C(state, Pol).EventModifiers, m => m.Modifier.Name == CWarSides.Mobilisation
			&& m.UntilHour == state.TotalHours + 30 * CGameState.HoursPerDay);
		// The guarantee between them... France's of Poland stands; the declaration's message.
		Assert.Contains(told, m => m.We == "WEDECLWAR" && m.They == "DECLWAR" && m.Messenger == "MESSAGE_HEAD_MARS");
		Assert.Contains(told, m => m.We == "FACTIONWAROFF" && m.Told == Ita);
	}

	[Fact]
	public void A_limited_war_leaves_the_declarers_faction_and_allies_out()
	{
		CGameState state = World();
		Run(state, Declaration(state, Ger, Pol, value: false));

		CWar war = Assert.Single(state.Wars);
		Assert.True(war.IsLimited);
		Assert.DoesNotContain(Ita, war.Attackers);
		Assert.Contains(Fra, war.Defenders);
		// The defender's allies are still asked - with calls that ask.
		Assert.True(Assert.Single(C(state, Eng).PendingOffers).Value);
	}

	[Fact]
	public void A_declaration_with_no_real_goal_does_nothing_and_the_war_effect_needs_none()
	{
		CGameState state = World();
		Run(state, Declaration(state, Ger, Pol) with { WarGoal = new CWarGoal(CCasusBelliType.Null, Ger, Pol) });
		Assert.Empty(state.Wars);

		CEffects.Run(state, ScriptWorld.Effects("war = { target = POL }"), CEventScope.Start(Ger));
		CWar war = Assert.Single(state.Wars);
		Assert.Empty(war.GoalsOf(Ger));
		// At war already: nothing more, goals neither.
		CEffects.Run(state, ScriptWorld.Effects("war = { target = POL attacker_goal = { casus_belli = conquer } }"), CEventScope.Start(Ger));
		Assert.Single(state.Wars);
		Assert.Empty(war.GoalsOf(Ger));
	}

	[Fact]
	public void A_second_war_of_the_same_aggressor_is_the_second()
	{
		CGameState state = World();
		state.Alliances.Clear();
		Run(state, Declaration(state, Ger, Pol));
		// Britain is nobody's ally now, and in no war: Germany declares on it too, widening nothing.
		C(state, Ger).Faction = "";
		Run(state, Declaration(state, Ger, Eng));
		Assert.Equal(2, state.Wars.Count);
		Assert.Equal(2, state.Wars[1].MadeName!.Order);
	}

	[Fact]
	public void The_row_is_greyed_by_a_truce_a_puppet_and_units_on_their_ground()
	{
		CGameState state = World();
		CCountry germany = C(state, Ger);
		CCountry poland = C(state, Pol);
		Assert.True(CDeclarations.IsSelectable(state, germany, poland));

		CGameState.Status(germany, poland).LastWar = state.TotalHours - CTruces.Hours + 1;
		Assert.True(CTruces.Has(state, germany, poland));
		Assert.False(CDeclarations.IsSelectable(state, germany, poland));
		CGameState.Status(germany, poland).LastWar = state.TotalHours - CTruces.Hours;
		Assert.False(CTruces.Has(state, germany, poland));
		Assert.True(CDeclarations.IsSelectable(state, germany, poland));

		// Lithuania is Poland's: only a country at war with Poland may declare on it; Slovakia only on Germany.
		Assert.False(CDeclarations.IsSelectable(state, germany, C(state, Lit)));
		Assert.False(CDeclarations.IsSelectable(state, C(state, Slo), poland));
		Assert.False(CDeclarations.IsSelectable(state, germany, C(state, Slo)));
	}

	[Fact]
	public void A_call_to_arms_accepted_joins_the_callers_side_and_one_declined_ends_the_alliance()
	{
		CGameState state = World();
		Run(state, Declaration(state, Ger, Pol));
		CWar war = state.Wars[0];
		COffer call = C(state, Eng).PendingOffers[0];

		Run(state, call with { Stage = EOfferStage.Declined });
		Assert.DoesNotContain(state.Alliances, a => a.Joins(Pol, Eng));
		Assert.Equal(CFixedPoint.FromInt(-50), CGameState.Status(C(state, Eng), C(state, Pol)).Relation);
		Assert.Single(CGameState.Status(C(state, Pol), C(state, Eng)).CasusBelli);
		Assert.DoesNotContain(Eng, war.CallsWaiting);

		// Asked again and accepted, Britain is a defender.
		state.Alliances.Add(new CAlliance(Pol, Eng));
		Run(state, call with { Stage = EOfferStage.Accepted });
		Assert.Contains(Eng, war.Defenders);
		Assert.Equal(CFixedPoint.FromInt(-100), CGameState.Status(C(state, Ger), C(state, Eng)).Relation);
	}

	[Fact]
	public void A_players_call_that_does_not_ask_is_accepted_for_him()
	{
		CGameState state = World();
		state.Played.Add(Eng);
		Run(state, Declaration(state, Ger, Pol));
		CPactPasses.AnswerOffers(state);
		COfferCommand answer = Assert.IsType<COfferCommand>(Assert.Single(state.Outbox));
		Assert.Equal(EOfferStage.Accepted, answer.Offer.Stage);
	}

	[Fact]
	public void A_goal_is_added_once_a_month_and_spends_a_casus_belli_of_its_type()
	{
		CGameState state = World();
		Run(state, Declaration(state, Ger, Pol));
		CCountry germany = C(state, Ger);
		CCountry poland = C(state, Pol);
		Assert.False(CWarGoals.CooledDown(state, germany, poland));
		var add = new COffer(EOfferKind.AddWarGoal, Ger, Pol, EOfferStage.Accepted, true, state.TotalHours)
		{
			WarGoal = new CWarGoal(state.CasusBelliTypes[2], Ger, Pol),
		};
		Run(state, add);
		Assert.Single(state.Wars[0].GoalsOf(Ger));

		for (int hour = 0; hour < 32 * CGameState.HoursPerDay; hour++)
		{
			state.AdvanceOneHour();
		}
		CGameState.Status(germany, poland).CasusBelli.Add(new CCasusBelli(2));
		Run(state, add);
		Assert.Equal(new[] { "conquer", "puppet" }, state.Wars[0].GoalsOf(Ger).Select(g => g.Type.Name));
		Assert.Empty(CGameState.Status(germany, poland).CasusBelli);
	}

	[Fact]
	public void A_casus_belli_given_in_play_is_the_null_type_and_the_condition_asks_the_types()
	{
		CGameState state = World();
		CEffects.Run(state, ScriptWorld.Effects("casus_belli = POL"), CEventScope.Start(Ger));
		Assert.Equal(0, Assert.Single(CGameState.Status(C(state, Ger), C(state, Pol)).CasusBelli).Type);
		// Both types are always and ask nothing: Germany has a casus belli on Poland, held or not.
		Assert.True(CConditions.Holds(state, ScriptWorld.Condition("casus_belli = POL"), CEventScope.Start(Ger)));
		state.CasusBelliTypes = new[] { CCasusBelliType.Null, new CCasusBelliType("conquer", 1) };
		Assert.False(CConditions.Holds(state, ScriptWorld.Condition("casus_belli = POL"), CEventScope.Start(Ger)));
	}
}
