using System;
using System.Collections.Generic;
using OpenHOI3.Content.Mods;

namespace OpenHOI3.Content.Launch;

/// <summary>
/// What passes between the launcher and an engine: the command line and nothing else (<c>docs/launcher.md</c>, section 5).
/// Everything goes after <c>--</c>, where an engine's own switches are, so that no value is cut at a '-' as the original's
/// reading of its line cuts it (<c>docs/mechanics/mods.md</c>, section 3).
/// </summary>
public static class CLaunchContract
{
	/// <summary>
	/// The version of this command line, which an engine's <c>engine.txt</c> states and the launcher matches exactly. Raised
	/// whenever a switch changes its meaning or a new one is one the engine must not ignore.
	/// </summary>
	public const int Version = 1;

	/// <summary>The install to play, a folder; checked by the engine as a player's choice is.</summary>
	public const string InstallSwitch = "--install=";

	/// <summary>
	/// The arguments that start an engine on an install with mods, each one a mod's virtual path
	/// (<c>mod/&lt;file&gt;.mod</c>), which <see cref="CModSwitch.Developer"/> takes as it is. The engine orders them as the
	/// original mounts them.
	/// </summary>
	public static List<string> Arguments(string install, IEnumerable<string> modVirtualPaths)
	{
		var arguments = new List<string> { "--", InstallSwitch + install };
		foreach (string mod in modVirtualPaths)
		{
			arguments.Add(CModSwitch.Developer + mod);
		}
		return arguments;
	}

	/// <summary>The install an engine's own arguments name, or null.</summary>
	public static string? Install(IEnumerable<string> arguments)
	{
		string? install = null;
		foreach (string argument in arguments)
		{
			string trimmed = argument.Trim();
			if (trimmed.StartsWith(InstallSwitch, StringComparison.Ordinal))
			{
				install = trimmed[InstallSwitch.Length..].Trim().Trim('"');
			}
		}
		return string.IsNullOrEmpty(install) ? null : install;
	}

	/// <summary>Whether an argument is one of the contract's own, for a program that lists the arguments it leaves aside.</summary>
	public static bool Is(string argument)
	{
		return argument.Trim().StartsWith(InstallSwitch, StringComparison.Ordinal);
	}
}
