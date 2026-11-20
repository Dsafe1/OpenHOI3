using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenHOI3.Content.Checksum;

/// <summary>
/// The folders the original's file system reads through, first first, as its file checksum sees them: a name is listed once
/// across all of them and read from the first that has it, and a folder a mod replaces lists only what the mod's folders
/// hold (<c>docs/mechanics/checksum.md</c>, section 3).
/// </summary>
public sealed class CChecksumSearchPath
{
	private readonly string[] _mounts;
	private readonly IReadOnlyDictionary<string, int> _replaceLimits;

	/// <param name="mounts">Absolute folders, highest priority first.</param>
	/// <param name="replaced">Game-relative folders a mod's <c>replace_path</c> names, written with '/'.</param>
	/// <param name="replaceLimit">The last of <paramref name="mounts"/> a replaced folder may list a file from.</param>
	public CChecksumSearchPath(IEnumerable<string> mounts, IEnumerable<string>? replaced = null, int replaceLimit = -1)
		: this(mounts, (replaced ?? Array.Empty<string>()).Distinct(OpenHOI3.Content.Mods.CModRules.Comparer)
			.ToDictionary(folder => folder, _ => replaceLimit, OpenHOI3.Content.Mods.CModRules.Comparer))
	{
	}

	/// <param name="mounts">Absolute folders, highest priority first.</param>
	/// <param name="replaceLimits">
	/// For each game-relative folder a mod's <c>replace_path</c> names, written with '/', the last of
	/// <paramref name="mounts"/> a listed file's first copy may be in.
	/// </param>
	public CChecksumSearchPath(IEnumerable<string> mounts, IReadOnlyDictionary<string, int> replaceLimits)
	{
		_mounts = mounts.ToArray();
		_replaceLimits = replaceLimits;
	}

	/// <summary>
	/// The search path of what a set of directory settings plays (<see cref="CDirectorySettings.SearchPath"/>): the
	/// player's folder, which holds the map cache, each mod's folder, then the install's layers - Their Finest Hour's
	/// <c>tfh/</c> before the root; a replaced folder listing a file only where its first copy is in the replacing mod or
	/// in front of it.
	/// </summary>
	public static CChecksumSearchPath For(CDirectorySettings directories)
	{
		(string[] mounts, IReadOnlyDictionary<string, int> limits) = directories.SearchPath();
		return new CChecksumSearchPath(mounts.Select(Path.GetFullPath), limits);
	}

	/// <summary>
	/// The folders a game here reads its files from, for <see cref="CPlayChecksum"/>: the same without the player's
	/// folder in front, which is no layer of game files here (<c>docs/compatibility.md</c>, entry 28) - so that what a
	/// player keeps in his Documents folder does not part his figure from another's.
	/// </summary>
	public static CChecksumSearchPath ForPlay(CDirectorySettings directories)
	{
		if (OpenHOI3.Content.Mods.CModRules.UserFolderIsLayer)
		{
			return For(directories);
		}
		(string[] mounts, IReadOnlyDictionary<string, int> limits) = directories.SearchPath();
		return new CChecksumSearchPath(mounts.Skip(1).Select(Path.GetFullPath),
			limits.ToDictionary(pair => pair.Key, pair => pair.Value - 1, OpenHOI3.Content.Mods.CModRules.Comparer));
	}

	/// <summary>The index of the first folder holding the path, file or folder, or -1.</summary>
	public int First(string relative)
	{
		for (int index = 0; index < _mounts.Length; index++)
		{
			string full = Full(index, relative);
			if (File.Exists(full) || Directory.Exists(full))
			{
				return index;
			}
		}
		return -1;
	}

	/// <summary>Every name in the folder across the folders, each once, in the order first met.</summary>
	public List<string> Names(string relative)
	{
		var names = new List<string>();
		var seen = new HashSet<string>(StringComparer.Ordinal);
		for (int index = 0; index < _mounts.Length; index++)
		{
			string full = Full(index, relative);
			if (!Directory.Exists(full))
			{
				continue;
			}
			foreach (string entry in Directory.EnumerateFileSystemEntries(full))
			{
				string name = Path.GetFileName(entry);
				if (seen.Add(name))
				{
					names.Add(name);
				}
			}
		}
		return names;
	}

	/// <summary>Whether the first folder holding the path holds a folder there.</summary>
	public bool IsFolder(string relative)
	{
		int index = First(relative);
		return index >= 0 && Directory.Exists(Full(index, relative));
	}

	/// <summary>Whether a file of the folder may be listed: in a replaced folder, only from the mod's folders.</summary>
	public bool MayList(string folder, string relative)
	{
		return !_replaceLimits.TryGetValue(folder, out int limit) || First(relative) <= limit;
	}

	/// <summary>The bytes of the first copy of a file, or null where no folder holds it as a file.</summary>
	public byte[]? Read(string relative)
	{
		int index = First(relative);
		if (index < 0)
		{
			return null;
		}
		string full = Full(index, relative);
		return File.Exists(full) ? File.ReadAllBytes(full) : null;
	}

	private string Full(int index, string relative)
	{
		return Path.Combine(_mounts[index], relative.Replace('/', Path.DirectorySeparatorChar));
	}
}
