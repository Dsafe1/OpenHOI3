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
/// <c>script/openhoi/windows</c>, in the menu's order; a <c>Collect</c>'s table as texts, what is shown and a list's
/// rows; a click handed to the page; a file that fails being no page; and the reads the engine gives a page.
/// </summary>
public sealed class LuaWindowsTests : IDisposable
{
	private readonly string _root = Path.Combine(Path.GetTempPath(), "ohoi-luawindows-" + Guid.NewGuid().ToString("N"));
	private readonly List<string> _reports = new();

	public LuaWindowsTests()
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
		var germany = new CCountry { Tag = new CCountryTag("GER"), Name = "Germany", DefinitionFile = "" };
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
		Page("second", "return { window = 'openhoi_b', title = 'TITLE_B', order = 20 }");
		Page("first", "return { window = 'openhoi_a', title = 'TITLE_A', order = 10 }");
		Page("also_second", "return { window = 'openhoi_c', order = 20 }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			Assert.Equal(new[] { "first", "also_second", "second" }, windows.Pages.Select(page => page.Name));
			Assert.Equal(new CModWindowPage("first", "openhoi_a", "TITLE_A", 10), windows.Pages[0]);
			Assert.Equal("", windows.Pages[1].Title);
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void AFileThatFailsOrNamesNoWindowIsNoPageAndIsReported()
	{
		Page("broken", "return {{{");
		Page("nameless", "return { title = 'X' }");
		Page("nothing", "local x = 1");
		Page("good", "return { window = 'openhoi_a' }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			Assert.Equal(new[] { "good" }, windows.Pages.Select(page => page.Name));
			Assert.Equal(3, _reports.Count);
			Assert.Contains(_reports, report => report.Contains("broken.lua"));
			Assert.Contains(_reports, report => report.Contains("nameless") && report.Contains("names no window"));
			Assert.Contains(_reports, report => report.Contains("nothing") && report.Contains("does not return a table"));
		}
	}

	[Fact]
	public void ACollectsTableIsTextsWhatIsShownAndAListsRows()
	{
		Page("flags", "return { window = 'openhoi_flags', Collect = function(player) return { "
			+ "tag_value = player, count_value = 3, warning_label = false, "
			+ "flags_list = { { name_value = 'a' }, { name_value = 'b', marker = true } }, "
			+ "empty_list = {} } end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			CModWindowData? data = windows.Collect(State(), windows.Pages[0], new CCountryTag("GER"));
			Assert.NotNull(data);
			Assert.Equal("GER", data!.Window.Texts["tag_value"]);
			Assert.Equal("3", data.Window.Texts["count_value"]);
			Assert.False(data.Window.Shown["warning_label"]);
			List<CModWindowRow> rows = data.Lists["flags_list"];
			Assert.Equal(2, rows.Count);
			Assert.Equal("a", rows[0].Texts["name_value"]);
			Assert.Equal("b", rows[1].Texts["name_value"]);
			Assert.True(rows[1].Shown["marker"]);
			// A list given empty is there, with no row: the screen empties it.
			Assert.Empty(data.Lists["empty_list"]);
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void APageReadsFlagsAndTextsThroughTheEngine()
	{
		Page("flags", "return { window = 'openhoi_flags', Collect = function(player) "
			+ "local rows = {} for _, name in ipairs(OpenHOI3.CountryFlags(player)) do rows[#rows + 1] = { name_value = name } end "
			+ "return { title = OpenHOI3.Localise('SOME_KEY'), missing = OpenHOI3.Localise('NO_KEY'), "
			+ "world = table.concat(OpenHOI3.GlobalFlags(), ','), flags_list = rows } end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			CModWindowData data = windows.Collect(State(), windows.Pages[0], new CCountryTag("GER"))!;
			// Sorted by name without case, a cleared flag left out.
			Assert.Equal(new[] { "Alpha_flag", "zeta_flag" }, data.Lists["flags_list"].Select(row => row.Texts["name_value"]));
			Assert.Equal("Some text", data.Window.Texts["title"]);
			Assert.Equal("NO_KEY", data.Window.Texts["missing"]);
			Assert.Equal("world_flag", data.Window.Texts["world"]);
			Assert.Empty(_reports);
		}
	}

	[Fact]
	public void AClickIsHandedToThePageAndAFailingCollectIsReportedAndAnswersNothing()
	{
		Page("clicks", "local last = 'none' return { window = 'openhoi_clicks', "
			+ "OnClick = function(element, row, player) last = element .. ':' .. row .. ':' .. player end, "
			+ "Collect = function(player) if last == 'boom:-1:GER' then error('it broke') end return { last_value = last } end }");
		(CAiScripts scripts, CLuaWindows windows) = Load();
		using (scripts)
		{
			CGameState state = State();
			var ger = new CCountryTag("GER");
			CModWindowPage page = windows.Pages[0];
			Assert.Equal("none", windows.Collect(state, page, ger)!.Window.Texts["last_value"]);
			windows.Click(state, page, ger, "refresh_button", 2);
			Assert.Equal("refresh_button:2:GER", windows.Collect(state, page, ger)!.Window.Texts["last_value"]);
			Assert.Empty(_reports);

			windows.Click(state, page, ger, "boom", -1);
			Assert.Null(windows.Collect(state, page, ger));
			Assert.Single(_reports);
			Assert.Contains("it broke", _reports[0]);
		}
	}
}
