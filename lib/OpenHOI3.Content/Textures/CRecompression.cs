using System;

namespace OpenHOI3.Content.Textures;

/// <summary>
/// What the original's device makes of a compressed picture whose size is not a power of two: its texture handler loads
/// with the size rounded up to powers of two and no filter (<c>reversing/findings/FINDINGS-mapdraw.md</c>, A.3), so D3DX
/// compresses the picture afresh into a texture of the new size, and each texel comes out as the nearest colour of five,
/// six and five bits - *inferred*, from the maintainer's screenshot of 2026-11-06 (<c>docs/mechanics/mapdraw.md</c>,
/// section 6). The alpha is left as it is.
/// </summary>
public static class CRecompression
{
	public static bool PowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;

	/// <summary>Whether a compressed picture of this size is compressed afresh: one whose width or height is not a power of two.</summary>
	public static bool Applies(int width, int height) => !(PowerOfTwo(width) && PowerOfTwo(height));

	/// <summary>Every pixel of four bytes - red, green, blue, alpha - set to its nearest colour of five, six and five bits.</summary>
	public static void ToNearest565(Span<byte> rgba)
	{
		for (int at = 0; at + 3 < rgba.Length; at += 4)
		{
			rgba[at] = Nearest(rgba[at], 5);
			rgba[at + 1] = Nearest(rgba[at + 1], 6);
			rgba[at + 2] = Nearest(rgba[at + 2], 5);
		}
	}

	/// <summary>A channel to the nearest value of so many bits, widened again by repeating its high bits, as a decoder widens it.</summary>
	public static byte Nearest(byte value, int bits)
	{
		int levels = (1 << bits) - 1;
		int q = (value * levels + 127) / 255;
		return (byte)(q << (8 - bits) | q >> (2 * bits - 8));
	}
}
