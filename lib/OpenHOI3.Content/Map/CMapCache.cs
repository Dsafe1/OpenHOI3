using System;
using System.IO;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Content.Map;

/// <summary>
/// Where the original's map cache is found, and whether it is the cache of the map files the install has
/// (<c>docs/mechanics/mapdraw.md</c>, section 2.1). A file of <c>map/cache</c> is looked for in the player's folder
/// under the Documents folder first - where the original writes the cache it builds - and then through the install's
/// layers, which is where Their Finest Hour ships its own. The cache is the map's only while its <c>time</c> file holds the
/// stamp of seven map files: each file's Fletcher-32, the seven summed, a file that is not there counting nought
/// (<c>reversing/findings/FINDINGS-mapbuild.md</c>). A missing <c>time</c> is stale too. A stale cache is not read: the
/// original builds a new one, and here the map is then drawn flat.
/// </summary>
public sealed class CMapCache
{
	public const string Folder = "map/cache";

	public const string StampFile = "time";

	private readonly CDirectorySettings _directories;

	private readonly string? _userFolder;

	private CMapCache(CDirectorySettings directories, string? userFolder, bool current)
	{
		_directories = directories;
		_userFolder = userFolder;
		IsCurrent = current;
	}

	/// <summary>Whether the cache's stamp is the map files'; nothing of a cache that is not is read.</summary>
	public bool IsCurrent { get; }

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
		string installed = _directories.Resolve(relative);
		return File.Exists(installed) ? installed : null;
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
