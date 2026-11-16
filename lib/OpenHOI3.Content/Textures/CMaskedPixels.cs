using System;
using System.IO;
using System.Buffers.Binary;
using System.Numerics;

namespace OpenHOI3.Content.Textures;

/// <summary>
/// An uncompressed DDS's pixels, one to four bytes each, by the masks its pixel format gives - never by a guess at the
/// layout: BlackICE's 16-bit counters are 1555 with alpha and its cut-unit counters 4444, and the masks say which. A
/// channel of fewer than eight bits is widened by repeating its bits from the top, as Direct3D widens one - five bits
/// <c>abcde</c> become <c>abcdeabc</c>. Colour comes from the red, green and blue masks; grey from the red mask alone; a
/// format of alpha alone is black; alpha comes from its mask where the format says it has one, and is solid otherwise -
/// so an X1R5G5B5 or X4R4G4B4 picture is solid whatever its spare bits hold. An 8-bit palette's 256 entries follow the
/// header as red, green, blue and a fourth byte that Direct3D 9 takes for alpha (<c>docs/mechanics/interface.md</c>,
/// section 5). Rows are packed, a pixel's bytes after the last's, as D3DX writes them.
/// </summary>
public static class CMaskedPixels
{
	/// <summary>Whether the format is one this reads: masked colour, grey or alpha, or an 8-bit palette, of 8 to 32 bits.</summary>
	public static bool Reads(CDdsHeader header)
	{
		if ((header.PixelFlags & CDdsHeader.FourCcFlag) != 0 && header.FourCc.Trim('\0', ' ').Length > 0)
		{
			return false;
		}
		if ((header.PixelFlags & CDdsHeader.PaletteIndexed8) != 0)
		{
			return true;
		}
		bool masked = (header.PixelFlags & (CDdsHeader.Rgb | CDdsHeader.Luminance | CDdsHeader.Alpha)) != 0;
		return masked && header.RgbBitCount is 8 or 16 or 24 or 32;
	}

	/// <summary>The full-size level of an uncompressed DDS; the bytes are the whole file, header first.</summary>
	public static CDecodedImage Decode(ReadOnlySpan<byte> file, CDdsHeader header)
	{
		if (!Reads(header))
		{
			throw new InvalidDataException($"pixel format {header.PixelFlags:x} of {header.RgbBitCount} bits is not one read here");
		}
		int width = header.Width, height = header.Height;
		if (width <= 0 || height <= 0)
		{
			throw new InvalidDataException($"a picture of {width} by {height}");
		}
		bool palette = (header.PixelFlags & CDdsHeader.PaletteIndexed8) != 0;
		int bytesPerPixel = palette ? 1 : header.RgbBitCount / 8;
		int start = CDdsHeader.Size + (palette ? 1024 : 0);
		long needed = start + (long)width * height * bytesPerPixel;
		if (file.Length < needed)
		{
			throw new InvalidDataException($"the file ends at {file.Length} bytes where its picture of {width} by {height} needs {needed}");
		}
		var pixels = new byte[width * height * 4];
		ReadOnlySpan<byte> data = file[start..];
		if (palette)
		{
			ReadOnlySpan<byte> entries = file.Slice(CDdsHeader.Size, 1024);
			for (int i = 0; i < width * height; i++)
			{
				entries.Slice(data[i] * 4, 4).CopyTo(pixels.AsSpan(i * 4, 4));
			}
			return new CDecodedImage(width, height, pixels);
		}
		bool grey = (header.PixelFlags & CDdsHeader.Luminance) != 0;
		bool colour = !grey && (header.PixelFlags & CDdsHeader.Rgb) != 0;
		bool hasAlpha = (header.PixelFlags & (CDdsHeader.AlphaPixels | CDdsHeader.Alpha)) != 0 && header.AlphaMask != 0;
		var red = new CChannel(colour || grey ? header.RedMask : 0);
		var green = new CChannel(colour ? header.GreenMask : 0);
		var blue = new CChannel(colour ? header.BlueMask : 0);
		var alpha = new CChannel(hasAlpha ? header.AlphaMask : 0);
		for (int i = 0; i < width * height; i++)
		{
			uint value = bytesPerPixel switch
			{
				1 => data[i],
				2 => BinaryPrimitives.ReadUInt16LittleEndian(data[(i * 2)..]),
				3 => (uint)(data[i * 3] | data[i * 3 + 1] << 8 | data[i * 3 + 2] << 16),
				_ => BinaryPrimitives.ReadUInt32LittleEndian(data[(i * 4)..]),
			};
			byte r = red.Of(value);
			int o = i * 4;
			pixels[o] = r;
			pixels[o + 1] = grey ? r : green.Of(value);
			pixels[o + 2] = grey ? r : blue.Of(value);
			pixels[o + 3] = hasAlpha ? alpha.Of(value) : (byte)255;
		}
		return new CDecodedImage(width, height, pixels);
	}

	/// <summary>A value of so many bits widened to eight by repeating its bits from the top; the top eight of more.</summary>
	public static byte Widen(uint value, int bits)
	{
		if (bits <= 0)
		{
			return 0;
		}
		if (bits >= 8)
		{
			return (byte)(value >> (bits - 8));
		}
		int result = 0;
		for (int shift = 8 - bits; shift > -bits; shift -= bits)
		{
			result |= shift >= 0 ? (int)(value << shift) : (int)(value >> -shift);
		}
		return (byte)result;
	}

	/// <summary>One channel's mask: where its bits lie and how many there are.</summary>
	private readonly struct CChannel
	{
		private readonly uint _mask;
		private readonly int _shift;
		private readonly int _bits;

		public CChannel(uint mask)
		{
			_mask = mask;
			_shift = mask == 0 ? 0 : BitOperations.TrailingZeroCount(mask);
			_bits = BitOperations.PopCount(mask);
		}

		public byte Of(uint value) => _mask == 0 ? (byte)0 : Widen((value & _mask) >> _shift, _bits);
	}
}
