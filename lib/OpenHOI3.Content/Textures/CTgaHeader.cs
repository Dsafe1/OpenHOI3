using System;
using System.Buffers.Binary;

namespace OpenHOI3.Content.Textures;

/// <summary>
/// The 18 bytes a TGA file begins with. A TGA has no mark of its own, so a header is taken for one only where every field
/// holds a value a TGA can have - which is also how a file is told to be a TGA at all (<see cref="CTextureSniffer"/>).
/// </summary>
public sealed class CTgaHeader
{
	public const int Size = 18;

	public int IdLength { get; private init; }

	/// <summary>Whether a colour map follows the header.</summary>
	public bool HasColourMap { get; private init; }

	/// <summary>1 colour-mapped, 2 true colour, 3 grey; 9, 10 and 11 the same run-length encoded.</summary>
	public int ImageType { get; private init; }

	public int ColourMapFirst { get; private init; }
	public int ColourMapLength { get; private init; }

	/// <summary>The bits of one colour map entry: 15, 16, 24 or 32.</summary>
	public int ColourMapEntryBits { get; private init; }

	public int Width { get; private init; }
	public int Height { get; private init; }

	/// <summary>The bits of a pixel: 8, 15, 16, 24 or 32.</summary>
	public int PixelBits { get; private init; }

	/// <summary>The descriptor: the alpha bits in the low four, the rows from the top at 0x20, right to left at 0x10.</summary>
	public int Descriptor { get; private init; }

	public bool IsRunLength => ImageType >= 9;
	public bool IsColourMapped => ImageType is 1 or 9;
	public bool IsGrey => ImageType is 3 or 11;
	public bool TopToBottom => (Descriptor & 0x20) != 0;
	public bool RightToLeft => (Descriptor & 0x10) != 0;

	/// <summary>The bits of alpha the descriptor says each pixel holds.</summary>
	public int AlphaBits => Descriptor & 0xF;

	/// <summary>Where the colour map begins, after the header and the image's id.</summary>
	public int ColourMapOffset => Size + IdLength;

	/// <summary>Where the pixels begin, after the colour map.</summary>
	public int PixelOffset => ColourMapOffset + (HasColourMap ? ColourMapLength * ((ColourMapEntryBits + 7) / 8) : 0);

	/// <summary>The header of a TGA file, or null where the bytes cannot be one.</summary>
	public static CTgaHeader? Parse(ReadOnlySpan<byte> bytes)
	{
		if (bytes.Length < Size)
		{
			return null;
		}
		var header = new CTgaHeader
		{
			IdLength = bytes[0],
			HasColourMap = bytes[1] == 1,
			ImageType = bytes[2],
			ColourMapFirst = BinaryPrimitives.ReadUInt16LittleEndian(bytes[3..]),
			ColourMapLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes[5..]),
			ColourMapEntryBits = bytes[7],
			Width = BinaryPrimitives.ReadUInt16LittleEndian(bytes[12..]),
			Height = BinaryPrimitives.ReadUInt16LittleEndian(bytes[14..]),
			PixelBits = bytes[16],
			Descriptor = bytes[17],
		};
		bool known = bytes[1] is 0 or 1
			&& header.ImageType is 1 or 2 or 3 or 9 or 10 or 11
			&& header.Width > 0 && header.Height > 0
			&& header.PixelBits is 8 or 15 or 16 or 24 or 32
			&& (header.Descriptor & 0xC0) == 0
			&& (!header.IsColourMapped || header.HasColourMap && header.ColourMapEntryBits is 15 or 16 or 24 or 32
				&& header.PixelBits is 8 or 16)
			&& (!header.HasColourMap || header.IsColourMapped || header.ColourMapEntryBits is 15 or 16 or 24 or 32);
		return known ? header : null;
	}
}
