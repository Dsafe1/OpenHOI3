using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Joining, inviting, leaving and the rules, against <c>docs/mechanics/factions.md</c>, on <see cref="WarTests.World"/>
/// with three factions: Germany leads the axis with Italy; Slovakia is Germany's puppet and Lithuania Poland's;
/// France guarantees Poland and Britain is allied to it. The axis has <c>limited_war</c> and a modifier, the allies
/// <c>allience_wide_guarantee</c>.
/// </summary>
public class FactionTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Ita = new("ITA");
	private static readonly CCountryTag Fra = new("FRA");
	private static readonly CCountryTag Eng = new("ENG");
	private static readonly CCountryTag Slo = new("SLO");
	private static readonly CCountryTag Lit = new("LIT");

	internal static CGameState World()
	{
		var axisModifier = new CModifier("axis");
		axisModifier.Add("territorial_pride", CFixedPoint.Parse("0.1"));
		var axis = new CIdeologyGroup("fascism", 200000, 200000) { Faction = "axis", FactionModifier = axisModifier };
		axis.FactionRules.Add(CFactions.LimitedWarRule);
		var allies = new CIdeologyGroup("democracy", 0, -146000) { Faction = "allies" };
		allies.FactionRules.Add(CFactions.GuaranteeRule);
		CGameState state = WarTests.World(new[] { axis, allies });
		state.Defines.Set(CAlignment.Section, CFactionOffers.JoinDistance, CFixedPoint.FromInt(50));
		state.Defines.Set(CAlignment.Section, CStrategicResources.BonusDistance, CFixedPoint.FromInt(100));
		state.Defines.Set(COfferRules.Section, "JOIN_FACTION_NEUTRALITY", CFixedPoint.FromInt(25));
		state.Defines.Set(COfferRules.Section, "INVITE_FACTION_NEUTRALITY", CFixedPoint.FromInt(25));
		// Poland sits near the axis's corner and in no faction.
		C(state, Pol).AlignmentX = 190000;
		C(state, Pol).AlignmentY = 190000;
		return state;
	}

	private static CCountry C(CGameState state, CCountryTag tag) => state.Country(tag)!;

	private static COffer Offer(CGameState state, CCountryTag actor, CCountryTag recipient, bool join, EOfferStage stage) =>
		new(EOfferKind.Faction, actor, recipient, stage, join, state.TotalHours);

	private static CWar War(CGameState state, CCountryTag attacker, CCountryTag defender, bool limited = false)
	{
		var war = new CWar("test") { IsLimited = limited };
		state.Wars.Add(war);
		CWarSides.Join(state, war, attacker, attacking: true);
		CWarSides.Join(state, war, defender, attacking: false);
		return war;
	}

	[Fact]
	public void A_joiner_goes_to_the_back_and_into_its_leaders_wars_as_an_attacker_only_where_not_limited()
	{
		CGameState state = World();
		CWar attack = War(state, Ger, Fra);
		CWar limited = War(state, Ger, Eng, limited: true);
		CWar defence = War(state, Lit, Ger);

		CFactions.AddCountry(state, C(state, Pol), "axis", joinWars: true);

		Assert.Equal(new[] { Ger, Ita, Pol }, CCountryLists.FactionMembers(state, "axis").Select(c => c.Tag));
		Assert.Contains(Pol, attack.Attackers);
		Assert.DoesNotContain(Pol, limited.Attackers);
		Assert.Contains(Pol, defence.Defenders);
		Assert.Equal(-100, CRelations.Of(C(state, Pol), C(state, Fra)).Raw / 1000);
	}

	[Fact]
	public void A_puppet_joins_only_the_wars_its_overlord_is_in()
	{
		CGameState state = World();
		CWar war = War(state, Ger, Fra);

		CFactions.AddCountry(state, C(state, Lit), "axis", joinWars: true);
		Assert.DoesNotContain(Lit, war.Attackers);

		CWarSides.Join(state, war, Pol, attacking: false);
		state.Wars.Remove(war);
		CWar second = War(state, Ger, Eng);
		CWarSides.Join(state, second, Pol, attacking: true);
		C(state, Lit).Faction = "";
		CFactions.AddCountry(state, C(state, Lit), "axis", joinWars: true);
		Assert.Contains(Lit, second.Attackers);
	}

	[Fact]
	public void Join_and_invite_rows_are_valid_only_towards_and_from_a_faction_leader()
	{
		CGameState state = World();

		Assert.True(COfferRules.IsValid(state, Offer(state, Pol, Ger, join: true, EOfferStage.Proposed)));
		Assert.False(COfferRules.IsValid(state, Offer(state, Pol, Ita, join: true, EOfferStage.Proposed)));
		Assert.True(COfferRules.IsValid(state, Offer(state, Ger, Pol, join: false, EOfferStage.Proposed)));
		Assert.False(COfferRules.IsValid(state, Offer(state, Ita, Pol, join: false, EOfferStage.Proposed)));
		C(state, Pol).InExile = true;
		Assert.False(COfferRules.IsValid(state, Offer(state, Pol, Ger, join: true, EOfferStage.Proposed)));
	}

	[Fact]
	public void A_row_is_greyed_by_a_puppet_recipient_neutrality_distance_and_war_in_that_order()
	{
		CGameState state = World();
		COffer invite = Offer(state, Ger, Pol, join: false, EOfferStage.Proposed);
		Assert.Equal(EFactionRefusal.None, CFactionOffers.Refusal(state, invite, C(state, Ger), C(state, Pol)));

		// Slovakia, Germany's puppet: no invitation, whatever else holds.
		COffer toSlovakia = Offer(state, Ger, Slo, join: false, EOfferStage.Proposed);
		Assert.Equal(EFactionRefusal.Puppet, CFactionOffers.Refusal(state, toSlovakia, C(state, Ger), C(state, Slo)));

		C(state, Pol).EffectiveNeutrality = CFixedPoint.FromInt(26);
		Assert.Equal(EFactionRefusal.Neutrality, CFactionOffers.Refusal(state, invite, C(state, Ger), C(state, Pol)));
		C(state, Pol).EffectiveNeutrality = CFixedPoint.FromInt(25);

		// Fifty from the corner is not under fifty.
		C(state, Pol).AlignmentX = 150000;
		C(state, Pol).AlignmentY = 200000;
		Assert.Equal(EFactionRefusal.Distance, CFactionOffers.Refusal(state, invite, C(state, Ger), C(state, Pol)));
		C(state, Pol).AlignmentX = 151000;
		Assert.Equal(EFactionRefusal.None, CFactionOffers.Refusal(state, invite, C(state, Ger), C(state, Pol)));

		War(state, Ita, Pol);
		Assert.Equal(EFactionRefusal.War, CFactionOffers.Refusal(state, invite, C(state, Ger), C(state, Pol)));
	}

	[Fact]
	public void A_proposal_does_nothing_and_an_accepted_join_brings_the_subjects_and_ends_alliances_with_members()
	{
		CGameState state = World();
		state.Played.Add(Fra);
		state.Alliances.Add(new CAlliance(Pol, Ita));
		C(state, Pol).DiplomaticInfluence = CFixedPoint.FromInt(3);

		new COfferCommand(Offer(state, Pol, Ger, join: true, EOfferStage.Proposed)).Execute(state);
		Assert.Equal("", C(state, Pol).Faction);
		Assert.Equal(CFixedPoint.FromInt(3), C(state, Pol).DiplomaticInfluence);

		var told = new System.Collections.Generic.List<CPactMessage>();
		state.PactMessaged += told.Add;
		new COfferCommand(Offer(state, Pol, Ger, join: true, EOfferStage.Accepted)).Execute(state);

		Assert.Equal("axis", C(state, Pol).Faction);
		Assert.Equal("axis", C(state, Lit).Faction);
		Assert.DoesNotContain(state.Alliances, a => a.Joins(Pol, Ita));
		// Britain is no member: that alliance stays.
		Assert.Contains(state.Alliances, a => a.Joins(Pol, Eng));
		CPactMessage message = Assert.Single(told);
		Assert.Equal("FACTIONJOINACCEPTOTHER", message.We);
		Assert.Equal(Fra, message.Told);
		Assert.Contains(("COMMENT", "FACTIONCOMMENT_axis"), message.Values);
	}

	[Fact]
	public void An_answer_finding_the_joiner_at_war_with_a_member_does_nothing()
	{
		CGameState state = World();
		War(state, Ita, Pol);

		new COfferCommand(Offer(state, Ger, Pol, join: false, EOfferStage.Accepted)).Execute(state);
		Assert.Equal("", C(state, Pol).Faction);
	}

	[Fact]
	public void Under_the_allies_rule_a_guarantee_is_every_members_and_an_answered_call_brings_the_faction()
	{
		CGameState state = World();
		C(state, Fra).Faction = "allies";
		C(state, Eng).Faction = "allies";
		C(state, Eng).FactionSeat = 1;

		new COfferCommand(new COffer(EOfferKind.Guarantee, Fra, Ita, EOfferStage.Proposed, true, state.TotalHours)).Execute(state);
		Assert.Contains(state.Guarantees, g => g.Guarantor == Eng && g.Guaranteed == Ita && g.ViaFaction);
		Assert.Contains(state.Guarantees, g => g.Guarantor == Fra && g.Guaranteed == Ita && !g.ViaFaction);

		// Poland is attacked; France answers its call, and Britain is called with it at once.
		state.Alliances.Clear();
		CWar war = War(state, Ger, Pol);
		var call = new COffer(EOfferKind.CallAlly, Pol, Fra, EOfferStage.Accepted, true, state.TotalHours) { Versus = Ger };
		new COfferCommand(call).Execute(state);
		Assert.Contains(Fra, war.Defenders);
		Assert.Contains(Eng, war.Defenders);
	}

	[Fact]
	public void A_member_carries_its_factions_modifier_and_a_leader_at_war_takes_an_aligned_outsiders_resources()
	{
		CGameState state = World();
		CCountryModifiers.Rebuild(state);
		Assert.Equal(CFixedPoint.Parse("0.1"), C(state, Ita).Modifier["territorial_pride"]);
		Assert.Equal(CFixedPoint.Zero, C(state, Pol).Modifier["territorial_pride"]);

		// Poland stands within a hundred of the axis's corner; Germany, its leader, takes from it only at war.
		Assert.False(CStrategicResources.GetsFrom(state, C(state, Ger), C(state, Pol)));
		War(state, Ger, Fra);
		Assert.True(CStrategicResources.GetsFrom(state, C(state, Ger), C(state, Pol)));
		Assert.False(CStrategicResources.GetsFrom(state, C(state, Ita), C(state, Pol)));
	}

	[Fact]
	public void Leave_faction_takes_the_country_out_of_the_wars_its_leader_leads_its_side_in()
	{
		CGameState state = World();
		CWar led = War(state, Ger, Fra);
		CWarSides.Join(state, led, Ita, attacking: true);
		CWar own = War(state, Ita, Eng);
		CEventScope scope = CEventScope.Start(Ita);

		Assert.True(CCountryEffects.TryRun(state, new CEffect(EEffectKind.Keyword, "leave_faction", new CScriptArgument("axis", false)), scope));

		Assert.Equal("", C(state, Ita).Faction);
		Assert.DoesNotContain(Ita, led.Attackers);
		Assert.Contains(Ita, own.Attackers);

		Assert.True(CCountryEffects.TryRun(state, new CEffect(EEffectKind.Keyword, "join_faction", new CScriptArgument("AXIS", false)), scope));
		Assert.Equal("axis", C(state, Ita).Faction);
		Assert.Contains(Ita, led.Attackers);
	}
}
