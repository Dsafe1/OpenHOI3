using System.Collections.Generic;
using OpenHOI3.Content.Script;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// Reading <c>common/triggered_modifiers.txt</c>, against <c>docs/mechanics/modifiers.md</c>, section 5.
/// </summary>
public class TriggeredModifierReaderTests
{
	[Fact]
	public void An_entry_reads_its_two_blocks_and_its_icon_and_every_other_key_is_a_modifier()
	{
		CScriptContext context = ScriptFixture.Context();
		var read = new List<CTriggeredModifier>();
		CTriggeredModifierReader.ReadFile(ScriptFixture.Parse(@"
			neutrality = {
				potential = { always = yes }
				trigger = { always = yes always = no }
				research_efficiency = -0.05
				counter_espionage = 0.1
				icon = 14
			}
			great_army = {
				trigger = { always = yes }
				global_leadership = 0.15
			}", context), read, context);

		Assert.Equal(new[] { "neutrality", "great_army" }, read.ConvertAll(entry => entry.Name));
		CTriggeredModifier neutrality = read[0];
		Assert.Single(neutrality.Potential.Children);
		Assert.Equal(2, neutrality.Trigger.Children.Count);
		Assert.Equal(14, neutrality.Icon);
		Assert.Equal(CFixedPoint.Parse("-0.05"), neutrality.Modifier["research_efficiency"]);
		Assert.Equal(CFixedPoint.Parse("0.1"), neutrality.Modifier["counter_espionage"]);
		Assert.False(neutrality.Modifier.Values.ContainsKey("icon"));
		// No potential is one that always holds, and no icon is nought.
		Assert.Empty(read[1].Potential.Children);
		Assert.Equal(0, read[1].Icon);
		Assert.Empty(context.Problems.All);
	}

	[Fact]
	public void A_block_that_is_not_its_own_and_a_value_that_is_not_a_number_are_reported()
	{
		CScriptContext context = ScriptFixture.Context();
		var read = new List<CTriggeredModifier>();
		CTriggeredModifierReader.ReadFile(ScriptFixture.Parse(@"
			odd = {
				limit = { always = yes }
				global_leadership = lots
			}", context), read, context);

		Assert.Equal(CFixedPoint.Zero, Assert.Single(read).Modifier["global_leadership"]);
		// A block of a key that is no kind is a key nothing takes; a kind's word is the wrong shape.
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.UnknownKey));
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.WrongShape));
	}

	[Fact]
	public void A_key_that_is_no_kind_of_modifier_is_reported_and_dropped()
	{
		CScriptContext context = ScriptFixture.Context();
		var read = new List<CTriggeredModifier>();
		CTriggeredModifierReader.ReadFile(ScriptFixture.Parse(@"
			odd = {
				global_lc = 0.1
				""global_ic"" = 0.2
				convoy_raid = 0.05
				suseptibility_axis = 0.3
			}", context), read, context, new CModifierKeys(new[] { "axis" }));

		CModifier modifier = Assert.Single(read).Modifier;
		Assert.False(modifier.Values.ContainsKey("global_lc"));
		Assert.False(modifier.Values.ContainsKey("global_ic"));
		Assert.Equal(CFixedPoint.Parse("0.05"), modifier["convoy_raid"]);
		Assert.Equal(CFixedPoint.Parse("0.3"), modifier["suseptibility_axis"]);
		// The misspelt kind, and the quoted key, which is never a keyword.
		Assert.Equal(2, ScriptFixture.Of(context, EScriptProblem.UnknownKey).Length);
	}

	[Fact]
	public void A_factions_kind_is_unknown_without_its_faction()
	{
		CScriptContext context = ScriptFixture.Context();
		var read = new List<CTriggeredModifier>();
		CTriggeredModifierReader.ReadFile(ScriptFixture.Parse(@"
			odd = { suseptibility_axis = 0.3 }", context), read, context);

		Assert.Empty(Assert.Single(read).Modifier.Values);
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.UnknownKey));
	}
}
