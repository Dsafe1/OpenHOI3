using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Map.Cache;
using OpenHOI3.Content.Settings;

/// <summary>
/// Builds the map cache from the map files and sets every file beside a cache the original wrote:
///
///   OpenHOI3.SaveCheck mapcache &lt;install or mod folder&gt; [cache folder] [calls of rand before the first beach] [out=folder]
///
/// <c>out=</c> keeps what was built in a folder. The cache folder is the player's in Documents with none given. The last figure is the original run's and not the map's: 292 for
/// Their Finest Hour's shipped cache. A map with another count differs only in the beaches' vertices and indices.
/// See <c>docs/mechanics/mapcache.md</c>.
/// </summary>
public static class CMapCacheCheck
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck mapcache <install or mod folder> [cache folder] [calls of rand before the first beach]");
			return 2;
		}
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CMap map = CMap.Load(directories, warnings.Add);
		string? keep = args.FirstOrDefault(arg => arg.StartsWith("out=", StringComparison.Ordinal))?[4..];
		args = args.Where(arg => !arg.StartsWith("out=", StringComparison.Ordinal)).ToArray();
		string shipped = args.Length > 2 ? args[2] : Path.Combine(CUserSettingsReader.UserFolder(), "map", "cache");
		int calls = args.Length > 3 ? int.Parse(args[3]) : CBeachNoise.DefaultCallsBefore;
		CMapCacheSource? source = CMapCacheSource.From(map, warnings.Add);
		if (source is null)
		{
			Console.Error.WriteLine(string.Join("\n", warnings));
			return 1;
		}
		string built = keep ?? Path.Combine(Path.GetTempPath(), "openhoi3-mapcache-check-" + Environment.ProcessId);
		try
		{
			var watch = Stopwatch.StartNew();
			CMapCacheBuilder.Build(source, built, CMapCache.Stamp(directories), calls);
			Console.WriteLine($"built in {watch.ElapsedMilliseconds} ms, the stamp {CMapCache.Stamp(directories):X8}, {calls} calls of rand before the first beach");
			bool allSame = true;
			foreach (string name in new[]
				{
					CMapBin.File, CBorderCache.BitmapFile, CBorderCache.PairsFile, CQuadCache.IndexFile, CQuadCache.QuadsFile, CMapDataFiles.AdjacenciesFile,
					CMapDataFiles.NavalFile, CMapDataFiles.ClimateFile, CMapDataFiles.ProvincesFile, CMapDataFiles.FormatFile, CMapCache.StampFile,
				})
			{
				allSame &= Compare(name, built, shipped);
			}
			allSame &= CompareTextures(built, shipped);
			Console.WriteLine(allSame ? "every file agrees byte for byte" : "some files differ");
			return allSame ? 0 : 1;
		}
		finally
		{
			if (keep is null && Directory.Exists(built))
			{
				Directory.Delete(built, recursive: true);
			}
		}
	}

	/// <summary>A file of the original's cache: the player's in Documents, or else the install's.</summary>
	public static string FileOf(CDirectorySettings directories, string name)
	{
		string user = Path.Combine(CUserSettingsReader.UserFolder(), "map", "cache", name);
		return File.Exists(user) ? user : directories.Resolve("map/cache/" + name);
	}

	private static bool Compare(string name, string built, string shipped)
	{
		string path = Path.Combine(shipped, name);
		if (!File.Exists(path))
		{
			// The original writes this one only where it finds none it can use.
			Console.WriteLine($"  {name}: not in that folder");
			return name == CMapDataFiles.FormatFile;
		}
		byte[] ours = File.ReadAllBytes(Path.Combine(built, name));
		byte[] theirs = File.ReadAllBytes(path);
		int common = Math.Min(ours.Length, theirs.Length);
		int firstDifference = -1;
		long same = 0;
		for (int i = 0; i < common; i++)
		{
			if (ours[i] == theirs[i])
			{
				same++;
			}
			else if (firstDifference < 0)
			{
				firstDifference = i;
			}
		}
		bool equal = ours.Length == theirs.Length && same == common;
		Console.WriteLine($"  {name}: {same:N0} of {theirs.Length:N0} bytes agree"
			+ (ours.Length != theirs.Length ? $", ours {ours.Length:N0} bytes" : "")
			+ (firstDifference >= 0 ? $", first differing at {firstDifference:N0}" : ""));
		return equal;
	}

	private static bool CompareTextures(string built, string shipped)
	{
		string ours = Path.Combine(built, "quad_textures");
		string theirs = Path.Combine(shipped, "quad_textures");
		string[] ourNames = Directory.GetFiles(ours).Select(Path.GetFileName).ToArray()!;
		string[] theirNames = Directory.GetFiles(theirs).Select(Path.GetFileName).ToArray()!;
		int same = 0;
		foreach (string name in ourNames.Intersect(theirNames))
		{
			if (File.ReadAllBytes(Path.Combine(ours, name)).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(theirs, name))))
			{
				same++;
			}
		}
		Console.WriteLine($"  quad_textures: {same:N0} of {theirNames.Length:N0} files agree whole, ours {ourNames.Length:N0}");
		return same == theirNames.Length && ourNames.Length == theirNames.Length;
	}
}
