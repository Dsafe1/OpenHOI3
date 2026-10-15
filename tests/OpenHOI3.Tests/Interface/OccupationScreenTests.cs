using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Script;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Occupation;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the politics screen says of occupation, against <c>docs/mechanics/occupation.md</c>, section 4, on
/// <see cref="ScriptWorld"/>.
/// </summary>
public class OccupationScreenTests
{
	private static string Localise(string key) => key;

	private static CGameState World(bool atWar = true)
	{
		CGameState state = ScriptWorld.State(atWar);
		List<COccupationPolicy> policies = COccupationPolicies.WithNull();
		var collaboration = new CModifier("collaboration_government");
		collaboration.Set("local_ic", CFixedPoint.Parse("-0.95"));
		policies.Add(new COccupationPolicy(collaboration, 1));
		policies.Add(new COccupationPolicy(new CModifier("civil_war"), 2) { Allow = ScriptWorld.Condition("FROM = { tag = ITA }") });
		state.OccupationPolicies = policies;
		return state;
	}

	[Fact]
	public void A_country_is_listed_where_the_player_holds_its_land_at_war()
	{
		CGameState state = World();
		CCountry germany = state.Country(ScriptWorld.Ger)!;
		Assert.Empty(COccupationText.OccupiedCountries(state, germany));

		state.Province(4).Controller = ScriptWorld.Ger;
		Assert.Equal(new[] { ScriptWorld.Pol }, COccupationText.OccupiedCountries(state, germany).Select(country => country.Tag));

		// Italy's land held at peace is not listed, though the policy would still apply to it.
		state.Province(7).Controller = ScriptWorld.Ger;
		Assert.Equal(new[] { ScriptWorld.Pol }, COccupationText.OccupiedCountries(state, germany).Select(country => country.Tag));
	}

	[Fact]
	public void At_peace_nobody_is_listed()
	{
		CGameState state = World(atWar: false);
		state.Province(4).Controller = ScriptWorld.Ger;
		Assert.Empty(COccupationText.OccupiedCountries(state, state.Country(ScriptWorld.Ger)!));
	}

	[Fact]
	public void The_choice_lists_the_files_policies_and_greys_by_allow_alone()
	{
		CGameState state = World();
		CCountry germany = state.Country(ScriptWorld.Ger)!;
		CCountry poland = state.Country(ScriptWorld.Pol)!;

		Assert.Equal(new[] { "collaboration_government", "civil_war" }, COccupationText.Choices(state).Select(policy => policy.Name));
		COccupationPolicy collaboration = state.OccupationPolicies[1];
		COccupationPolicy civilWar = state.OccupationPolicies[2];
		Assert.False(COccupationText.IsCurrent(state, germany, poland, collaboration));

		CGameState.Status(germany, poland).OccupationPolicy = 1;
		Assert.True(COccupationText.IsCurrent(state, germany, poland, collaboration));
		Assert.True(COccupationText.MayChoose(state, germany, poland, collaboration));       // the current one too
		Assert.False(COccupationText.MayChoose(state, germany, poland, civilWar));
		Assert.True(COccupationText.MayChoose(state, germany, state.Country(ScriptWorld.Ita)!, civilWar));
	}

	[Fact]
	public void A_policys_tooltip_is_its_figures_and_its_description_delayed()
	{
		CGameState state = World();
		CTooltip tooltip = COccupationText.Tooltip(Localise, state.OccupationPolicies[1]);
		Assert.Contains("95", tooltip.Text);
		Assert.Equal("collaboration_government_desc", tooltip.Delayed);

		CTooltip none = COccupationText.Tooltip(Localise, state.OccupationPolicies[0]);
		Assert.Equal("", none.Text);
		Assert.Equal("no_occupation_desc", none.Delayed);
	}

	[Fact]
	public void A_choices_tooltip_is_its_allow_asked_with_the_occupied_as_from()
	{
		CGameState state = World();
		CCountry germany = state.Country(ScriptWorld.Ger)!;
		var words = new CScriptWords(Localise, state, germany.Tag);

		Assert.Equal("", COccupationText.ChoiceTooltip(words, germany, state.Country(ScriptWorld.Pol)!, state.OccupationPolicies[1]));
		Assert.NotEqual("", COccupationText.ChoiceTooltip(words, germany, state.Country(ScriptWorld.Pol)!, state.OccupationPolicies[2]));
	}
}
