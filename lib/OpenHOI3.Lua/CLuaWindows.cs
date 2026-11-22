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
/// <c>.lua</c> file of <c>script/openhoi/windows</c>, over all the layers, is a page: the file returns a table with
/// the key of its name (<c>title</c>), its place on the menu (<c>order</c>), and two functions -
/// <c>Collect(player)</c>, which reads the game and answers whatever the page will show, and
/// <c>Draw(data, player)</c>, which draws that with <c>OpenHOI3.ImGui</c> (<see cref="CLuaImGui"/>). So a page is
/// added by adding its file, and nothing lists the pages in one place a submod would have to overwrite.
///
/// The two are apart because a page is drawn every frame, the frames in which the game's hours run beside it among
/// them: <c>Draw</c> is given no game to ask, and reads only what <c>Collect</c> kept when the game stood still.
/// The pages run in a Lua state the screens have for themselves, never the one the AI's ministers run in.
/// </summary>
public sealed class CLuaWindows : IModPages
{
	/// <summary>The folder a mod keeps its pages in, a file a page.</summary>
	public const string Folder = "script/openhoi/windows";

	/// <summary>
	/// The Lua the pages are kept and called through, and what the engine gives them under <c>OpenHOI3</c> beside the
	/// widgets: <c>Localise(key)</c>, <c>CountryFlags(tag)</c> and <c>GlobalFlags()</c>, each list sorted by name.
	/// </summary>
	private const string Glue =
		"local localise, countryflags, globalflags = __ohoi_w_localise, __ohoi_w_countryflags, __ohoi_w_globalflags " +
		"__ohoi_w_localise, __ohoi_w_countryflags, __ohoi_w_globalflags = nil, nil, nil " +
		"local pages, data = {}, {} " +
		"local function lines(joined) local list = {} " +
		"for line in string.gmatch(joined or '', '[^\\n]+') do list[#list + 1] = line end return list end " +
		"OpenHOI3.Localise = function(key) return localise(tostring(key)) end " +
		"OpenHOI3.CountryFlags = function(tag) return lines(countryflags(tostring(tag))) end " +
		"OpenHOI3.GlobalFlags = function() return lines(globalflags()) end " +
		"function __ohoi_window_register(name, page) " +
		"if type(page) ~= 'table' then error('the page ' .. name .. ' does not return a table', 0) end " +
		"if type(page.Draw) ~= 'function' then error('the page ' .. name .. ' has no Draw function', 0) end " +
		"pages[name] = page return tostring(page.title or ''), tonumber(page.order) or 0, tostring(page.group or ''), tonumber(page.group_order) or 0 end " +
		"function __ohoi_window_collect(name, player) local page = pages[name] " +
		"if type(page.Collect) == 'function' then data[name] = page.Collect(player) end end " +
		"function __ohoi_window_draw(name, player) pages[name].Draw(data[name], player) end";

	private readonly CAiScripts _scripts;
	private readonly Func<string, string> _localise;
	private readonly Action<string> _report;
	private readonly CLuaImGui _widgets = new();
	private readonly List<CModPage> _pages = new();

	/// <summary>Why each page's drawing failed, shown in its window until the page is gathered again.</summary>
	private readonly Dictionary<string, string> _failed = new(StringComparer.Ordinal);

	/// <summary>The game being asked, while a page gathers.</summary>
	private CGameState? _state;

	/// <param name="scripts">The Lua state the screens have for themselves, the install's scripts loaded.</param>
	/// <param name="localise">The localisation, which a page asks through <c>OpenHOI3.Localise</c>.</param>
	public CLuaWindows(CAiScripts scripts, CDirectorySettings directories, Func<string, string> localise, Action<string>? report = null)
	{
		_scripts = scripts;
		_localise = localise;
		_report = report ?? CDiagnostics.Warn;
		CLuaState lua = scripts.Lua;
		lua.SetGlobal("__ohoi_w_localise", (CLuaFunction)(call => call.Return(_localise(call.String(1)))));
		lua.SetGlobal("__ohoi_w_countryflags", (CLuaFunction)CountryFlags);
		lua.SetGlobal("__ohoi_w_globalflags", (CLuaFunction)GlobalFlags);
		if (!scripts.RunChunk(Encoding.Latin1.GetBytes(Glue), "=OpenHOI3"))
		{
			return;
		}
		try
		{
			_widgets.Install(lua);
		}
		catch (CLuaException e)
		{
			_report("LUA Error: " + e.Message);
			return;
		}
		foreach (string file in directories.ListFiles(Folder, "*.lua"))
		{
			Register(file);
		}
		_pages.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Name, b.Name));
	}

	public IReadOnlyList<CModPage> Pages => _pages;

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
		byte[] head = Encoding.Latin1.GetBytes("__ohoi_w_page = (function() ");
		byte[] tail = Encoding.Latin1.GetBytes("\nend)()");
		if (!_scripts.RunChunk(head.Concat(body).Concat(tail).ToArray(), Folder + "/" + Path.GetFileName(file)))
		{
			return;
		}
		if (!_scripts.RunChunk(Encoding.Latin1.GetBytes(
			$"function __ohoi_w_register() local page = __ohoi_w_page __ohoi_w_page = nil return __ohoi_window_register('{name}', page) end"), "=OpenHOI3"))
		{
			return;
		}
		try
		{
			// No game is asked while a page is registered: a page reads one in its Collect.
			if (_scripts.Lua.Call("__ohoi_w_register", 4) is [string title, double order, string group, double groupOrder])
			{
				_pages.Add(new CModPage(name, title, (int)order, group, (int)groupOrder));
			}
		}
		catch (CLuaException e)
		{
			_report("LUA Error: " + e.Message);
		}
	}

	public void Collect(CGameState state, CModPage page, CCountryTag player)
	{
		_state = state;
		try
		{
			if (_scripts.CallInGame(state, "__ohoi_window_collect", 0, page.Name, player.Value) is not null)
			{
				// Gathered afresh, a page whose drawing failed is tried again.
				_failed.Remove(page.Name);
			}
		}
		finally
		{
			_state = null;
		}
	}

	public void Draw(CModPage page, CCountryTag player, IModWindowCanvas canvas)
	{
		if (_failed.TryGetValue(page.Name, out string? why))
		{
			canvas.TextWrapped(why);
			return;
		}
		_widgets.Canvas = canvas;
		try
		{
			string? error = _scripts.CallOutsideGame("__ohoi_window_draw", page.Name, player.Value);
			// Whatever the page left open is ended for it, a failure half way among it; the library forgives neither.
			_widgets.CloseAll();
			if (error is not null)
			{
				_failed[page.Name] = error;
			}
			else if (_widgets.TakeUnmatched() > 0)
			{
				_failed[page.Name] = $"LUA Error: the page {page.Name} ends a table, a tab or a child it did not begin";
				_report(_failed[page.Name]);
			}
		}
		finally
		{
			_widgets.Canvas = null;
		}
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
