using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A country's government against <c>docs/mechanics/politics.md</c>, sections 1 to 8: the parties from the history, the
/// monthly drift, the cabinet's seats, elections, national unity's day, strategic warfare, the filling of posts, and
/// the conditions and effects on them. Four ideologies in two groups - national_socialist and fascistic, social_conservative
/// and social_democrat - and four posts, the first two fixed. Germany rules as national socialists with a dictatorship and
/// owns Berlin; Britain is a social democracy holding elections every 48 months and owns nothing.
/// </summary>
public class PoliticsTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Eng = new("ENG");

	private const int NationalSocialist = 1;
	private const int Fascistic = 2;
	private const int SocialConservative = 3;
	private const int SocialDemocrat = 4;

	private static CGameState World()
	{
		var fascism = new CIdeologyGroup("fascism", 0, 0);
		fascism.IdeologyOrder.AddRange(new[] { "national_socialist", "fascistic" });
		fascism.Ideologies.UnionWith(fascism.IdeologyOrder);
		var democracy = new CIdeologyGroup("democracy", 0, 0);
		democracy.IdeologyOrder.AddRange(new[] { "social_conservative", "social_democrat" });
		democracy.Ideologies.UnionWith(democracy.IdeologyOrder);

		var dictatorship = new CGovernment("dictatorship");
		dictatorship.Accepted.UnionWith(new[] { "national_socialist", "fascistic" });
		var social = new CGovernment("social_democracy") { HasElections = true, Duration = 48 };
		social.Accepted.UnionWith(new[] { "social_conservative", "social_democrat" });
		social.Elected.UnionWith(new[] { "foreign_minister", "armament_minister" });

		var fractured = new CModifier(CCabinet.Fractured);
		fractured.Set(CNationalUnity.Modifier, CFixedPoint.Parse("-0.05"));

		CCountry germany = new() { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1 };
		germany.RulingIdeology = "national_socialist";
		germany.GovernmentType = "dictatorship";
		CCountry britain = new() { Tag = Eng, Name = "Britain", DefinitionFile = "" };
		britain.RulingIdeology = "social_democrat";
		britain.GovernmentType = "social_democracy";

		var state = new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger } },
			new[] { germany, britain }, staticModifiers: new[] { fractured })
		{
			IdeologyGroups = new[] { fascism, democracy },
			Governments = new Dictionary<string, CGovernment> { [dictatorship.Name] = dictatorship, [social.Name] = social },
			GovernmentPositions = new[]
			{
				new CGovernmentPosition("head_of_state", 1, false), new CGovernmentPosition("head_of_government", 2, false),
				new CGovernmentPosition("foreign_minister", 3, true), new CGovernmentPosition("armament_minister", 4, true),
			},
		};
		void Set(string section, string name, string value) => state.Defines.Set(section, name, CFixedPoint.Parse(value));
		Set(CCabinet.Section, "PARTY_POP_CHANGE_RATE", "10");
		Set(CCabinet.Section, "POPULARITIY_RANDOMNESS", "10");
		Set(CCabinet.Section, "ELECTION_MINISTER_DISSENT_PENALTY", "-0.1");
		Set(CCabinet.Section, "PARTY_POPULARITY_CUTOFF", "0.1");
		Set(CCabinet.Section, "ELECTION_RANDOMNESS", "0.15");
		Set(CStrategicWarfareImpacts.Section, "STRAT_CONVOY_DAMAGE", "-0.5");
		Set(CStrategicWarfareImpacts.Section, "STRAT_BOMBING_PENALTY", "-0.05");
		return state;
	}

	private static CMinister Minister(CCountry country, int id, string ideology, params string[] posts)
	{
		var minister = new CMinister(id, $"m{id}") { Ideology = ideology };
		foreach (string post in posts)
		{
			minister.SetType(post, new CMinisterType(new CModifier("type")));
		}
		country.Ministers[id] = minister;
		return minister;
	}

	private static CCountry C(CGameState state, CCountryTag tag) => state.Country(tag)!;

	[Fact]
	public void A_new_game_takes_the_history_raw_and_normalises_a_country_that_owns_land_once_more()
	{
		CGameState state = World();
		CCountry germany = C(state, Ger);
		germany.HistoryOrganisation["national_socialist"] = CFixedPoint.FromInt(90);
		germany.HistoryOrganisation["social_conservative"] = CFixedPoint.FromInt(30);
		germany.HistoryOrganisation["social_democrat"] = CFixedPoint.FromInt(40);
		C(state, Eng).HistoryOrganisation["social_democrat"] = CFixedPoint.FromInt(40);
		CPolitics.SetUpAtStart(state);

		// Germany owns Berlin: 90/30/40 of 160, each to a tenth of a point - 56.2, 18.7, 25.0.
		Assert.Equal(new long[] { 0, 56200, 0, 18700, 25000 }, germany.Organisation.Values);
		// Britain owns nothing and keeps its history's figure as it was written.
		Assert.Equal(40000, C(state, Eng).Organisation[SocialDemocrat]);
		Assert.Equal(40000, C(state, Eng).Organisation.Tallies[SocialDemocrat, CPartyFigures.HistorySlot]);
	}

	[Fact]
	public void The_drift_closes_a_tenth_of_the_gap_draws_for_every_party_and_does_not_apply_a_figure_below_nought()
	{
		CGameState state = World();
		CPolitics.SetUpAtStart(state);
		CCountry germany = C(state, Ger);
		germany.Popularity.Values[0] = 1000;
		germany.Popularity.Values[NationalSocialist] = 60000;
		germany.Organisation.Values[NationalSocialist] = 90000;
		germany.Popularity.Values[SocialConservative] = 40000;
		germany.Organisation.Values[SocialConservative] = 0;

		var draws = new CRandom();
		draws.Restore(state.Random.Snapshot());
		var expected = new long[5];
		long[] popularity = { 1000, 60000, 0, 40000, 0 };
		long[] organisation = { 0, 90000, 0, 0, 0 };
		for (int i = 0; i < 5; i++)
		{
			long next = popularity[i] + (organisation[i] - popularity[i]) / 10 + (draws.Next() % 1000) * 10 - 5000;
			expected[i] = next >= 0 ? next : popularity[i];
		}
		CParties.Drift(state, germany);

		// Party 0 is drifted and drawn for, and never normalised.
		Assert.Equal(expected[0], germany.Popularity[0]);
		// The rest are normalised to a hundred afterwards.
		long sum = expected[1] + expected[2] + expected[3] + expected[4];
		Assert.Equal(expected[NationalSocialist] * 1000 / sum * 100000 / 1000, germany.Popularity[NationalSocialist]);
		Assert.Equal(0, germany.Popularity.Tallies[NationalSocialist, CPartyFigures.DriftSlot]);   // the month's tallies cleared
	}

	[Fact]
	public void Seats_go_by_share_above_the_cutoff_rounded_to_even_greatest_first_and_what_is_left_goes_to_nobody()
	{
		CGameState state = World();
		CPolitics.SetUpAtStart(state);
		CCountry germany = C(state, Ger);

		// Two changeable posts: 60 and 25 of 85 after the 9 and the 6 are cut - 1.41 and 0.59 seats, one each.
		germany.Popularity.Values[NationalSocialist] = 60000;
		germany.Popularity.Values[SocialConservative] = 25000;
		germany.Popularity.Values[SocialDemocrat] = 9000;
		germany.Popularity.Values[Fascistic] = 6000;
		CCabinet.Allot(state, germany);
		Assert.Equal(new[] { 0, 1, 0, 1, 0 }, germany.Cabinet);
		Assert.True(CCabinet.IsFractured(state, germany));

		// Four parties of a quarter: half a seat each rounds to nought, and both seats go to nobody.
		for (int i = 1; i <= 4; i++)
		{
			germany.Popularity.Values[i] = 25000;
		}
		CCabinet.Allot(state, germany);
		Assert.Equal(new[] { 0, 0, 0, 0, 0 }, germany.Cabinet);
		Assert.False(CCabinet.IsFractured(state, germany));
	}

	[Fact]
	public void A_fractured_government_has_its_modifier_and_loses_national_unity_daily_scaled_and_slowed_from_fifty()
	{
		CGameState state = World();
		CPolitics.SetUpAtStart(state);
		CCountry germany = C(state, Ger);
		germany.Cabinet[SocialConservative] = 1;
		germany.NationalUnity = CFixedPoint.FromInt(90);
		CCountryModifiers.Rebuild(state);
		Assert.Equal(CFixedPoint.Parse("-0.05"), germany.Modifier[CNationalUnity.Modifier]);

		// A twentieth a month is two thousandths a day.
		CNationalUnity.RunDaily(state);
		Assert.Equal(CFixedPoint.Parse("89.998"), germany.NationalUnity);

		// Propaganda's tenth less: -2 x 0.9 is -1.8, cut to -1.
		germany.Modifier.Set(CNationalUnity.Effect, CFixedPoint.Parse("-0.1"));
		CNationalUnity.RunDaily(state);
		Assert.Equal(CFixedPoint.Parse("89.997"), germany.NationalUnity);

		// At 30 the day's -54 is slowed by the square root of the 20 points from 50: -13, a thousandth.
		germany.NationalUnity = CFixedPoint.FromInt(30);
		germany.Modifier.Set(CNationalUnity.Effect, CFixedPoint.Zero);
		CNationalUnity.RunDaily(state);
		Assert.Equal(CFixedPoint.Parse("29.999"), germany.NationalUnity);

		// The effect is scaled and neither divided nor slowed, and held to 1..100.
		CNationalUnity.Change(germany, -50000);
		Assert.Equal(CFixedPoint.One, germany.NationalUnity);
	}

	[Fact]
	public void Strategic_warfare_scales_by_ic_holds_its_impact_to_two_and_forgets_after_a_week()
	{
		CGameState state = World();
		CCountry germany = C(state, Ger);
		germany.TotalIc = 50;                                   // half a scale
		CStrategicWarfareImpacts.AddConvoy(state, germany, 100);
		Assert.Equal(50, germany.StrategicWarfare.ConvoyImpact);
		CStrategicWarfareImpacts.AddConvoy(state, germany, 10000);
		Assert.Equal(CStrategicWarfare.MostImpact, germany.StrategicWarfare.ConvoyImpact);

		for (int hour = 0; hour <= CStrategicWarfare.KeptHours; hour++)
		{
			state.AdvanceOneHour();
		}
		CStrategicWarfareImpacts.Prune(state, germany.StrategicWarfare);
		Assert.Empty(germany.StrategicWarfare.Convoys);
		Assert.Equal(0, germany.StrategicWarfare.ConvoyImpact);
	}

	[Fact]
	public void A_post_takes_the_first_of_the_ruling_party_else_the_last_of_its_group_else_the_first_of_anyone()
	{
		CGameState state = World();
		CCountry germany = C(state, Ger);
		Minister(germany, 1, "social_democrat", "foreign_minister", "armament_minister");
		Minister(germany, 2, "fascistic", "foreign_minister", "armament_minister");
		Minister(germany, 3, "fascistic", "foreign_minister");
		Minister(germany, 4, "national_socialist", "armament_minister");
		Minister(germany, 5, "national_socialist", "armament_minister");
		CPolitics.SetUpAtStart(state);

		Assert.Equal(3, germany.Government["foreign_minister"].Id);   // the last of the group
		Assert.Equal(4, germany.Government["armament_minister"].Id);  // the first of the party
		Assert.False(germany.Government.ContainsKey("head_of_state"));   // nobody may take it
	}

	[Fact]
	public void An_election_due_is_won_by_the_most_popular_party_which_fills_the_elected_posts_and_the_player_is_told()
	{
		CGameState state = World();
		CCountry britain = C(state, Eng);
		Minister(britain, 10, "social_democrat", "foreign_minister");
		Minister(britain, 11, "social_conservative", "foreign_minister");
		britain.LastElection = new CDate(1931, 6, 1);
		CPolitics.SetUpAtStart(state);
		Assert.Equal(new CDate(1935, 6, 1), britain.LastElection);    // brought up to the last term before today
		Assert.Equal(10, britain.Government["foreign_minister"].Id);

		britain.Popularity.Values[SocialConservative] = 55000;
		britain.Popularity.Values[SocialDemocrat] = 45000;
		britain.LastElection = new CDate(1932, 1, 1);
		state.Played.Add(Eng);
		var told = new List<CPactMessage>();
		state.PactMessaged += told.Add;
		CElections.RunDaily(state);

		Assert.Equal("social_conservative", britain.RulingIdeology);
		Assert.Equal(11, britain.Government["foreign_minister"].Id);
		Assert.Equal(state.Date, britain.LastElection);
		CPactMessage message = Assert.Single(told);
		Assert.Equal(CElections.ResultsMessage, message.We);
		Assert.Contains(("WINNING_PARTY", "social_conservative"), message.Values);
		Assert.Contains(("PREVIOUS_PARTY", "social_democrat"), message.Values);
	}

	[Fact]
	public void The_conditions_read_the_ruling_party_and_an_ideology_block_holds_on_either_figure()
	{
		CGameState state = World();
		CPolitics.SetUpAtStart(state);
		CCountry germany = C(state, Ger);
		germany.Popularity.Values[NationalSocialist] = 60000;
		germany.Organisation.Values[SocialConservative] = 61000;
		CEventScope scope = CEventScope.Start(Ger);
		bool Keyword(string key, string text, string amount = "0") =>
			CConditions.Holds(state, new CTrigger(ETriggerKind.Keyword, key, new CScriptArgument(text, false) { Amount = CFixedPoint.Parse(amount) }), scope);

		Assert.True(Keyword("ideology", "national_socialist"));
		Assert.False(Keyword("ideology", "National_Socialist"));       // a name is matched case and all
		Assert.True(Keyword("ideology_group", "fascism"));
		Assert.True(Keyword("government", "dictatorship"));
		Assert.True(Keyword("popularity", "60", "60"));
		Assert.False(Keyword("popularity", "60.001", "60.001"));

		var block = new CTrigger(ETriggerKind.Ideology, "social_conservative", CScriptArgument.None);
		block.Fields.Add(new CScriptField("organisation", new CScriptArgument("60", false) { Amount = CFixedPoint.FromInt(60) }));
		block.Fields.Add(new CScriptField("popularity", new CScriptArgument("60", false) { Amount = CFixedPoint.FromInt(60) }));
		Assert.True(CConditions.Holds(state, block, scope));            // organisation 61 is enough on its own
	}

	[Fact]
	public void The_effects_move_the_ruling_party_change_the_government_and_remove_a_minister_from_his_post_and_the_pool()
	{
		CGameState state = World();
		CCountry germany = C(state, Ger);
		Minister(germany, 1, "national_socialist", "foreign_minister");
		Minister(germany, 2, "social_democrat", "foreign_minister");
		CPolitics.SetUpAtStart(state);
		germany.Popularity.Values[NationalSocialist] = 99000;
		CEventScope scope = CEventScope.Start(Ger);
		void Run(string key, string text, string amount = "0") =>
			CEffects.Run(state, new CEffect(EEffectKind.Keyword, key, new CScriptArgument(text, false) { Amount = CFixedPoint.Parse(amount) }), scope);

		// Held to a hundred, the new figure to the effects' tally.
		Run("popularity", "5", "5");
		Assert.Equal(100000, germany.Popularity[NationalSocialist]);
		Assert.Equal(100000, germany.Popularity.Tallies[NationalSocialist, CPartyFigures.EffectSlot]);

		// A party's block: a result below nought is not applied, and there is no upper limit.
		var block = new CEffect(EEffectKind.Ideology, "social_democrat", CScriptArgument.None);
		block.Fields.Add(new CScriptField("organisation", new CScriptArgument("-5", false) { Amount = CFixedPoint.FromInt(-5) }));
		block.Fields.Add(new CScriptField("popularity", new CScriptArgument("120", false) { Amount = CFixedPoint.FromInt(120) }));
		CEffects.Run(state, block, scope);
		Assert.Equal(0, germany.Organisation[SocialDemocrat]);
		Assert.Equal(120000, germany.Popularity[SocialDemocrat]);

		// A new government, and every post filled again from the pool.
		Run("government", "social_democracy");
		Assert.Equal("social_democracy", germany.GovernmentType);
		Run("coup_by", "social_democrat");
		Assert.Equal("social_democrat", germany.RulingIdeology);
		Assert.Equal(2, germany.Government["foreign_minister"].Id);
		Assert.Equal(state.Date, germany.LastElection);

		// Out of his changeable post and out of the pool.
		Run("remove_minister", "foreign_minister");
		Assert.False(germany.Government.ContainsKey("foreign_minister"));
		Assert.DoesNotContain(germany.Ministers[2], germany.PossibleMinisters);
	}

	[Fact]
	public void A_minister_is_alive_from_his_start_and_with_no_death_date_for_ever()
	{
		CGameState state = World();
		CCountry germany = C(state, Ger);
		Minister(germany, 1, "national_socialist").StartDate = new CDate(1935, 1, 1);
		Minister(germany, 2, "national_socialist").StartDate = new CDate(1937, 1, 1);
		CMinister dead = Minister(germany, 3, "national_socialist");
		dead.DeathDate = new CDate(1935, 12, 31);

		Assert.True(CPoliticsConditions.IsAlive(state, germany.Ministers[1]));
		Assert.False(CPoliticsConditions.IsAlive(state, germany.Ministers[2]));
		Assert.False(CPoliticsConditions.IsAlive(state, dead));
	}
}
