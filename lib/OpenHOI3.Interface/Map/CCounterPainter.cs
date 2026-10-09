using OpenHOI3.Sim;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// Puts a counter's picture together out of its parts, pixel by pixel, the way the
/// original's own shader does - <c>gfx/FX/counter.fx</c>, which is plain text in an
/// install. See <c>docs/mechanics/mapunits.md</c>, section 2.
///
/// The original does this on the graphics card every frame for every counter. Here it
/// is done once for each kind of counter that is on the map - a country's colour, a
/// unit type, a size - and the result drawn as an ordinary picture.
/// </summary>
public static class CCounterPainter
{
	/// <summary>
	/// A counter of one country, unit type and size.
	///
	///     the plate's colour times the country's, where the mask is red
	///     the unit type's picture over that, as solid as it is
	///     the size mark over that, as solid as it is
	///     and as solid as the plate
	/// </summary>
	/// <param name="background">The plate.</param>
	/// <param name="mask">Red where the country's colour goes.</param>
	/// <param name="sizes">The strip of size marks, <paramref name="frames"/> side by side.</param>
	/// <param name="picture">The unit type's picture, or null for none.</param>
	/// <param name="size">Which mark of the strip, from 0.</param>
	public static CPixels Paint(CPixels background, CPixels mask, CPixels sizes, int frames, CPixels? picture,
		CColor colour, int size)
	{
		var result = new CPixels(background.Width, background.Height);
		float step = 1f / (frames < 1 ? 1 : frames);
		for (int y = 0; y < result.Height; y++)
		{
			float v = (y + 0.5f) / result.Height;
			for (int x = 0; x < result.Width; x++)
			{
				float u = (x + 0.5f) / result.Width;
				(float plateR, float plateG, float plateB, float plateA) = background.At(u, v);
				float painted = mask.At(u, v).R;

				float r = painted * colour.R * plateR;
				float g = painted * colour.G * plateG;
				float b = painted * colour.B * plateB;

				if (picture is not null)
				{
					(float pictureR, float pictureG, float pictureB, float pictureA) = picture.At(u, v);
					r += (pictureR - r) * pictureA;
					g += (pictureG - g) * pictureA;
					b += (pictureB - b) * pictureA;
				}

				(float markR, float markG, float markB, float markA) = sizes.At((u + size) * step, v);
				r += (markR - r) * markA;
				g += (markG - g) * markA;
				b += (markB - b) * markA;

				result.Set(x, y, r, g, b, plateA);
			}
		}
		return result;
	}

	/// <summary>
	/// The ring a selected counter wears, to be drawn over it in the selection's
	/// colour: the mask's green as its brightness and the mask's blue as how solid.
	/// </summary>
	public static CPixels Ring(CPixels mask)
	{
		var result = new CPixels(mask.Width, mask.Height);
		for (int y = 0; y < mask.Height; y++)
		{
			for (int x = 0; x < mask.Width; x++)
			{
				(float _, float green, float blue, float _) = mask.At((x + 0.5f) / mask.Width, (y + 0.5f) / mask.Height);
				result.Set(x, y, green, green, green, blue);
			}
		}
		return result;
	}
}
