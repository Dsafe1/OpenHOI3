using System;
using System.Collections.Generic;
using System.IO;

namespace OpenHOI3.Content.Map.Cache;

/// <summary>
/// A tile's terrain-index picture: for every land pixel of its land provinces the texture-sheet entries of the pixel and of its left,
/// up-left and up neighbours, each offered to the eight texels around where they are still unset, written as the uncompressed
/// <c>.dds</c> D3DX would save. See <c>docs/mechanics/mapcache.md</c>, section 10.
/// </summary>
public sealed class CTerrainIndexTexture
{
	private const byte Unset = 0xFF;

	/// <summary>Where a pixel's entry has no <c>priority</c>, its value [exe].</summary>
	private const int MissingPriority = 2;

	private readonly byte[] _texels;

	private CTerrainIndexTexture(int width, int height, byte[] texels)
	{
		Width = width;
		Height = height;
		_texels = texels;
	}

	public int Width { get; }

	public int Height { get; }

	/// <summary>A tile's picture; the tile must have a land province.</summary>
	public static CTerrainIndexTexture Build(CMapCacheSource source, CProvincePixels pixels, IReadOnlyList<int> provinces)
	{
		int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
		foreach (int province in provinces)
		{
			if (source.IsLand(province))
			{
				(int bx0, int by0, int bx1, int by1) = pixels.Box(province);
				x0 = Math.Min(x0, bx0);
				y0 = Math.Min(y0, by0);
				x1 = Math.Max(x1, bx1);
				y1 = Math.Max(y1, by1);
			}
		}
		int width = x1 - x0 + 1 + 4;
		int height = y1 - y0 + 1 + 4;
		int offsetX = x0 - 2;
		int offsetY = y0 - 2;
		var texels = new byte[width * height * 4];
		Array.Fill(texels, Unset);
		int scale = 0x100 / source.PaletteSize;
		foreach (int province in provinces)
		{
			if (!source.IsLand(province))
			{
				continue;
			}
			foreach (int pixel in pixels.Of(province))
			{
				int x = pixels.X(pixel);
				int y = pixels.Y(pixel);
				int own = source.Terrain[pixel];
				if (source.WaterIndex[own])
				{
					continue;
				}
				int value = source.Priority[own];
				if (value < 0)
				{
					value = MissingPriority;
				}
				bool hasLeft = x - 1 > 0;
				bool hasUp = y + 1 < source.Height;
				int left = hasLeft ? Neighbour(source, pixel - 1, value) : value;
				int upLeft = hasLeft && hasUp ? Neighbour(source, pixel + source.Width - 1, value) : value;
				int up = hasUp ? Neighbour(source, pixel + source.Width, value) : value;
				int tx = Math.Clamp(x - offsetX, 0, width - 1);
				int ty = Math.Clamp(y - offsetY, 0, height - 1);
				Put(texels, width, height, tx, ty, 0, (byte)(scale * value));
				Put(texels, width, height, tx, ty, 1, (byte)(scale * upLeft));
				Put(texels, width, height, tx, ty, 2, (byte)(scale * left));
				Put(texels, width, height, tx, ty, 3, (byte)(scale * up));
			}
		}
		return new CTerrainIndexTexture(width, height, texels);
	}

	private static int Neighbour(CMapCacheSource source, int pixel, int own)
	{
		int index = source.Terrain[pixel];
		int value = source.Priority[index];
		return source.HasEntry[index] && value >= 0 ? value : own;
	}

	/// <summary>A byte written and offered to the eight around it; the first column and row are never offered to [exe].</summary>
	private static void Put(byte[] texels, int width, int height, int x, int y, int channel, byte value)
	{
		texels[(y * width + x) * 4 + channel] = value;
		bool north = y + 1 < height;
		bool south = y - 1 > 0;
		bool east = x + 1 < width;
		bool west = x - 1 > 0;
		if (north) { Offer(texels, width, x, y + 1, channel, value); }
		if (south) { Offer(texels, width, x, y - 1, channel, value); }
		if (east) { Offer(texels, width, x + 1, y, channel, value); }
		if (west) { Offer(texels, width, x - 1, y, channel, value); }
		if (east && north) { Offer(texels, width, x + 1, y + 1, channel, value); }
		if (east && south) { Offer(texels, width, x + 1, y - 1, channel, value); }
		if (west && north) { Offer(texels, width, x - 1, y + 1, channel, value); }
		if (west && south) { Offer(texels, width, x - 1, y - 1, channel, value); }
	}

	private static void Offer(byte[] texels, int width, int x, int y, int channel, byte value)
	{
		int at = (y * width + x) * 4 + channel;
		if (texels[at] == Unset)
		{
			texels[at] = value;
		}
	}

	/// <summary>The file: a 128-byte head as D3DX writes it for one level of A8R8G8B8, and the texels from the south row.</summary>
	public void Write(Stream stream)
	{
		var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
		writer.Write(System.Text.Encoding.ASCII.GetBytes("DDS "));
		foreach (int value in new[] { 124, 0x1007, Height, Width, 0, 0, 0 })
		{
			writer.Write(value);
		}
		writer.Write(new byte[44]);
		foreach (uint value in new uint[] { 32, 0x41, 0, 32, 0x00FF0000, 0x0000FF00, 0x000000FF, 0xFF000000, 0x1002, 0, 0, 0, 0 })
		{
			writer.Write(value);
		}
		writer.Write(_texels);
		writer.Flush();
	}
}
