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
		File.WriteAllText(Path.Combine(_root, "script", "openhoi", "windows", name + ".lua"), lua);
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
