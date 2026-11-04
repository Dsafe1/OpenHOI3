using OpenHOI3.Content.Settings;
using OpenHOI3.Interface.Screens;

namespace OpenHOI3.Tests.Interface;

/// <summary>The game options window's rules, against <c>docs/mechanics/gameoptions.md</c>.</summary>
public class GameOptionsTests
{
	private static readonly CDisplayMode[] Modes =
	{
		new(1024, 768, new[] { 60, 75 }),
		new(1920, 1080, new[] { 144, 60, 120 }),
	};

	private static readonly CGameOptionsSliders Sliders = new(50f, 100f, 100f, 100f, 50f, 50f, true, false);

	private static CUserSettings Settings => CUserSettings.Default with { Width = 1920, Height = 1080, RefreshRate = 60 };

	[Fact]
	public void The_autosave_frequency_wraps_both_ways_and_is_read_once()
	{
		var options = new CGameOptions(Modes, new[] { 2, 4 }, CUserSettings.Default with { Autosave = "FIVE_YEAR" });

		options.StepAutosave(1);
		Assert.Equal("NEVER", options.AutosaveKey);
		options.StepAutosave(-1);
		options.StepAutosave(-1);
		Assert.Equal("YEARLY", options.AutosaveKey);
		options.Refresh(Settings, 1920, 1080);                         // the opening leaves it be
		Assert.Equal("YEARLY", options.AutosaveKey);
	}

	[Fact]
	public void The_rate_is_looked_for_among_the_first_modes_rates_as_the_original_does()
	{
		var options = new CGameOptions(Modes, new[] { 2 }, Settings);

		options.Refresh(Settings, 1920, 1080);

		Assert.Equal(1, options.Mode);
		// 60 is the first mode's first rate, so index 0 - which in the second mode's list is 144.
		Assert.Equal("1920x1080", options.ModeText());
		Assert.Equal("144 Hz", options.RateText());
	}

	[Fact]
	public void A_changed_resolution_asks_a_restart_and_brackets_it_and_the_rate_starts_again()
	{
		var options = new CGameOptions(Modes, new[] { 2 }, Settings);
		options.Refresh(Settings, 1920, 1080);
		options.StepMode(1);                                           // wraps to the first

		(CUserSettings stored, bool restart) = options.Apply(Settings, Sliders, 1920, 1080, 60);

		Assert.True(restart);
		Assert.Equal((1024, 768, 60), (stored.Width, stored.Height, stored.RefreshRate));
		Assert.Equal("1024x768 (1024x768)", options.ModeText());
	}

	[Fact]
	public void Multisampling_goes_round_the_supported_counts_through_nought_and_only_a_change_asks_a_restart()
	{
		var options = new CGameOptions(Modes, new[] { 8, 2, 4 }, Settings);
		options.Refresh(Settings, 1920, 1080);

		options.StepMultisample(1);
		Assert.Equal("2", options.MultisampleText());
		options.StepMultisample(-1);
		options.StepMultisample(-1);
		Assert.Equal("8", options.MultisampleText());
		options.StepMultisample(1);
		Assert.Equal("0", options.MultisampleText());

		Assert.False(new CGameOptions(Modes, new int[0], Settings).MultisamplingAvailable);
	}

	[Fact]
	public void Apply_stores_the_choices_the_distance_five_times_its_slider_and_the_least_speeds()
	{
		var options = new CGameOptions(Modes, new[] { 2 }, Settings);
		options.Refresh(Settings, 1920, 1080);
		options.CounterDistanceMoved(CGameOptions.CounterDistanceSlider(500f));
		options.Trees = false;
		options.AdvancedWater = false;

		(CUserSettings stored, bool restart) = options.Apply(Settings,
			Sliders with { ScrollSpeed = 3f, ZoomSpeed = 0f, LegacyCameraTicked = true, Gamma = 120f }, 1920, 1080, 144);

		Assert.False(restart);                                         // the rate shown, 144, is the running one
		Assert.Equal(500f, stored.CounterDistance);
		Assert.False(stored.RenderTrees);
		Assert.True(stored.SimpleWater);
		Assert.Equal((5f, 1f), (stored.ScrollSpeed, stored.ZoomSpeed));
		Assert.True(stored.UseLegacyCamera);
		Assert.Equal(144, stored.RefreshRate);
		Assert.Equal(1.9f, CGameOptions.Brightness(stored.Gamma));
		Assert.Equal(0.1f, CGameOptions.Brightness(4f));
	}
}
