using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Content.Map.Cache;

/// <summary>
/// The rest of the map cache, built for compatibility with the original and read by nothing here: figures the game works out for itself,
/// written as the original writes them so that the folder is whole - the ways between provinces, the distances by sea, each province's climate, the province raster and the vertex declarations. See
/// <c>docs/mechanics/mapcache.md</c>, section 12.
/// </summary>
public static class CMapDataFiles
{
	public const string AdjacenciesFile = "adjacencies.bin";
	public const string NavalFile = "navaldist.bin";
	public const string ClimateFile = "climate.bin";
	public const string ProvincesFile = "provincecache.bin";
	public const string FormatFile = "quad_format.bin";

	/// <summary>For each province from id 0, the count of its ways and each as five numbers: kind, to, through, distance and bearing.</summary>
	public static void WriteAdjacencies(Stream stream, IReadOnlyList<CProvinceEdge[]> edges)
	{
		using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
		foreach (CProvinceEdge[] ways in edges)
		{
			writer.Write(ways.Length);
			foreach (CProvinceEdge way in ways)
			{
				writer.Write((int)way.Kind);
				writer.Write(way.To);
				writer.Write(way.Through);
				writer.Write(checked((int)way.Distance.Raw));
				writer.Write(way.Bearing);
			}
		}
	}

	/// <summary>
	/// The upper triangle of the distances by sea, the diagonal in it, row by row in sixteen bits. Rows are measured side by side: the whole
	/// table is some twenty seconds' work one after another.
	/// </summary>
	public static byte[] NavalTable(CMap map)
	{
		var provinces = new CProvince[map.ProvinceCount];
		for (int id = 0; id < provinces.Length; id++)
		{
			provinces[id] = new CProvince(id, "", map.IsSea(id))
			{
				Edges = map.Edges[id],
				CentreX = map.Shapes[id].IsEmpty ? 0 : map.Shapes[id].CentreX,
				CentreY = map.Shapes[id].IsEmpty ? 0 : map.Shapes[id].CentreY,
				PortSea = map.PortSea[id],
				SeaIndex = map.SeaIndex[id],
			};
		}
		int count = map.SeaIndexCount;
		var distances = new CNavalDistances(provinces, map.Width);
		var rows = new ushort[count][];
		Parallel.For(0, count, from => rows[from] = distances.Row(from));
		var cells = new ushort[count * (count + 1) / 2];
		int at = 0;
		for (int from = 0; from < count; from++)
		{
			Array.Copy(rows[from], from, cells, at, count - from);
			at += count - from;
		}
		return MemoryMarshal.AsBytes(cells.AsSpan()).ToArray();
	}

	/// <summary>Each province's climate index in 32 bits, from id 0.</summary>
	public static void WriteClimate(Stream stream, IReadOnlyList<byte> climate)
	{
		using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
		foreach (byte index in climate)
		{
			writer.Write((int)index);
		}
	}

	/// <summary>
	/// The province raster as a 16-bit bitmap: the rows from the north, though a positive height says otherwise, and the head's file size
	/// the original's own slip, the width and three heights and 110 where a size in bytes belongs [exe].
	/// </summary>
	public static void WriteProvinceRaster(Stream stream, int width, int height, ushort[] provinces)
	{
		const int PixelsPerMetre = 0xB12;
		const int HeadBytes = 0x36;
		using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
		writer.Write((byte)'B');
		writer.Write((byte)'M');
		writer.Write(width + 3 * height + 110);
		writer.Write(0);
		writer.Write(HeadBytes);
		writer.Write(40);
		writer.Write(width);
		writer.Write(height);
		writer.Write((short)1);
		writer.Write((short)16);
		writer.Write(0);
		writer.Write(0);
		writer.Write(PixelsPerMetre);
		writer.Write(PixelsPerMetre);
		writer.Write(0);
		writer.Write(0);
		for (int y = height - 1; y >= 0; y--)
		{
			writer.Write(MemoryMarshal.AsBytes(provinces.AsSpan(y * width, width)));
		}
	}

	/// <summary>
	/// The two vertex declarations the cache's meshes are made for - each element its method, offset, stream, type, usage and usage index, a
	/// list closed by the end marker - with one more number after them (<c>docs/mechanics/mapdraw.md</c>, section 2).
	/// </summary>
	public static void WriteQuadFormat(Stream stream)
	{
		const byte Short2 = 6;
		const byte Float2 = 1;
		const byte Colour = 4;
		const byte Unused = 0x11;
		const byte Position = 0;
		const byte TextureCoordinate = 5;
		const byte ColourUsage = 10;
		const short EndStream = 0xFF;
		using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
		void Element(short offset, short streamNumber, byte type, byte usage)
		{
			writer.Write((byte)0);
			writer.Write(offset);
			writer.Write(streamNumber);
			writer.Write(type);
			writer.Write(usage);
			writer.Write((byte)0);
		}
		writer.Write(3);
		Element(0, 0, Short2, Position);
		Element(4, 0, Short2, TextureCoordinate);
		Element(0, EndStream, Unused, Position);
		writer.Write(4);
		Element(0, 0, Float2, Position);
		Element(8, 0, Colour, ColourUsage);
		Element(0, EndStream, Unused, Position);
		// The beaches' list is counted one too long and so has an element past its end marker: as the file holds it.
		Element(0, 0, 3, Position);
		writer.Write(1);
	}
}
