using System;
using System.Linq;
using System.Text;
using OpenHOI3.Lua.Api;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Mods;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Lua.BiceLib;

/// <summary>
/// <c>OpenHOI3.BiceLib</c>: every function BiceLib, BlackICE's DLL, registers with the original's Lua, given by this
/// engine, which cannot load the DLL - BlackICE's <c>script/bicelib_lua.lua</c> takes it in the DLL's place where the global
/// <c>OpenHOI3</c> exists. A function about the injection or that this engine already does is a plain no-op; one not built
/// does nothing, answers nil, and is reported once by name through the API's report of what is not built; one built sets
/// a switch of the game's - into the scripts' own copy while they load, before any game, which each game made with them
/// takes (<see cref="Switches"/>), and by a command while a game is played. Ours; <c>docs/mechanics/bicelib.md</c>.
/// </summary>
public sealed class CBiceLibApi
{
	private const string NullTag = "---";

	private readonly CGameApi _api;
	private readonly Func<CGameState?> _game;

	/// <param name="api">Where a function not built is reported, and where a command is posted.</param>
	/// <param name="game">The game being asked, or null while the scripts load.</param>
	public CBiceLibApi(CGameApi api, Func<CGameState?> game)
	{
		_api = api;
		_game = game;
	}

	/// <summary>What the scripts turned on as they loaded, before any game was made; each game takes a copy.</summary>
	public CBiceLibSwitches Switches { get; } = new();

	/// <summary>Puts <c>OpenHOI3.BiceLib</c> in the state; <c>OpenHOI3</c> has to be there already (<see cref="CLuaFiles"/>).</summary>
	public void Install(CLuaState lua)
	{
		lua.SetGlobal("__ohoi_bicelib", (CLuaFunction)Dispatch);
		lua.Run(Encoding.Latin1.GetBytes(Maker()), "=OpenHOI3.BiceLib");
	}

	/// <summary>The chunk that makes the table: each function a closure handing its path and its arguments to the dispatcher.</summary>
	private static string Maker()
	{
		var text = new StringBuilder("local call = __ohoi_bicelib __ohoi_bicelib = nil ");
		text.Append("local function fn(path) return function(...) return call(path, ...) end end ");
		text.Append("OpenHOI3.BiceLib = { ");
		foreach (CBiceLibFunction function in CBiceLibFunctions.All.Where(f => !f.Path.Contains('.')))
		{
			text.Append(function.Path).Append(" = fn('").Append(function.Path).Append("'), ");
		}
		foreach (IGrouping<string, CBiceLibFunction> table in CBiceLibFunctions.All.Where(f => f.Path.Contains('.'))
			.GroupBy(f => f.Path[..f.Path.IndexOf('.')]))
		{
			text.Append(table.Key).Append(" = { ");
			foreach (CBiceLibFunction function in table)
			{
				text.Append(function.Path[(function.Path.IndexOf('.') + 1)..]).Append(" = fn('").Append(function.Path).Append("'), ");
			}
			text.Append("}, ");
		}
		text.Append('}');
		return text.ToString();
	}

	private int Dispatch(CLuaCall call)
	{
		string path = call.String(1);
		CBiceLibFunction function = CBiceLibFunctions.Find(path) ?? throw new CLuaError($"BiceLib has no function '{path}'");
		if (CBiceLibSwitches.Simple.Contains(path))
		{
			TurnOn(path);
			return function.Answer is { } turned ? call.Return(turned) : 0;
		}
		switch (path)
		{
			case "Units.setCorpsUnitLimit":
				SetUnitLimit(EUnitKind.Corps, call);
				break;
			case "Units.setArmyUnitLimit":
				SetUnitLimit(EUnitKind.Army, call);
				break;
			case "Units.setArmyGroupUnitLimit":
				SetUnitLimit(EUnitKind.ArmyGroup, call);
				break;
			case "Units.addCommandLimitTrait":
				AddCommandLimitTrait(call.String(2), call.Integer(3));
				break;
			default:
				if (function.Unbuilt)
				{
					_api.ReportUnbuilt(function.Name);
					return 0;
				}
				break;
		}
		return function.Answer is { } answer ? call.Return(answer) : 0;
	}

	/// <summary>A switch on: the scripts' own copy while they load, else the game's by a command.</summary>
	private void TurnOn(string name)
	{
		if (_game() is not { } game)
		{
			Switches.TurnOn(name);
		}
		else if (!game.BiceLib.IsOn(name))
		{
			_api.Post(new CBiceLibSwitchCommand(name));
		}
	}

	/// <summary>
	/// <c>set*UnitLimit(limit, tag)</c>: <c>"---"</c> the default for every country without its own. In a game a limit the
	/// game already holds is not posted again, so the daily look at every country posts only what changes.
	/// </summary>
	private void SetUnitLimit(EUnitKind level, CLuaCall call)
	{
		int limit = call.Integer(2);
		string text = call.String(3);
		CCountryTag? tag = text == NullTag ? null : new CCountryTag(text);
		if (_game() is not { } game)
		{
			Switches.SetUnitLimit(level, tag, limit);
			return;
		}
		CBiceLibSwitches switches = game.BiceLib;
		if (tag is { } country && game.Country(country) is null)
		{
			return;
		}
		bool held = switches.IsOn(CBiceLibSwitches.UnitLimits) && (tag is { } own
			? switches.CountryUnitLimit(level, own) == limit
			: switches.UnitLimit(level, CGameApi.NullTag) == limit);
		if (!held)
		{
			_api.Post(new CSetUnitLimitCommand(level, tag, limit));
		}
	}

	private void AddCommandLimitTrait(string trait, int effect)
	{
		if (_game() is not { } game)
		{
			Switches.AddCommandLimitTrait(trait, effect);
		}
		else
		{
			_api.Post(new CBiceLibSwitchCommand(trait, effect));
		}
	}
}
