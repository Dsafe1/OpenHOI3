using System;
using System.IO;
using System.Text;

namespace OpenHOI3.Tests.Content;

/// <summary>Builds the bytes of a DDS or a TGA file, so no test needs one from an install.</summary>
internal static class TestDds
{
	/// <summary>A DDS header for a block compression, its blocks after it.</summary>
	public static byte[] Compressed(string fourCc, int width, int height, byte[] blocks, int mipMapCount = 0)
	{
		return File(width, height, mipMapCount, 0x4, fourCc, 0, 0, 0, 0, 0, blocks);
	}

	/// <summary>A DDS header for an uncompressed format by its masks, its pixels (and a palette, if any) after it.</summary>
	public static byte[] Masked(int width, int height, uint pixelFlags, int bits, uint red, uint green, uint blue, uint alpha,
		byte[] data)
	{
		return File(width, height, 0, pixelFlags, "", bits, red, green, blue, alpha, data);
	}

	private static byte[] File(int width, int height, int mipMapCount, uint pixelFlags, string fourCc, int bits, uint red,
		uint green, uint blue, uint alpha, byte[] data)
	{
		var bytes = new byte[128 + data.Length];
		using var writer = new BinaryWriter(new MemoryStream(bytes));
		writer.Write(Encoding.ASCII.GetBytes("DDS "));
		writer.Write(124);
		writer.Write(0x1007 | (mipMapCount > 1 ? 0x20000 : 0));   // caps, height, width, pixel format
		writer.Write(height);
		writer.Write(width);
		writer.Write(0);                                          // pitch or linear size
		writer.Write(0);                                          // depth
		writer.Write(mipMapCount);
		writer.Write(new byte[44]);
		writer.Write(32);                                         // the pixel format's size
		writer.Write(pixelFlags);
		writer.Write(Encoding.ASCII.GetBytes(fourCc.PadRight(4, '\0')));
		writer.Write(bits);
		writer.Write(red);
		writer.Write(green);
		writer.Write(blue);
		writer.Write(alpha);
		writer.Write(0x1000);                                     // caps: a texture
		writer.Write(new byte[16]);
		writer.Write(data);
		return bytes;
	}

	/// <summary>A TGA header and what follows it - the colour map, then the pixels as stored.</summary>
	public static byte[] Tga(int imageType, int width, int height, int pixelBits, int descriptor, byte[] body,
		int colourMapLength = 0, int colourMapBits = 0, int colourMapFirst = 0)
	{
		var bytes = new byte[18 + body.Length];
		bytes[1] = (byte)(colourMapLength > 0 ? 1 : 0);
		bytes[2] = (byte)imageType;
		BitConverter.GetBytes((ushort)colourMapFirst).CopyTo(bytes, 3);
		BitConverter.GetBytes((ushort)colourMapLength).CopyTo(bytes, 5);
		bytes[7] = (byte)colourMapBits;
		BitConverter.GetBytes((ushort)width).CopyTo(bytes, 12);
		BitConverter.GetBytes((ushort)height).CopyTo(bytes, 14);
		bytes[16] = (byte)pixelBits;
		bytes[17] = (byte)descriptor;
		body.CopyTo(bytes, 18);
		return bytes;
	}

	/// <summary>The pixel at (x, y) of a decoded picture, as red, green, blue, alpha.</summary>
	public static (byte R, byte G, byte B, byte A) Pixel(OpenHOI3.Content.Textures.CDecodedImage image, int x, int y)
	{
		int o = (y * image.Width + x) * 4;
		return (image.Rgba[o], image.Rgba[o + 1], image.Rgba[o + 2], image.Rgba[o + 3]);
	}
}
