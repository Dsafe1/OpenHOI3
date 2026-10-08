using System.IO;

namespace OpenHOI3.Tests.Content;

/// <summary>Builds the bytes of an uncompressed bitmap, so no test needs one from an install.</summary>
internal static class TestBitmap
{
	/// <param name="storedRows">Rows in the order the file stores them, each pixel as red, green, blue.</param>
	public static byte[] Rgb24(int width, (byte R, byte G, byte B)[][] storedRows)
	{
		int stride = (width * 3 + 3) & ~3;
		int height = storedRows.Length;
		var bytes = new byte[54 + stride * height];
		using var writer = new BinaryWriter(new MemoryStream(bytes));
		writer.Write((byte)'B');
		writer.Write((byte)'M');
		writer.Write(bytes.Length);
		writer.Write(0);
		writer.Write(54);                                     // where the pixels start
		writer.Write(40);                                     // info header size
		writer.Write(width);
		writer.Write(height);
		writer.Write((ushort)1);
		writer.Write((ushort)24);
		writer.Write(0);                                      // no compression

		for (int row = 0; row < height; row++)
		{
			for (int x = 0; x < width; x++)
			{
				int at = 54 + row * stride + x * 3;
				bytes[at] = storedRows[row][x].B;
				bytes[at + 1] = storedRows[row][x].G;
				bytes[at + 2] = storedRows[row][x].R;
			}
		}
		return bytes;
	}

	/// <param name="storedRows">Rows in the order the file stores them, each pixel a palette index.</param>
	public static byte[] Indexed8(int width, byte[][] storedRows)
	{
		const int pixels = 54 + 256 * 4;                       // the palette sits between header and pixels
		int stride = (width + 3) & ~3;
		int height = storedRows.Length;
		var bytes = new byte[pixels + stride * height];
		using var writer = new BinaryWriter(new MemoryStream(bytes));
		writer.Write((byte)'B');
		writer.Write((byte)'M');
		writer.Write(bytes.Length);
		writer.Write(0);
		writer.Write(pixels);
		writer.Write(40);
		writer.Write(width);
		writer.Write(height);
		writer.Write((ushort)1);
		writer.Write((ushort)8);
		writer.Write(0);

		for (int row = 0; row < height; row++)
		{
			storedRows[row].CopyTo(bytes, pixels + row * stride);
		}
		return bytes;
	}
}
