using System;
using System.Collections.Generic;

namespace OpenHOI3.Content.Mods;

/// <summary>
/// The mods a command line selects (<c>docs/mechanics/mods.md</c>, section 3): the original's own reading of its command
/// line, which the original launcher's <c>-mod=mod/BlackICE GitHub.mod</c> is written for, and this project's developer
/// switch <c>--mod=&lt;name&gt;</c>.
/// </summary>
public static class CModSwitch
{
	/// <summary>This project's spelling, beside the other developer switches: a mod's file name, with or without <c>.mod</c>.</summary>
	public const string Developer = "--mod=";

	private const string ModPrefix = "mod=";

	private const string UserDirPrefix = "userdir=";

	/// <summary>What the original trims a piece of its command line of: space, tab, CR and LF, and not quotes.</summary>
	private static readonly char[] Blanks = { ' ', '\t', '\r', '\n' };

	/// <summary>
	/// The original's reading of a command line, its program's name taken off: cut at every '-', the piece before the
	/// first passed over, each trimmed of blanks and nothing else, and the prefixes compared case and all. Every
	/// <c>mod=</c> with something after it is a mod's virtual path - so one whose file name holds a '-' cannot be named -
	/// and the last <c>userdir=</c> is the player's folder, used as given. Every other piece is not read here.
	/// </summary>
	public static (List<string> Mods, string? UserDir) Parse(string commandLine)
	{
		var mods = new List<string>();
		string? userDir = null;
		string[] pieces = commandLine.Split('-');
		for (int index = 1; index < pieces.Length; index++)
		{
			string piece = pieces[index].Trim(Blanks);
			if (piece.StartsWith(ModPrefix, StringComparison.Ordinal))
			{
				string value = piece[ModPrefix.Length..].Trim(Blanks);
				if (value.Length > 0)
				{
					mods.Add(value);
				}
			}
			else if (piece.StartsWith(UserDirPrefix, StringComparison.Ordinal) && piece.Length > UserDirPrefix.Length)
			{
				userDir = piece[UserDirPrefix.Length..];
			}
		}
		return (mods, userDir);
	}

	/// <summary>The names this project's <c>--mod=</c> gives, each with something after it, in order.</summary>
	public static List<string> DeveloperNames(IEnumerable<string> arguments)
	{
		var names = new List<string>();
		foreach (string argument in arguments)
		{
			string trimmed = argument.Trim();
			if (trimmed.StartsWith(Developer, StringComparison.Ordinal) && trimmed[Developer.Length..].Trim().Trim('"') is { Length: > 0 } name)
			{
				names.Add(name);
			}
		}
		return names;
	}

	/// <summary>Whether an argument is the developer switch, for a program that lists the arguments it leaves aside.</summary>
	public static bool Is(string argument)
	{
		return argument.Trim().StartsWith(Developer, StringComparison.Ordinal);
	}
}
