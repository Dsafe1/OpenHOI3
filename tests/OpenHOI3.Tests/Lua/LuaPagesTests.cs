using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Windows;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Lua;

/// <summary>
/// The windows a mod defines, against <c>docs/mechanics/modwindows.md</c>: a page for each file of
/// <c>script/openhoi/windows</c>, in the menu's order; a file that fails being no page; a page gathering with the game
/// and drawing without it; a widget's answers; what a page left open ended for it; and a drawing that fails said in
/// the window until the page gathers again.
/// </summary>
public sealed class LuaPagesTests : IDisposable
{
	private readonly string _root = Path.Combine(Path.GetTempPath(), "ohoi-luapages-" + Guid.NewGuid().ToString("N"));
	private readonly List<string> _reports = new();
	private static readonly CCountryTag Ger = new("GER");

	public LuaPagesTests()
	{
		Directory.CreateDirectory(Path.Combine(_root, "script", "openhoi", "windows"));
		File.WriteAllText(Path.Combine(_root, "script", "autoexec.lua"), "");
	}

	public void Dispose() => Directory.Delete(_root, recursive: true);

	private void Page(string name, string lua)
	{
		string file = Path.Combine(_root, "script", "openhoi", "windows", name + ".lua");
		Directory.CreateDirectory(Path.GetDirectoryName(file)!);
		File.WriteAllText(file, lua);
	}

	[Fact]
	public void APageInASubfolderIsNamedByItsPathAndTheFolderSaysNothingOfItsTab()
	{
		Page("top", "return { order = 30, Draw = function() end }");
		Page("country/conversion", "return { order = 20, group = 'GROUP_COUNTRY', Draw = function() end }");
		Page("country/deep/flags", "return { order = 10, group = 'GROUP_INSPECTOR', Draw = function(data) OpenHOI3.ImGui.Text(data) end, " +
			"Collect = function() return 'gathered' end }");
		// Two folders may hold a file of one name: they are two pages.
		Page("stats/flags", "return { order = 40, Draw = function() end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			Assert.Equal(new[] { "country/deep/flags", "country/conversion", "top", "stats/flags" }, windows.Pages.Select(page => page.Name));
			Assert.Equal("GROUP_INSPECTOR", windows.Pages[0].Group);
			Assert.Equal("", windows.Pages[3].Group);
			Assert.Empty(_reports);
		}
	}

	private (CAiScripts Scripts, CLuaWindows Windows) Load()
	{
		CDirectorySettings directories = CDirectorySettings.At(_root);
		var scripts = new CAiScripts(directories, _reports.Add);
		return (scripts, new CLuaWindows(scripts, directories, key => key == "SOME_KEY" ? "Some text" : key, _reports.Add));
	}

	private static CGameState State()
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", false) }, new[] { germany });
		germany.Flags.Set("zeta_flag");
		germany.Flags.Set("Alpha_flag");
		germany.Flags.Set("cleared_flag");
		germany.Flags.Clear("cleared_flag");
		state.Flags.Set("world_flag");
		return state;
	}

	[Fact]
	public void EachFileOfTheFolderIsAPageInTheMenusOrder()
	{
		Page("second", "return { title = 'TITLE_B', order = 20, Draw = function() end }");
		Page("first", "return { title = 'TITLE_A', order = 10, Draw = function() end }");
		Page("also_second", "return { order = 20, Draw = function() end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			Assert.Equal(new[] { "first", "also_second", "second" }, windows.Pages.Select(page => page.Name));
			Assert.Equal(new CModPage("first", "TITLE_A", 10), windows.Pages[0]);
			Assert.Equal("", windows.Pages[1].Title);
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void APageNamesItsTabAndTheTabsAreSortedByTheLowestPlaceAnyOfTheirPagesGives()
	{
		Page("flags", "return { title = 'FLAGS', order = 20, group = 'GROUP_INSPECTOR', group_order = 40, Draw = function() end }");
		Page("variables", "return { title = 'VARS', order = 10, group = 'GROUP_INSPECTOR', Draw = function() end }");
		Page("modifiers", "return { title = 'MODS', order = 5, group = 'GROUP_COUNTRY', group_order = 20, Draw = function() end }");
		Page("loose", "return { Draw = function() end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			CModPage flags = windows.Pages.Single(page => page.Name == "flags");
			Assert.Equal("GROUP_INSPECTOR", flags.Group);
			Assert.Equal(40, flags.GroupOrder);
			Assert.Equal("", windows.Pages.Single(page => page.Name == "loose").Group);

			List<CModPageGroup> groups = CModPageGroup.Of(windows.Pages);
			// The pages naming no tab and the Inspector, one of whose pages gives no place, both stand at nought: by key.
			Assert.Equal(new[] { "", "GROUP_INSPECTOR", "GROUP_COUNTRY" }, groups.Select(group => group.Key));
			// A tab's pages by their own order.
			Assert.Equal(new[] { "variables", "flags" }, groups[1].Pages.Select(page => page.Name));
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void TabsAreSortedByPlaceAndThenByKey()
	{
		var pages = new[]
		{
			new CModPage("a", "", 0, "GROUP_STATS", 50),
			new CModPage("b", "", 0, "GROUP_SETUP", 10),
			new CModPage("c", "", 30, "GROUP_SETUP", 10),
			new CModPage("d", "", 30, "GROUP_HELP", 50),
			new CModPage("e", "", 20, "GROUP_SETUP", 10),
		};
		List<CModPageGroup> groups = CModPageGroup.Of(pages);
		Assert.Equal(new[] { "GROUP_SETUP", "GROUP_HELP", "GROUP_STATS" }, groups.Select(group => group.Key));
		Assert.Equal(new[] { "b", "e", "c" }, groups[0].Pages.Select(page => page.Name));
	}

	[Fact]
	public void AFileThatFailsOrDrawsNothingIsNoPageAndIsReported()
	{
		Page("broken", "return {{{");
		Page("drawless", "return { title = 'X' }");
		Page("nothing", "local x = 1");
		Page("good", "return { Draw = function() end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			Assert.Equal(new[] { "good" }, windows.Pages.Select(page => page.Name));
			Assert.Equal(3, _reports.Count);
			Assert.Contains(_reports, report => report.Contains("broken.lua"));
			Assert.Contains(_reports, report => report.Contains("drawless") && report.Contains("has no Draw function"));
			Assert.Contains(_reports, report => report.Contains("nothing") && report.Contains("does not return a table"));
		}
	}

	[Fact]
	public void APageReadsACountrysVariablesAndTheModifiersItWasGiven()
	{
		Page("inspect", "local ImGui = OpenHOI3.ImGui return { "
			+ "Collect = function(player) return { v = OpenHOI3.CountryVariables(player), m = OpenHOI3.CountryModifiers(player), "
			+ "none = #OpenHOI3.CountryVariables('XXX') + #OpenHOI3.CountryModifiers('XXX') } end, "
			+ "Draw = function(data) "
			+ "for _, v in ipairs(data.v) do ImGui.Text(v.name .. '=' .. v.value) end "
			+ "for _, m in ipairs(data.m) do ImGui.Text(m.name .. '|' .. m.expires .. '|' .. tostring(m.never)) end "
			+ "ImGui.Text('none ' .. data.none) end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			CGameState state = State();
			CCountry germany = state.Countries[0];
			germany.Variables["zeta"] = new CFixedPoint(12050);
			germany.Variables["Alpha"] = new CFixedPoint(3000);
			germany.Variables["nought"] = CFixedPoint.Zero;
			// Thirty days from 1 January 1936, and one that never ends: a date in year one.
			germany.EventModifiers.Add(OpenHOI3.Sim.Modifiers.CTimedModifier.Given(state, new OpenHOI3.Sim.Modifiers.CModifier("second_given"), 30));
			germany.EventModifiers.Add(OpenHOI3.Sim.Modifiers.CTimedModifier.Given(state, new OpenHOI3.Sim.Modifiers.CModifier("for_ever"), -1));

			var canvas = new CRecordingCanvas();
			windows.Collect(state, windows.Pages[0], Ger);
			windows.Draw(windows.Pages[0], Ger, canvas);
			Assert.Equal(new[]
			{
				// The variables by name without case, one at nought among them; the modifiers in the order given.
				"Text Alpha=3", "Text nought=0", "Text zeta=12.05",
				"Text second_given|1936.1.31 0:00|false", "Text for_ever|1.1.1 0:00|true",
				"Text none 0",
			}, canvas.Calls);
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void APageDrawsPanesAPictureAndAsksTheKeyboardAndTheClipboard()
	{
		Page("panes", "local ImGui = OpenHOI3.ImGui return { Draw = function() "
			+ "if ImGui.BeginPanes('two', 2) then ImGui.TableSetupColumn('left', 280) ImGui.TableSetupColumn('right') "
			+ "ImGui.TableNextRow() ImGui.TableNextColumn() "
			+ "if ImGui.IsWindowFocused() and ImGui.IsKeyPressed('DownArrow') and not ImGui.IsKeyPressed('UpArrow') then ImGui.SetScrollHereY(1) end "
			+ "ImGui.TableNextColumn() "
			+ "local w, h = ImGui.ImageSize('gfx/a.tga') ImGui.Image('gfx/a.tga', w * 2, h * 2) "
			+ "local none = ImGui.ImageSize('gfx/missing.png') ImGui.Text('none ' .. none) "
			+ "ImGui.SetClipboardText('copied') "
			// Left open: the panes are a table, and are ended for the page as one.
			+ "end end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			var canvas = new CRecordingCanvas { Focused = true };
			canvas.Keys.Add("DownArrow");
			windows.Draw(windows.Pages[0], Ger, canvas);
			Assert.Equal(new[]
			{
				"BeginTable two 2 panes", "TableSetupColumn left 280", "TableSetupColumn right", "TableNextRow", "TableNextColumn",
				"SetScrollHereY 1", "TableNextColumn", "Image gfx/a.tga 128 64", "Text none 0", "SetClipboardText copied", "EndTable",
			}, canvas.Calls);
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void APageReadsAndSetsTheWindowsOwnSettings()
	{
		Page("options", "local ImGui, Window = OpenHOI3.ImGui, OpenHOI3.Window return { Draw = function() "
			+ "ImGui.Text(Window.Key() .. ' ' .. Window.Theme() .. ' ' .. Window.Scale() .. ' ' .. tostring(Window.Capturing()) .. ' ' .. table.concat(Window.Themes(), ',')) "
			+ "Window.SetTheme('light') Window.SetScale(1.25) Window.CaptureKey() Window.CancelCapture() Window.ResetLayout() "
			+ "ImGui.Text(Window.Theme() .. ' ' .. tostring(OpenHOI3.ImGui.WindowSetting)) end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			var canvas = new CRecordingCanvas();
			windows.Draw(windows.Pages[0], Ger, canvas);
			Assert.Equal(new[]
			{
				"Text Insert dark 1 false dark,light,classic",
				"SetWindowSetting theme light", "SetWindowSetting scale 1.25", "SetWindowSetting key capture",
				"SetWindowSetting key cancel", "SetWindowSetting layout reset",
				// They are the window's, under OpenHOI3.Window, and not among the widgets.
				"Text light nil",
			}, canvas.Calls);
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void APageDrawsALineThroughAListOfValuesHoweverLong()
	{
		// Nine thousand values: more than a call of the Lua's takes as arguments, which is why they cross as one text.
		Page("plots", "local ImGui = OpenHOI3.ImGui return { Draw = function() "
			+ "ImGui.PlotLines('##a', { 1, 2.5, -3 }, 0, 90, 'three') "
			+ "local long = {} for i = 1, 9000 do long[i] = i end "
			+ "ImGui.PlotLines('##b', long) ImGui.PlotLines('##c', {}) end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			var canvas = new CRecordingCanvas();
			windows.Draw(windows.Pages[0], Ger, canvas);
			Assert.Equal(3, canvas.Calls.Count);
			Assert.Equal("PlotLines ##a 1,2,-3 90 three", canvas.Calls[0]);
			Assert.StartsWith("PlotLines ##b 1,2,3,", canvas.Calls[1]);
			Assert.EndsWith(",8999,9000 0 ", canvas.Calls[1]);
			Assert.Equal("PlotLines ##c  0 ", canvas.Calls[2]);
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void WhatTheLuaPostsAsAPageGathersIsThePagesOrderAndNotTheGamesOutbox()
	{
		// The original's own way of setting a variable, as BlackICE's data functions do it.
		Page("sets", "local ImGui = OpenHOI3.ImGui local want = false return { "
			+ "Collect = function(player) if want then want = false "
			+ "CCurrentGameState.Post(CSetVariableCommand(CCountryDataBase.GetTag(player), CString('national_focus'), CFixedPoint(3))) end "
			+ "return {} end, "
			+ "Draw = function() if ImGui.Button('set') then want = true OpenHOI3.Refresh() end "
			+ "local changed, value = ImGui.InputFloat('##n', 2.5, 2) ImGui.ProgressBar(0.25, 0, '25%') "
			+ "ImGui.Text(tostring(ImGui.IsItemDeactivatedAfterEdit())) end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			CGameState state = State();
			var canvas = new CRecordingCanvas();
			canvas.Pressed.Add("set");
			windows.Draw(windows.Pages[0], Ger, canvas);
			Assert.Equal(new[] { "Button set", "InputFloat ##n 2500 2", "ProgressBar 25 25%", "Text false" }, canvas.Calls);
			Assert.True(windows.TakeRefresh());
			Assert.Empty(windows.TakePosted());

			windows.Collect(state, windows.Pages[0], Ger);
			Assert.Empty(state.Outbox);
			var posted = Assert.IsType<OpenHOI3.Sim.Commands.CSetVariableCommand>(Assert.Single(windows.TakePosted()));
			Assert.Equal(Ger, posted.Country);
			Assert.Equal("national_focus", posted.Name);
			Assert.Equal(CFixedPoint.FromInt(3), posted.Value);
			// Nothing was changed by the gathering itself: the order is the screen's to post.
			Assert.False(state.Countries[0].Variables.ContainsKey("national_focus"));
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void APageReadsAProvincesOwnFiguresInThousandths()
	{
		Page("province", "local ImGui = OpenHOI3.ImGui return { "
			+ "Collect = function() return { a = OpenHOI3.ProvinceDetails(1), none = OpenHOI3.ProvinceDetails(99) } end, "
			+ "Draw = function(data) local a = data.a "
			+ "ImGui.Text(a.id .. ' ' .. a.supply_pool .. ' ' .. a.fuel_pool .. ' ' .. a.metal .. ' ' .. a.manpower .. ' ' .. a.leadership) "
			+ "ImGui.Text(a.modifiers.local_ic .. ' ' .. a.modifiers.local_rares .. ' ' .. a.modifiers.local_oil) "
			+ "ImGui.Text(tostring(data.none)) end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
			var province = new CProvince(1, "Essen", false) { Manpower = new CFixedPoint(340), Leadership = new CFixedPoint(1250) };
			province.Pool[OpenHOI3.Sim.Economy.EGoods.Supplies] = new CFixedPoint(12500);
			province.Pool[OpenHOI3.Sim.Economy.EGoods.Fuel] = new CFixedPoint(700);
			province.CurrentProducing[OpenHOI3.Sim.Economy.EGoods.Metal] = new CFixedPoint(3000);
			province.Modifier.Set("local_ic", new CFixedPoint(50));
			province.Modifier.Set("local_rare_materials", new CFixedPoint(250));
			var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", false), province }, new[] { germany });

			var canvas = new CRecordingCanvas();
			windows.Collect(state, windows.Pages[0], Ger);
			windows.Draw(windows.Pages[0], Ger, canvas);
			// Thousandths throughout, as the mod's own data divides them; nil for a number the map has not.
			Assert.Equal(new[] { "Text 1 12500 700 3000 340 1250", "Text 50 250 0", "Text nil" }, canvas.Calls);
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void APageReadsACountrysOrderOfBattleEveryUnitWithTheOneOverIt()
	{
		Page("oob", "local ImGui = OpenHOI3.ImGui return { "
			+ "Collect = function(player) return { units = OpenHOI3.OrderOfBattle(player), none = #OpenHOI3.OrderOfBattle('XXX') } end, "
			+ "Draw = function(data) for _, u in ipairs(data.units) do "
			+ "ImGui.Text(u.id .. ' ' .. u.parent .. ' ' .. u.branch .. ' ' .. u.kind .. ' ' .. u.name .. ' ' .. u.province .. ' ' "
			+ ".. (u.leader and (u.leader.name .. ' ' .. u.leader.skill .. '/' .. u.leader.max_skill) or 'nobody') .. ' ' "
			+ ".. u.supply .. ' ' .. u.dug_in .. ' [' .. u.orders .. '] ' .. #u.regiments) end "
			+ "ImGui.Text('none ' .. data.none) end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			CGameState state = State();
			CCountry germany = state.Countries[0];
			var corps = new OpenHOI3.Sim.Units.CUnit(OpenHOI3.Sim.Units.EUnitKind.Corps, Ger) { Id = 5, Location = 1861, Name = "I. A.K." };
			corps.Leader = new OpenHOI3.Sim.Leaders.CLeader(7) { Name = "Schroth", Skill = 3, MaxSkill = 7 };
			// A name with the game's colour escapes in it, a unit under another, one told to do neither.
			var division = new OpenHOI3.Sim.Units.CUnit(OpenHOI3.Sim.Units.EUnitKind.Division, Ger)
			{
				Id = 9, Location = 1527, Name = "§Y1. Kavallerie-Brigade§W", Parent = corps, DigIn = new CFixedPoint(2500),
				MayReinforce = false, MayUpgrade = false, IsPrioritised = true, SuppliesReceived = new CFixedPoint(500),
			};
			corps.Children.Add(division);
			var fleet = new OpenHOI3.Sim.Units.CUnit(OpenHOI3.Sim.Units.EUnitKind.Navy, Ger) { Id = 12, Location = 30, Name = "Baltische Flotte" };
			germany.Units.Add(corps);
			germany.Units.Add(fleet);

			var canvas = new CRecordingCanvas();
			windows.Collect(state, windows.Pages[0], Ger);
			windows.Draw(windows.Pages[0], Ger, canvas);
			Assert.Equal(new[]
			{
				// A unit before what stands under it; the top ones in the country's own order.
				"Text 5 0 land Corps I. A.K. 1861 Schroth 3/7 1 0 [reinforcing upgrading] 0",
				"Text 9 5 land Division 1. Kavallerie-Brigade 1527 nobody 0.5 2.5 [priority] 0",
				"Text 12 0 naval Fleet Baltische Flotte 30 nobody 1 0 [reinforcing upgrading] 0",
				"Text none 0",
			}, canvas.Calls);
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void APageReadsWhereALeaderIs()
	{
		Page("leader", "local ImGui = OpenHOI3.ImGui return { "
			+ "Collect = function() return { a = OpenHOI3.LeaderDetails(7), b = OpenHOI3.LeaderDetails(8), c = OpenHOI3.LeaderDetails(9) } end, "
			+ "Draw = function(data) "
			+ "ImGui.Text(tostring(data.a.active) .. ' ' .. data.a.unit_name .. ' ' .. data.a.province_id) "
			+ "ImGui.Text(tostring(data.b.active) .. ' ' .. tostring(data.b.unit_name) .. ' ' .. tostring(data.b.province_id)) "
			+ "ImGui.Text(tostring(data.c)) end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			CGameState state = State();
			CCountry germany = state.Countries[0];
			var commanding = new OpenHOI3.Sim.Leaders.CLeader(7) { Country = Ger };
			commanding.Unit = new OpenHOI3.Sim.Units.CUnit(OpenHOI3.Sim.Units.EUnitKind.Army, Ger) { Id = 1, Location = 1861, Name = "Heeresgruppe" };
			var idle = new OpenHOI3.Sim.Leaders.CLeader(8) { Country = Ger };
			state.Leaders[7] = commanding;
			state.Leaders[8] = idle;
			germany.ActiveLeaders.Add(commanding);

			var canvas = new CRecordingCanvas();
			windows.Collect(state, windows.Pages[0], Ger);
			windows.Draw(windows.Pages[0], Ger, canvas);
			// In service with a unit; known and out of service with none; and no leader of that number.
			Assert.Equal(new[] { "Text true Heeresgruppe 1861", "Text false nil nil", "Text nil" }, canvas.Calls);
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void APageAsksToGatherAgainAsItDrawsAndTheAskingIsTakenOnce()
	{
		Page("asks", "local ImGui = OpenHOI3.ImGui return { Draw = function() "
			+ "if ImGui.Button('again') then OpenHOI3.Refresh() end "
			+ "local w, h = ImGui.GetContentRegionAvail() "
			+ "local changed, text = ImGui.InputTextMultiline('##box', 'two\\nlines', -1, h / 2, true) "
			+ "ImGui.Text(tostring(changed) .. ' ' .. w) end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			var canvas = new CRecordingCanvas();
			windows.Draw(windows.Pages[0], Ger, canvas);
			Assert.False(windows.TakeRefresh());
			Assert.Equal(new[] { "Button again", "InputTextMultiline ##box 'two\nlines' -1 150 True", "Text false 400" }, canvas.Calls);

			canvas.Pressed.Add("again");
			windows.Draw(windows.Pages[0], Ger, canvas);
			Assert.True(windows.TakeRefresh());
			Assert.False(windows.TakeRefresh());
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void APageGathersWithTheGameAndDrawsWhatItGathered()
	{
		Page("flags", "local ImGui = OpenHOI3.ImGui return { "
			+ "Collect = function(player) return { tag = player, flags = OpenHOI3.CountryFlags(player), world = OpenHOI3.GlobalFlags() } end, "
			+ "Draw = function(data, player) if data == nil then ImGui.Text('nothing yet') return end "
			+ "ImGui.Text(OpenHOI3.Localise('SOME_KEY') .. ' ' .. data.tag .. ' ' .. player) "
			+ "if ImGui.BeginTable('flags', 1) then for _, name in ipairs(data.flags) do "
			+ "ImGui.TableNextRow() ImGui.TableNextColumn() ImGui.Text(name) end ImGui.EndTable() end "
			+ "ImGui.Text(table.concat(data.world, ',')) end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			CModPage page = windows.Pages[0];
			var canvas = new CRecordingCanvas();
			// Before it has gathered, a page draws with nothing.
			windows.Draw(page, Ger, canvas);
			Assert.Equal(new[] { "Text nothing yet" }, canvas.Calls);

			windows.Collect(State(), page, Ger);
			canvas.Calls.Clear();
			windows.Draw(page, Ger, canvas);
			// The flags sorted by name without case, a cleared one left out.
			Assert.Equal(new[]
			{
				"Text Some text GER GER", "BeginTable flags 1",
				"TableNextRow", "TableNextColumn", "Text Alpha_flag",
				"TableNextRow", "TableNextColumn", "Text zeta_flag",
				"EndTable", "Text world_flag",
			}, canvas.Calls);
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void AWidgetThatTakesAValueAnswersWhetherItChangedAndTheValue()
	{
		Page("widgets", "local ImGui = OpenHOI3.ImGui local text, ticked, choice, pressed = 'old', false, 1, 0 return { "
			+ "Draw = function() local changed "
			+ "changed, text = ImGui.InputText('Filter', text) "
			+ "changed, ticked = ImGui.Checkbox('Auto', ticked) "
			+ "changed, choice = ImGui.Combo('Mode', choice, { 'a', 'b', 'c' }) "
			+ "if ImGui.Button('Refresh') then pressed = pressed + 1 end "
			+ "ImGui.Text(text .. ' ' .. tostring(ticked) .. ' ' .. choice .. ' ' .. pressed) end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			var canvas = new CRecordingCanvas();
			windows.Draw(windows.Pages[0], Ger, canvas);
			Assert.Equal("Text old false 1 0", canvas.Calls[^1]);
			Assert.Contains("Combo Mode 0 [a,b,c]", canvas.Calls);

			canvas.Calls.Clear();
			canvas.Typed["Filter"] = "new";
			canvas.Pressed.UnionWith(new[] { "Auto", "Mode", "Refresh" });
			windows.Draw(windows.Pages[0], Ger, canvas);
			// The combo's choice is counted from one on the page's side.
			Assert.Equal("Text new true 3 1", canvas.Calls[^1]);
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void WhatAPageLeftOpenIsEndedForItAndAScopeThatDidNotOpenIsNot()
	{
		Page("open", "local ImGui = OpenHOI3.ImGui return { Draw = function() "
			+ "ImGui.PushID('row') ImGui.BeginChild('list', 0, 100) "
			+ "if ImGui.BeginTabBar('tabs') then if ImGui.BeginTabItem('one') then ImGui.BeginTable('t', 2) end end end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			var canvas = new CRecordingCanvas();
			windows.Draw(windows.Pages[0], Ger, canvas);
			// Ended the last begun first.
			Assert.Equal(new[] { "EndTable", "EndTabItem", "EndTabBar", "EndChild", "PopID" }, canvas.Calls.TakeLast(5));

			// With every scope answering shut, only what is ended whatever it answered is ended: the child and the id.
			canvas = new CRecordingCanvas { ScopesOpen = false };
			windows.Draw(windows.Pages[0], Ger, canvas);
			Assert.Equal(new[] { "PushID row", "BeginChild list 0 100", "BeginTabBar tabs", "EndChild", "PopID" }, canvas.Calls);
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void ADrawingThatFailsIsSaidInTheWindowUntilThePageGathersAgain()
	{
		Page("failing", "local ImGui = OpenHOI3.ImGui local broken = true return { "
			+ "Collect = function() broken = false return {} end, "
			+ "Draw = function() ImGui.BeginTable('t', 1) if broken then error('it broke') end ImGui.Text('fine') ImGui.EndTable() end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			CModPage page = windows.Pages[0];
			var canvas = new CRecordingCanvas();
			windows.Draw(page, Ger, canvas);
			// The table it had begun is ended for it; the failure is reported once.
			Assert.Equal(new[] { "BeginTable t 1", "EndTable" }, canvas.Calls);
			Assert.Single(_reports);
			Assert.Contains("it broke", _reports[0]);

			// From then on its window says why, and its Lua is not run - nothing more is reported.
			canvas.Calls.Clear();
			windows.Draw(page, Ger, canvas);
			windows.Draw(page, Ger, canvas);
			Assert.Equal(2, canvas.Calls.Count);
			Assert.All(canvas.Calls, call => Assert.StartsWith("TextWrapped ", call));
			Assert.Contains("it broke", canvas.Calls[0]);
			Assert.Single(_reports);

			// Gathered again, it is tried again.
			windows.Collect(State(), page, Ger);
			canvas.Calls.Clear();
			windows.Draw(page, Ger, canvas);
			Assert.Equal(new[] { "BeginTable t 1", "Text fine", "EndTable" }, canvas.Calls);
		}
	}

	[Fact]
	public void APageReadsACountrysConversionsAndOrdersOneSetWhileItDraws()
	{
		Page("conversion", "local ImGui = OpenHOI3.ImGui return { "
			+ "Collect = function(player) return OpenHOI3.Conversion(player) end, "
			+ "Draw = function(data, player) "
			+ "ImGui.Text(tostring(data.played) .. ' ' .. tostring(data.energy_conversion.own) .. ' ' .. data.energy_conversion.per_day "
			+ ".. ' ' .. data.crude_oil_conversion.keep .. ' ' .. data.energy_floor) "
			+ "if ImGui.Button('Apply') then "
			+ "ImGui.Text(tostring(OpenHOI3.SetConversion(player, 'crude_oil', true, 20, 100.5, 0))) "
			+ "ImGui.Text(tostring(OpenHOI3.SetConversion(player, 'coal', true, 1, 1, 1))) end end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			CGameState state = State();
			state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
			CCountry germany = state.Country(Ger)!;
			germany.EnergyConversion.Set(true, CFixedPoint.FromInt(30), CFixedPoint.Zero, CFixedPoint.Zero);
			germany.CrudeOilConversion.Set(false, CFixedPoint.Zero, CFixedPoint.Parse("12.5"), CFixedPoint.Zero);
			CModPage page = windows.Pages[0];
			windows.Collect(state, page, Ger);

			var canvas = new CRecordingCanvas();
			windows.Draw(page, Ger, canvas);
			// Nobody plays it; the energy's setting its own at 30 a day; the crude oil's keep; no IC, so no floor.
			Assert.Equal("Text false true 30 12.5 0", canvas.Calls[0]);
			Assert.Empty(windows.TakePosted());

			// A button pressed: the order is kept for the screen to post, and one that names no conversion is refused.
			canvas.Calls.Clear();
			canvas.Pressed.Add("Apply");
			windows.Draw(page, Ger, canvas);
			Assert.Equal(new[] { "Text true", "Text false" }, canvas.Calls.TakeLast(2));
			var posted = windows.TakePosted();
			Assert.Single(posted);
			Assert.Empty(windows.TakePosted());

			// Posted, it sets what it names and no more.
			posted[0].Execute(state);
			Assert.True(germany.CrudeOilConversion.Own);
			Assert.Equal(CFixedPoint.FromInt(20), germany.CrudeOilConversion.PerDay);
			Assert.Equal(CFixedPoint.Parse("100.5"), germany.CrudeOilConversion.Keep);
			Assert.Equal(CFixedPoint.FromInt(30), germany.EnergyConversion.PerDay);
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void APageReadsACountrysOffMapIcAndItsTrickleback()
	{
		Page("figures", "local ImGui = OpenHOI3.ImGui return { "
			+ "Collect = function(player) return { ic = OpenHOI3.OffmapIc('ger'), back = OpenHOI3.Trickleback(player), "
			+ "none = tostring(OpenHOI3.OffmapIc('XXX')) .. tostring(OpenHOI3.Trickleback('XXX')) } end, "
			+ "Draw = function(data) ImGui.Text(data.ic .. ' ' .. data.back * 1000 .. ' ' .. data.none) end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			CGameState state = State();
			CCountry germany = state.Country(Ger)!;
			germany.OffmapIc = 12;
			germany.Modifier.Set("casualty_trickleback", CFixedPoint.Parse("0.2"));
			germany.Technology.Set("casualty_trickleback", CFixedPoint.Parse("0.05"));
			CModPage page = windows.Pages[0];
			windows.Collect(state, page, Ger);

			var canvas = new CRecordingCanvas();
			windows.Draw(page, Ger, canvas);
			Assert.Equal("Text 12 250 nilnil", canvas.Calls[0]);
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void APageDrawsSeveralSeriesIntoOneFrameEachWithItsFirstStepAndColour()
	{
		Page("plots", "local ImGui = OpenHOI3.ImGui return { Draw = function() "
			+ "ImGui.PlotMany('##all', { { first = 10, colour = { 1, 0, 0, 1 }, values = { 1, 2, 3 } }, "
			+ "{ first = 12, values = { 7, 8 } }, { first = 1, values = {} } }, 260) "
			+ "ImGui.PlotMany('##none', {}) end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			var canvas = new CRecordingCanvas();
			windows.Draw(windows.Pages[0], Ger, canvas);
			// A series with no colour is white; one with no values is left out.
			Assert.Equal(new[] { "PlotMany ##all 260 10:1/0/0/1:1,2,3 | 12:1/1/1/1:7,8", "PlotMany ##none 0 " }, canvas.Calls);
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void APlotAnswersTheStepUnderTheMouseAndTakesItsAxesAndATooltipLeftOpenIsEnded()
	{
		Page("plots", "local ImGui = OpenHOI3.ImGui return { Draw = function() "
			+ "local step = ImGui.PlotMany('##all', { { first = 10, values = { 1, 2, 3 } } }, 90, "
			+ "{ x = 'Date', y = 'mixed', from = 'a', to = 'b' }) "
			+ "ImGui.Text(tostring(step)) "
			+ "if step ~= nil then ImGui.BeginTooltip() ImGui.Text('day ' .. step) end end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			var canvas = new CRecordingCanvas();
			windows.Draw(windows.Pages[0], Ger, canvas);
			// The mouse elsewhere: nil.
			Assert.Equal(new[] { "PlotMany ##all 90 10:1/1/1/1:1,2,3 axes Date/mixed/a/b", "Text nil" }, canvas.Calls);
			canvas.Calls.Clear();
			canvas.PlotStep = 11;
			windows.Draw(windows.Pages[0], Ger, canvas);
			Assert.Equal(new[] { "PlotMany ##all 90 10:1/1/1/1:1,2,3 axes Date/mixed/a/b", "Text 11", "BeginTooltip",
				"Text day 11", "EndTooltip" }, canvas.Calls);
		}
	}

	[Fact]
	public void APageReadsTheStatisticsRecordAndAsksForItWrittenOut()
	{
		Page("stats", "local ImGui = OpenHOI3.ImGui return { "
			+ "Collect = function(player) local series = OpenHOI3.Statistics.Series(player, 'prod__TotalIc') "
			+ "return { on = OpenHOI3.Statistics.Collecting(), names = #OpenHOI3.Statistics.Names(), "
			+ "countries = table.concat(OpenHOI3.Statistics.Countries(), ','), "
			+ "first = series and series.first_day, values = series and table.concat(series.values, ' '), "
			+ "none = tostring(OpenHOI3.Statistics.Series('XXX', 'prod__TotalIc')) .. tostring(OpenHOI3.Statistics.Series(player, 'nothing')) } end, "
			+ "Draw = function(data) ImGui.Text(tostring(data.on) .. ' ' .. data.countries .. ' ' .. tostring(data.first) .. ' ' "
			+ ".. tostring(data.values) .. ' ' .. data.none .. ' ' .. tostring(data.names > 40)) end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			CGameState state = State();
			state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
			CCountry germany = state.Country(Ger)!;
			var kept = new OpenHOI3.Sim.Statistics.CCountryStatistics { FirstDayNumber = state.Date.DayNumber, Year = state.Date.Year };
			OpenHOI3.Sim.Statistics.CStatisticSeries series = state.Statistics.Series(state);
			kept.Rows = Enumerable.Range(0, series.Count).Select(_ => new long[OpenHOI3.Sim.Statistics.CStatistics.DaysInYear]).ToArray();
			int day = state.Date.DayNumber - new CDate(state.Date.Year, 1, 1).DayNumber;
			// Kept in hundredths: 258 and 258.5 IC.
			kept.Rows[series.IndexOf("prod__TotalIc")][day] = 25800;
			kept.Rows[series.IndexOf("prod__TotalIc")][day + 1] = 25850;
			kept.EndDay = day + 2;
			state.Statistics.Countries[germany.Tag] = kept;

			CModPage page = windows.Pages[0];
			windows.Collect(state, page, Ger);
			var canvas = new CRecordingCanvas();
			windows.Draw(page, Ger, canvas);
			Assert.Equal($"Text true GER {state.Date.DayNumber} 258 258.5 nilnil true", canvas.Calls[0]);
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void APageThatNamesAWindowIsANativeOneWithItsHostAndOpenerAndNeedsAFillFunction()
	{
		Page("sliders", "return { title = 'T', window = 'my_window', host = 'production', opener = 'my_opener', Fill = function() end }");
		Page("plain", "return { Draw = function() end }");
		Page("broken", "return { window = 'other', Draw = function() end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			CModPage native = windows.Pages.Single(page => page.Name == "sliders");
			Assert.True(native.IsNative);
			Assert.Equal(("my_window", "production", "my_opener"), (native.Window, native.Host, native.Opener));
			Assert.False(windows.Pages.Single(page => page.Name == "plain").IsNative);
			// A window named and nothing to fill it with: no page.
			Assert.DoesNotContain(windows.Pages, page => page.Name == "broken");
			Assert.Single(_reports, report => report.Contains("names a window and has no Fill function"));
		}
	}

	[Fact]
	public void ANativePageFillsItsWindowFromWhatItGatheredAndReadsWhatThePlayerDid()
	{
		Page("form", "local Gui = OpenHOI3.Gui local typed = '' return { window = 'w', "
			+ "Collect = function(player) return { tag = player } end, "
			+ "Fill = function(data, player) "
			+ "Gui.SetText('title', data.tag .. ' ' .. player) "
			+ "if Gui.Edited('amount') then typed = Gui.Text('amount') end "
			+ "Gui.SetText('echo', typed) "
			+ "if Gui.Pressed('apply') then Gui.SetEnabled('apply', false) Gui.SetRows('list', 'entry', 3) Gui.SetText('list#2/name', 'second') end "
			+ "Gui.SetChecked('tick', not Gui.Checked('tick')) "
			+ "Gui.SetColor('swatch', 255, 0, 0) Gui.SetColour('swatch', 0, 128, 0, 64) "
			+ "Gui.SetVisible('extra', false) Gui.SetTooltip('help', 'Help') Gui.SetFrame('icon', 2) "
			+ "if typed == 'bye' then Gui.Close() end end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			CModPage page = windows.Pages[0];
			windows.Collect(State(), page, Ger);
			var canvas = new CRecordingNativeCanvas();
			canvas.Ticked.Add("tick");
			windows.Fill(page, Ger, canvas);
			Assert.Equal(new[]
			{
				"SetText title GER GER", "SetText echo ", "SetChecked tick False", "SetColour swatch 255 0 0 255",
				"SetColour swatch 0 128 0 64", "SetVisible extra False", "SetTooltip help Help", "SetFrame icon 2",
			}, canvas.Calls);

			// Typed into and pressed: each is answered once.
			canvas.Calls.Clear();
			canvas.Texts["amount"] = "bye";
			canvas.EditedOnce.Add("amount");
			canvas.PressedOnce.Add("apply");
			windows.Fill(page, Ger, canvas);
			Assert.Contains("SetText echo bye", canvas.Calls);
			Assert.Contains("SetEnabled apply False", canvas.Calls);
			Assert.Contains("SetRows list entry 3", canvas.Calls);
			Assert.Contains("SetText list#2/name second", canvas.Calls);
			Assert.Equal("Close", canvas.Calls[^1]);
			Assert.Empty(canvas.PressedOnce);
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void ANativePageThatFailsIsReportedOnceAndNotFilledUntilItGathersAgain()
	{
		Page("bad", "local Gui = OpenHOI3.Gui local fills = 0 return { window = 'w', "
			+ "Fill = function() fills = fills + 1 Gui.SetText('count', tostring(fills)) if fills == 1 then error('broke', 0) end end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			CModPage page = windows.Pages[0];
			var canvas = new CRecordingNativeCanvas();
			windows.Fill(page, Ger, canvas);
			windows.Fill(page, Ger, canvas);
			Assert.Equal(new[] { "SetText count 1" }, canvas.Calls);
			Assert.Single(_reports, report => report.Contains("broke"));

			windows.Collect(State(), page, Ger);
			windows.Fill(page, Ger, canvas);
			Assert.Equal(new[] { "SetText count 1", "SetText count 2" }, canvas.Calls);
		}
	}

	[Fact]
	public void APageThatAsksTheGameWhileItDrawsFailsAndOneThatEndsWhatItDidNotBeginIsToldSo()
	{
		Page("asks", "return { Draw = function() local date = CCurrentGameState.GetCurrentDate() end }");
		Page("ends", "local ImGui = OpenHOI3.ImGui return { Draw = function() ImGui.EndTable() end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			var canvas = new CRecordingCanvas();
			windows.Draw(windows.Pages.Single(page => page.Name == "asks"), Ger, canvas);
			windows.Draw(windows.Pages.Single(page => page.Name == "ends"), Ger, canvas);
			// Nothing reached the canvas from either: no game to ask, and an end with nothing open is passed over.
			Assert.Empty(canvas.Calls);
			Assert.Equal(2, _reports.Count);
			Assert.Contains(_reports, report => report.Contains("ends") && report.Contains("did not begin"));
		}
	}
}
