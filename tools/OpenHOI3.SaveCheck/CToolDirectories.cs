using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content;

/// <summary>
/// The directory settings every command reads an install through. <c>mod=&lt;name&gt;</c> anywhere among the tool's
/// arguments lays the mod of <c>tfh/mod/&lt;name&gt;.mod</c> over the install for whatever command runs, as the client's
/// <c>--mod=</c> does (<c>docs/mechanics/mods.md</c>, section 4): <c>Program</c> takes it out before a command sees its
/// arguments, so a command's own arguments keep their places, and <see cref="At"/> lays it over. Under a mod the tool then
/// works in the install, as the original's process does, so that the mod's Lua finds the files it names by paths written
/// against it (<c>mods.md</c>, section 6) - or in the folder <c>OPENHOI3_WORKDIR</c> names, one whose <c>tfh</c> is a link to
/// the install's, for a run that is not to write into the install; a relative file argument is made whole first.
/// </summary>
internal static class CToolDirectories
{
	public const string Switch = "mod=";

	/// <summary>The extensions that mark an argument, or an argument's value, as a file named relative to where the tool was started.</summary>
	private static readonly string[] FileExtensions = { ".hoi3", ".txt", ".gz", ".csv", ".log", ".png", ".json", ".fx", ".bin" };

	/// <summary>The mod <c>mod=</c> named for the whole run, or null for the install alone.</summary>
	public static string? Mod { get; private set; }

	/// <summary>
	/// Takes <c>mod=&lt;name&gt;</c> out of the tool's arguments, the last one counting, and keeps it for <see cref="At"/>. With
	/// one, every argument that names a file or a folder relative to where the tool was started is made whole, since the
	/// working folder will move to the install.
	/// </summary>
	public static string[] TakeMod(string[] args)
	{
		string? name = args.LastOrDefault(argument => argument.StartsWith(Switch, StringComparison.Ordinal))?[Switch.Length..];
		if (name is null)
		{
			return args;
		}
		Mod = name;
		return args.Where(argument => !argument.StartsWith(Switch, StringComparison.Ordinal)).Select(WithFullPath).ToArray();
	}

	/// <summary>An argument naming a relative file or folder - or a <c>key=</c> whose value does - made whole.</summary>
	private static string WithFullPath(string argument)
	{
		int equals = argument.IndexOf('=');
		string key = equals > 0 ? argument[..(equals + 1)] : "";
		string value = equals > 0 ? argument[(equals + 1)..] : argument;
		if (value.Length == 0 || Path.IsPathRooted(value))
		{
			return argument;
		}
		bool looksLikePath = value.Contains('/') || value.Contains('\\')
			|| FileExtensions.Any(extension => value.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
			|| File.Exists(value) || Directory.Exists(value);
		// A list of files, as series= takes, is made whole one by one.
		if (looksLikePath && value.Contains(','))
		{
			return key + string.Join(",", value.Split(',').Select(part => part.Length == 0 || Path.IsPathRooted(part) ? part : Path.GetFullPath(part)));
		}
		return looksLikePath ? key + Path.GetFullPath(value) : argument;
	}

	/// <summary>
	/// The install with the run's mod laid over it, and the working folder moved to it; the install alone where no mod was
	/// named. A mod named that is not there ends the run, with the reason and the mods there are.
	/// </summary>
	public static CDirectorySettings At(string install)
	{
		CDirectorySettings directories = CDirectorySettings.At(install);
		if (Mod is null)
		{
			return directories;
		}
		CDirectorySettings? modded = WithMod(directories, Mod);
		if (modded is null)
		{
			Environment.Exit(2);
		}
		string workingFolder = Environment.GetEnvironmentVariable("OPENHOI3_WORKDIR") is { Length: > 0 } other ? other : modded.GameRoot;
		Directory.SetCurrentDirectory(workingFolder);
		return modded;
	}

	/// <summary>
	/// The install with the mod an argument names, the argument taken out of <paramref name="args"/>; else with the run's
	/// mod, if any; null, with the reason written, where the named mod is not there.
	/// </summary>
	public static CDirectorySettings? Make(string install, ref string[] args)
	{
		string? name = args.LastOrDefault(argument => argument.StartsWith(Switch, StringComparison.Ordinal))?[Switch.Length..];
		args = args.Where(argument => !argument.StartsWith(Switch, StringComparison.Ordinal)).ToArray();
		return name is null ? At(install) : WithMod(CDirectorySettings.At(install), name);
	}

	/// <summary>The install with the mod a name means, or null with the reason and the mods there are written.</summary>
	public static CDirectorySettings? WithMod(CDirectorySettings directories, string name)
	{
		var problems = new List<string>();
		CDirectorySettings? modded = directories.WithMod(name, problems.Add);
		foreach (string problem in problems)
		{
			Console.Error.WriteLine("mod: " + problem);
		}
		if (modded is not null)
		{
			Console.Error.WriteLine($"mod:      {string.Join(", ", modded.Mods.Select(m => m.Name))} - {string.Join(", ", modded.Layers)}; "
				+ $"{modded.ReplacedFolders.Count} replaced folders; the player's folder {modded.UserFolder}");
		}
		return modded;
	}
}
