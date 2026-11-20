using System;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Map.Cache;

namespace OpenHOI3.Content.Map;

/// <summary>
/// Where the map cache is found, and whether it is the cache of the map files the install has
/// (<c>docs/mechanics/mapdraw.md</c>, section 2.1). The game asks the player's folder under the Documents folder and nothing else, and
/// builds the cache there where it is not the map's (<see cref="OpenOrBuild"/>). The tools' <see cref="Open"/> looks there and then
/// through the install's layers, as the original does. The cache is the map's only while its <c>time</c> file holds the
/// stamp of seven map files: each file's Fletcher-32, the seven summed, a file that is not there counting nought
/// (<c>reversing/findings/FINDINGS-mapbuild.md</c>). A missing <c>time</c> is stale too.
/// </summary>
public sealed class CMapCache
{
	public const string Folder = "map/cache";

	public const string StampFile = "time";

	private readonly CDirectorySettings _directories;

	private readonly string? _userFolder;

	/// <summary>Whether a file the player's folder has not is looked for in the install; a cache built here has every file itself.</summary>
	private readonly bool _askInstall;

	private CMapCache(CDirectorySettings directories, string? userFolder, bool current, bool askInstall = true)
	{
		_directories = directories;
		_userFolder = userFolder;
		IsCurrent = current;
		_askInstall = askInstall;
	}

	/// <summary>Whether the cache's stamp is the map files'; nothing of a cache that is not is read.</summary>
	public bool IsCurrent { get; }

	/// <summary>The install whose map files the cache is of.</summary>
	public CDirectorySettings Directories => _directories;

	/// <summary>
	/// The cache of an install, its stamp checked once. <paramref name="userFolder"/> is the player's folder of the game
	/// being played - the base game's <c>Documents\Paradox Interactive\Hearts of Iron III</c> - or null to look only in
	/// the install.
	/// </summary>
	public static CMapCache Open(CDirectorySettings directories, string? userFolder, Action<string> problem)
	{
		var cache = new CMapCache(directories, userFolder, current: true);
		string? stampPath = cache.Find(StampFile);
		if (stampPath is null)
		{
			problem($"cache: no {Folder}/{StampFile}, so no cache is read");
			return new CMapCache(directories, userFolder, current: false);
		}
		byte[] stored = File.ReadAllBytes(stampPath);
		if (stored.Length < 4)
		{
			problem($"cache: {stampPath} holds {stored.Length} bytes where a stamp is four, so no cache is read");
			return new CMapCache(directories, userFolder, current: false);
		}
		uint expected = BitConverter.ToUInt32(stored, 0);
		uint actual = Stamp(directories);
		if (expected != actual)
		{
			problem($"cache: {stampPath} holds the stamp {expected:X8} and the map files make {actual:X8}; the cache is of other "
				+ "map files and is not read - the original builds a new one when it next starts");
			return new CMapCache(directories, userFolder, current: false);
		}
		return cache;
	}

	/// <summary>A file of the cache, the player's folder's copy before the install's, or null where neither has it.</summary>
	public string? Find(string name)
	{
		string relative = Folder + "/" + name;
		if (_userFolder is not null)
		{
			string user = Path.Combine(_userFolder, relative.Replace('/', Path.DirectorySeparatorChar));
			if (File.Exists(user))
			{
				return user;
			}
		}
		if (!_askInstall)
		{
			return null;
		}
		string installed = _directories.Resolve(relative);
		return File.Exists(installed) ? installed : null;
	}

	/// <summary>
	/// The game's cache: the one in the player's folder and no other, the install's never asked, built there first where it is not this
	/// map's (decided so; <c>docs/compatibility.md</c>, entry 22). See <c>docs/mechanics/mapcache.md</c>, section 11.
	/// </summary>
	public static CMapCache OpenOrBuild(CDirectorySettings directories, string userFolder, CMap map, Action<string> problem)
	{
		uint stamp = Stamp(directories);
		string folder = Path.Combine(userFolder, Folder.Replace('/', Path.DirectorySeparatorChar));
		if (!HoldsStamp(folder, stamp) || !IsWhole(folder, stamp))
		{
			CMapCacheSource? source = CMapCacheSource.From(map, problem);
			if (source is null)
			{
				return new CMapCache(directories, userFolder, current: false, askInstall: false);
			}
			problem($"cache: building the map's cache in {folder}");
			// The stamp goes last, so that a build cut short is never taken for a cache - and the list of what was built
			// after it, last of all.
			CMapCacheBuilder.Build(source, folder, stamp);
			WriteContents(folder, stamp);
		}
		return new CMapCache(directories, userFolder, current: true, askInstall: false);
	}

	/// <summary>
	/// The file a cache built here ends with: the stamp, then every file of the cache with its length. *Ours* - the
	/// original writes no such file and passes it over.
	/// </summary>
	public const string ContentsFile = "openhoi3_contents.txt";

	/// <summary>
	/// Whether the cache in the folder is one built here, whole: its list of contents is of this stamp and every file it
	/// names is there at the length it had. The stamp alone does not say so: the original keeps part of a cache in the
	/// install and part in the player's folder, and a player's folder with the stamp and half the files drew a broken map
	/// (2026-11-20). A cache with no list - the original's own, whole or not - is built again, once.
	/// </summary>
	private static bool IsWhole(string folder, uint stamp)
	{
		string path = Path.Combine(folder, ContentsFile);
		if (!File.Exists(path))
		{
			return false;
		}
		string[] lines = File.ReadAllLines(path);
		if (lines.Length < 2 || lines[0] != stamp.ToString("X8"))
		{
			return false;
		}
		for (int index = 1; index < lines.Length; index++)
		{
			int tab = lines[index].IndexOf('\t');
			if (tab <= 0 || !long.TryParse(lines[index][(tab + 1)..], System.Globalization.NumberStyles.None,
				System.Globalization.CultureInfo.InvariantCulture, out long length))
			{
				return false;
			}
			var file = new FileInfo(Path.Combine(folder, lines[index][..tab].Replace('/', Path.DirectorySeparatorChar)));
			if (!file.Exists || file.Length != length)
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>The list of a cache just built: every file under the folder but the list itself, by its path and its length.</summary>
	private static void WriteContents(string folder, uint stamp)
	{
		var lines = new System.Collections.Generic.List<string> { stamp.ToString("X8") };
		string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
		Array.Sort(files, StringComparer.Ordinal);
		foreach (string file in files)
		{
			string relative = Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/');
			if (relative != ContentsFile)
			{
				lines.Add(relative + "\t" + new FileInfo(file).Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
			}
		}
		File.WriteAllLines(Path.Combine(folder, ContentsFile), lines);
	}

	private static bool HoldsStamp(string folder, uint stamp)
	{
		string path = Path.Combine(folder, StampFile);
		if (!File.Exists(path))
		{
			return false;
		}
		byte[] stored = File.ReadAllBytes(path);
		return stored.Length >= 4 && BitConverter.ToUInt32(stored, 0) == stamp;
	}

	/// <summary>
	/// The stamp of the map files a cache is built from: <c>map/default.map</c>, then the files it names as its positions,
	/// terrain definition, provinces, terrain, definitions and adjacencies, each through the layers, their checksums summed.
	/// climate.bmp, rivers.bmp, continent.txt, region.txt and the rest are not in it, as in the original.
	/// </summary>
	public static uint Stamp(CDirectorySettings directories)
	{
		string defaultMap = directories.Resolve("map/default.map");
		ClausewitzNode definition = ClausewitzParser.ParseFile(defaultMap);
		string[] named =
		{
			definition.GetString("positions", "positions.txt"),
			definition.GetString("terrain_definition", "terrain.txt"),
			definition.GetString("provinces", "provinces.bmp"),
			definition.GetString("terrain", "terrain.bmp"),
			definition.GetString("definitions", "definition.csv"),
			definition.GetString("adjacencies", "adjacencies.csv"),
		};
		uint stamp = Checksum(defaultMap);
		foreach (string name in named)
		{
			string path = directories.Resolve("map/" + name);
			if (File.Exists(path))
			{
				unchecked { stamp += Checksum(path); }
			}
		}
		return stamp;
	}

	public static uint Checksum(string path)
	{
		return Checksum(File.ReadAllBytes(path));
	}

	/// <summary>
	/// The original's Fletcher-32 of a file's bytes: both sums start at nought and each is brought back by subtracting
	/// 65535 once whenever it passes 65535 - so a sum that lands on 65535 stays there, which a remainder would make nought.
	/// </summary>
	public static uint Checksum(ReadOnlySpan<byte> bytes)
	{
		uint a = 0;
		uint b = 0;
		foreach (byte value in bytes)
		{
			a += value;
			if (a > 0xFFFF)
			{
				a -= 0xFFFF;
			}
			b += a;
			if (b > 0xFFFF)
			{
				b -= 0xFFFF;
			}
		}
		return a << 16 | b;
	}
}
