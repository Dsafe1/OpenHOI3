using System;
using OpenHOI3.Sim;

namespace OpenHOI3.Content.Gui;

/// <summary>
/// Paints a built-in element's picture: a rectangle in a style, as rows of red, green, blue and alpha bytes from the
/// top left. See <c>docs/mechanics/builtin.md</c>, section 4.
/// </summary>
public static class CBuiltInPainter
{
	/// <summary>How much darker than its fill an element's border is where the element gave its own colour.</summary>
	public const float OwnBorderShade = 0.45f;

	/// <summary>
	/// The picture, four bytes a pixel. An element's own colour is the fill in place of the style's, and where the style
	/// has a border and is not hollow, the border is that colour darkened; a hollow style's border is the colour itself.
	/// </summary>
	public static byte[] Paint(CBuiltInStyle style, int width, int height, CColor? colour = null)
	{
		CColor fill = colour ?? style.Fill;
		CColor border = colour is { } own
			? style.Hollow ? own : new CColor(own.R * OwnBorderShade, own.G * OwnBorderShade, own.B * OwnBorderShade, own.A)
			: style.Border;
		// A border never wider than half the shorter side: a thin element is all border.
		int edge = Math.Min(style.BorderWidth, (Math.Min(width, height) + 1) / 2);
		var pixels = new byte[width * height * 4];
		for (int y = 0; y < height; y++)
		{
			// Lighter by half the gradient at the top, darker by half at the foot.
			float along = height > 1 ? (float)y / (height - 1) : 0.5f;
			float shade = 1f + style.Gradient * (0.5f - along);
			for (int x = 0; x < width; x++)
			{
				CColor pixel;
				if (x < edge || y < edge || x >= width - edge || y >= height - edge)
				{
					pixel = border;
				}
				else if (style.Hollow)
				{
					continue;
				}
				else
				{
					float lit = shade;
					// The bevel: the first row and column inside the border one way, the last the other.
					if (style.Bevel != 0f)
					{
						if (y == edge || x == edge)
						{
							lit += style.Bevel;
						}
						else if (y == height - edge - 1 || x == width - edge - 1)
						{
							lit -= style.Bevel;
						}
					}
					pixel = new CColor(fill.R * lit, fill.G * lit, fill.B * lit, fill.A);
				}
				int at = (y * width + x) * 4;
				pixels[at] = Byte(pixel.R);
				pixels[at + 1] = Byte(pixel.G);
				pixels[at + 2] = Byte(pixel.B);
				pixels[at + 3] = Byte(pixel.A);
			}
		}
		return pixels;
	}

	private static byte Byte(float part) => (byte)Math.Clamp((int)MathF.Round(part * 255f), 0, 255);
}
