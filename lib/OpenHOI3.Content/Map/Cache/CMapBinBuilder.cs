using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenHOI3.Content.Map.Cache;

/// <summary>
/// Makes <c>map.bin</c>: a head, the rivers' sources and a raster of (Width + 1) x (Height + 1) bytes that marks where the
/// province borders step diagonally, which the land mesh and the borders both cut by. A pixel's low four bits say which of its
/// corners is cut - 2 south-west, 8 south-east, 4 north-west, 1 north-east - and 0x20 and 0x10 mark the pixel across the cut, on
/// its west and on its east. See <c>docs/mechanics/mapcache.md</c>, section 4.
/// </summary>
public static class CMapBinBuilder
{
	public static byte[] Raster(CMapCacheSource source)
	{
		int width = source.Width;
		int height = source.Height;
		ushort[] p = source.Provinces;
		bool[] sea = source.IsSea;
		int stride = width + 1;
		var raster = new byte[stride * (height + 1)];
		// First the cuts between any two provinces alike in being sea or not...
		for (int y = 1; y < height - 1; y++)
		{
			for (int x = 1; x < width - 1; x++)
			{
				int at = y * width + x;
				int down = p[at - width];
				if (down == 0)
				{
					continue;
				}
				int c = p[at];
				if (sea[down] != sea[c])
				{
					continue;
				}
				int left = p[at - 1];
				int right = p[at + 1];
				int cell = y * stride + x;
				if (c != left && left == p[at - width - 1] && left == down)
				{
					// The whole byte, where the three cuts after it keep the high bits [exe].
					raster[cell] = 2;
					raster[cell - 1] |= 0x20;
				}
				if (c != down && down == p[at - width + 1] && down == right)
				{
					raster[cell] = (byte)(raster[cell] & 0xF0 | 8);
					raster[cell + 1] |= 0x10;
				}
			}
		}
		// ...then those between two land provinces, where the first pass left the pixel uncut.
		for (int y = 1; y < height - 1; y++)
		{
			for (int x = 1; x < width - 1; x++)
			{
				int at = y * width + x;
				int c = p[at];
				if (c == 0 || sea[c] || c != p[at - width])
				{
					continue;
				}
				int left = p[at - 1];
				int right = p[at + 1];
				int up = p[at + width];
				int cell = y * stride + x;
				if (c == right && c != left && left != 0 && !sea[left] && left == p[at + width - 1] && left == up
					&& (raster[cell] & 0xF) == 0 && (raster[cell + stride] & 10) == 0 && (raster[cell - 1] & 10) == 0)
				{
					raster[cell] = (byte)(raster[cell] & 0xF0 | 4);
					raster[cell - 1] |= 0x20;
				}
				if (c == left && c != right && right != 0 && !sea[right] && right == p[at + width + 1] && right == up
					&& (raster[cell] & 0xF) == 0 && (raster[cell + stride] & 10) == 0 && (raster[cell + 1] & 10) == 0)
				{
					raster[cell] = (byte)(raster[cell] & 0xF0 | 1);
					raster[cell + 1] |= 0x10;
				}
			}
		}
		return raster;
	}

	/// <summary>The file: the name in twelve bytes, two constants, the count of sources, each source and the raster.</summary>
	public static void Write(Stream stream, CMapCacheSource source, byte[] raster)
	{
		var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
		writer.Write(Encoding.ASCII.GetBytes("MAP_CACHE\0\0\0"));
		writer.Write(2);
		writer.Write(3);
		writer.Write(source.RiverSources.Count);
		foreach ((int x, int y) in source.RiverSources)
		{
			writer.Write(x);
			writer.Write(y);
		}
		writer.Write(MemoryMarshal.AsBytes(raster.AsSpan()));
		writer.Flush();
	}
}
