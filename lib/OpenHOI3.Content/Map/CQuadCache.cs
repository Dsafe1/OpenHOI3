using System;
using System.Collections.Generic;
using System.IO;

namespace OpenHOI3.Content.Map;

/// <summary>
/// The original's quad cache - the map's meshes as it draws them - read from the install: <c>map/cache/quad_index.bin</c>
/// (how many tiles, the bytes of them all, the largest record, then each tile's number and its record's bytes) and
/// <c>map/cache/all_quads.bin</c>, the records one after another. A record that does not read to its stated size is
/// reported and left out. See <c>docs/mechanics/mapdraw.md</c>, section 2;
/// <c>reversing/findings/FINDINGS-mapdraw.md</c>, A.1.
/// </summary>
public static class CQuadCache
{
	public const string IndexFile = "quad_index.bin";
	public const string QuadsFile = "all_quads.bin";

	/// <summary>A tile's terrain-index picture, in the cache's folder.</summary>
	public static string TextureFile(int tile) => $"quad_textures/quad_texture_{tile}.dds";

	/// <summary>
	/// How many tiles a row of the map has: its width over 50 and one [exe], the width provinces.bmp's - the file's head
	/// read, the picture not.
	/// </summary>
	public static int Columns(CDirectorySettings directories)
	{
		string path = directories.Resolve("map/provinces.bmp");
		byte[] head = new byte[26];
		using (FileStream file = File.OpenRead(path))
		{
			file.ReadExactly(head);
		}
		return BitConverter.ToInt32(head, 18) / CQuadTile.Size + 1;
	}

	/// <summary>Every tile of the cache, or none where there is no cache or it is stale.</summary>
	public static IReadOnlyList<CQuadTile> Load(CMapCache cache, Action<string> problem)
	{
		if (!cache.IsCurrent)
		{
			return Array.Empty<CQuadTile>();
		}
		string? index = cache.Find(IndexFile);
		string? quads = cache.Find(QuadsFile);
		if (index is null || quads is null)
		{
			problem($"quads: no {CMapCache.Folder}/{IndexFile} and {QuadsFile}; the map has no meshes");
			return Array.Empty<CQuadTile>();
		}
		return Read(File.ReadAllBytes(index), File.ReadAllBytes(quads), problem);
	}

	public static IReadOnlyList<CQuadTile> Read(byte[] index, byte[] quads, Action<string> problem)
	{
		var tiles = new List<CQuadTile>();
		if (index.Length < 12)
		{
			problem("quads: the index is shorter than its head");
			return tiles;
		}
		int count = BitConverter.ToInt32(index, 0);
		int total = BitConverter.ToInt32(index, 4);
		if (total != quads.Length)
		{
			problem($"quads: the index gives {total} bytes, the file has {quads.Length}");
		}
		int at = 0;
		for (int i = 0; i < count && 12 + i * 8 + 8 <= index.Length; i++)
		{
			int tile = BitConverter.ToInt32(index, 12 + i * 8);
			int bytes = BitConverter.ToInt32(index, 16 + i * 8);
			if (at + bytes > quads.Length)
			{
				problem($"quads: tile {tile}'s record runs past the end of the file");
				break;
			}
			try
			{
				var reader = new BinaryReader(new MemoryStream(quads, at, bytes, writable: false));
				CQuadTile read = ReadRecord(reader, tile);
				if (reader.BaseStream.Position != bytes)
				{
					problem($"quads: tile {tile}'s record reads to {reader.BaseStream.Position} of its {bytes} bytes");
				}
				else
				{
					tiles.Add(read);
				}
			}
			catch (EndOfStreamException)
			{
				problem($"quads: tile {tile}'s record ends early");
			}
			at += bytes;
		}
		return tiles;
	}

	/// <summary>
	/// One record, in the order the original's loader reads it (<c>FINDINGS-mapdraw.md</c>, A.1): the land's vertices, its
	/// triangles in one group for each province, the borders' triangles and vertices, the beaches' vertices and - only
	/// where there are some - their triangles, and the surf's records.
	/// </summary>
	private static CQuadTile ReadRecord(BinaryReader reader, int tile)
	{
		reader.ReadInt32();
		int landBytes = reader.ReadInt32();
		int landCount = landBytes / 8;
		var positions = new short[landCount * 2];
		var provinces = new ushort[landCount];
		for (int v = 0; v < landCount; v++)
		{
			positions[v * 2] = reader.ReadInt16();
			positions[v * 2 + 1] = reader.ReadInt16();
			ushort low = (ushort)reader.ReadInt16();
			ushort high = (ushort)reader.ReadInt16();
			provinces[v] = (ushort)(low | high << 8);
		}
		reader.ReadInt32();
		int groups = reader.ReadInt32();
		var indices = new List<ushort>();
		var starts = new List<int>();
		for (int g = 0; g < groups; g++)
		{
			starts.Add(indices.Count);
			indices.AddRange(ReadIndices(reader, reader.ReadInt32()));
		}
		starts.Add(indices.Count);
		ushort[] borderIndices = ReadIndices(reader, reader.ReadInt32());
		int borderBytes = reader.ReadInt32();
		int borderCount = borderBytes / 12;
		var borderShorts = new short[borderCount * 4];
		var borderColours = new uint[borderCount];
		for (int v = 0; v < borderCount; v++)
		{
			for (int k = 0; k < 4; k++)
			{
				borderShorts[v * 4 + k] = reader.ReadInt16();
			}
			borderColours[v] = reader.ReadUInt32();
		}
		int beachBytes = reader.ReadInt32();
		int beachCount = beachBytes / 12;
		var beachPositions = new float[beachCount * 2];
		var beachColours = new uint[beachCount];
		for (int v = 0; v < beachCount; v++)
		{
			beachPositions[v * 2] = reader.ReadSingle();
			beachPositions[v * 2 + 1] = reader.ReadSingle();
			beachColours[v] = reader.ReadUInt32();
		}
		ushort[] beachIndices = beachBytes != 0 ? ReadIndices(reader, reader.ReadInt32()) : Array.Empty<ushort>();
		int shores = reader.ReadInt32();
		byte[] shore = reader.ReadBytes(shores * 0x1C);
		if (shore.Length != shores * 0x1C)
		{
			throw new EndOfStreamException();
		}
		return new CQuadTile
		{
			Index = tile,
			LandPositions = positions,
			LandProvinces = provinces,
			LandIndices = indices.ToArray(),
			GroupStarts = starts,
			BorderIndices = borderIndices,
			BorderShorts = borderShorts,
			BorderColours = borderColours,
			BeachPositions = beachPositions,
			BeachColours = beachColours,
			BeachIndices = beachIndices,
			ShoreRecords = shore,
		};
	}

	private static ushort[] ReadIndices(BinaryReader reader, int bytes)
	{
		var indices = new ushort[bytes / 2];
		for (int i = 0; i < indices.Length; i++)
		{
			indices[i] = reader.ReadUInt16();
		}
		if (bytes % 2 != 0)
		{
			reader.ReadByte();
		}
		return indices;
	}
}
