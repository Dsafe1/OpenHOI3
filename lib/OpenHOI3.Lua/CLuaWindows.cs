using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenHOI3.Content;
using OpenHOI3.Content.Windows;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua;

/// <summary>
/// The windows a mod defines for an enhanced game, as its Lua gives them (<c>docs/mechanics/modwindows.md</c>). Every
/// <c>.lua</c> file of <c>script/openhoi/windows</c>, over all the layers, is a page: the file returns a table naming
/// the window of <c>interface/openhoi</c> it fills (<c>window</c>), the key of its name on the menu (<c>title</c>),
/// its place there (<c>order</c>), and two functions - <c>Collect(player)</c>, which answers a table by element name,
/// and <c>OnClick(element, row, player)</c>. So a page is added by adding its files, and nothing lists them in one
/// place a submod would have to overwrite.
///
/// A <c>Collect</c>'s table, each entry by the name of an element of the window: a string or a number is its text; a
/// boolean, whether it is shown; a list of tables, the rows of the list box of that name, each row a table of the same
/// kind for the elements of the list's entry window.
///
/// The pages run in a Lua state the screens have for themselves, never the one the AI's ministers run in, and only
/// read: what a page may change, it changes by posting a command.
/// </summary>
public sealed class CLuaWindows : IModWindows
{
	/// <summary>The folder a mod keeps its pages in, a file a page.</summary>
	public const string Folder = "script/openhoi/windows";

	/// <summary>
	/// The Lua the pages are kept and called through, and what the engine gives them under <c>OpenHOI3</c>:
	/// <c>Localise(key)</c>, <c>CountryFlags(tag)</c> and <c>GlobalFlags()</c>, each list sorted by name.
	/// </summary>
	private const string Glue =
		"local w_text, w_shown, w_list, w_row, w_end = __ohoi_w_text, __ohoi_w_shown, __ohoi_w_list, __ohoi_w_row, __ohoi_w_end " +
		"local localise, countryflags, globalflags = __ohoi_w_localise, __ohoi_w_countryflags, __ohoi_w_globalflags " +
		"__ohoi_w_text, __ohoi_w_shown, __ohoi_w_list, __ohoi_w_row, __ohoi_w_end = nil, nil, nil, nil, nil " +
		"__ohoi_w_localise, __ohoi_w_countryflags, __ohoi_w_globalflags = nil, nil, nil " +
		"local pages = {} " +
		"local function lines(joined) local list = {} " +
		"for line in string.gmatch(joined or '', '[^\\n]+') do list[#list + 1] = line end return list end " +
		"OpenHOI3.Localise = function(key) return localise(tostring(key)) end " +
		"OpenHOI3.CountryFlags = function(tag) return lines(countryflags(tostring(tag))) end " +
		"OpenHOI3.GlobalFlags = function() return lines(globalflags()) end " +
		"local function apply(map) for name, value in pairs(map) do local kind = type(value) " +
		"if kind == 'string' or kind == 'number' then w_text(tostring(name), tostring(value)) " +
		"elseif kind == 'boolean' then w_shown(tostring(name), value) end end end " +
		"function __ohoi_window_register(name, page) " +
		"if type(page) ~= 'table' then error('the page ' .. name .. ' does not return a table', 0) end " +
		"if type(page.window) ~= 'string' then error('the page ' .. name .. ' names no window', 0) end " +
		"pages[name] = page return page.window, tostring(page.title or ''), tonumber(page.order) or 0 end " +
		"function __ohoi_window_collect(name, player) local page = pages[name] " +
		"if type(page.Collect) ~= 'function' then return end " +
		"local data = page.Collect(player) if type(data) ~= 'table' then return end " +
		"apply(data) " +
		"for list, rows in pairs(data) do if type(rows) == 'table' then w_list(tostring(list)) " +
		"for i = 1, #rows do w_row() if type(rows[i]) == 'table' then apply(rows[i]) end w_end() end end end end " +
		"function __ohoi_window_click(name, player, element, row) local page = pages[name] " +
		"if type(page.OnClick) == 'function' then page.OnClick(element, row, player) end end";

	private readonly CAiScripts _scripts;
	private readonly Func<string, string> _localise;
	private readonly Action<string> _report;
	private readonly List<CModWindowPage> _pages = new();

	/// <summary>The game being asked, while a page runs.</summary>
	private CGameState? _state;

	/// <summary>What the page running is filling: its data, the list being filled and the row.</summary>
	private CModWindowData? _data;
	private List<CModWindowRow>? _list;
	private CModWindowRow? _row;

	/// <param name="scripts">The Lua state the screens have for themselves, the install's scripts loaded.</param>
	/// <param name="localise">The localisation, which a page asks through <c>OpenHOI3.Localise</c>.</param>
	public CLuaWindows(CAiScripts scripts, CDirectorySettings directories, Func<string, string> localise, Action<string>? report = null)
	{
		_scripts = scripts;
		_localise = localise;
		_report = report ?? CDiagnostics.Warn;
		CLuaState lua = scripts.Lua;
		lua.SetGlobal("__ohoi_w_text", (CLuaFunction)Text);
		lua.SetGlobal("__ohoi_w_shown", (CLuaFunction)Shown);
		lua.SetGlobal("__ohoi_w_list", (CLuaFunction)List);
		lua.SetGlobal("__ohoi_w_row", (CLuaFunction)Row);
		lua.SetGlobal("__ohoi_w_end", (CLuaFunction)End);
		lua.SetGlobal("__ohoi_w_localise", (CLuaFunction)(call => call.Return(_localise(call.String(1)))));
		lua.SetGlobal("__ohoi_w_countryflags", (CLuaFunction)CountryFlags);
		lua.SetGlobal("__ohoi_w_globalflags", (CLuaFunction)GlobalFlags);
		if (!scripts.RunChunk(Encoding.Latin1.GetBytes(Glue), "=OpenHOI3"))
		{
			return;
		}
		foreach (string file in directories.ListFiles(Folder, "*.lua"))
		{
			Register(file);
		}
		_pages.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Name, b.Name));
	}

	public IReadOnlyList<CModWindowPage> Pages => _pages;

	/// <summary>
	/// A page's file run and its table kept: the file is the body of a function whose answer is registered under the
	/// file's name, so that what it declares local stays its own. A file that fails is reported and is no page.
	/// </summary>
	private void Register(string file)
	{
		string name = Path.GetFileNameWithoutExtension(file);
		// The name stands in the chunk as a Lua string: only what a string holds as it is.
		if (name.Any(c => !(char.IsLetterOrDigit(c) || c is '_' or '-') || c > 127))
		{
			_report($"{file}: a page's file name may hold letters, digits, - and _ only; it is not loaded");
			return;
		}
		byte[] body;
		try
		{
			body = File.ReadAllBytes(file);
		}
		catch (IOException e)
		{
			_report($"{file}: could not be read: {e.Message}");
			return;
		}
		// On the file's own first line, so that a Lua error names the file's line.
		byte[] head = Encoding.Latin1.GetBytes($"__ohoi_w_page = (function() ");
		byte[] tail = Encoding.Latin1.GetBytes("\nend)()");
		if (!_scripts.RunChunk(head.Concat(body).Concat(tail).ToArray(), Folder + "/" + Path.GetFileName(file)))
		{
			return;
		}
		object?[]? answer = null;
		if (_scripts.RunChunk(Encoding.Latin1.GetBytes(
			$"function __ohoi_w_register() local page = __ohoi_w_page __ohoi_w_page = nil return __ohoi_window_register('{name}', page) end"), "=OpenHOI3"))
		{
			// No game is asked while a page is registered: a page that reads one at its top level fails here, and says so.
			answer = CallQuietly("__ohoi_w_register", 3);
		}
		if (answer is [string window, string title, double order])
		{
			_pages.Add(new CModWindowPage(name, window, title, (int)order));
		}
	}

	private object?[]? CallQuietly(string function, int results)
	{
		try
		{
			return _scripts.Lua.Call(function, results);
		}
		catch (CLuaException e)
		{
			_report("LUA Error: " + e.Message);
			return null;
		}
	}

	public CModWindowData? Collect(CGameState state, CModWindowPage page, CCountryTag player)
	{
		_state = state;
		_data = new CModWindowData();
		_list = null;
		_row = null;
		try
		{
			return _scripts.CallInGame(state, "__ohoi_window_collect", 0, page.Name, player.Value) is null ? null : _data;
		}
		finally
		{
			_state = null;
			_data = null;
			_list = null;
			_row = null;
		}
	}

	public void Click(CGameState state, CModWindowPage page, CCountryTag player, string element, int row)
	{
		_state = state;
		try
		{
			_scripts.CallInGame(state, "__ohoi_window_click", 0, page.Name, player.Value, element, (double)row);
		}
		finally
		{
			_state = null;
		}
	}

	private CModWindowRow? Target => _row ?? _data?.Window;

	private int Text(CLuaCall call)
	{
		if (Target is { } target)
		{
			target.Texts[call.String(1)] = call.String(2);
		}
		return 0;
	}

	private int Shown(CLuaCall call)
	{
		if (Target is { } target)
		{
			target.Shown[call.String(1)] = call.Boolean(2);
		}
		return 0;
	}

	/// <summary>A list box named: its rows follow, none where the page gave an empty list.</summary>
	private int List(CLuaCall call)
	{
		if (_data is { } data)
		{
			_list = new List<CModWindowRow>();
			data.Lists[call.String(1)] = _list;
		}
		return 0;
	}

	private int Row(CLuaCall call)
	{
		if (_list is { } list)
		{
			_row = new CModWindowRow();
			list.Add(_row);
		}
		return 0;
	}

	private int End(CLuaCall call)
	{
		_row = null;
		return 0;
	}

	/// <summary>The flags a country holds, sorted by name and joined by line breaks; empty for a tag that is no country's.</summary>
	private int CountryFlags(CLuaCall call)
	{
		string tag = call.String(1);
		CCountry? country = _state?.Countries.FirstOrDefault(c => string.Equals(c.Tag.Value, tag, StringComparison.OrdinalIgnoreCase));
		return call.Return(country is null ? "" : Joined(country.Flags.All.Where(flag => flag.Value).Select(flag => flag.Key)));
	}

	private int GlobalFlags(CLuaCall call)
	{
		return call.Return(_state is null ? "" : Joined(_state.Flags.All.Where(flag => flag.Value).Select(flag => flag.Key)));
	}

	private static string Joined(IEnumerable<string> names)
	{
		return string.Join("\n", names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
	}
}
