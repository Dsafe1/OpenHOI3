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
	public void ARowShowsItsKeysTextFromTheLocalisationWithItsFigurePutIn()
	{
		var table = new Dictionary<string, string>
		{
			["OPENHOI_OPT_OFF"] = "Aus",
			["OPENHOI_OPT_ON"] = "An",
			["OPENHOI_OPT_PREMONTH_ONE"] = "Am letzten Tag",
			["OPENHOI_OPT_PREMONTH_DAYS"] = "$DAYS$ Tage vorher",
			["OPENHOI_OPT_TIMED_MINUTES"] = "Alle $MINUTES$ Minuten",
			["OPENHOI_OPT_EVENT_LEFT"] = "Links",
		};
		// As the localisation answers: a key no table has is itself.
		string Localise(string key) => table.TryGetValue(key, out string? text) ? text : key;

		var options = new CEnhancedOptions();
		options.Refresh(new CEnhancedSettings(2, 15, false, EEventWindowPlace.Left));
		Assert.Equal("2 Tage vorher", CEnhancedOptionsText.Value("premonth", options, Localise));
		Assert.Equal("Alle 15 Minuten", CEnhancedOptionsText.Value("timed", options, Localise));
		Assert.Equal("Aus", CEnhancedOptionsText.Value("edge", options, Localise));
		Assert.Equal("Links", CEnhancedOptionsText.Value("event", options, Localise));

		options.Refresh(new CEnhancedSettings(1, 0, true, EEventWindowPlace.Centre));
		Assert.Equal("Am letzten Tag", CEnhancedOptionsText.Value("premonth", options, Localise));
		Assert.Equal("Aus", CEnhancedOptionsText.Value("timed", options, Localise));
		Assert.Equal("An", CEnhancedOptionsText.Value("edge", options, Localise));
		// Nothing is worded by the engine: a text no table gives shows as its key.
		Assert.Equal("OPENHOI_OPT_EVENT_CENTRE", CEnhancedOptionsText.Value("event", options, Localise));
		options.Refresh(CEnhancedSettings.Default);
		Assert.Equal("OPENHOI_OPT_EVENT_DEFAULT", CEnhancedOptionsText.Value("event", options, Localise));
	}

	[Fact]
	public void TheRowsElementsAreFoundByTheNamesAModDeclaresThemUnder()
	{
		Assert.Equal("openhoi_game_options", CEnhancedOptionRows.WindowName);
		Assert.Equal("openhoi_timed_label", CEnhancedOptionRows.LabelName("timed"));
		Assert.Equal("openhoi_timed_value", CEnhancedOptionRows.ValueName("timed"));
		Assert.Equal("decrease_openhoi_timed_button", CEnhancedOptionRows.DecreaseName("timed"));
		Assert.Equal("increase_openhoi_timed_button", CEnhancedOptionRows.IncreaseName("timed"));
	}

	[Fact]
	public void AModsEnhancedInterfaceFolderIsReadOnlyWhereTheModeIsOn()
	{
		string root = Path.Combine(Path.GetTempPath(), "openhoi3-enhgui-" + Guid.NewGuid().ToString("N"));
		try
		{
			Directory.CreateDirectory(Path.Combine(root, "interface", "openhoi"));
			Directory.CreateDirectory(Path.Combine(root, "tfh", "common"));
			File.WriteAllText(Path.Combine(root, "interface", "core.gui"),
				"guiTypes = { windowType = { name = \"plain_window\" size = { x = 10 y = 10 } } }");
			File.WriteAllText(Path.Combine(root, "interface", "openhoi", "gameoptions.gui"),
				"guiTypes = { windowType = { name = \"openhoi_game_options\" size = { x = 240 y = 300 }\n"
				+ "guiButtonType = { name = \"increase_openhoi_edge_button\" position = { x = 165 y = 10 } quadTextureSprite = \"option_next\" } } }");
			var warnings = new List<string>();

			// A faithful game: the folder is not read, whatever lies in it.
			CGuiTypes faithful = CGuiTypes.Load(OpenHOI3.Content.CDirectorySettings.At(root), warnings.Add);
			Assert.NotNull(faithful.FindWindow("plain_window"));
			Assert.Null(faithful.FindWindow(CEnhancedOptionRows.WindowName));

			// An enhanced one: its declarations are there with the rest.
			File.WriteAllText(Path.Combine(root, "tfh", "common", "openhoi_enhanced.txt"), "");
			CGuiTypes enhanced = CGuiTypes.Load(OpenHOI3.Content.CDirectorySettings.At(root), warnings.Add);
			Assert.NotNull(enhanced.FindWindow("plain_window"));
			CGuiWindowType? window = enhanced.FindWindow(CEnhancedOptionRows.WindowName);
			Assert.NotNull(window);
			Assert.NotNull(window!.Find<CGuiButtonType>("increase_openhoi_edge_button"));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void AModsEnhancedLocalisationFolderIsReadOnlyWhereTheModeIsOnAndAfterTheRest()
	{
		string root = Path.Combine(Path.GetTempPath(), "openhoi3-enhloc-" + Guid.NewGuid().ToString("N"));
		try
		{
			Directory.CreateDirectory(Path.Combine(root, "localisation", "openhoi"));
			Directory.CreateDirectory(Path.Combine(root, "tfh", "common"));
			File.WriteAllText(Path.Combine(root, "localisation", "text.csv"), "PLAIN;Plain;;;;;;;;;;;;;x\nSHARED;The original's;;;;;;;;;;;;;x\n");
			File.WriteAllText(Path.Combine(root, "localisation", "openhoi", "options.csv"),
				"OPENHOI_OPT_OFF;Off;;;;;;;;;;;;;x\nSHARED;Ours;;;;;;;;;;;;;x\n");
			var warnings = new List<string>();

			// A faithful game: the folder is not read, as the original does not read it.
			var faithful = OpenHOI3.Content.CLocalisation.Load(OpenHOI3.Content.CDirectorySettings.At(root), warn: warnings.Add);
			Assert.Equal("Plain", faithful.Localise("PLAIN"));
			Assert.Null(faithful.Find("OPENHOI_OPT_OFF"));

			// An enhanced one: its tables after the rest, so a key both have keeps the first text, as between any two tables.
			File.WriteAllText(Path.Combine(root, "tfh", "common", "openhoi_enhanced.txt"), "");
			var enhanced = OpenHOI3.Content.CLocalisation.Load(OpenHOI3.Content.CDirectorySettings.At(root), warn: warnings.Add);
			Assert.Equal("Off", enhanced.Localise("OPENHOI_OPT_OFF"));
			Assert.Equal("Plain", enhanced.Localise("PLAIN"));
			Assert.Equal("The original's", enhanced.Localise("SHARED"));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
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
		// Past an edge - off the window, on a second screen - it scrolls on as at the edge.
		Assert.Equal(new Vector2(-1, 0), CMapEdgeScroll.Direction(new Vector2(-3, 540), view));
		Assert.Equal(new Vector2(-1, 0), CMapEdgeScroll.Direction(new Vector2(-1500, 540), view));
		Assert.Equal(new Vector2(0, 1), CMapEdgeScroll.Direction(new Vector2(960, 1100), view));
		Assert.Equal(new Vector2(1, -1), CMapEdgeScroll.Direction(new Vector2(2500, -40), view));
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
