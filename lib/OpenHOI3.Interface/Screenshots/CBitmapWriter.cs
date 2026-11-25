using System;

namespace OpenHOI3.Interface.Screenshots;

/// <summary>
/// A picture as a Windows bitmap of 24 bits, the format of the original's two pictures
/// (<c>docs/mechanics/screenshots.md</c>, section 4): a head of 14 bytes and one of 40, then the rows from the
/// bottom up, blue, green, red, each padded to a multiple of four bytes. The resolution is the original's 2834 a
/// metre both ways. The size in the head is the file's size in bytes, where the original's own writer puts a figure
/// that is not - ours.
/// </summary>
public static class CBitmapWriter
{
	private const int FileHead = 14;
	private const int InfoHead = 40;
	private const int PixelsPerMetre = 0xB12;

	/// <summary>The file's bytes of a picture given as rows from the top down, three bytes a pixel: blue, green, red.</summary>
	public static byte[] Write(int width, int height, ReadOnlySpan<byte> topDownBgr)
	{
		if (width <= 0 || height <= 0 || topDownBgr.Length != width * height * 3)
		{
			throw new ArgumentException("The pixels are not the picture's size.", nameof(topDownBgr));
		}
		int row = width * 3;
		int stride = (row + 3) & ~3;
		var file = new byte[FileHead + InfoHead + stride * height];
		file[0] = (byte)'B';
		file[1] = (byte)'M';
		Put(file, 2, file.Length);
		Put(file, 10, FileHead + InfoHead);
		Put(file, 14, InfoHead);
		Put(file, 18, width);
		Put(file, 22, height);
		file[26] = 1;
		file[28] = 24;
		Put(file, 38, PixelsPerMetre);
		Put(file, 42, PixelsPerMetre);
		for (int y = 0; y < height; y++)
		{
			topDownBgr.Slice((height - 1 - y) * row, row).CopyTo(file.AsSpan(FileHead + InfoHead + y * stride, row));
		}
		return file;
	}

	private static void Put(byte[] file, int at, int value)
	{
		file[at] = (byte)value;
		file[at + 1] = (byte)(value >> 8);
		file[at + 2] = (byte)(value >> 16);
		file[at + 3] = (byte)(value >> 24);
	}
}
