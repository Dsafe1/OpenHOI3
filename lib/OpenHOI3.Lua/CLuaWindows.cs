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
		// A country's variables and the modifiers it was given for a time, a row a line, the row's parts by tabs.
		"local countryvariables, countrymodifiers, refresh = __ohoi_w_countryvariables, __ohoi_w_countrymodifiers, __ohoi_w_refresh " +
		"__ohoi_w_countryvariables, __ohoi_w_countrymodifiers, __ohoi_w_refresh = nil, nil, nil " +
		"OpenHOI3.CountryVariables = function(tag) local list = {} " +
		"for _, line in ipairs(lines(countryvariables(tostring(tag)))) do " +
		"local name, value = string.match(line, '^(.*)\\t(.*)$') list[#list + 1] = { name = name, value = tonumber(value) or 0 } end " +
		"return list end " +
		"OpenHOI3.CountryModifiers = function(tag) local list = {} " +
		"for _, line in ipairs(lines(countrymodifiers(tostring(tag)))) do " +
		"local name, expires, never = string.match(line, '^(.*)\\t(.*)\\t(.*)$') " +
		"list[#list + 1] = { name = name, expires = expires, never = never == '1' } end " +
		"return list end " +
		// A page asks, as it draws, to gather again at once: what it shows hangs on a choice just made.
		"OpenHOI3.Refresh = function() refresh() end " +
		// Where the running game has a leader: nil for none of that number, else whether he is in service and, where
		// he commands a unit, its name and the province it stands in.
		"local leader = __ohoi_w_leader __ohoi_w_leader = nil " +
		"OpenHOI3.LeaderDetails = function(id) local known, active, unit, province = leader(tonumber(id) or -1) " +
		"if not known then return nil end " +
		"return { active = active, unit_name = unit, province_id = province } end " +
		// A province's own figures, in thousandths as the mod's data divides them: nil for no province of that number.
		"local province, oob = __ohoi_w_province, __ohoi_w_oob __ohoi_w_province, __ohoi_w_oob = nil, nil " +
		"OpenHOI3.ProvinceDetails = function(id) " +
		"local known, supply, fuel, oil, metal, energy, rares, manpower, leadership, lic, loil, lenergy, lmetal, lrares, lleadership = province(tonumber(id) or -1) " +
		"if not known then return nil end " +
		"return { id = tonumber(id), supply_pool = supply, fuel_pool = fuel, oil = oil, metal = metal, energy = energy, rares = rares, " +
		"manpower = manpower, leadership = leadership, modifiers = { local_ic = lic, local_oil = loil, local_energy = lenergy, " +
		"local_metal = lmetal, local_rares = lrares, local_leadership = lleadership } } end " +
		// A country's order of battle, every unit with what it holds: a line a unit and a line a regiment, their parts
		// by tabs, made into tables here. A unit's parent is the number of the unit over it, 0 for none.
		"OpenHOI3.OrderOfBattle = function(tag) local units, byId = {}, {} " +
		"for _, line in ipairs(lines(oob(tostring(tag)))) do local p = {} " +
		"for part in string.gmatch(line .. '\\t', '(.-)\\t') do p[#p + 1] = part end " +
		"if p[1] == 'U' then local unit = { id = tonumber(p[2]), parent = tonumber(p[3]), branch = p[4], kind = p[5], name = p[6], " +
		"province = tonumber(p[7]), supply = tonumber(p[11]), fuel = tonumber(p[12]), supply_used = tonumber(p[13]), fuel_used = tonumber(p[14]), " +
		"dug_in = tonumber(p[15]), combined_arms = tonumber(p[16]), cooldown = tonumber(p[17]), orders = p[18], regiments = {} } " +
		"if p[8] ~= '' or p[9] ~= '-1' then unit.leader = { name = p[8], skill = tonumber(p[9]), max_skill = tonumber(p[10]) } end " +
		"units[#units + 1] = unit byId[unit.id] = unit " +
		"elseif p[1] == 'R' and byId[tonumber(p[2])] ~= nil then local regiments = byId[tonumber(p[2])].regiments " +
		"regiments[#regiments + 1] = { name = p[3], type = p[4], strength = tonumber(p[5]), organisation = tonumber(p[6]) } end end " +
		"return units end " +
		// The game's date, and a date as the number of its day and back: years of 365 days, as the game counts.
		"local today, daynumber, dateof = __ohoi_w_today, __ohoi_w_daynumber, __ohoi_w_dateof " +
		"__ohoi_w_today, __ohoi_w_daynumber, __ohoi_w_dateof = nil, nil, nil " +
		"OpenHOI3.Today = function() local year, month, day, hour, number = today() if year == nil then return nil end " +
		"return { year = year, month = month, day = day, hour = hour, day_number = number } end " +
		"OpenHOI3.DayNumber = function(year, month, day) return daynumber(tonumber(year) or 0, tonumber(month) or 0, tonumber(day) or 0) end " +
		"OpenHOI3.DateOf = function(number) return dateof(tonumber(number) or 0) end " +
		// An enhanced game's record of finished battles (combatreports.md): a country's counted over days, by kind,
		// and its last ones.
		"local tally, lastcombats = __ohoi_w_combattally, __ohoi_w_lastcombats __ohoi_w_combattally, __ohoi_w_lastcombats = nil, nil " +
		"OpenHOI3.CombatTally = function(tag, from_day, to_day) local joined, recorded = tally(tostring(tag), tonumber(from_day) or 0, tonumber(to_day) or 0) " +
		"local result = { recorded = recorded or 0 } " +
		"for _, line in ipairs(lines(joined)) do local p = {} for part in string.gmatch(line .. '\\t', '(.-)\\t') do p[#p + 1] = part end " +
		"result[p[1]] = { combats = tonumber(p[2]), won = tonumber(p[3]), lost = tonumber(p[4]), as_attacker = tonumber(p[5]), " +
		"as_defender = tonumber(p[6]), kills = tonumber(p[7]), losses = tonumber(p[8]) } end " +
		"return result end " +
		"OpenHOI3.LastCombats = function(tag, count) local list = {} " +
		"for _, line in ipairs(lines(lastcombats(tostring(tag), tonumber(count) or 16))) do local p = {} " +
		"for part in string.gmatch(line .. '\\t', '(.-)\\t') do p[#p + 1] = part end " +
		"list[#list + 1] = { ended = p[1], kind = p[2], result = p[3], role = p[4], against = p[5], enemy_losses = tonumber(p[6]), " +
		"enemy_men = tonumber(p[7]), own_losses = tonumber(p[8]), own_men = tonumber(p[9]), province = tonumber(p[10]) } end " +
		"return list end " +
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
		lua.SetGlobal("__ohoi_w_countryvariables", (CLuaFunction)CountryVariables);
		lua.SetGlobal("__ohoi_w_countrymodifiers", (CLuaFunction)CountryModifiers);
		lua.SetGlobal("__ohoi_w_refresh", (CLuaFunction)(call => { _refresh = true; return 0; }));
		lua.SetGlobal("__ohoi_w_leader", (CLuaFunction)LeaderDetails);
		lua.SetGlobal("__ohoi_w_province", (CLuaFunction)ProvinceDetails);
		lua.SetGlobal("__ohoi_w_oob", (CLuaFunction)OrderOfBattle);
		lua.SetGlobal("__ohoi_w_today", (CLuaFunction)Today);
		lua.SetGlobal("__ohoi_w_daynumber", (CLuaFunction)DayNumber);
		lua.SetGlobal("__ohoi_w_dateof", (CLuaFunction)DateOf);
		lua.SetGlobal("__ohoi_w_combattally", (CLuaFunction)CombatTally);
		lua.SetGlobal("__ohoi_w_lastcombats", (CLuaFunction)LastCombats);
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
			// What the mod's Lua posts as the page gathers - a data function of the mod's own setting a country's
			// variable with the original's CSetVariableCommand - joins the page's orders, and the screen posts it as
			// a player's order; it does not go into the game's outbox from here.
			if (_scripts.CallInGame(state, _posted, "__ohoi_window_collect", 0, page.Name, player.Value) is not null)
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

	/// <summary>
	/// A country's variables, a line each - the name, a tab, the value - sorted by name; empty for a tag that is no
	/// country's. A variable at nought is among them: what a page leaves out is the page's.
	/// </summary>
	private int CountryVariables(CLuaCall call)
	{
		string tag = call.String(1);
		CCountry? country = _state?.Countries.FirstOrDefault(c => string.Equals(c.Tag.Value, tag, StringComparison.OrdinalIgnoreCase));
		return call.Return(country is null ? "" : Joined(country.Variables.Select(variable =>
			variable.Key + "\t" + Number(variable.Value).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))));
	}

	/// <summary>
	/// The modifiers a country was given for a time, a line each in the order they were given - the name, a tab, the
	/// hour it ends at as <c>year.month.day hour:00</c>, a tab, and 1 where it never ends, which the original keeps as
	/// a date in year one.
	/// </summary>
	private int CountryModifiers(CLuaCall call)
	{
		string tag = call.String(1);
		CCountry? country = _state?.Countries.FirstOrDefault(c => string.Equals(c.Tag.Value, tag, StringComparison.OrdinalIgnoreCase));
		if (country is null)
		{
			return call.Return("");
		}
		return call.Return(string.Join("\n", country.EventModifiers.Select(given =>
		{
			CDate date = CDate.FromDayNumber((int)(given.UntilHour / CGameState.HoursPerDay));
			long hour = given.UntilHour % CGameState.HoursPerDay;
			return $"{given.Modifier.Name}\t{date.Year}.{date.Month}.{date.Day} {hour}:00\t{(date.Year <= 1 ? 1 : 0)}";
		})));
	}

	/// <summary>
	/// A leader by his number: whether there is one, whether he is in service, and where he commands a unit its name
	/// and the province it stands in - neither where he commands none.
	/// </summary>
	private int LeaderDetails(CLuaCall call)
	{
		if (_state is not { } state || !state.Leaders.TryGetValue(call.Integer(1), out Sim.Leaders.CLeader? leader))
		{
			return call.Return(false);
		}
		bool active = leader.Country is { } tag && state.Country(tag) is { } country && country.ActiveLeaders.Contains(leader);
		return leader.Unit is { } unit
			? call.ReturnMany(true, active, unit.Name, (double)unit.Location)
			: call.ReturnMany(true, active);
	}

	/// <summary>
	/// A province's own figures by its number, each in thousandths: what lies there of supplies and of fuel, what it
	/// makes now of crude oil, metal, energy and rare materials, its manpower and leadership, and its own modifier of
	/// six kinds - IC, crude oil, energy, metal, rare materials, leadership. Nothing for no such province. The shape
	/// is the one BiceLib's read has on the original, so that the mod's data asks whichever is there.
	/// </summary>
	private int ProvinceDetails(CLuaCall call)
	{
		int id = call.Integer(1);
		if (_state is not { } state || id < 1 || id >= state.Provinces.Count)
		{
			return call.Return(false);
		}
		Sim.Provinces.CProvince province = state.Province(id);
		double Made(Sim.Economy.EGoods goods) => province.CurrentProducing[goods].Raw;
		double Local(string kind) => province.Modifier[kind].Raw;
		return call.ReturnMany(true,
			(double)province.Pool[Sim.Economy.EGoods.Supplies].Raw, (double)province.Pool[Sim.Economy.EGoods.Fuel].Raw,
			Made(Sim.Economy.EGoods.CrudeOil), Made(Sim.Economy.EGoods.Metal), Made(Sim.Economy.EGoods.Energy),
			Made(Sim.Economy.EGoods.RareMaterials), (double)province.Manpower.Raw, (double)province.Leadership.Raw,
			Local("local_ic"), Local("local_crude_oil"), Local("local_energy"), Local("local_metal"),
			Local("local_rare_materials"), Local("local_leadership"));
	}

	/// <summary>
	/// A country's units, a line each and a line for each regiment under its unit's, the parts by tabs: for a unit
	/// <c>U</c>, its number, its parent's (0 for none), its branch, its kind, its name, its province, its leader's
	/// name, skill and highest skill (-1 for no leader), the supplies and the fuel it gets as a share, what it uses a
	/// day of each, how far it is dug in, its combined arms bonus, the hours it may not attack, and its orders; for
	/// a regiment <c>R</c>, its unit's number, its name, its type, its strength and its organisation.
	/// </summary>
	private int OrderOfBattle(CLuaCall call)
	{
		string tag = call.String(1);
		if (_state is not { } state
			|| state.Countries.FirstOrDefault(c => string.Equals(c.Tag.Value, tag, StringComparison.OrdinalIgnoreCase)) is not { } country)
		{
			return call.Return("");
		}
		var text = new StringBuilder();
		// A name as plain text: the parts' separators taken out, and the colour escapes a name may carry - the
		// section sign and the letter after it - which the game's own text draws as colour and a page would as letters.
		static string Clean(string name)
		{
			var plain = new StringBuilder(name.Length);
			for (int i = 0; i < name.Length; i++)
			{
				char c = name[i];
				if (c == '§')
				{
					i++;
				}
				else
				{
					plain.Append(c is '\t' or '\n' or '\r' ? ' ' : c);
				}
			}
			return plain.ToString();
		}
		static string Figure(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
		// Every unit from the top ones down, each before what stands under it.
		foreach (Sim.Units.CUnit unit in country.AllUnits())
		{
			if (unit.IsRemoved)
			{
				continue;
			}
			(string branch, string kind) = unit.Kind switch
			{
				Sim.Units.EUnitKind.Theatre => ("land", "Theatre"),
				Sim.Units.EUnitKind.ArmyGroup => ("land", "Army group"),
				Sim.Units.EUnitKind.Army => ("land", "Army"),
				Sim.Units.EUnitKind.Corps => ("land", "Corps"),
				Sim.Units.EUnitKind.Division => ("land", "Division"),
				Sim.Units.EUnitKind.Navy => ("naval", "Fleet"),
				_ => ("air", "Air wing"),
			};
			bool holds = unit.SubUnits.Count > 0;
			double combined = unit.Kind == Sim.Units.EUnitKind.Division && holds
				? Number(Sim.Units.CCombinedArms.Bonus(state, country, unit.SubUnits.Select(subUnit => subUnit.Type)))
				: 0.0;
			var orders = new List<string>();
			if (unit.IsPrioritised)
			{
				orders.Add("priority");
			}
			if (unit.MayReinforce)
			{
				orders.Add("reinforcing");
			}
			if (unit.MayUpgrade)
			{
				orders.Add("upgrading");
			}
			text.Append("U\t").Append(unit.Id).Append('\t').Append(unit.Parent is { IsRemoved: false } parent ? parent.Id : 0).Append('\t')
				.Append(branch).Append('\t').Append(kind).Append('\t').Append(Clean(unit.Name)).Append('\t').Append(unit.Location).Append('\t')
				.Append(unit.Leader is { } leader ? Clean(leader.Name) : "").Append('\t')
				.Append(unit.Leader?.Skill ?? -1).Append('\t').Append(unit.Leader?.MaxSkill ?? -1).Append('\t')
				.Append(Figure(Number(unit.SuppliesReceived))).Append('\t').Append(Figure(Number(unit.FuelReceived))).Append('\t')
				.Append(Figure(holds ? Number(Sim.Units.CUnitSupply.SupplyConsumption(state, country, unit)) : 0.0)).Append('\t')
				.Append(Figure(holds ? Number(Sim.Units.CUnitSupply.FuelConsumption(state, country, unit)) : 0.0)).Append('\t')
				.Append(Figure(Number(unit.DigIn))).Append('\t').Append(Figure(combined)).Append('\t')
				.Append(Figure(Number(unit.AttackDelay))).Append('\t').Append(string.Join(' ', orders)).Append('\n');
			foreach (Sim.Units.CSubUnit regiment in unit.SubUnits)
			{
				text.Append("R\t").Append(unit.Id).Append('\t').Append(Clean(regiment.Name)).Append('\t').Append(regiment.Type.Key).Append('\t')
					.Append(Figure(Number(regiment.Strength))).Append('\t').Append(Figure(Number(regiment.Organisation))).Append('\n');
			}
		}
		return call.Return(text.ToString());
	}

	/// <summary>The game's date: year, month, day, hour and the number of its day; nothing where no game is asked.</summary>
	private int Today(CLuaCall call)
	{
		if (_state is not { } state)
		{
			return 0;
		}
		CDate date = state.Date;
		return call.ReturnMany((double)date.Year, (double)date.Month, (double)date.Day, (double)state.Hour, (double)date.DayNumber);
	}

	/// <summary>The number of a date's day, or nothing for what is no date. It asks no game.</summary>
	private static int DayNumber(CLuaCall call)
	{
		var date = new CDate(call.Integer(1), call.Integer(2), call.Integer(3));
		// A month's own length: the thirty-first of a month of thirty is no date.
		return date.IsValid && date.Year > 0 && CDate.FromDayNumber(date.DayNumber) == date ? call.Return((double)date.DayNumber) : 0;
	}

	/// <summary>A day's date by its number: year, month, day. It asks no game.</summary>
	private static int DateOf(CLuaCall call)
	{
		CDate date = CDate.FromDayNumber(Math.Max(0, call.Integer(1)));
		return call.ReturnMany((double)date.Year, (double)date.Month, (double)date.Day);
	}

	/// <summary>The kinds of battle as a page names them, in the order of <see cref="Sim.Combat.ECombatKind"/>.</summary>
	private static readonly string[] CombatKinds = { "land", "naval", "air", "ground_bombing", "land_bombing", "naval_bombing" };

	/// <summary>
	/// A country's finished battles from one day to another, both whole, counted for each kind - a line a kind: its
	/// name, how many, won, lost, as attacker, as defender, what the others lost and what it lost, in thousandths - and
	/// beside them how many battles the record holds in all.
	/// </summary>
	private int CombatTally(CLuaCall call)
	{
		string tag = call.String(1);
		if (_state is not { } state
			|| state.Countries.FirstOrDefault(c => string.Equals(c.Tag.Value, tag, StringComparison.OrdinalIgnoreCase)) is not { } country)
		{
			return call.ReturnMany("", 0.0);
		}
		long from = (long)call.Integer(2) * CGameState.HoursPerDay;
		long to = (long)call.Integer(3) * CGameState.HoursPerDay + CGameState.HoursPerDay - 1;
		var text = new StringBuilder();
		for (int kind = 0; kind < CombatKinds.Length; kind++)
		{
			Sim.Combat.CCombatTally tally = state.CombatReports.Tally(country.Tag, from, to, (Sim.Combat.ECombatKind)kind);
			text.Append(CombatKinds[kind]).Append('\t').Append(tally.Combats).Append('\t').Append(tally.Won).Append('\t').Append(tally.Lost)
				.Append('\t').Append(tally.AsAttacker).Append('\t').Append(tally.AsDefender).Append('\t').Append(tally.Kills)
				.Append('\t').Append(tally.Losses).Append('\n');
		}
		return call.ReturnMany(text.ToString(), (double)state.CombatReports.Entries.Count);
	}

	/// <summary>
	/// A country's last battles, the newest first, a line each: when it ended as <c>year.month.day hour:00</c>, its
	/// kind, <c>won</c>, <c>lost</c> or nothing, <c>attacker</c> or <c>defender</c>, the other side's tag, the other
	/// side's losses and men, its own losses and men, and the province.
	/// </summary>
	private int LastCombats(CLuaCall call)
	{
		string tag = call.String(1);
		if (_state is not { } state
			|| state.Countries.FirstOrDefault(c => string.Equals(c.Tag.Value, tag, StringComparison.OrdinalIgnoreCase)) is not { } country)
		{
			return call.Return("");
		}
		var text = new StringBuilder();
		foreach (Sim.Combat.CCombatReport report in state.CombatReports.Last(country.Tag, Math.Clamp(call.Integer(2), 1, 1000)))
		{
			bool attacked = report.Attacker == country.Tag;
			CDate date = CDate.FromDayNumber((int)(report.Hour / CGameState.HoursPerDay));
			text.Append(date.Year).Append('.').Append(date.Month).Append('.').Append(date.Day).Append(' ')
				.Append(report.Hour % CGameState.HoursPerDay).Append(":00").Append('\t')
				.Append(CombatKinds[(int)report.Kind]).Append('\t')
				.Append(report.Winner == Sim.Combat.ECombatWinner.Nobody ? "" : report.WonBy(country.Tag) ? "won" : "lost").Append('\t')
				.Append(attacked ? "attacker" : "defender").Append('\t')
				.Append((attacked ? report.Defender : report.Attacker)?.Value ?? "---").Append('\t')
				.Append(attacked ? report.DefenderLosses : report.AttackerLosses).Append('\t')
				.Append(attacked ? report.DefenderMen : report.AttackerMen).Append('\t')
				.Append(attacked ? report.AttackerLosses : report.DefenderLosses).Append('\t')
				.Append(attacked ? report.AttackerMen : report.DefenderMen).Append('\t')
				.Append(report.Province).Append('\n');
		}
		return call.Return(text.ToString());
	}

	/// <summary>Whether a page asked, as it drew, to gather again at once.</summary>
	private bool _refresh;

	public bool TakeRefresh()
	{
		bool asked = _refresh;
		_refresh = false;
		return asked;
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
