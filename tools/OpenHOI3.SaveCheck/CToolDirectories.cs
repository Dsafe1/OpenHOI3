using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;

/// <summary>
/// The directory settings a command reads an install through: <c>mod=&lt;name&gt;</c> anywhere among its arguments lays
/// the mod of <c>tfh/mod/&lt;name&gt;.mod</c> over the install, as the client's <c>--mod=</c> does
/// (<c>docs/mechanics/mods.md</c>, section 4). The argument is taken out, so a command's own arguments keep their places.
/// </summary>
internal static class CToolDirectories
{
	public const string Switch = "mod=";

	/// <summary>
	/// The install with the mod an argument names, the argument taken out of <paramref name="args"/>; the install alone
	/// where none names one; null, with the reason written, where the named mod is not there.
	/// </summary>
	public static CDirectorySettings? Make(string install, ref string[] args)
	{
		string? name = args.LastOrDefault(argument => argument.StartsWith(Switch, StringComparison.Ordinal))?[Switch.Length..];
		args = args.Where(argument => !argument.StartsWith(Switch, StringComparison.Ordinal)).ToArray();
		CDirectorySettings directories = CDirectorySettings.At(install);
		return name is null ? directories : WithMod(directories, name);
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
			Console.WriteLine($"mod:      {string.Join(", ", modded.Mods.Select(m => m.Name))} - {string.Join(", ", modded.Layers)}; "
				+ $"{modded.ReplacedFolders.Count} replaced folders; the player's folder {modded.UserFolder}");
		}
		return modded;
	}
}
