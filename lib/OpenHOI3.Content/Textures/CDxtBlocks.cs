using System;
using System.IO;
using System.Buffers.Binary;

namespace OpenHOI3.Content.Textures;

/// <summary>
/// The block compressions DXT1 to DXT5 decoded as Direct3D decodes them. Each 4 by 4 block holds two colours of five,
/// six and five bits and a two-bit code a texel choosing between them and the colours a third and two thirds of the way;
/// in DXT1 a block whose first colour is not above its second has the half-way colour and see-through black instead.
/// DXT2 and DXT3 put sixteen alphas of four bits before it, DXT4 and DXT5 two alphas and a three-bit code a texel between
/// them and the six or four steps between. A texel past the picture's edge, in a block that runs over it, is left out -
/// which is what reads a picture whose width or height is not a whole number of blocks (<c>docs/mechanics/interface.md</c>,
/// section 5). A colour's widening and the thirds are the arithmetic the map's see-through DXT1 was decoded by first
/// (<c>docs/mechanics/mapdraw.md</c>, section 6), and are kept so.
/// </summary>
public static class CDxtBlocks
{
	/// <summary>The bytes of a picture's full-size level, its blocks rounded up at the edges.</summary>
	public static int LevelBytes(int width, int height, int blockBytes)
	{
		return Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * blockBytes;
	}

	/// <summary>Whether any DXT1 block has a texel coded see-through - a block of three colours whose texel takes the fourth.</summary>
	public static bool HasSeeThroughTexels(ReadOnlySpan<byte> blocks, int width, int height)
	{
		int count = LevelBytes(width, height, 8) / 8;
		for (int block = 0; block < count; block++)
		{
			ReadOnlySpan<byte> at = blocks.Slice(block * 8, 8);
			if (BinaryPrimitives.ReadUInt16LittleEndian(at) > BinaryPrimitives.ReadUInt16LittleEndian(at[2..]))
			{
				continue;
			}
			uint codes = BinaryPrimitives.ReadUInt32LittleEndian(at[4..]);
			for (int texel = 0; texel < 16; texel++)
			{
				if ((codes >> (2 * texel) & 3) == 3)
				{
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>
	/// The full-size level of a picture in the format whose four letters are given, from the first block; the blocks must
	/// cover the picture (<see cref="LevelBytes"/>).
	/// </summary>
	public static CDecodedImage Decode(ReadOnlySpan<byte> blocks, int width, int height, string fourCc)
	{
		int blockBytes = fourCc switch
		{
			"DXT1" => 8,
			"DXT2" or "DXT3" or "DXT4" or "DXT5" => 16,
			_ => throw new InvalidDataException($"'{fourCc}' is not a block compression read here"),
		};
		if (width <= 0 || height <= 0)
		{
			throw new InvalidDataException($"a picture of {width} by {height}");
		}
		if (blocks.Length < LevelBytes(width, height, blockBytes))
		{
			throw new InvalidDataException($"{blocks.Length} bytes of blocks for a picture of {width} by {height}");
		}
		bool explicitAlpha = fourCc is "DXT2" or "DXT3";
		bool interpolatedAlpha = fourCc is "DXT4" or "DXT5";
		int across = Math.Max(1, (width + 3) / 4), down = Math.Max(1, (height + 3) / 4);
		var pixels = new byte[width * height * 4];
		Span<uint> palette = stackalloc uint[4];
		Span<byte> alphas = stackalloc byte[16];
		for (int by = 0; by < down; by++)
		{
			for (int bx = 0; bx < across; bx++)
			{
				ReadOnlySpan<byte> block = blocks.Slice((by * across + bx) * blockBytes, blockBytes);
				if (explicitAlpha)
				{
					ulong bits = BinaryPrimitives.ReadUInt64LittleEndian(block);
					for (int texel = 0; texel < 16; texel++)
					{
						alphas[texel] = (byte)((bits >> (4 * texel) & 0xF) * 17);
					}
				}
				else if (interpolatedAlpha)
				{
					InterpolatedAlphas(block, alphas);
				}
				ReadOnlySpan<byte> colour = blockBytes == 16 ? block[8..] : block;
				Palette(colour, palette, threeColourMode: blockBytes == 8);
				uint codes = BinaryPrimitives.ReadUInt32LittleEndian(colour[4..]);
				for (int texel = 0; texel < 16; texel++)
				{
					int x = bx * 4 + texel % 4, y = by * 4 + texel / 4;
					if (x >= width || y >= height)
					{
						continue;
					}
					uint value = palette[(int)(codes >> (2 * texel) & 3)];
					int o = (y * width + x) * 4;
					pixels[o] = (byte)value;
					pixels[o + 1] = (byte)(value >> 8);
					pixels[o + 2] = (byte)(value >> 16);
					pixels[o + 3] = blockBytes == 16 ? alphas[texel] : (byte)(value >> 24);
				}
			}
		}
		return new CDecodedImage(width, height, pixels);
	}

	/// <summary>A colour block's four colours, each red, green, blue and alpha from the low byte up.</summary>
	private static void Palette(ReadOnlySpan<byte> colour, Span<uint> palette, bool threeColourMode)
	{
		ushort c0 = BinaryPrimitives.ReadUInt16LittleEndian(colour), c1 = BinaryPrimitives.ReadUInt16LittleEndian(colour[2..]);
		(int r0, int g0, int b0) = Expand(c0);
		(int r1, int g1, int b1) = Expand(c1);
		palette[0] = Pack(r0, g0, b0, 255);
		palette[1] = Pack(r1, g1, b1, 255);
		if (c0 > c1 || !threeColourMode)
		{
			palette[2] = Pack((2 * r0 + r1) / 3, (2 * g0 + g1) / 3, (2 * b0 + b1) / 3, 255);
			palette[3] = Pack((r0 + 2 * r1) / 3, (g0 + 2 * g1) / 3, (b0 + 2 * b1) / 3, 255);
		}
		else
		{
			palette[2] = Pack((r0 + r1) / 2, (g0 + g1) / 2, (b0 + b1) / 2, 255);
			palette[3] = 0;
		}
	}

	/// <summary>DXT5's alphas: two of eight bits, then a three-bit code a texel for them and the steps between.</summary>
	private static void InterpolatedAlphas(ReadOnlySpan<byte> block, Span<byte> alphas)
	{
		int a0 = block[0], a1 = block[1];
		Span<int> steps = stackalloc int[8];
		steps[0] = a0;
		steps[1] = a1;
		if (a0 > a1)
		{
			for (int i = 1; i <= 6; i++)
			{
				steps[i + 1] = ((7 - i) * a0 + i * a1) / 7;
			}
		}
		else
		{
			for (int i = 1; i <= 4; i++)
			{
				steps[i + 1] = ((5 - i) * a0 + i * a1) / 5;
			}
			steps[6] = 0;
			steps[7] = 255;
		}
		ulong bits = 0;
		for (int i = 0; i < 6; i++)
		{
			bits |= (ulong)block[2 + i] << (8 * i);
		}
		for (int texel = 0; texel < 16; texel++)
		{
			alphas[texel] = (byte)steps[(int)(bits >> (3 * texel) & 7)];
		}
	}

	private static (int R, int G, int B) Expand(ushort c)
	{
		int r = c >> 11 & 31, g = c >> 5 & 63, b = c & 31;
		return (r << 3 | r >> 2, g << 2 | g >> 4, b << 3 | b >> 2);
	}

	private static uint Pack(int r, int g, int b, int a) => (uint)(r | g << 8 | b << 16 | a << 24);
}
