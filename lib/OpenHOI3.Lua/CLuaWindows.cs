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
/// added by adding its file, and nothing lists the pages in one place a submod would have to overwrite. The folder's
/// subfolders are walked too, for a mod to keep its pages in order by: a page's name is its path below the folder,
/// and a folder says nothing of the tab a page stands under.
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
		"local conversion, setconversion = __ohoi_w_conversion, __ohoi_w_setconversion " +
		"__ohoi_w_localise, __ohoi_w_countryflags, __ohoi_w_globalflags = nil, nil, nil " +
		"__ohoi_w_conversion, __ohoi_w_setconversion = nil, nil " +
		// A country's conversions as a page reads them, and the order that sets one (ic.md, section 4.1).
		"OpenHOI3.Conversion = function(tag) " +
		"local played, rate, ecap, efloor, ccap, energy, crude, fuel, eo, ep, ek, et, co, cp, ck, ct = conversion(tostring(tag)) " +
		"if played == nil then return nil end " +
		"return { played = played, oil_rate = rate, energy_cap = ecap, energy_floor = efloor, crude_cap = ccap, " +
		"energy = energy, crude_oil = crude, fuel = fuel, " +
		"energy_conversion = { own = eo, per_day = ep, keep = ek, target = et }, " +
		"crude_oil_conversion = { own = co, per_day = cp, keep = ck, target = ct } } end " +
		"OpenHOI3.SetConversion = function(tag, which, own, per_day, keep, target) " +
		"return setconversion(tostring(tag), tostring(which), own and true or false, " +
		"tonumber(per_day) or 0, tonumber(keep) or 0, tonumber(target) or 0) end " +
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
		lua.SetGlobal("__ohoi_w_conversion", (CLuaFunction)Conversion);
		lua.SetGlobal("__ohoi_w_setconversion", (CLuaFunction)SetConversion);
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
		foreach (CDirectorySettings.LayeredFile file in directories.ListFilesRecursive(Folder, "*.lua"))
		{
			Register(file.RelativePath, file.AbsolutePath);
		}
		_pages.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Name, b.Name));
	}

	public IReadOnlyList<CModPage> Pages => _pages;

	/// <summary>
	/// A page's file run and its table kept: the file is the body of a function whose answer is registered under the
	/// file's name, so that what it declares local stays its own. A file that fails is reported and is no page.
	/// </summary>
	/// <param name="relative">The file's path below the pages' folder, written with '/'.</param>
	private void Register(string relative, string file)
	{
		// A page's name is its path below the folder without the ending: "country/conversion".
		string name = relative[..^Path.GetExtension(relative).Length];
		// The name stands in the chunk as a Lua string: only what a string holds as it is.
		if (name.Any(c => !(char.IsLetterOrDigit(c) || c is '_' or '-' or '/') || c > 127))
		{
			_report($"{file}: a page's file and its folders may be named with letters, digits, - and _ only; it is not loaded");
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
		if (!_scripts.RunChunk(head.Concat(body).Concat(tail).ToArray(), Folder + "/" + relative))
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

	/// <summary>The orders the pages have given since they were last taken: the screen posts them to the game.</summary>
	private readonly List<Sim.Commands.CCommand> _posted = new();

	public IReadOnlyList<Sim.Commands.CCommand> TakePosted()
	{
		if (_posted.Count == 0)
		{
			return Array.Empty<Sim.Commands.CCommand>();
		}
		Sim.Commands.CCommand[] taken = _posted.ToArray();
		_posted.Clear();
		return taken;
	}

	/// <summary>
	/// A country's two conversions as sixteen figures, which the Lua's side makes a table of: whether a person plays it,
	/// the crude oil one energy makes, the most energy a day, the energy kept for industry, the most crude oil a day,
	/// the energy, crude oil and fuel it holds, and each setting's four. Nothing where no game is asked or the tag is
	/// no country's.
	/// </summary>
	private int Conversion(CLuaCall call)
	{
		string tag = call.String(1);
		if (_state is not { } state
			|| state.Countries.FirstOrDefault(c => string.Equals(c.Tag.Value, tag, StringComparison.OrdinalIgnoreCase)) is not { } country)
		{
			return 0;
		}
		Sim.Economy.CGoodsPool? pool = state.Stockpile(country);
		double Held(Sim.Economy.EGoods goods) => pool is null ? 0.0 : Number(pool[goods]);
		Sim.Economy.CConversionSetting energy = country.EnergyConversion;
		Sim.Economy.CConversionSetting crude = country.CrudeOilConversion;
		return call.ReturnMany(
			state.Played.Contains(country.Tag),
			Number(Sim.Economy.CConversions.OilRate(country)),
			Number(Sim.Economy.CConversions.EnergyCapacity(country)),
			Number(Sim.Economy.CConversions.EnergyFloor(country)),
			Number(Sim.Economy.CConversions.CrudeOilCapacity(country)),
			Held(Sim.Economy.EGoods.Energy), Held(Sim.Economy.EGoods.CrudeOil), Held(Sim.Economy.EGoods.Fuel),
			energy.Own, Number(energy.PerDay), Number(energy.Keep), Number(energy.Target),
			crude.Own, Number(crude.PerDay), Number(crude.Keep), Number(crude.Target));
	}

	/// <summary>
	/// An order to set one of a country's conversions, kept for the screen to post: <c>energy</c> or <c>crude_oil</c>,
	/// whether the country sets it itself, and the three figures in a stockpile's units. It asks no game, so a page may
	/// give it as it draws - on the press of a button. Answers whether the order was understood.
	/// </summary>
	private int SetConversion(CLuaCall call)
	{
		Sim.Economy.EConversion? which = call.String(2) switch
		{
			"energy" => Sim.Economy.EConversion.EnergyIntoCrudeOil,
			"crude_oil" => Sim.Economy.EConversion.CrudeOilIntoFuel,
			_ => null,
		};
		string tag = call.String(1);
		if (which is not { } conversion || tag.Length != 3)
		{
			return call.Return(false);
		}
		_posted.Add(new Sim.Commands.CSetConversionCommand(new CCountryTag(tag.ToUpperInvariant()), conversion, call.Boolean(3),
			Fixed(call.Number(4)), Fixed(call.Number(5)), Fixed(call.Number(6))));
		return call.Return(true);
	}

	private static double Number(CFixedPoint value) => value.Raw / (double)CFixedPoint.Scale;

	/// <summary>A figure of the Lua's as thousandths, to the nearest; one below nought is nought.</summary>
	private static CFixedPoint Fixed(double value)
	{
		return double.IsFinite(value) && value > 0 ? new CFixedPoint((long)Math.Round(Math.Min(value, 1e12) * CFixedPoint.Scale)) : CFixedPoint.Zero;
	}

	private static string Joined(IEnumerable<string> names)
	{
		return string.Join("\n", names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
	}
}
