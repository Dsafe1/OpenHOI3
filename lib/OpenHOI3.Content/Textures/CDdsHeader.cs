using System;
using System.Buffers.Binary;
using System.Text;

namespace OpenHOI3.Content.Textures;

/// <summary>
/// The 128 bytes a DDS file begins with: the mark, the size, the chain of smaller copies, and the pixel format - a block
/// compression's four letters, or a count of bits a pixel with a mask for each channel. Read whole, so that which decoder
/// takes a file is decided from it before any reads the pixels (<c>docs/mechanics/interface.md</c>, section 5).
/// </summary>
public sealed class CDdsHeader
{
	public const int Size = 128;

	/// <summary>The pixel format's flags: a channel of alpha beside the colour.</summary>
	public const uint AlphaPixels = 0x1;

	/// <summary>Alpha alone.</summary>
	public const uint Alpha = 0x2;

	/// <summary>The four letters of a block compression say the format, and no mask does.</summary>
	public const uint FourCcFlag = 0x4;

	/// <summary>A byte a pixel, an index into a palette of 256 entries that follows the header.</summary>
	public const uint PaletteIndexed8 = 0x20;

	/// <summary>Colour by the red, green and blue masks.</summary>
	public const uint Rgb = 0x40;

	/// <summary>Grey, by the red mask.</summary>
	public const uint Luminance = 0x20000;

	public int Width { get; private init; }
	public int Height { get; private init; }

	/// <summary>The header's flags (<c>dwFlags</c>).</summary>
	public uint Flags { get; private init; }

	/// <summary>The number of levels in the chain of smaller copies, the full size among them; 0 and 1 both mean one.</summary>
	public int MipMapCount { get; private init; }

	/// <summary>The pixel format's flags: <see cref="Rgb"/>, <see cref="FourCcFlag"/> and the rest.</summary>
	public uint PixelFlags { get; private init; }

	/// <summary>The four letters of a block compression - <c>DXT1</c> - or empty where <see cref="FourCcFlag"/> is not set.</summary>
	public string FourCc { get; private init; } = "";

	public int RgbBitCount { get; private init; }
	public uint RedMask { get; private init; }
	public uint GreenMask { get; private init; }
	public uint BlueMask { get; private init; }
	public uint AlphaMask { get; private init; }

	/// <summary>The second caps word: a cube map or a volume.</summary>
	public uint Caps2 { get; private init; }

	/// <summary>Whether the file stores the smaller copies that make up a chain, as more than one level.</summary>
	public bool HasMipmaps => MipMapCount > 1;

	/// <summary>The bytes of one 4 by 4 block for a block compression this reads, DXT1 to DXT5; 0 for any other format.</summary>
	public int BlockBytes => FourCc switch
	{
		"DXT1" => 8,
		"DXT2" or "DXT3" or "DXT4" or "DXT5" => 16,
		_ => 0,
	};

	/// <summary>Whether the format is a block compression, DXT1 to DXT5.</summary>
	public bool IsBlockCompressed => BlockBytes > 0;

	/// <summary>Whether the width and the height are each a whole number of blocks - which the DDS format asks and not every file keeps.</summary>
	public bool IsWholeBlocks => Width % 4 == 0 && Height % 4 == 0;

	/// <summary>Whether the file is a cube map or a volume, which nothing here reads.</summary>
	public bool IsCubeOrVolume => (Caps2 & (0x200u | 0x200000u)) != 0;

	/// <summary>The header of a DDS file, or null where the bytes are not one or end before the header does.</summary>
	public static CDdsHeader? Parse(ReadOnlySpan<byte> bytes)
	{
		if (bytes.Length < Size || CTextureSniffer.Sniff(bytes[..4]) != ETextureFormat.Dds)
		{
			return null;
		}
		uint pixelFlags = BinaryPrimitives.ReadUInt32LittleEndian(bytes[80..]);
		return new CDdsHeader
		{
			Flags = BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]),
			Height = BinaryPrimitives.ReadInt32LittleEndian(bytes[12..]),
			Width = BinaryPrimitives.ReadInt32LittleEndian(bytes[16..]),
			MipMapCount = BinaryPrimitives.ReadInt32LittleEndian(bytes[28..]),
			PixelFlags = pixelFlags,
			FourCc = (pixelFlags & FourCcFlag) != 0 ? Encoding.ASCII.GetString(bytes.Slice(84, 4)) : "",
			RgbBitCount = BinaryPrimitives.ReadInt32LittleEndian(bytes[88..]),
			RedMask = BinaryPrimitives.ReadUInt32LittleEndian(bytes[92..]),
			GreenMask = BinaryPrimitives.ReadUInt32LittleEndian(bytes[96..]),
			BlueMask = BinaryPrimitives.ReadUInt32LittleEndian(bytes[100..]),
			AlphaMask = BinaryPrimitives.ReadUInt32LittleEndian(bytes[104..]),
			Caps2 = BinaryPrimitives.ReadUInt32LittleEndian(bytes[112..]),
		};
	}
}
