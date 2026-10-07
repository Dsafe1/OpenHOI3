using System;
using System.Buffers.Binary;
using System.IO;

namespace OpenHOI3.Content;

/// <summary>
/// An uncompressed Windows bitmap, held as the bytes of the file.
///
/// The map's rasters are this format and nothing else, so this reads exactly what they
/// use - 24 bits per pixel, or 8 with a palette - and refuses anything else rather than
/// guessing. Rows are handed out in the order the file stores them; which way up that
/// is belongs to the caller, because for the map it is the whole question. See
/// <c>docs/mechanics/map.md</c>, section 3.
/// </summary>
public sealed class CBitmapFile
{
	private const int FileHeaderSize = 14;
	private const int MinimumHeaderSize = FileHeaderSize + 40;

	private readonly byte[] _bytes;
	private readonly int _pixelOffset;
	private readonly int _stride;

	private CBitmapFile(byte[] bytes, int pixelOffset, int width, int height, int bitsPerPixel)
	{
		_bytes = bytes;
		_pixelOffset = pixelOffset;
		Width = width;
		Height = height;
		BitsPerPixel = bitsPerPixel;
		// Rows are padded to a multiple of four bytes.
		_stride = (width * (bitsPerPixel / 8) + 3) & ~3;
	}

	public int Width { get; }

	public int Height { get; }

	public int BitsPerPixel { get; }

	public int BytesPerPixel => BitsPerPixel / 8;

	public static CBitmapFile Read(string absolutePath)
	{
		return Parse(File.ReadAllBytes(absolutePath));
	}

	public static CBitmapFile Parse(byte[] bytes)
	{
		if (bytes.Length < MinimumHeaderSize || bytes[0] != (byte)'B' || bytes[1] != (byte)'M')
		{
			throw new InvalidDataException("not a Windows bitmap");
		}

		ReadOnlySpan<byte> span = bytes;
		int pixelOffset = BinaryPrimitives.ReadInt32LittleEndian(span[10..]);
		int width = BinaryPrimitives.ReadInt32LittleEndian(span[18..]);
		int height = BinaryPrimitives.ReadInt32LittleEndian(span[22..]);
		int bitsPerPixel = BinaryPrimitives.ReadUInt16LittleEndian(span[28..]);
		int compression = BinaryPrimitives.ReadInt32LittleEndian(span[30..]);

		if (compression != 0)
		{
			throw new InvalidDataException($"compressed bitmaps are not supported (compression {compression})");
		}
		if (bitsPerPixel != 24 && bitsPerPixel != 8)
		{
			throw new InvalidDataException($"only 24 and 8 bits per pixel are supported, not {bitsPerPixel}");
		}
		if (width <= 0 || height <= 0)
		{
			// A negative height is a top-down file. The map's are not, and treating one
			// as if it were would turn the world upside down without an error.
			throw new InvalidDataException($"unsupported dimensions {width} x {height}");
		}

		var bitmap = new CBitmapFile(bytes, pixelOffset, width, height, bitsPerPixel);
		long needed = (long)pixelOffset + (long)bitmap._stride * height;
		if (pixelOffset < MinimumHeaderSize || needed > bytes.Length)
		{
			throw new InvalidDataException(
				$"the file is {bytes.Length} bytes and {width} x {height} at {bitsPerPixel} bits needs {needed}");
		}
		return bitmap;
	}

	/// <summary>
	/// One row of pixel bytes without its padding, counted in the order the file stores
	/// them. At 24 bits the bytes are blue, green, red.
	/// </summary>
	public ReadOnlySpan<byte> StoredRow(int index)
	{
		return new ReadOnlySpan<byte>(_bytes, _pixelOffset + index * _stride, Width * BytesPerPixel);
	}
}
