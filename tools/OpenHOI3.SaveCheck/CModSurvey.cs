using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Script;
using OpenHOI3.Lua;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Script;

/// <summary>
/// What a mod asks of this game that it does not answer, read and not played:
///
///   OpenHOI3.SaveCheck modsurvey &lt;install&gt; [mod name] [mod=name] [examples=N]
///
/// The mod is laid over the install as the client lays it, and everything a new game reads is read: the localisation, the
/// countries, the map, the interface's <c>.gui</c> and <c>.gfx</c> - each texture and sprite they name looked for - the
/// install's <c>autoexec.lua</c> loaded, the world with every database, the history and the script, and then a new game
/// made as a player gets it, as far as it goes. Every problem the readers report is gathered by kind and by file, with
/// the script's own problems, the condition and effect keywords that are unbuilt, the Lua's errors and what it reported
/// unbuilt, and every global the mod's Lua uses that neither our Lua API nor its own scripts provide. Counts at the top,
/// the largest gaps first, details below. Without a mod it is the base game's control. Nothing in the simulation changes
/// for it; <c>docs/mechanics/mods.md</c>, section 5.
/// </summary>
internal static class CModSurvey
{
	/// <summary>One problem, as a section, a kind, the file it is in where one is named, and the reader's words.</summary>
	private sealed record Finding(string Section, string Kind, string File, string Message);

	/// <summary>A gap: the problems of one shape, with how many there are.</summary>
	private sealed record Gap(string Section, string Kind, string Shape, int Count, List<Finding> Findings);

	private static readonly Regex QuotedFile = new(@"'([^'\r\n]+?\.(?:txt|csv|gui|gfx|lua|mod|bmp|dds|tga|png|fnt|map|fx|inc|ttf|otf))'",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex LeadingFile = new(@"^([^:'\r\n]+?\.(?:txt|csv|gui|gfx|lua|mod)):\d+",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex Quoted = new(@"'[^'\r\n]*'|""[^""\r\n]*""", RegexOptions.Compiled);

	private static readonly Regex Number = new(@"-?\d+(\.\d+)?", RegexOptions.Compiled);

	/// <summary>The globals Lua 5.1 itself defines that a script may use without our having made them.</summary>
	private static readonly string[] LuaStringMethods =
	{
		"byte", "char", "dump", "find", "format", "gmatch", "gsub", "len", "lower", "match", "rep", "reverse", "sub", "upper",
		// a file's
		"close", "flush", "lines", "read", "seek", "setvbuf", "write",
	};

	/// <summary>
	/// The sections that are what the files say - a reader's report, the script's problems, a texture or sprite in no file -
	/// which the original passes over as well; every other section is what this game does not answer.
	/// </summary>
	private static readonly HashSet<string> FilesSections = new(StringComparer.Ordinal)
	{
		"mod file", "localisation", "countries", "map", "interface", "world", "script", "new game", "lua undefined",
	};

	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck modsurvey <install> [mod name] [mod=name] [examples=N]");
			return 2;
		}
		int examples = 3;
		if (args.FirstOrDefault(a => a.StartsWith("examples=", StringComparison.Ordinal)) is { } wanted)
		{
			examples = int.Parse(wanted["examples=".Length..]);
			args = args.Where(a => a != wanted).ToArray();
		}
		var findings = new List<Finding>();
		string step = "mod file";
		void Add(string section, string message, string? kind = null, string? file = null)
		{
			findings.Add(new Finding(section, kind ?? KindOf(message), file ?? "", message));
		}

		// The mod named as the second argument, or as mod=<name>.
		CDirectorySettings? directories = CDirectorySettings.At(args[1]);
		string? modName = args.FirstOrDefault(a => a.StartsWith(CToolDirectories.Switch, StringComparison.Ordinal))?[CToolDirectories.Switch.Length..]
			?? (args.Length > 2 ? args[2] : null);
		if (modName is not null)
		{
			directories = directories.WithMod(modName, message => Add("mod file", message));
			if (directories is null)
			{
				findings.ForEach(f => Console.Error.WriteLine(f.Message));
				return 2;
			}
		}
		CDirectorySettings dirs = directories;
		string Relative(string file)
		{
			if (file.Length == 0)
			{
				return "";
			}
			string full = Path.IsPathRooted(file) ? file : Path.Combine(dirs.GameRoot, file);
			return dirs.Relative(full);
		}
		Action<string> Sink(string section) => message => findings.Add(new Finding(section, KindOf(message), Relative(FileOf(message)), message));
		var previousWarn = CDiagnostics.WarnSink;
		CDiagnostics.WarnSink = message => findings.Add(new Finding(step, KindOf(message), Relative(FileOf(message)), message));

		Console.WriteLine($"modsurvey: {dirs.GameRoot}, {(dirs.Mods.Count > 0 ? "the mods " + string.Join(", ", dirs.Mods.Select(m => $"'{m.Name}' ({m.File})")) : "no mod: the base game's control")}");
		Console.WriteLine($"layers:   {string.Join(", ", dirs.Layers)}");
		Console.WriteLine($"player:   {dirs.UserFolder}; {dirs.ReplacedFolders.Count} replaced folders");
		var timings = new List<string>();
		var clock = Stopwatch.StartNew();
		T? Timed<T>(string name, Func<T> work) where T : class
		{
			step = name;
			clock.Restart();
			try
			{
				T result = work();
				timings.Add($"{name} {clock.ElapsedMilliseconds} ms");
				return result;
			}
			catch (Exception e)
			{
				timings.Add($"{name} threw after {clock.ElapsedMilliseconds} ms");
				Add("threw", $"{name}: {Thrown(e)}", "Threw");
				return null;
			}
		}

		CLocalisation? localisation = Timed("localisation", () => CLocalisation.Load(dirs, warn: Sink("localisation")));
		CCountries? countries = localisation is null ? null : Timed("countries", () => CCountries.Load(dirs, localisation, Sink("countries")));
		CMap? map = Timed("map", () => CMap.Load(dirs, Sink("map")));
		Timed("interface", () =>
		{
			Action<string> sink = Sink("interface");
			OpenHOI3.Content.Gui.CGuiTypes.Load(dirs, sink);
			CCounterTypes.Load(dirs, sink);
			CBillboardTypes.Load(dirs, sink);
			CArrowTypes.Load(dirs, sink);
			CProjectionTypes.Load(dirs, sink);
			CInterfaceColorsReader.Read(dirs, sink);
			OpenHOI3.Content.Messages.CMessageTypes.Load(dirs, null, sink);
			OpenHOI3.Content.BattlePlans.CPlanObjectTypes.Load(dirs, sink);
			OpenHOI3.Content.BattlePlans.CBattlePlanSettings.Load(dirs, sink);
			OpenHOI3.Content.Sound.CSoundEffectReader.Load(dirs, sink);
			CheckSprites(dirs, findings, Relative);
			return "";
		});

		// The install's Lua, autoexec.lua and everything it requires.
		// What the Lua reports - an error, a class's method not built - as it loads and later as the new game asks it.
		CAiScripts? scripts = Timed("lua", () => new CAiScripts(dirs,
			message => findings.Add(new Finding(step == "lua" ? "lua" : "lua, asked in the " + step, KindOf(message), "", message))));
		if (scripts is not null)
		{
			// On a state of its own, so that the modules it goes on to require are not in the one the game is made with.
			Timed("lua globals", () =>
			{
				using var probe = new CAiScripts(dirs, _ => { });
				ScanLua(dirs, probe, findings, Relative);
				return "";
			});
		}

		// The world: every database, the history, the events and decisions; then a new game as a player gets it.
		string? newGame = null;
		if (localisation is not null && countries is not null && map is not null)
		{
			CGameState? world = Timed("world", () => CGameStateLoader.LoadStart(dirs, map, localisation, countries, Sink("world"),
				fill: false, firstDays: false, aiScripts: scripts));
			if (world is not null)
			{
				Timed("script", () =>
				{
					CScriptProblems problems = CScriptLoader.Load(dirs, world, _ => { });
					foreach (CScriptProblem problem in problems.All)
					{
						findings.Add(new Finding("script", problem.Kind.ToString(), problem.File, problem.ToString()));
					}
					WalkKeywords(world, findings);
					return "";
				});
				var report = new CScriptReport();
				CCountries? fresh = Timed("countries again", () => CCountries.Load(dirs, localisation, _ => { }));
				if (fresh is not null)
				{
					var made = Stopwatch.StartNew();
					// What the world's build said already is not said again.
					var said = new HashSet<string>(findings.Where(f => f.Section == "world").Select(f => f.Message), StringComparer.Ordinal);
					Action<string> again = Sink("new game");
					CGameState? game = Timed("new game", () => CGameStateLoader.LoadStart(dirs, map, localisation, fresh,
						message => { if (!said.Contains(message)) { again(message); } },
						aiScripts: scripts, beforeFirstDays: state => state.ScriptReport = report));
					newGame = game is null ? "threw - see 'new game' below" : $"made in {made.ElapsedMilliseconds} ms, {game.Date}.{game.Hour}";
					foreach ((string key, (EScriptGap why, int times)) in report.Gaps)
					{
						for (int t = 0; t < times; t++)
						{
							findings.Add(new Finding("met in the two days", why.ToString(), "", $"'{key}' {why.ToString().ToLowerInvariant()}, met as the new game's two days ran"));
						}
					}
				}
			}
			else
			{
				newGame = "not tried: the world threw";
			}
		}
		else
		{
			newGame = "not tried: the localisation, the countries or the map threw";
		}
		CDiagnostics.WarnSink = previousWarn;
		scripts?.Dispose();

		Print(findings, timings, newGame, examples);
		return 0;
	}

	/// <summary>An exception, and where in the code it was thrown.</summary>
	private static string Thrown(Exception e)
	{
		string where = string.Join(" < ", (e.StackTrace ?? "").Split('\n')
			.Select(line => line.Trim())
			.Where(line => line.StartsWith("at ", StringComparison.Ordinal))
			.Take(6)
			.Select(line => line[3..].Split(" in ")[0]));
		return $"{e.GetType().Name}: {e.Message} - at {where}";
	}

	/// <summary>A reader's message as a kind, by its words.</summary>
	private static string KindOf(string message)
	{
		if (message.StartsWith("LUA Error: ", StringComparison.Ordinal))
		{
			return "LuaError";
		}
		if (message.StartsWith(OpenHOI3.Lua.Api.CGameApi.UnbuiltPrefix, StringComparison.Ordinal))
		{
			return "LuaUnbuilt";
		}
		string lower = message.ToLowerInvariant();
		if (lower.Contains("is not a key") || lower.Contains("unknown key") || lower.Contains("not a key of"))
		{
			return "UnknownKey";
		}
		if (lower.Contains("could not parse") || lower.Contains("could not read"))
		{
			return "Unreadable";
		}
		if (lower.Contains("not built") || lower.Contains("unbuilt") || lower.Contains("not supported"))
		{
			return "Unbuilt";
		}
		if (lower.Contains(" no ") || lower.Contains("not found") || lower.Contains("missing") || lower.Contains("unknown")
			|| lower.Contains("does not exist") || lower.Contains("nothing") || lower.Contains("is no "))
		{
			return "UnknownName";
		}
		return "Other";
	}

	/// <summary>The file a message names, quoted or before its line number, or none.</summary>
	private static string FileOf(string message)
	{
		Match leading = LeadingFile.Match(message);
		if (leading.Success)
		{
			return leading.Groups[1].Value;
		}
		Match quoted = QuotedFile.Match(message);
		return quoted.Success ? quoted.Groups[1].Value : "";
	}

	/// <summary>A message with its names and figures taken out, so that problems of one kind group together.</summary>
	private static string ShapeOf(string message)
	{
		string shape = LeadingFile.Replace(message, "FILE");
		shape = Quoted.Replace(shape, "'*'");
		shape = Number.Replace(shape, "#");
		return shape;
	}

	/// <summary>
	/// Every texture a <c>.gfx</c> file names looked for, and every sprite a <c>.gui</c> file names looked for among the
	/// <c>.gfx</c> files' names.
	/// </summary>
	private static void CheckSprites(CDirectorySettings directories, List<Finding> findings, Func<string, string> relative)
	{
		var sprites = new HashSet<string>(StringComparer.Ordinal);
		foreach (string file in directories.ListFiles("interface", "*.gfx"))
		{
			ClausewitzNode root = ClausewitzParser.ParseFile(file);
			void Walk(ClausewitzNode node, ClausewitzNode? parent)
			{
				if (node.Key is { } key && node.Value is { } value)
				{
					if (key.StartsWith("texturefile", StringComparison.OrdinalIgnoreCase) && value.Length > 0
						&& directories.ResolveTexture(value) is null)
					{
						findings.Add(new Finding("interface", "MissingTexture", relative(file), $"'{relative(file)}': the texture '{value}' is in no layer"));
					}
					if (key.Equals("name", StringComparison.OrdinalIgnoreCase) && parent?.Key is { } type
						&& type.EndsWith("type", StringComparison.OrdinalIgnoreCase))
					{
						sprites.Add(value);
					}
				}
				foreach (ClausewitzNode child in node.Children)
				{
					Walk(child, node);
				}
			}
			Walk(root, null);
		}
		foreach (string file in directories.ListFiles("interface", "*.gui"))
		{
			ClausewitzNode root = ClausewitzParser.ParseFile(file);
			void Walk(ClausewitzNode node)
			{
				if (node.Key is { } key && node.Value is { Length: > 0 } value
					&& (key.Equals("spriteType", StringComparison.OrdinalIgnoreCase) || key.Equals("quadTextureSprite", StringComparison.OrdinalIgnoreCase))
					&& !sprites.Contains(value))
				{
					findings.Add(new Finding("interface", "MissingSprite", relative(file), $"'{relative(file)}': the sprite '{value}' is in no .gfx file"));
				}
				node.Children.ForEach(Walk);
			}
			Walk(root);
		}
	}

	/// <summary>
	/// The Lua: where autoexec.lua stopped, each later module it requires tried on its own; then every Lua file of the
	/// layered <c>script</c> folder and <c>common</c>, and every module required, read for the globals they use - set against
	/// what is in the state once everything has loaded that would, and what any file defines. A module required that is no
	/// Lua file anywhere is a native one, a DLL beside the original (<c>BiceLib</c>, <c>lfs</c>, <c>wx</c>): every field
	/// used under it is listed. And the methods called with ':' that no class of ours has and no file defines.
	/// </summary>
	private static void ScanLua(CDirectorySettings directories, CAiScripts scripts, List<Finding> findings, Func<string, string> relative)
	{
		CLuaState lua = scripts.Lua;
		lua.Run(Encoding.Latin1.GetBytes(
			"function __ohoi_survey() local g, m = {}, {} "
			+ "for k in pairs(_G) do g[#g + 1] = tostring(k) end "
			+ "for k in pairs(package.loaded) do m[#m + 1] = tostring(k) end "
			+ "return table.concat(g, '\\n'), table.concat(m, '\\n') end "
			+ "function __ohoi_require(name) local ok, e = pcall(require, name) if ok then return nil end return tostring(e) end"),
			"=survey");
		(HashSet<string> Globals, HashSet<string> Modules) Read()
		{
			object?[] answer = lua.Call("__ohoi_survey", 2);
			return (new HashSet<string>(((string?)answer[0] ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal),
				new HashSet<string>(((string?)answer[1] ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal));
		}
		(HashSet<string> _, HashSet<string> loadedByAutoexec) = Read();

		// Each module autoexec.lua names that it did not come to, in its order, tried on its own.
		string autoexec = directories.Resolve("script/autoexec.lua");
		CLuaGlobalScan autoexecScan = CLuaGlobalScan.Read(File.ReadAllText(autoexec, Encoding.Latin1));
		// A module that is a Lua file below script/ but in none of the searcher's three folders is found by the original
		// only through a package.path the mod writes relative to the working folder - the install's, where the original runs.
		var belowScript = directories.ListFilesRecursive("script", "*.lua")
			.GroupBy(f => Path.GetFileNameWithoutExtension(f.RelativePath), StringComparer.Ordinal)
			.ToDictionary(g => g.Key, g => g.First().RelativePath, StringComparer.Ordinal);
		int tried = 0;
		foreach (string module in autoexecScan.Requires.Select(r => r.Module).Distinct(StringComparer.Ordinal))
		{
			if (loadedByAutoexec.Contains(module) || Read().Modules.Contains(module))
			{
				continue;
			}
			tried++;
			if (lua.Call("__ohoi_require", 1, module)[0] is string error)
			{
				string missing = Regex.Match(error, "module '([^']+)' not found") is { Success: true } match ? match.Groups[1].Value : "";
				bool fromInstall = missing.Length > 0 && SearcherFile(directories, missing) is null && belowScript.ContainsKey(missing);
				findings.Add(new Finding("lua, each require on its own", fromInstall ? "FoundOnlyFromTheInstallFolder" : "LuaError",
					relative(autoexec), $"require('{module}'): {FirstLine(error)}"
					+ (fromInstall ? $" - it is script/{belowScript[missing]}, on the package.path the mod writes relative to the working folder" : "")));
			}
		}
		(HashSet<string> globals, HashSet<string> modules) = Read();
		lua.SetGlobal("__ohoi_survey", null);
		lua.SetGlobal("__ohoi_require", null);
		findings.Add(new Finding("lua globals", "Read", "", $"autoexec.lua loaded {loadedByAutoexec.Count} modules before it stopped or ended; "
			+ $"{tried} more it names tried on their own, {modules.Count} loaded in all"));

		// The files: autoexec.lua, every module loaded as the original's searcher finds it, and every Lua file there is.
		var files = new List<string> { autoexec };
		foreach (string module in modules.OrderBy(m => m, StringComparer.Ordinal))
		{
			if (SearcherFile(directories, module) is { } path)
			{
				files.Add(path);
			}
		}
		files.AddRange(directories.ListFilesRecursive("script", "*.lua").Select(f => f.AbsolutePath));
		files.AddRange(directories.ListFiles("common", "*.lua"));
		var scans = new List<(string File, CLuaGlobalScan Scan)>();
		var defined = new HashSet<string>(StringComparer.Ordinal);
		var members = new HashSet<string>(LuaStringMethods, StringComparer.Ordinal);
		var luaFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string file in files.Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists))
		{
			CLuaGlobalScan scan = CLuaGlobalScan.Read(File.ReadAllText(file, Encoding.Latin1));
			scans.Add((relative(file), scan));
			defined.UnionWith(scan.Defined);
			members.UnionWith(scan.DefinedMembers);
			luaFileNames.Add(Path.GetFileNameWithoutExtension(file));
		}
		foreach (CLuaClass luaClass in lua.Classes.Values)
		{
			members.UnionWith(luaClass.Methods.Keys);
			members.UnionWith(luaClass.Fields.Keys);
		}
		findings.Add(new Finding("lua globals", "Read", "", $"{scans.Count} Lua files read for their globals"));

		// The native modules: required, never loaded, and no Lua file of the name anywhere.
		var natives = new HashSet<string>(StringComparer.Ordinal);
		foreach ((string file, CLuaGlobalScan scan) in scans)
		{
			foreach ((string module, int line) in scan.Requires)
			{
				if (!modules.Contains(module) && !luaFileNames.Contains(module.Replace('.', '/').Split('/')[^1]))
				{
					natives.Add(module);
					findings.Add(new Finding("lua native", "Required", file, $"{module}|require('{module}')|{file}:{line}"));
				}
			}
		}
		foreach ((string file, CLuaGlobalScan scan) in scans)
		{
			foreach (CLuaGlobalScan.Use use in scan.Paths)
			{
				string root = use.Path.Split('.', ':')[0];
				if (natives.Contains(root) && use.Path.Length > root.Length)
				{
					findings.Add(new Finding("lua native", root, file, $"{use.Path}|{file}:{use.Line}"));
				}
			}
		}

		foreach ((string file, CLuaGlobalScan scan) in scans)
		{
			foreach (CLuaGlobalScan.Use use in scan.Uses)
			{
				string root = use.Path.Split('.')[0];
				if (natives.Contains(root))
				{
					continue;
				}
				if (globals.Contains(root) || defined.Contains(root))
				{
					// A static of one of our classes that the class does not have.
					if (lua.Classes.TryGetValue(root, out CLuaClass? luaClass) && use.Path.Split('.') is { Length: > 1 } parts
						&& !luaClass.Statics.ContainsKey(parts[1]) && !luaClass.Constants.ContainsKey(parts[1]))
					{
						findings.Add(new Finding("lua static", "MissingStatic", file, $"{root}.{parts[1]}|{file}:{use.Line}"));
					}
					continue;
				}
				// One of the original's classes - CSetVariableCommand - is ours to provide; any other name nothing defines is
				// the scripts' own slip, a local used out of its block, or something a native module would have made.
				bool isClass = root.Length > 1 && root[0] == 'C' && char.IsUpper(root[1]);
				findings.Add(new Finding(isClass ? "lua class" : "lua undefined", use.Called ? "Called" : "Read", file,
					$"{root}|{use.Path}|{file}:{use.Line}"));
			}
			// A file that uses a native module's fields - wx's windows - calls that module's objects' methods too.
			bool native = scan.Paths.Any(use => natives.Contains(use.Path.Split('.', ':')[0]) && use.Path.Contains('.'));
			foreach ((string name, int line) in scan.MethodCalls)
			{
				if (!members.Contains(name))
				{
					findings.Add(new Finding("lua method", native ? "InAFileUsingANativeModule" : "MissingMethod", file, $"{name}|{file}:{line}"));
				}
			}
		}
	}

	/// <summary>Where the original's searcher finds a module: <c>script/</c>, <c>script/country/</c>, <c>common/</c>.</summary>
	private static string? SearcherFile(CDirectorySettings directories, string module)
	{
		foreach (string folder in new[] { "script/", "script/country/", "common/" })
		{
			string path = directories.Resolve(folder + module + ".lua");
			if (File.Exists(path))
			{
				return path;
			}
		}
		return null;
	}

	/// <summary>A Lua error's first line, the searcher's list of every file it tried left out.</summary>
	private static string FirstLine(string error)
	{
		int newline = error.IndexOf('\n');
		return newline < 0 ? error : error[..newline].TrimEnd(':', ' ');
	}
	/// <summary>Every condition and effect keyword of the events, decisions and triggered modifiers that is unbuilt.</summary>
	private static void WalkKeywords(CGameState state, List<Finding> findings)
	{
		void Trigger(CTrigger trigger, string file)
		{
			if (trigger.Kind == ETriggerKind.Keyword && CUnansweredConditions.Reason(trigger.Keyword) is { } why)
			{
				findings.Add(new Finding("unbuilt condition", why.ToString(), file, trigger.Keyword));
			}
			trigger.Children.ForEach(child => Trigger(child, file));
		}
		void Effect(CEffect effect, string file)
		{
			if (effect.Kind == EEffectKind.Keyword && CUndoneEffects.Reason(effect.Keyword) is { } why)
			{
				findings.Add(new Finding("unbuilt effect", why.ToString(), file, effect.Keyword));
			}
			effect.Children.ForEach(child => Effect(child, file));
			if (effect.Limit is not null)
			{
				Trigger(effect.Limit, file);
			}
		}
		foreach (CEvent read in state.Events)
		{
			Trigger(read.Trigger, read.File);
			read.MeanTimeToHappen.Modifiers.ForEach(m => Trigger(m.Condition, read.File));
			read.Immediate.Effects.ForEach(e => Effect(e, read.File));
			foreach (CEventOption option in read.Options)
			{
				option.Effects.ForEach(e => Effect(e, read.File));
				option.AiChance.Modifiers.ForEach(m => Trigger(m.Condition, read.File));
			}
		}
		foreach (CDecision read in state.Decisions)
		{
			Trigger(read.Potential, read.File);
			Trigger(read.Allow, read.File);
			read.Effects.ForEach(e => Effect(e, read.File));
			read.AiWillDo.Modifiers.ForEach(m => Trigger(m.Condition, read.File));
		}
	}

	private static void Print(List<Finding> findings, List<string> timings, string? newGame, int examples)
	{
		// Lua globals, methods and statics group by their name; the keywords by theirs; everything else by its shape.
		static string GroupKey(Finding f) => f.Section switch
		{
			"lua class" or "lua undefined" or "lua method" or "lua static" or "lua native" => f.Message.Split('|')[0],
			"unbuilt condition" or "unbuilt effect" => f.Message,
			_ => ShapeOf(f.Message),
		};
		List<Gap> gaps = findings
			.Where(f => f.Kind != "Read")
			.GroupBy(f => (f.Section, f.Kind, Shape: GroupKey(f)))
			.Select(g => new Gap(g.Key.Section, g.Key.Kind, g.Key.Shape, g.Count(), g.ToList()))
			.OrderByDescending(g => g.Count).ThenBy(g => g.Section, StringComparer.Ordinal).ThenBy(g => g.Shape, StringComparer.Ordinal)
			.ToList();

		Console.WriteLine($"steps:    {string.Join(", ", timings)}");
		Console.WriteLine($"new game: {newGame}");
		Console.WriteLine();
		Console.WriteLine("counts, by section and kind (problems, and how many different ones) - 'ours' what this game does not answer,");
		Console.WriteLine("'files' what the files say that the original passes over too:");
		foreach (var section in gaps.GroupBy(g => g.Section).OrderBy(s => FilesSections.Contains(s.Key)).ThenByDescending(s => s.Sum(g => g.Count)))
		{
			string whose = FilesSections.Contains(section.Key) ? "files" : "ours ";
			Console.WriteLine($"  {whose} {section.Key,-30} {section.Sum(g => g.Count),7} in {section.Count(),5}   "
				+ string.Join(", ", section.GroupBy(g => g.Kind).OrderByDescending(k => k.Sum(g => g.Count))
					.Select(k => $"{k.Key} {k.Sum(g => g.Count)} ({k.Count()})")));
		}
		Console.WriteLine($"  {"",5} {"ours",-30} {gaps.Where(g => !FilesSections.Contains(g.Section)).Sum(g => g.Count),7}");
		Console.WriteLine($"  {"",5} {"files",-30} {gaps.Where(g => FilesSections.Contains(g.Section)).Sum(g => g.Count),7}");
		Console.WriteLine();
		// For the headline a native module is one gap however many of its fields are used, and so are the methods of its
		// objects; the details below keep every field.
		var headline = new List<(int Count, string Text)>();
		foreach (var native in gaps.Where(g => g.Section == "lua native" && g.Kind != "Required").GroupBy(g => g.Kind))
		{
			int requires = gaps.Where(g => g.Section == "lua native" && g.Kind == "Required" && g.Shape == native.Key).Sum(g => g.Count);
			int files = native.SelectMany(g => g.Findings).Select(f => f.File).Distinct(StringComparer.OrdinalIgnoreCase).Count();
			headline.Add((native.Sum(g => g.Count), $"lua native / {native.Key}: a native module, required {requires} times; "
				+ $"{native.Count()} of its fields used in {files} files"));
		}
		foreach (var methods in gaps.Where(g => g.Section == "lua method" && g.Kind == "InAFileUsingANativeModule").GroupBy(g => g.Kind))
		{
			headline.Add((methods.Sum(g => g.Count), $"lua method / {methods.Key}: {methods.Count()} methods of a native module's objects"));
		}
		headline.AddRange(gaps.Where(g => g.Section != "lua native" && g.Kind != "InAFileUsingANativeModule")
			.Select(g => (g.Count, $"{g.Section} / {g.Kind}: {g.Shape}")));
		Console.WriteLine("the twenty largest gaps:");
		foreach ((int count, string text) in headline.OrderByDescending(h => h.Count).Take(20))
		{
			Console.WriteLine($"  {count,6}  {text}");
		}
		Console.WriteLine();

		foreach (string read in findings.Where(f => f.Kind == "Read").Select(f => f.Message))
		{
			Console.WriteLine(read);
			Console.WriteLine();
		}

		foreach (var section in gaps.GroupBy(g => g.Section).OrderByDescending(s => s.Sum(g => g.Count)))
		{
			Console.WriteLine($"== {section.Key}: {section.Sum(g => g.Count)} problems in {section.Count()} different ones");
			// By file, the files with the most first.
			var byFile = section.SelectMany(g => g.Findings).Where(f => f.File.Length > 0)
				.GroupBy(f => f.File, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()).Take(15).ToList();
			if (byFile.Count > 0)
			{
				Console.WriteLine("   by file: " + string.Join(", ", byFile.Select(g => $"{g.Key} {g.Count()}")));
			}
			foreach (Gap gap in section)
			{
				Console.WriteLine($"  {gap.Count,6}  {gap.Kind}: {gap.Shape}");
				foreach (Finding finding in gap.Findings.Take(examples))
				{
					string text = finding.Section is "lua class" or "lua undefined" or "lua method" or "lua static" or "lua native"
						? string.Join("  ", finding.Message.Split('|').Skip(1))
						: finding.Section is "unbuilt condition" or "unbuilt effect" ? finding.File : finding.Message;
					Console.WriteLine($"            {text}");
				}
				if (section.Key is "lua class" or "lua undefined")
				{
					// Every path of fields used under a missing global, with how often.
					foreach (var path in gap.Findings.GroupBy(f => f.Message.Split('|')[1]).OrderByDescending(p => p.Count()).ThenBy(p => p.Key))
					{
						if (path.Key != gap.Shape)
						{
							Console.WriteLine($"            {path.Count(),4} x {path.Key}");
						}
					}
				}
			}
			Console.WriteLine();
		}
	}
}
