using System.Collections.Generic;
using OpenHOI3.Content.Script;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// Reading an effect block, against <c>docs/mechanics/script.md</c>, section 6.
/// </summary>
public class EffectReaderTests
{
	private static List<CEffect> Read(string text, CScriptContext context)
	{
		var effects = new List<CEffect>();
		CEffectReader.ReadInto(effects, ScriptFixture.Parse(text, context), context);
		return effects;
	}

	[Fact]
	public void There_are_91_keywords()
	{
		Assert.Equal(91, CEffectKeywords.All.Count);
	}

	[Fact]
	public void Names_are_tried_in_the_originals_order()
	{
		CScriptContext context = ScriptFixture.Context();
		List<CEffect> read = Read(@"
			head_of_state = 23050
			industry = 1
			stalinist = { organisation = 5.5 popularity = 2 }
			infantry_activation = 1
			partisans = 1
			sudetenland = { limit = { ai = yes } add_core = GER }
			infantry_brigade = current", context);

		Assert.Equal(
			new[]
			{
				EEffectKind.GovernmentPosition, EEffectKind.Building, EEffectKind.Ideology, EEffectKind.Technology,
				EEffectKind.RebelType, EEffectKind.Region, EEffectKind.UnitType,
			},
			read.ConvertAll(e => e.Kind));
		Assert.Equal(new CFixedPoint(5500), read[2].Fields[0].Argument.Amount);
		Assert.NotNull(read[5].Limit);
		Assert.Equal(0, context.Problems.Count);
	}

	[Fact]
	public void A_law_is_not_an_effect()
	{
		CScriptContext context = ScriptFixture.Context();
		Assert.Empty(Read("conscription_law = service_by_requirement", context));
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.UnknownKey));
	}

	[Fact]
	public void A_block_under_a_key_that_names_nothing_is_a_scope_that_never_runs()
	{
		CScriptContext context = ScriptFixture.Context();
		List<CEffect> read = Read("GER = { money = 5 } 42 = { industry = 1 } nowhere = { money = 5 } PHI = { money = 5 }", context);

		Assert.Equal(
			new[] { EEffectKind.Scope, EEffectKind.Scope, EEffectKind.DeadScope, EEffectKind.DeadScope },
			read.ConvertAll(e => e.Kind));
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.UnknownKey));
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.Quirk));
	}

	[Fact]
	public void A_limit_inside_a_scope_does_nothing_and_the_effects_beside_it_are_still_done()
	{
		CScriptContext context = ScriptFixture.Context();
		List<CEffect> read = Read("42 = { limit = { ai = yes } land_fort = 10 }", context);

		CEffect scope = Assert.Single(read);
		Assert.Equal(new[] { EEffectKind.DeadScope, EEffectKind.Building }, scope.Children.ConvertAll(e => e.Kind));
		Assert.Single(scope.Children[0].Limit!.Children);
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.Quirk));
	}

	[Fact]
	public void An_iterating_effect_ands_its_limits_and_does_its_effects()
	{
		CScriptContext context = ScriptFixture.Context();
		CEffect any = Assert.Single(Read("any_country = { limit = { ai = yes } limit = { war = yes } relation = { who = THIS value = 10 } }", context));

		Assert.Equal(2, any.Limit!.Children.Count);
		CEffect relation = Assert.Single(any.Children);
		Assert.Equal(new[] { "who", "value" }, relation.Fields.ConvertAll(f => f.Key));
		Assert.Equal(0, context.Problems.Count);
	}

	[Fact]
	public void A_random_list_has_weights_each_with_effects()
	{
		CScriptContext context = ScriptFixture.Context();
		CEffect list = Assert.Single(Read("random_list = { 60 = { money = 5 } 40 = { money = 10 } many = { money = 1 } }", context));

		Assert.Equal(2, list.Children.Count);
		Assert.Equal(CFixedPoint.FromInt(60), list.Children[0].Argument.Amount);
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.WrongShape));
	}

	[Fact]
	public void A_war_takes_its_goals_and_a_goal_takes_only_its_own_keys()
	{
		CScriptContext context = ScriptFixture.Context();
		CEffect war = Assert.Single(Read(@"
			war = {
				target = ENG
				call_ally = no
				attacker_goal = { casus_belli = conquer region = sudetenland colour = red }
				money = 5
			}", context));

		Assert.Equal(new[] { "target", "call_ally", "attacker_goal" }, war.Fields.ConvertAll(f => f.Key));
		Assert.Equal(2, war.Fields[2].Fields.Count);
		Assert.Single(war.Children);
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.UnknownKey));
	}

	[Fact]
	public void A_division_names_its_brigades_by_their_unit_types()
	{
		CScriptContext context = ScriptFixture.Context();
		CEffect division = Assert.Single(Read("add_division = { name = \"1. Division\" where = 42 infantry_brigade = \"1. Brigade\" }", context));

		Assert.Equal(new[] { "name", "where", "infantry_brigade" }, division.Fields.ConvertAll(f => f.Key));
		Assert.Equal(0, context.Problems.Count);
	}

	[Fact]
	public void Values_are_read_by_their_keywords_shapes()
	{
		CScriptContext context = ScriptFixture.Context();
		List<CEffect> read = Read(@"
			add_core = 42 add_core = ITA remove_core = REB
			crude_oil = 2.5
			country_event = 1001
			load_oob = ""AnschlussOOB.txt""
			load_oob = ""anschlussoob.txt""
			join_faction = axis
			release_vassal = XXX
			remove_minister = 23050
			remove_minister = head_of_state
			add_country_modifier = { name = us_new_deal duration = 365 }", context);

		Assert.Equal(42, read[0].Argument.Province);
		Assert.Equal("ITA", read[1].Argument.Tag.Value);
		Assert.Equal(CFixedPoint.FromInt(2), read[3].Argument.Amount);
		Assert.Equal("random", read[8].Argument.Text);
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.WrongShape));
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.Quirk));
		Assert.Equal(2, context.Problems.Count);
		Assert.Single(context.EventReferences);
	}
}
