using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHOI3.Content.Map;

namespace OpenHOI3.Content.Checksum;

/// <summary>
/// The original's file checksum: the figure its startup logs and writes as four letters on the main menu and in the lobby,
/// worked out over the same files in the same way, so that the same install gives the same letters here as there
/// (<c>docs/mechanics/checksum.md</c>; <c>reversing/findings/FINDINGS-checksum.md</c>).
/// </summary>
public static class CFileChecksum
{
	/// <summary>
	/// The original's version name, whose own sum is the last term: kept, by the maintainer's word of 2026-11-13, so that
	/// the letters are the original's for the same files.
	/// </summary>
	public const string VersionName = "HoI3 v4.02";

	/// <summary>The folders summed before the map's files, each with its extension and whether its subfolders go too.</summary>
	private static readonly (string Folder, string Extension, bool Recurse)[] Folders =
	{
		("common", ".lua", false), ("common", ".txt", false), ("common/countries", ".txt", false),
		("events", ".txt", false), ("missions", ".txt", false),
		("units", ".txt", false), ("units/models", ".txt", false),
		("units", ".lua", false), ("units/country", ".lua", false),
		("technologies", ".txt", false), ("decisions", ".txt", false),
		("history/countries", ".txt", false), ("history/diplomacy", ".txt", false),
		("history/provinces", ".txt", true), ("history/wars", ".txt", false),
		("history/leaders", ".txt", false), ("history/units", ".txt", false),
	};

	/// <summary>The map's three files, each summed alone.</summary>
	private static readonly string[] MapFiles = { "map/default.map", "map/cache/time", "map/cache/adjacencies.bin" };

	/// <summary>The folders summed after them.</summary>
	private static readonly (string Folder, string Extension, bool Recurse)[] LaterFolders =
	{
		("cgm", ".txt", true), ("battleplans", ".txt", true), ("battleplans", ".pln", true),
	};

	/// <summary>Names a folder's listing passes over, as files and as subfolders.</summary>
	private static readonly string[] Skipped = { ".", "..", ".svn" };

	/// <summary>The checksum, a signed 32-bit figure as the original logs it, and how many files went into it.</summary>
	public static (int Checksum, int Files) Compute(CChecksumSearchPath search)
	{
		uint total = 0;
		int files = 0;
		foreach ((string folder, string extension, bool recurse) in Folders)
		{
			total += SumFolder(search, folder, extension, recurse, ref files);
		}
		foreach (string path in MapFiles)
		{
			if (search.Read(path) is { } bytes)
			{
				total += CMapCache.Checksum(bytes);
				files++;
			}
		}
		foreach ((string folder, string extension, bool recurse) in LaterFolders)
		{
			total += SumFolder(search, folder, extension, recurse, ref files);
		}
		total += SignedChecksum(Encoding.ASCII.GetBytes(VersionName));
		return (unchecked((int)total), files);
	}

	/// <summary>
	/// The four letters: the figure, and it shifted right by 4, 8 and 12 keeping its sign, each C's remainder by 26 made
	/// positive and added to A.
	/// </summary>
	public static string Letters(int checksum)
	{
		var letters = new StringBuilder(4);
		foreach (int shift in new[] { 0, 4, 8, 12 })
		{
			letters.Append((char)('A' + Math.Abs((checksum >> shift) % 26)));
		}
		return letters.ToString();
	}

	/// <summary>
	/// The file checksum's sum over bytes taken as signed, as the original takes a string's: a byte of 0x80 or more
	/// subtracts, and each sum is brought back by 65535 only when it passes 65535.
	/// </summary>
	public static uint SignedChecksum(ReadOnlySpan<byte> bytes)
	{
		int a = 0;
		int b = 0;
		foreach (byte value in bytes)
		{
			a += (sbyte)value;
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
		return unchecked((uint)(a << 16) | (uint)b);
	}

	/// <summary>
	/// Every file of the folder whose name ends in the extension, case and all, then where asked every subfolder the same
	/// way: names with a character past ASCII, which the original's file system will not list, and <c>.svn</c> left out.
	/// </summary>
	private static uint SumFolder(CChecksumSearchPath search, string folder, string extension, bool recurse, ref int files)
	{
		uint total = 0;
		List<string> names = search.Names(folder);
		foreach (string name in names)
		{
			string path = folder + "/" + name;
			if (!name.EndsWith(extension, StringComparison.Ordinal) || !Listable(name) || search.IsFolder(path)
				|| Skipped.Contains(name) || !search.MayList(folder, path))
			{
				continue;
			}
			if (search.Read(path) is { } bytes)
			{
				total += CMapCache.Checksum(bytes);
				files++;
			}
		}
		if (recurse)
		{
			foreach (string name in names)
			{
				string path = folder + "/" + name;
				if (Listable(name) && !Skipped.Contains(name) && search.IsFolder(path))
				{
					total += SumFolder(search, path, extension, recurse, ref files);
				}
			}
		}
		return total;
	}

	private static bool Listable(string name)
	{
		return name.All(character => character < 0x80);
	}
}
