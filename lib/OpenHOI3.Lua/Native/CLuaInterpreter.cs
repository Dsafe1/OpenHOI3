using System;

namespace OpenHOI3.Lua.Native;

/// <summary>
/// The switch between the two interpreters the install's Lua may run on, for the whole process: LuaJIT by default, and
/// the original's Lua 5.1.4 where <c>OPENHOI3_LUA=5.1.4</c> is in the environment, or where the client's <c>--lua=5.1.4</c>
/// or the savegame tool's <c>lua=5.1.4</c> chooses it. The two answer every script alike but for the order <c>pairs</c>
/// and <c>next</c> walk a table in, which is the original's under 5.1.4 alone - so a script that keeps the first of equal
/// choices may choose another under LuaJIT (docs/mechanics/lua.md, section 1; docs/compatibility.md, entry 30). It is
/// chosen once, before the first call into Lua: one process loads one interpreter.
/// </summary>
public static class CLuaInterpreter
{
	/// <summary>The environment variable read when nothing else has chosen.</summary>
	public const string Variable = "OPENHOI3_LUA";

	private static ELuaInterpreter? _chosen;

	private static bool _loaded;

	/// <summary>The interpreter chosen, or the environment's, or LuaJIT.</summary>
	public static ELuaInterpreter Chosen
	{
		get
		{
			if (_chosen is { } chosen)
			{
				return chosen;
			}
			string? named = Environment.GetEnvironmentVariable(Variable);
			if (named is not { Length: > 0 })
			{
				return ELuaInterpreter.LuaJit;
			}
			if (!TryParse(named, out ELuaInterpreter fromEnvironment))
			{
				throw new ArgumentException($"{Variable}={named} names no interpreter; it may be luajit or 5.1.4.");
			}
			return fromEnvironment;
		}
	}

	/// <summary>
	/// Chooses the interpreter by its name, <c>luajit</c> or <c>5.1.4</c>; answers false for another name. Chosen after
	/// the interpreter has been loaded, it throws.
	/// </summary>
	public static bool Choose(string name)
	{
		if (!TryParse(name, out ELuaInterpreter interpreter))
		{
			return false;
		}
		if (_loaded && interpreter != Chosen)
		{
			throw new InvalidOperationException($"The {Chosen} interpreter is already loaded; {interpreter} cannot be chosen now.");
		}
		_chosen = interpreter;
		return true;
	}

	/// <summary>A name as <see cref="Choose"/> takes it.</summary>
	public static bool TryParse(string name, out ELuaInterpreter interpreter)
	{
		switch (name.Trim().ToLowerInvariant())
		{
			case "luajit":
				interpreter = ELuaInterpreter.LuaJit;
				return true;
			case "5.1.4":
			case "lua5.1.4":
			case "514":
				interpreter = ELuaInterpreter.Lua514;
				return true;
			default:
				interpreter = default;
				return false;
		}
	}

	/// <summary>The file the chosen interpreter is built as, without its extension.</summary>
	internal static string FileName => Chosen == ELuaInterpreter.Lua514 ? "openhoi3_lua514" : "openhoi3_lua";

	/// <summary>What the interpreter says it is, for a log line.</summary>
	public static string Description => Chosen == ELuaInterpreter.Lua514 ? "Lua 5.1.4" : "LuaJIT 2.1 (interpreter)";

	/// <summary>Told by <see cref="CLuaLibrary"/> as it loads the file, after which nothing else may be chosen.</summary>
	internal static void MarkLoaded()
	{
		_chosen = Chosen;
		_loaded = true;
	}
}
