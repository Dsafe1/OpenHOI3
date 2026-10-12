using OpenHOI3.Content.Script;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// Reading a trigger block, against <c>docs/mechanics/script.md</c>, sections 5 and 7.
/// </summary>
public class TriggerReaderTests
{
	private static CTrigger Read(string text, CScriptContext context)
	{
		CTrigger all = CTrigger.All();
		CTriggerReader.ReadInto(all, ScriptFixture.Parse(text, context), context);
		return all;
	}

	[Fact]
	public void There_are_153_keywords()
	{
		Assert.Equal(153, CTriggerKeywords.All.Count);
		Assert.NotNull(CTriggerKeywords.Find("port"));
	}

	[Fact]
	public void A_keyword_is_matched_whatever_its_case_and_never_in_quotes()
	{
		CScriptContext context = ScriptFixture.Context();
		CTrigger read = Read("OR = { NOT = { War = YES } } \"and\" = { ai = yes }", context);

		Assert.Single(read.Children);
		Assert.Equal("or", read.Children[0].Keyword);
		Assert.Equal("not", read.Children[0].Children[0].Keyword);
		Assert.True(read.Children[0].Children[0].Children[0].Argument.IsYes);
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.UnknownKey));
	}

	[Fact]
	public void An_unknown_key_is_reported_inside_a_block_as_well_as_at_the_top()
	{
		CScriptContext context = ScriptFixture.Context();
		Read("no_such_key = 1 and = { or = { GER = { nor_this = yes } } }", context);

		Assert.Equal(2, ScriptFixture.Of(context, EScriptProblem.UnknownKey).Length);
	}

	[Fact]
	public void Names_are_tried_in_the_originals_order_so_a_building_wins_over_a_technology()
	{
		CScriptContext context = ScriptFixture.Context();
		CTrigger read = Read("industry = 3 infantry_activation = 1 head_of_state = 23050 infantry_brigade = 4", context);

		Assert.Equal(
			new[] { ETriggerKind.Building, ETriggerKind.Technology, ETriggerKind.GovernmentPosition, ETriggerKind.UnitType },
			read.Children.ConvertAll(t => t.Kind));
		Assert.Equal(CFixedPoint.FromInt(3), read.Children[0].Argument.Amount);
		Assert.Equal(0, context.Problems.Count);
	}

	[Fact]
	public void A_name_is_matched_case_and_all()
	{
		CScriptContext context = ScriptFixture.Context();
		CTrigger read = Read("Industry = 3 has_building = Land_Fort", context);

		Assert.Single(read.Children);
		Assert.Equal(2, ScriptFixture.Of(context, EScriptProblem.OtherCase).Length);
	}

	[Fact]
	public void A_law_of_another_group_and_a_combined_arms_group_are_reported()
	{
		CScriptContext context = ScriptFixture.Context();
		CTrigger read = Read(
			"conscription_law = service_by_requirement press_laws = service_by_requirement armor_unit_type = yes", context);

		Assert.Equal(ETriggerKind.LawGroup, read.Children[0].Kind);
		Assert.Equal(ETriggerKind.CombinedArms, read.Children[2].Kind);
		Assert.Equal(2, ScriptFixture.Of(context, EScriptProblem.Quirk).Length);
	}

	[Fact]
	public void An_ideology_takes_organisation_and_popularity_and_nothing_else()
	{
		CScriptContext context = ScriptFixture.Context();
		CTrigger read = Read("stalinist = { organisation = 60 popularity = 55 strength = 1 }", context);

		CTrigger ideology = Assert.Single(read.Children);
		Assert.Equal(ETriggerKind.Ideology, ideology.Kind);
		Assert.Equal(new[] { "organisation", "popularity" }, ideology.Fields.ConvertAll(f => f.Key));
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.UnknownKey));
	}

	[Fact]
	public void Regions_provinces_tags_and_scope_words_are_scopes()
	{
		CScriptContext context = ScriptFixture.Context();
		CTrigger read = Read(
			"sudetenland = { ai = yes } 42 = { ai = yes } GER = { ai = yes } FROM = { ai = yes } 400 = { ai = yes }", context);

		Assert.Equal(
			new[] { ETriggerKind.Region, ETriggerKind.Scope, ETriggerKind.Scope, ETriggerKind.Scope, ETriggerKind.Scope },
			read.Children.ConvertAll(t => t.Kind));
		Assert.Equal(42, read.Children[1].Argument.Province);
		Assert.Equal("GER", read.Children[2].Argument.Tag.Value);
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.UnknownName));
	}

	[Fact]
	public void A_tag_is_its_first_three_letters_matched_without_case_only_in_the_same_bucket()
	{
		CScriptContext context = ScriptFixture.Context();
		CTrigger read = Read("war_with = Ger war_with = gEr war_with = gER war_with = ger war_with = GERMANY", context);

		// Changing a letter's case moves its code by 32, so only an even number of changes keeps the bucket.
		Assert.Equal(new[] { "GER", "GER", "---", "---", "GER" }, read.Children.ConvertAll(t => t.Argument.Tag.Value));
		Assert.Equal(2, ScriptFixture.Of(context, EScriptProblem.UnknownName).Length);
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.Quirk));
	}

	[Fact]
	public void PHI_is_the_originals_own_word_and_never_a_scope_though_it_is_a_tag_as_a_value()
	{
		CScriptContext context = ScriptFixture.Context();
		CTrigger read = Read("PHI = { ai = yes } vassal_of = PHI", context);

		CTrigger vassal = Assert.Single(read.Children);
		Assert.Equal("PHI", vassal.Argument.Tag.Value);
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.Quirk));
	}

	[Fact]
	public void A_value_of_the_wrong_shape_is_reported_and_left_out()
	{
		CScriptContext context = ScriptFixture.Context();
		CTrigger read = Read("and = yes money = { a = 1 } always = 1 month = 2.5 units_in_province = GER", context);

		Assert.Equal(new[] { "always", "month", "units_in_province" }, read.Children.ConvertAll(t => t.Keyword));
		Assert.Equal(CFixedPoint.FromInt(2), read.Children[1].Argument.Amount);
		Assert.Equal(CFixedPoint.Zero, read.Children[2].Argument.Amount);
		Assert.Equal(5, ScriptFixture.Of(context, EScriptProblem.WrongShape).Length);
	}

	[Fact]
	public void Own_keys_take_their_shapes_and_a_name_list_its_words()
	{
		CScriptContext context = ScriptFixture.Context();
		CTrigger read = Read(@"
			relation = { who = THIS value = 50 }
			has_cb = { type = conquer actor = ENG }
			controlled_by = owner
			faction = none
			strategic_resource = none
			exists = no
			is_core = REB
			date = 1938.13.1", context);

		Assert.Equal(new[] { "who", "value" }, read.Children[0].Fields.ConvertAll(f => f.Key));
		Assert.True(read.Children[0].Fields[0].Argument.IsThis);
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.WrongShape));
		Assert.Single(context.Problems.All);
	}

	[Fact]
	public void The_two_broken_keywords_are_reported()
	{
		CScriptContext context = ScriptFixture.Context();
		CTrigger read = Read("unit_has_leader = no has_hostile_spy_mission = yes", context);

		Assert.Equal("unit_has_leader", Assert.Single(read.Children).Keyword);
		Assert.Equal(2, ScriptFixture.Of(context, EScriptProblem.Quirk).Length);
	}
}
