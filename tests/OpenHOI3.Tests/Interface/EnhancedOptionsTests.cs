using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using OpenHOI3.Content.Gui;
using OpenHOI3.Content.Saves;
using OpenHOI3.Content.Settings;
using OpenHOI3.Interface.Events;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The options an enhanced game adds, against <c>docs/mechanics/gameoptions.md</c>, section 5, and
/// <c>savegame.md</c>, section 11.1: their file, the rows' steps and words, the rows' elements copied from the autosave
/// row, the two extra autosaves' days and timer, the pointer at the screen's edge, and where an event's window stands.
/// </summary>
public class EnhancedOptionsTests
{
	// --- the file ---

	[Fact]
	public void TheFileIsReadBackAsWrittenAndNoFileIsTheOriginalsBehaviour()
	{
		var settings = new CEnhancedSettings(2, 15, false, EEventWindowPlace.Centre);
		var warnings = new List<string>();
		Assert.Equal(settings, CEnhancedSettingsFile.Parse(CEnhancedSettingsFile.Write(settings), "test", warnings.Add));
		Assert.Empty(warnings);

		string none = Path.Combine(Path.GetTempPath(), "openhoi3-no-such-" + Guid.NewGuid().ToString("N") + ".txt");
		CEnhancedSettings defaults = CEnhancedSettingsFile.Read(none);
		Assert.Equal(0, defaults.PreMonthDays);
		Assert.Equal(0, defaults.TimedMinutes);
		Assert.True(defaults.EdgeScrolling);
		Assert.Equal(EEventWindowPlace.AsFiled, defaults.EventWindow);
	}

	[Fact]
	public void AFigureIsHeldToWhatItMayBeAndWhatIsNotUnderstoodIsReported()
	{
		var warnings = new List<string>();
		CEnhancedSettings read = CEnhancedSettingsFile.Parse(
			"premonth_autosave_days = 40\ntimed_autosave_minutes = 900\nedge_scrolling = maybe\nevent_window = right\nsomething = 1\n",
			"test", warnings.Add);
		Assert.Equal(27, read.PreMonthDays);
		Assert.Equal(240, read.TimedMinutes);
		Assert.True(read.EdgeScrolling);
		Assert.Equal(EEventWindowPlace.AsFiled, read.EventWindow);
		Assert.Equal(3, warnings.Count);
	}

	// --- the rows ---

	[Fact]
	public void TheArrowsStepEachRowAndWrapAtItsEnds()
	{
		var options = new CEnhancedOptions();
		options.Refresh(CEnhancedSettings.Default);

		options.Step("premonth", -1);
		Assert.Equal(27, options.PreMonthDays);
		options.Step("premonth", 1);
		Assert.Equal(0, options.PreMonthDays);
		options.Step("premonth", 1);
		Assert.Equal(1, options.PreMonthDays);

		options.Step("timed", 1);
		Assert.Equal(5, options.TimedMinutes);
		options.Step("timed", -1);
		options.Step("timed", -1);
		Assert.Equal(240, options.TimedMinutes);
		options.Step("timed", 1);
		Assert.Equal(0, options.TimedMinutes);

		options.Step("edge", 1);
		Assert.False(options.EdgeScrolling);
		options.Step("edge", -1);
		Assert.True(options.EdgeScrolling);

		options.Step("event", 1);
		Assert.Equal(EEventWindowPlace.Left, options.EventWindow);
		options.Step("event", 1);
		Assert.Equal(EEventWindowPlace.Centre, options.EventWindow);
		options.Step("event", 1);
		Assert.Equal(EEventWindowPlace.AsFiled, options.EventWindow);
		options.Step("event", -1);
		Assert.Equal(EEventWindowPlace.Centre, options.EventWindow);

		Assert.Equal(new CEnhancedSettings(1, 0, true, EEventWindowPlace.Centre), options.Apply());
	}

	[Fact]
	public void ATimerTheFileSetBetweenTwoStepsGoesToTheStepBesideIt()
	{
		Assert.Equal(15, CEnhancedOptions.StepMinutes(12, 1));
		Assert.Equal(10, CEnhancedOptions.StepMinutes(12, -1));
		Assert.Equal(5, CEnhancedOptions.StepMinutes(3, 1));
		Assert.Equal(0, CEnhancedOptions.StepMinutes(3, -1));
	}

	[Fact]
	public void ARowSaysItsChoiceInTheModsWordsOrTheEnginesOwn()
	{
		var options = new CEnhancedOptions();
		options.Refresh(new CEnhancedSettings(2, 15, false, EEventWindowPlace.Left));
		string? None(string key) => null;
		Assert.Equal("Save before the month ends", CEnhancedOptionsText.Label("premonth", None));
		Assert.Equal("2 days before", CEnhancedOptionsText.Value("premonth", options, None));
		Assert.Equal("Every 15 minutes", CEnhancedOptionsText.Value("timed", options, None));
		Assert.Equal("Off", CEnhancedOptionsText.Value("edge", options, None));
		Assert.Equal("On the left", CEnhancedOptionsText.Value("event", options, None));

		options.Refresh(new CEnhancedSettings(1, 0));
		Assert.Equal("On its last day", CEnhancedOptionsText.Value("premonth", options, None));
		Assert.Equal("Off", CEnhancedOptionsText.Value("timed", options, None));
		Assert.Equal("On", CEnhancedOptionsText.Value("edge", options, None));

		// A mod's table comes first.
		string? Mod(string key) => key == "OPENHOI_OPT_OFF" ? "Aus" : null;
		Assert.Equal("Aus", CEnhancedOptionsText.Value("timed", options, Mod));
	}

	[Fact]
	public void EachRowIsTheAutosaveRowOverAgainFurtherDown()
	{
		var tab = new CGuiWindowType { Name = "menu_settings_GAME" };
		tab.Children.Add(new CGuiTextBoxType { Name = "autosave_label", Position = new CGuiPoint(30, 50), Font = "Arial12_bold", MaxWidth = 172, Text = "SM_AUTOSAVE_INTERVAL" });
		tab.Children.Add(new CGuiTextBoxType { Name = "autosave_value", Position = new CGuiPoint(30, 69), Font = "Arial12_headline", MaxWidth = 172 });
		tab.Children.Add(new CGuiButtonType { Name = "decrease_autosave_button", Position = new CGuiPoint(30, 60), Sprite = "option_prev", Shortcut = "a" });
		tab.Children.Add(new CGuiButtonType { Name = "increase_autosave_button", Position = new CGuiPoint(165, 60), Sprite = "option_next" });

		List<CGuiType> rows = CEnhancedOptionRows.Build(tab, CEnhancedOptions.Rows);
		Assert.Equal(16, rows.Count);
		Assert.Equal(16, rows.Select(row => row.Name).Distinct().Count());

		var label = Assert.IsType<CGuiTextBoxType>(rows.Single(row => row.Name == "openhoi_timed_label"));
		Assert.Equal(new CGuiPoint(30, 50 + 2 * CEnhancedOptionRows.Pitch), label.Position);
		Assert.Equal("Arial12_bold", label.Font);
		Assert.Equal(172, label.MaxWidth);
		Assert.Equal("", label.Text);

		var decrease = Assert.IsType<CGuiButtonType>(rows.Single(row => row.Name == "decrease_openhoi_premonth_button"));
		Assert.Equal(new CGuiPoint(30, 60 + CEnhancedOptionRows.Pitch), decrease.Position);
		Assert.Equal("option_prev", decrease.Sprite);
		Assert.Equal("", decrease.Shortcut);
		var increase = Assert.IsType<CGuiButtonType>(rows.Single(row => row.Name == "increase_openhoi_event_button"));
		Assert.Equal(new CGuiPoint(165, 60 + 4 * CEnhancedOptionRows.Pitch), increase.Position);

		// A tab with no autosave row has nothing to copy.
		Assert.Empty(CEnhancedOptionRows.Build(new CGuiWindowType { Name = "menu_settings_GAME" }, CEnhancedOptions.Rows));
	}

	// --- the extra autosaves ---

	[Fact]
	public void ThePreMonthDayIsCountedBackFromEachMonthsOwnEnd()
	{
		Assert.True(CExtraAutosaves.IsPreMonthDay(new CDate(1936, 1, 30), 2));
		Assert.False(CExtraAutosaves.IsPreMonthDay(new CDate(1936, 1, 31), 2));
		// No leap years on the original's calendar: February has 28 days in 1936 too.
		Assert.True(CExtraAutosaves.IsPreMonthDay(new CDate(1936, 2, 27), 2));
		Assert.True(CExtraAutosaves.IsPreMonthDay(new CDate(1936, 4, 30), 1));
		Assert.True(CExtraAutosaves.IsPreMonthDay(new CDate(1936, 2, 2), 27));
		Assert.False(CExtraAutosaves.IsPreMonthDay(new CDate(1936, 1, 31), 0));
	}

	[Fact]
	public void ThePreMonthSaveIsAskedOnceOnItsDay()
	{
		var saves = new CExtraAutosaves();
		var settings = new CEnhancedSettings(PreMonthDays: 2);
		Assert.Equal(EExtraAutosave.None, saves.Decide(settings, new CDate(1936, 1, 29), 0));
		Assert.Equal(EExtraAutosave.PreMonth, saves.Decide(settings, new CDate(1936, 1, 30), 0));
		Assert.Equal(EExtraAutosave.None, saves.Decide(settings, new CDate(1936, 1, 30), 0));
		Assert.Equal(EExtraAutosave.None, saves.Decide(settings, new CDate(1936, 1, 31), 0));
		Assert.Equal(EExtraAutosave.PreMonth, saves.Decide(settings, new CDate(1936, 2, 27), 0));
		// With neither on, nothing is ever asked.
		Assert.Equal(EExtraAutosave.None, new CExtraAutosaves().Decide(CEnhancedSettings.Default, new CDate(1936, 1, 30), 0));
	}

	[Fact]
	public void TheTimerStartsOnTheFirstDayAndAsksOnceItsIntervalIsUp()
	{
		const long minute = 60_000;
		var saves = new CExtraAutosaves();
		var settings = new CEnhancedSettings(TimedMinutes: 15);
		// The first day starts the timer and asks nothing.
		Assert.Equal(EExtraAutosave.None, saves.Decide(settings, new CDate(1936, 1, 1), 100 * minute));
		Assert.Equal(EExtraAutosave.None, saves.Decide(settings, new CDate(1936, 1, 2), 114 * minute));
		Assert.Equal(EExtraAutosave.Timed, saves.Decide(settings, new CDate(1936, 1, 3), 115 * minute));
		// And counts afresh from the save.
		Assert.Equal(EExtraAutosave.None, saves.Decide(settings, new CDate(1936, 1, 4), 129 * minute));
		Assert.Equal(EExtraAutosave.Timed, saves.Decide(settings, new CDate(1936, 1, 5), 131 * minute));

		// Switched off, the timer stops: switched on again, the first day only starts it.
		Assert.Equal(EExtraAutosave.None, saves.Decide(CEnhancedSettings.Default, new CDate(1936, 1, 6), 500 * minute));
		Assert.Equal(EExtraAutosave.None, saves.Decide(settings, new CDate(1936, 1, 7), 900 * minute));
		Assert.Equal(EExtraAutosave.Timed, saves.Decide(settings, new CDate(1936, 1, 8), 915 * minute));
	}

	[Fact]
	public void APreMonthSaveComesFirstAndStartsTheTimerAfresh()
	{
		const long minute = 60_000;
		var saves = new CExtraAutosaves();
		var settings = new CEnhancedSettings(PreMonthDays: 2, TimedMinutes: 15);
		Assert.Equal(EExtraAutosave.None, saves.Decide(settings, new CDate(1936, 1, 29), 0));
		// The timer is up on the pre-month day: one save, the pre-month one.
		Assert.Equal(EExtraAutosave.PreMonth, saves.Decide(settings, new CDate(1936, 1, 30), 20 * minute));
		Assert.Equal(EExtraAutosave.None, saves.Decide(settings, new CDate(1936, 1, 31), 34 * minute));
		Assert.Equal(EExtraAutosave.Timed, saves.Decide(settings, new CDate(1936, 2, 1), 35 * minute));
	}

	[Fact]
	public void EachKindHasThreeFilesOfItsOwn()
	{
		Assert.Equal(new[] { "autosave_premonth.hoi3", "oldautosave_premonth.hoi3", "olderautosave_premonth.hoi3" }, CExtraAutosaves.PreMonthFiles);
		Assert.Equal(new[] { "autosave_timed.hoi3", "oldautosave_timed.hoi3", "olderautosave_timed.hoi3" }, CExtraAutosaves.FilesOf(EExtraAutosave.Timed));
		Assert.Empty(CExtraAutosaves.PreMonthFiles.Intersect(CAutosave.Files));
	}

	// --- the map and the event window ---

	[Fact]
	public void ThePointerWithinSixPixelsOfAnEdgeScrollsThatWay()
	{
		var view = new Vector2(1920, 1080);
		Assert.Equal(Vector2.Zero, CMapEdgeScroll.Direction(new Vector2(960, 540), view));
		Assert.Equal(new Vector2(-1, 0), CMapEdgeScroll.Direction(new Vector2(6, 540), view));
		Assert.Equal(Vector2.Zero, CMapEdgeScroll.Direction(new Vector2(7, 540), view));
		Assert.Equal(new Vector2(1, 0), CMapEdgeScroll.Direction(new Vector2(1914, 540), view));
		Assert.Equal(new Vector2(0, -1), CMapEdgeScroll.Direction(new Vector2(960, 0), view));
		Assert.Equal(new Vector2(1, 1), CMapEdgeScroll.Direction(new Vector2(1919, 1079), view));
		// Off the view - a window that is not the whole screen - nothing.
		Assert.Equal(Vector2.Zero, CMapEdgeScroll.Direction(new Vector2(-3, 540), view));
		Assert.Equal(Vector2.Zero, CMapEdgeScroll.Direction(new Vector2(960, 1100), view));
	}

	[Fact]
	public void ACountryEventsWindowIsPutAtTheLeftOrInTheMiddleAndAProvincesNever()
	{
		var country = CEventScope.Start(new OpenHOI3.Sim.Countries.CCountryTag("GER"));
		Assert.Null(CEventWindowLayout.PlacedX(country, EEventWindowPlace.AsFiled, 522, 1920));
		Assert.Equal(0f, CEventWindowLayout.PlacedX(country, EEventWindowPlace.Left, 522, 1920));
		Assert.Equal(699f, CEventWindowLayout.PlacedX(country, EEventWindowPlace.Centre, 522, 1920));

		var province = CEventScope.Start(new OpenHOI3.Sim.Countries.CCountryTag("GER"), 1928);
		Assert.Null(CEventWindowLayout.PlacedX(province, EEventWindowPlace.Left, 522, 1920));
	}
}
