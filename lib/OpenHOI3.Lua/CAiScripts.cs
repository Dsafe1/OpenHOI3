using System;
using System.IO;
using System.Text;
using OpenHOI3.Content;
using OpenHOI3.Lua.Api;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua;

/// <summary>
/// The install's Lua, as the original runs it: one state, every standard library, the game's classes,
/// a searcher over the install's layers ahead of Lua's own, and <c>script/autoexec.lua</c> run once.
/// What the simulation asks of it, it answers here. See <c>docs/mechanics/lua.md</c>.
/// </summary>
public sealed class CAiScripts : IAiScripts, IDisposable
{
	private const string Autoexec = "script/autoexec.lua";

	/// <summary>Where a <c>require</c>d module is looked for, in order, each over every layer.</summary>
	private static readonly string[] ModuleFolders = { "script/", "script/country/", "common/" };

	/// <summary>What the relation is where the call fails: the original sets it before calling.</summary>
	private const int RelationOnFailure = 25;

	private readonly CDirectorySettings _directories;
	private readonly Action<string> _report;
	private readonly CLuaState _lua = new();
	private readonly CGameApi _api;
	private CGameState? _state;
	private int _failures;

	public CAiScripts(CDirectorySettings directories, Action<string>? report = null)
	{
		_directories = directories;
		_report = report ?? CDiagnostics.Warn;
		_api = new CGameApi(() => _state ?? throw new CLuaError("no game is being asked"));
		_lua.OpenLibraries("base", "package", "table", "io", "os", "string", "math", "debug");
		foreach (CLuaClass luaClass in _api.Classes)
		{
			_lua.DefineClass(luaClass);
		}
		InstallSearcher();
		RunFile(Autoexec);
	}

	/// <summary>The state, for a check or a test that asks it something directly.</summary>
	public CLuaState Lua => _lua;

	/// <summary>How many calls failed and were reported.</summary>
	public int Failures => _failures;

	public int? NonHistoricalRelation(CGameState state, CCountry country, CCountry other)
	{
		_state = state;
		try
		{
			object? value = _lua.Call("AI_GenerateNonHistoricalRelation", 1, _api.Of(country), _api.Tag(other.Tag))[0];
			if (value is double number)
			{
				return ToInteger(number);
			}
			Fail($"AI_GenerateNonHistoricalRelation({country.Tag}, {other.Tag}) answered {value ?? "nil"}, not a number");
		}
		catch (CLuaException e)
		{
			Fail(e.Message);
		}
		return RelationOnFailure;
	}

	public void Dispose()
	{
		_lua.Dispose();
	}

	/// <summary>
	/// <c>lua_tointeger</c> as the original's build has it: to the nearest, ties to even; out of range
	/// the x87's "indefinite", the lowest int.
	/// </summary>
	public static int ToInteger(double number)
	{
		double rounded = Math.Round(number, MidpointRounding.ToEven);
		return double.IsNaN(rounded) || rounded < int.MinValue || rounded > int.MaxValue ? int.MinValue : (int)rounded;
	}

	private void Fail(string message)
	{
		_failures++;
		_report("LUA Error: " + message);
	}

	/// <summary>Runs a file of the install's, reporting it where it fails, as the original logs it.</summary>
	private void RunFile(string path)
	{
		if (Read(path) is not { } chunk)
		{
			Fail($"{path} is in no layer of the install");
			return;
		}
		try
		{
			_lua.Run(chunk, path);
		}
		catch (CLuaException e)
		{
			Fail(e.Message);
		}
	}

	/// <summary>
	/// The original's searcher, put at <c>package.loaders[2]</c>: <c>script/</c>, <c>script/country/</c>
	/// and <c>common/</c>, each over every layer, the chunk named by its bare path.
	/// </summary>
	private void InstallSearcher()
	{
		_lua.SetGlobal("__ohoi_searcher", (CLuaFunction)Search);
		_lua.Run(Encoding.Latin1.GetBytes("table.insert(package.loaders, 2, __ohoi_searcher) __ohoi_searcher = nil"),
			"=OpenHOI3");
	}

	private int Search(CLuaCall call)
	{
		string name = call.String(1);
		foreach (string folder in ModuleFolders)
		{
			string path = folder + name + ".lua";
			if (Read(path) is { } chunk)
			{
				// A chunk that does not parse is the error require raises.
				if (!call.Load(chunk, path))
				{
					return -1;
				}
				return 1;
			}
		}
		return call.Return($"\n\tno file in the install's {string.Join(", ", ModuleFolders)} for '{name}'");
	}

	private byte[]? Read(string gameRelative)
	{
		string path = _directories.Resolve(gameRelative);
		return File.Exists(path) ? File.ReadAllBytes(path) : null;
	}
}
