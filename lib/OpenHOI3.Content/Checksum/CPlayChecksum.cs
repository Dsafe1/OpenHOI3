using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OpenHOI3.Content.Checksum;

/// <summary>
/// This engine's own checksum, beside the original's (<see cref="CFileChecksum"/>, which is kept as it is): what two
/// machines compare before they play one game (<c>docs/multiplayer.md</c>, section 2;
/// <c>docs/mechanics/checksum.md</c>, section 8). Where the original's is a sum of sums over some of the files, this is
/// a hash of what decides how a game goes: the engine's version, the rules it is played by, and every file of the
/// folders the simulation reads - by its path and its bytes, so that a file moved, exchanged with another or added
/// empty changes it - the AI's Lua among them.
/// </summary>
public static class CPlayChecksum
{
	/// <summary>What the hash begins with: raised when what goes into it, or how, changes.</summary>
	public const string Format = "OpenHOI3 play checksum 2";

	/// <summary>
	/// The kinds of file counted, by their extension: those a game reads out of these folders. Anything else lying
	/// there is a machine's own and no part of the game - a `Thumbs.db` Windows made, an archive or a `.bak` a player
	/// kept, a map cache under another folder's name - and counting it parted two players of one game: on 2026-11-20
	/// two installs of the same files were refused each other for sixteen such files in one of them.
	/// </summary>
	public static readonly string[] Extensions = { ".txt", ".lua", ".csv", ".bmp", ".dds", ".tga", ".map" };

	/// <summary>Whether a file is of a kind that is counted.</summary>
	public static bool Counts(string name)
	{
		return Extensions.Any(extension => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// The folders hashed, each with all its subfolders and every file of a kind a game reads (<see cref="Extensions"/>):
	/// what the simulation is made from and run by. Not the interface, the pictures, the localisation, the sound or the music, which change
	/// what a player sees and hears and nothing a game does.
	/// </summary>
	public static readonly string[] Folders =
	{
		"common", "decisions", "events", "history", "map", "missions", "script", "technologies", "units",
	};

	/// <summary>
	/// Folders left out wherever they stand: the map's cache, which each machine makes for itself from the map's files,
	/// and a version control's own.
	/// </summary>
	private static readonly string[] SkippedFolders = { "map/cache", ".svn", ".git" };

	/// <summary>
	/// The one folder under <c>map</c> a game reads: the terrain's pictures. Any other there is a machine's own - a cache
	/// kept under another name held 1,774 tile pictures, of a kind that counts, and they parted two players of the same
	/// files (2026-11-20).
	/// </summary>
	private const string MapSubfolder = "terrain";

	/// <summary>The checksum as 32 bytes, and how many files went into it.</summary>
	/// <param name="search">The folders a game reads through (<see cref="CChecksumSearchPath.ForPlay"/>).</param>
	/// <param name="engineVersion">The engine's version as its <c>engine.txt</c> names it; empty for one run from its sources.</param>
	/// <param name="rules">The rules a game is played by beyond the original's, in a form that is the same wherever they are - none yet.</param>
	/// <param name="listed">
	/// Where given, a line is added for the format, the engine's version and the rules, and one for every file that went
	/// in - its path as hashed, its length and the first sixteen digits of its own SHA-256 - so that two machines whose
	/// figures differ can set the two lists beside each other and see in which file (section 8).
	/// </param>
	public static (byte[] Hash, int Files) Compute(CChecksumSearchPath search, string engineVersion, string rules = "",
		List<string>? listed = null)
	{
		listed?.Add("format\t" + Format);
		listed?.Add("engine\t" + engineVersion);
		listed?.Add("rules\t" + rules);
		using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		AppendText(hash, Format);
		AppendText(hash, engineVersion);
		AppendText(hash, rules);

		var paths = new List<string>();
		foreach (string folder in Folders)
		{
			Collect(search, folder, paths);
		}
		// By the path's bytes, lower case: two file systems may spell one file's name in two cases.
		paths.Sort((a, b) => string.CompareOrdinal(a.ToLowerInvariant(), b.ToLowerInvariant()));

		int files = 0;
		Span<byte> length = stackalloc byte[8];
		foreach (string path in paths)
		{
			if (search.Read(path) is not { } bytes)
			{
				continue;
			}
			AppendText(hash, path.ToLowerInvariant());
			// The length in front of the bytes too, so that where one file ends and the next begins is part of it.
			BinaryPrimitives.WriteInt64LittleEndian(length, bytes.LongLength);
			hash.AppendData(length);
			hash.AppendData(bytes);
			files++;
			listed?.Add(path.ToLowerInvariant() + "\t" + bytes.LongLength.ToString(System.Globalization.CultureInfo.InvariantCulture)
				+ "\t" + Convert.ToHexString(SHA256.HashData(bytes), 0, 8));
		}
		byte[] whole = hash.GetHashAndReset();
		listed?.Add("checksum\t" + Convert.ToHexString(whole));
		return (whole, files);
	}

	/// <summary>
	/// What two machines may share while their play checksums differ, and have to for a host's rule to let one in
	/// (<c>docs/multiplayer.md</c>, section 15): a hash of the engine's version, the rules and the original's file
	/// checksum, in hexadecimal.
	/// </summary>
	public static string Base(string engineVersion, string rules, int fileChecksum)
	{
		using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		AppendText(hash, "OpenHOI3 play base 1");
		AppendText(hash, engineVersion);
		AppendText(hash, rules);
		AppendText(hash, fileChecksum.ToString(System.Globalization.CultureInfo.InvariantCulture));
		return Convert.ToHexString(hash.GetHashAndReset(), 0, 8);
	}

	/// <summary>The first eight hexadecimal digits of the checksum in fours, which is what a player is shown: <c>3F9A-01C7</c>.</summary>
	public static string Label(byte[] hash)
	{
		string digits = Convert.ToHexString(hash, 0, 4);
		return digits[..4] + "-" + digits[4..];
	}

	/// <summary>Every file of a folder and of its subfolders, as the folders a game reads through list them.</summary>
	private static void Collect(CChecksumSearchPath search, string folder, List<string> paths)
	{
		foreach (string name in search.Names(folder))
		{
			string path = folder + "/" + name;
			if (search.IsFolder(path))
			{
				if (!SkippedFolders.Any(skipped => path.Equals(skipped, StringComparison.OrdinalIgnoreCase)
					|| name.Equals(skipped, StringComparison.OrdinalIgnoreCase))
					&& (!folder.Equals("map", StringComparison.OrdinalIgnoreCase)
						|| name.Equals(MapSubfolder, StringComparison.OrdinalIgnoreCase)))
				{
					Collect(search, path, paths);
				}
			}
			else if (Counts(name) && search.MayList(folder, path))
			{
				paths.Add(path);
			}
		}
	}

	/// <summary>A text with its length in front, so that two texts never run together into a third.</summary>
	private static void AppendText(IncrementalHash hash, string text)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		Span<byte> length = stackalloc byte[4];
		BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
		hash.AppendData(length);
		hash.AppendData(bytes);
	}
}
