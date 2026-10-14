using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Which triggered modifiers a country has and what the player is told, against <c>docs/mechanics/modifiers.md</c>,
/// section 5, on <see cref="ScriptWorld"/>.
/// </summary>
public class TriggeredModifierTests
{
	private static CTriggeredModifier Entry(string name, string potential, string trigger, string kind = "research_efficiency")
	{
		var entry = new CTriggeredModifier(name);
		entry.Potential.Children.AddRange(ScriptWorld.Condition(potential).Children);
		entry.Trigger.Children.AddRange(ScriptWorld.Condition(trigger).Children);
		entry.Modifier.Set(kind, CFixedPoint.Parse("0.05"));
		return entry;
	}

	[Fact]
	public void Both_blocks_must_hold_and_an_empty_one_holds()
	{
		CGameState state = ScriptWorld.State();
		CCountry germany = state.Country(ScriptWorld.Ger)!;

		Assert.True(CTriggeredModifiers.Applies(state, Entry("a", "", "tag = GER"), germany));
		Assert.False(CTriggeredModifiers.Applies(state, Entry("b", "tag = POL", "always = yes"), germany));
		Assert.False(CTriggeredModifiers.Applies(state, Entry("c", "tag = GER", "always = no"), germany));
		Assert.True(CTriggeredModifiers.PotentialHolds(state, Entry("d", "tag = GER", "always = no"), germany));
	}

	[Fact]
	public void The_list_keeps_the_order_they_came_on_and_the_lapsed_leave_where_they_stand()
	{
		CGameState state = ScriptWorld.State();
		CCountry germany = state.Country(ScriptWorld.Ger)!;
		CTriggeredModifier first = Entry("first", "", "has_country_flag = later");
		CTriggeredModifier second = Entry("second", "", "always = yes");
		CTriggeredModifier third = Entry("third", "", "not = { has_country_flag = later }");
		state.TriggeredModifiers.AddRange(new[] { first, second, third });

		CTriggeredModifiers.Update(state, germany);
		Assert.Equal(new[] { second, third }, germany.TriggeredModifiers);

		// The first comes on after the others, and so goes at the end; the third lapses.
		germany.Flags.Set("later");
		CTriggeredModifiers.Update(state, germany);
		Assert.Equal(new[] { second, first }, germany.TriggeredModifiers);
	}

	[Fact]
	public void The_days_rebuild_tests_first_and_adds_them_in_the_lists_order_and_another_rebuild_adds_the_list_as_it_stands()
	{
		CGameState state = ScriptWorld.State();
		CCountry germany = state.Country(ScriptWorld.Ger)!;
		CTriggeredModifier neutral = Entry("neutral", "", "has_country_flag = neutral");
		state.TriggeredModifiers.Add(neutral);
		germany.Flags.Set("neutral");

		CCountryModifiers.Rebuild(state);
		Assert.Equal(CFixedPoint.Zero, germany.Modifier["research_efficiency"]);

		CCountryModifiers.RebuildDaily(state);
		Assert.Equal(CFixedPoint.Parse("0.05"), germany.Modifier["research_efficiency"]);
		Assert.Contains(germany.ModifierSources, source => source.Modifier == neutral.Modifier);

		// A rebuild at another time keeps the list as it is.
		germany.Flags.Clear("neutral");
		CCountryModifiers.Rebuild(state);
		Assert.Equal(CFixedPoint.Parse("0.05"), germany.Modifier["research_efficiency"]);
		CCountryModifiers.RebuildDaily(state);
		Assert.Equal(CFixedPoint.Zero, germany.Modifier["research_efficiency"]);
	}

	[Fact]
	public void A_player_is_told_of_one_come_and_gone_and_nobody_else_is()
	{
		CGameState state = ScriptWorld.State();
		CCountry germany = state.Country(ScriptWorld.Ger)!;
		CCountry poland = state.Country(ScriptWorld.Pol)!;
		state.TriggeredModifiers.Add(Entry("steel", "", "has_country_flag = steel"));
		state.Played.Add(ScriptWorld.Ger);
		var told = new List<CPactMessage>();
		state.PactMessaged += told.Add;
		germany.Flags.Set("steel");
		poland.Flags.Set("steel");

		CTriggeredModifiers.Update(state, germany);
		CTriggeredModifiers.Update(state, poland);
		CPactMessage gained = Assert.Single(told);
		Assert.Equal(CTriggeredModifiers.Gained, gained.We);
		Assert.Equal(ScriptWorld.Ger, gained.Told);
		Assert.Equal(CTriggeredModifiers.Messenger, gained.Messenger);
		Assert.Equal(new[] { ("NAME", "steel"), ("EFFECT", "steel"), ("EXPLANATION", "steel") }, gained.Values);

		told.Clear();
		germany.Flags.Clear("steel");
		CTriggeredModifiers.Update(state, germany);
		CPactMessage lost = Assert.Single(told);
		Assert.Equal(CTriggeredModifiers.Lost, lost.We);
		Assert.Equal(new[] { ("NAME", "steel"), ("EFFECT", "steel") }, lost.Values);
	}
}
