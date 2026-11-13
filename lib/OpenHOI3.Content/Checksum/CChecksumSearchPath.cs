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
	private readonly HashSet<string> _replaced;
	private readonly int _replaceLimit;

	/// <param name="mounts">Absolute folders, highest priority first.</param>
	/// <param name="replaced">Game-relative folders a mod's <c>replace_path</c> names, written with '/'.</param>
	/// <param name="replaceLimit">The last of <paramref name="mounts"/> a replaced folder may list a file from.</param>
	public CChecksumSearchPath(IEnumerable<string> mounts, IEnumerable<string>? replaced = null, int replaceLimit = -1)
	{
		_mounts = mounts.ToArray();
		_replaced = new HashSet<string>(replaced ?? Array.Empty<string>(), StringComparer.Ordinal);
		_replaceLimit = replaceLimit;
	}

	/// <summary>
	/// The install's layers - Their Finest Hour's <c>tfh/</c> before the root - then the player's folder, which holds the
	/// map cache: the base game's search path.
	/// </summary>
	public static CChecksumSearchPath ForInstall(CDirectorySettings directories, string? userFolder)
	{
		var mounts = directories.LayerFolders("").Select(Path.GetFullPath).ToList();
		if (userFolder is not null)
		{
			mounts.Add(userFolder);
		}
		return new CChecksumSearchPath(mounts);
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
		return !_replaced.Contains(folder) || First(relative) <= _replaceLimit;
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
