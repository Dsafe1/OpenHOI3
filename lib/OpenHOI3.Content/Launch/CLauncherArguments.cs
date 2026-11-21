using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenHOI3.Content.Launch;

/// <summary>
/// The launcher's own command line (<c>docs/launcher.md</c>, section 6): what a shortcut to it may say, so that it comes
/// up on a choice - as the shortcut a mod's installer makes does, naming the install it put the mod in and the mod.
/// <list type="bullet">
/// <item><c>--install=&lt;folder&gt;</c> the install to come up on, in place of the one remembered;</item>
/// <item><c>--mod=mod/&lt;file&gt;.mod</c> a mod to come up ticked, as the engine's own switch names one; given once for
/// each, and in place of the mods remembered;</item>
/// <item><c>--engine=&lt;folder&gt;</c> the engine to take as the launcher's own, for development.</item>
/// </list>
/// A value may stand in quotes. Anything else on the line is passed over.
/// </summary>
/// <param name="Engine">The folder of the engine to take as the launcher's own, or null for its own.</param>
/// <param name="Install">The install named, or null.</param>
/// <param name="Mods">The mods named, by virtual path, in the order given.</param>
public sealed record CLauncherArguments(string? Engine, string? Install, IReadOnlyList<string> Mods)
{
	public const string EngineSwitch = "--engine=";
	public const string InstallSwitch = "--install=";
	public const string ModSwitch = "--mod=";

	/// <summary>The command line read.</summary>
	public static CLauncherArguments Read(IEnumerable<string> arguments)
	{
		string? engine = null;
		string? install = null;
		var mods = new List<string>();
		foreach (string argument in arguments)
		{
			if (Value(argument, EngineSwitch) is { } engineFolder)
			{
				engine = engineFolder;
			}
			else if (Value(argument, InstallSwitch) is { } installFolder)
			{
				install = installFolder;
			}
			else if (Value(argument, ModSwitch) is { } mod && !mods.Contains(mod, StringComparer.Ordinal))
			{
				// With forward slashes, as a descriptor's virtual path is written.
				mods.Add(mod.Replace('\\', '/'));
			}
		}
		return new CLauncherArguments(engine, install, mods);
	}

	/// <summary>
	/// What the launcher comes up on: the install and the mods named here over those remembered. Naming a mod replaces
	/// every remembered tick - a shortcut to one mod comes up on that mod, whatever was played last.
	/// </summary>
	public CLauncherSettings Over(CLauncherSettings remembered)
	{
		return new CLauncherSettings(Install ?? remembered.Install, Mods.Count > 0 ? Mods : remembered.Mods);
	}

	/// <summary>A switch's value with its quotes off, or null where the argument is not that switch or says nothing.</summary>
	private static string? Value(string argument, string name)
	{
		if (!argument.StartsWith(name, StringComparison.Ordinal))
		{
			return null;
		}
		string value = argument[name.Length..].Trim('"');
		return value.Length == 0 ? null : value;
	}
}
