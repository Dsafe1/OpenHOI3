using System;

namespace OpenHOI3.Content.Textures;

/// <summary>
/// What a picture's file holds, by its first bytes, whatever its name says. The original loads its textures through D3DX,
/// which goes by a file's content: BlackICE has TGAs named <c>.dds</c> and JPEGs named <c>.tga</c>, and the original draws
/// both (<c>docs/mechanics/interface.md</c>, section 5). A DDS, a PNG, a JPEG and a BMP each begin with a mark of their
/// own; a TGA has none, so it is what is left when its header is one a TGA can have.
/// </summary>
public static class CTextureSniffer
{
	public static ETextureFormat Sniff(ReadOnlySpan<byte> bytes)
	{
		if (bytes.Length >= 4 && bytes[0] == 'D' && bytes[1] == 'D' && bytes[2] == 'S' && bytes[3] == ' ')
		{
			return ETextureFormat.Dds;
		}
		if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A }))
		{
			return ETextureFormat.Png;
		}
		if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
		{
			return ETextureFormat.Jpeg;
		}
		if (bytes.Length >= 26 && bytes[0] == 'B' && bytes[1] == 'M')
		{
			return ETextureFormat.Bmp;
		}
		return CTgaHeader.Parse(bytes) is not null ? ETextureFormat.Tga : ETextureFormat.Unknown;
	}
}
