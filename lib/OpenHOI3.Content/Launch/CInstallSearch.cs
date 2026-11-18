using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenHOI3.Content.Launch;

/// <summary>
/// Where the launcher looks for an install it has not been given (<c>docs/launcher.md</c>, section 6): the one it
/// remembers, its own folder and the walk up from it as the engine walks, then each Steam library's
/// <c>steamapps/common/Hearts of Iron 3</c>. The first that is an install is taken; the player may choose another.
/// </summary>
public static partial class CInstallSearch
{
	/// <summary>The folder name an install has in a Steam library.</summary>
	public const string SteamFolder = "Hearts of Iron 3";

	/// <summary>The first install among the places looked in, or null.</summary>
	public static string? Find(IEnumerable<string> candidates)
	{
		return candidates.FirstOrDefault(CDirectorySettings.IsInstall) is { } found ? Path.GetFullPath(found) : null;
	}

	/// <summary>The places looked in, in order.</summary>
	/// <param name="remembered">The install remembered, or null.</param>
	/// <param name="launcherFolder">The launcher's own folder.</param>
	/// <param name="steamLibraries">The Steam libraries on this machine, Steam's own folder first.</param>
	public static List<string> Candidates(string? remembered, string launcherFolder, IEnumerable<string> steamLibraries)
	{
		var candidates = new List<string>();
		if (remembered is not null)
		{
			candidates.Add(remembered);
		}
		candidates.Add(launcherFolder);
		candidates.AddRange(CDirectorySettings.SearchedLocations(launcherFolder));
		candidates.AddRange(steamLibraries.Select(library => Path.Combine(library, "steamapps", "common", SteamFolder)));
		return candidates;
	}

	/// <summary>
	/// The libraries Steam's <c>steamapps/libraryfolders.vdf</c> lists - each <c>"path"</c> value, its backslashes
	/// unescaped - after Steam's own folder, each once.
	/// </summary>
	public static List<string> SteamLibraries(string? steamFolder)
	{
		var libraries = new List<string>();
		if (string.IsNullOrEmpty(steamFolder))
		{
			return libraries;
		}
		libraries.Add(steamFolder);
		string file = Path.Combine(steamFolder, "steamapps", "libraryfolders.vdf");
		if (File.Exists(file))
		{
			try
			{
				libraries.AddRange(LibraryPaths(File.ReadAllText(file)));
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				// No libraries but Steam's own: the player can still choose the install.
			}
		}
		return libraries.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
	}

	/// <summary>Every <c>"path" "..."</c> pair of a <c>libraryfolders.vdf</c>, its value's <c>\\</c> made one backslash.</summary>
	public static List<string> LibraryPaths(string vdf)
	{
		return PathLine().Matches(vdf).Select(match => match.Groups[1].Value.Replace("\\\\", "\\")).ToList();
	}

	[GeneratedRegex("\"path\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.IgnoreCase)]
	private static partial Regex PathLine();
}
