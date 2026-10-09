using System;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// A picture as plain numbers: four bytes a pixel - red, green, blue and how solid -
/// row by row from the top. What <see cref="CCounterPainter"/> reads and writes, so that
/// a counter can be put together and checked with no engine.
/// </summary>
public sealed class CPixels
{
	public CPixels(int width, int height, byte[]? rgba = null)
	{
		if (width <= 0 || height <= 0)
		{
			throw new ArgumentException("a picture has a width and a height");
		}
		Width = width;
		Height = height;
		Rgba = rgba ?? new byte[width * height * 4];
		if (Rgba.Length != width * height * 4)
		{
			throw new ArgumentException("four bytes for every pixel");
		}
	}

	public int Width { get; }

	public int Height { get; }

	public byte[] Rgba { get; }

	/// <summary>
	/// The colour at a point given as fractions of the width and the height, each
	/// component from 0 to 1, taken from the pixel the point falls in. A point off the
	/// picture takes the nearest edge's.
	/// </summary>
	public (float R, float G, float B, float A) At(float u, float v)
	{
		int x = Math.Clamp((int)(u * Width), 0, Width - 1);
		int y = Math.Clamp((int)(v * Height), 0, Height - 1);
		int at = (y * Width + x) * 4;
		return (Rgba[at] / 255f, Rgba[at + 1] / 255f, Rgba[at + 2] / 255f, Rgba[at + 3] / 255f);
	}

	public void Set(int x, int y, float r, float g, float b, float a)
	{
		int at = (y * Width + x) * 4;
		Rgba[at] = Byte(r);
		Rgba[at + 1] = Byte(g);
		Rgba[at + 2] = Byte(b);
		Rgba[at + 3] = Byte(a);
	}

	private static byte Byte(float value)
	{
		return (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
	}
}
