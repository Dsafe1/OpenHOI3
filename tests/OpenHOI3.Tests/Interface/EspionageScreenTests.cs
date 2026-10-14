using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Espionage;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The intelligence screen's figures, lists and tooltips, against <c>docs/mechanics/espionagescreen.md</c>, on
/// <see cref="ScriptWorld"/>'s strip with Germany as the player.
/// </summary>
public class EspionageScreenTests
{
	private static readonly CCountryTag Ger = ScriptWorld.Ger;
	private static readonly CCountryTag Pol = ScriptWorld.Pol;
	private static readonly CCountryTag Ita = ScriptWorld.Ita;

	private static CCountry C(CGameState state, CCountryTag tag) => state.Country(tag)!;

	private static string Localise(string key) => key;

	[Fact]
	public void The_lists_leave_out_the_player_and_unaligned_the_chosen_country_too()
	{
		CGameState state = ScriptWorld.State();

		Assert.Equal(new[] { "ITA", "POL" }, CEspionageLists.All(state, C(state, Ger)).Select(c => c.Tag.Value));
		Assert.Empty(CEspionageLists.Unaligned(state, C(state, Ger), C(state, Pol)));
		Assert.Equal(new[] { "POL" }, CEspionageLists.Unaligned(state, C(state, Ger), null).Select(c => c.Tag.Value));
		Assert.Equal(new[] { "ITA" }, CEspionageLists.FactionMembers(state, C(state, Ger), "axis").Select(c => c.Tag.Value));
	}

	[Fact]
	public void The_priority_sort_is_always_highest_first_and_equals_keep_their_order()
	{
		CGameState state = ScriptWorld.State();
		CCountry germany = C(state, Ger);
		CGameState.Presence(germany, C(state, Ita)).Priority = 2;
		var countries = new List<CCountry> { C(state, Pol), C(state, Ita), C(state, ScriptWorld.Slo) };

		Assert.Equal(new[] { "ITA", "POL", "SLO" },
			CEspionageLists.Sorted(germany, countries, ESpySort.Priority, descending: false).Select(c => c.Tag.Value));
		Assert.Equal(new[] { "SLO", "POL", "ITA" },
			CEspionageLists.Sorted(germany, countries, ESpySort.Name, descending: true).Select(c => c.Tag.Value));
	}

	[Fact]
	public void The_first_chosen_country_is_the_one_feared_most_or_the_first()
	{
		CGameState state = ScriptWorld.State();
		Assert.Equal(Pol, CEspionageLists.FirstChosen(state, C(state, Ger))!.Tag);

		CGameState.Status(C(state, Ger), C(state, Ita)).Threat = CFixedPoint.FromInt(5);
		Assert.Equal(Ita, CEspionageLists.FirstChosen(state, C(state, Ger))!.Tag);
	}

	[Fact]
	public void A_priority_button_goes_round_both_ways()
	{
		Assert.Equal(1, CEspionageScreenText.NextFrame(4, up: true));
		Assert.Equal(3, CEspionageScreenText.NextFrame(2, up: true));
		Assert.Equal(4, CEspionageScreenText.NextFrame(1, up: false));
	}

	[Fact]
	public void The_chosen_country_s_spies_are_coloured_by_how_well_they_know_it()
	{
		CGameState state = ScriptWorld.State();
		CCountry germany = C(state, Ger);
		CGameState.Presence(germany, C(state, Pol)).Level = 7000;
		Assert.Equal("§G7§W", CEspionageScreenText.ChosenSpies(germany, C(state, Pol)));

		CGameState.Presence(germany, C(state, Pol)).Level = 3000;
		Assert.Equal("§R3§W", CEspionageScreenText.ChosenSpies(germany, C(state, Pol)));
	}

	[Fact]
	public void The_foreign_actions_mark_wants_more_than_three_spies_at_home()
	{
		CGameState state = ScriptWorld.State();
		state.Defines.Set("country", CSpyPlacing.MostSpies, CFixedPoint.FromInt(10));
		CCountry germany = C(state, Ger);

		CGameState.Presence(germany, germany).Level = 3000;
		Assert.False(CEspionageScreenText.ShowsForeignActions(state, germany));
		CGameState.Presence(germany, germany).Level = 4000;
		Assert.True(CEspionageScreenText.ShowsForeignActions(state, germany));
	}

	[Fact]
	public void The_production_roll_is_the_C_runtime_s_generator_stepped_twice()
	{
		// From a seed of 1 the runtime's rand() gives 41 and then 18467: 41 x 18467 = 757147.
		Assert.Equal(47, CEspionageScreenText.Roll(1));
	}

	[Fact]
	public void Under_one_whole_spy_every_estimate_is_a_question_mark_and_a_month_s_estimates_hold_still()
	{
		CGameState state = ScriptWorld.State();
		CCountry germany = C(state, Ger);
		CCountry poland = C(state, Pol);

		CEspionageEstimates none = CEspionageEstimates.Make(Localise, state, germany, poland);
		Assert.Equal("?", none.Boxes["dissent_number"]);
		Assert.Equal("?/?/?", none.Boxes["ic_number"]);
		Assert.Equal("SPYMISSION_COUNTER_ESPIONAGE_TITLE: ?", none.Boxes["counter_intel"]);

		CGameState.Presence(germany, poland).Level = 5000;
		poland.NationalUnity = CFixedPoint.FromInt(70);
		CEspionageEstimates first = CEspionageEstimates.Make(Localise, state, germany, poland);
		CEspionageEstimates again = CEspionageEstimates.Make(Localise, state, germany, poland);
		Assert.Equal(first.Boxes["national_unity_number"], again.Boxes["national_unity_number"]);
		Assert.NotEqual("?", first.Boxes["national_unity_number"]);
	}

	[Fact]
	public void A_perturbed_estimate_stays_within_half_the_spread_of_the_scale()
	{
		var random = new CRandom(7);
		for (int i = 0; i < 200; i++)
		{
			long value = CEspionageEstimates.AtLeast(random, 50000, 0, 100000, 0.4f);
			Assert.InRange(value, 30000, 70000);
		}
	}

	[Fact]
	public void The_coup_s_tooltip_is_too_strong_where_the_groups_are_one_and_then_the_cost_lacking()
	{
		CGameState state = ScriptWorld.State();
		state.Defines.Set("country", "COUP_COVERT_OPS_COST", CFixedPoint.FromInt(20));

		// Nobody here has a ruling party, so the two are of one group.
		CTooltip tooltip = CEspionageTooltips.Coup(Localise, state, C(state, Ger), C(state, Pol));

		Assert.Equal("COUP_DESC_DISABLED\nCOV_OPS_COST_LACKING", tooltip.Text);
	}

	[Fact]
	public void An_estimate_s_tooltip_repeats_the_box_and_names_what_it_is_of()
	{
		CTooltip tooltip = CEspionageTooltips.Estimate(Localise, "metal_number", "§R?");

		Assert.Equal("ESP_ESTIMATE_RES", tooltip.Text);   // the keys are their own text here
		Assert.Equal("ESP_ESTIMATE_RES_DELAY", tooltip.Delayed);
		Assert.True(CEspionageTooltips.IsEstimate("leadership_number"));
		Assert.False(CEspionageTooltips.IsEstimate("counter_intel"));
	}
}
