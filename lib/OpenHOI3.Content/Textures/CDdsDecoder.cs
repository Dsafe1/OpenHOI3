using System;
using System.IO;

namespace OpenHOI3.Content.Textures;

/// <summary>
/// A DDS file's full-size picture decoded here, for the formats the client does not hand to Godot's own decoder: a block
/// compression whose size is not a whole number of blocks (<see cref="CDxtBlocks"/>), and an uncompressed picture of
/// fewer than 24 bits a pixel or an 8-bit palette (<see cref="CMaskedPixels"/>). A cube map, a volume and the extended
/// <c>DX10</c> header are not read (<c>docs/mechanics/interface.md</c>, section 5).
/// </summary>
public static class CDdsDecoder
{
	/// <summary>Whether this reads the format the header gives.</summary>
	public static bool Reads(CDdsHeader header)
	{
		return !header.IsCubeOrVolume && (header.IsBlockCompressed || CMaskedPixels.Reads(header));
	}

	/// <summary>The full-size picture of a DDS file, header first; an <see cref="InvalidDataException"/> says why where it cannot be.</summary>
	public static CDecodedImage Decode(ReadOnlySpan<byte> file, CDdsHeader header)
	{
		if (!Reads(header))
		{
			throw new InvalidDataException(header.IsCubeOrVolume
				? "a cube map or a volume"
				: $"pixel format {(header.FourCc.Length > 0 ? header.FourCc : $"{header.PixelFlags:x} of {header.RgbBitCount} bits")} is not one read here");
		}
		if (!header.IsBlockCompressed)
		{
			return CMaskedPixels.Decode(file, header);
		}
		int needed = CDxtBlocks.LevelBytes(header.Width, header.Height, header.BlockBytes);
		if (header.Width <= 0 || header.Height <= 0 || file.Length < CDdsHeader.Size + needed)
		{
			throw new InvalidDataException($"the file ends at {file.Length} bytes where its {header.FourCc} picture of "
				+ $"{header.Width} by {header.Height} needs {CDdsHeader.Size + needed}");
		}
		return CDxtBlocks.Decode(file[CDdsHeader.Size..], header.Width, header.Height, header.FourCc);
	}
}
