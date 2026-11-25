using System;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Interface.Screenshots;

/// <summary>
/// The picture of the whole map the original saves on F12 (<c>docs/mechanics/screenshots.md</c>, section 3): a pixel
/// for each of the province bitmap's, north up. Water is white; land is its controller's colour, black where nobody
/// controls it. With Shift held it is the players' land alone: a played controller's colour, a played overlord's at
/// three quarters for its subject's, and black for everything else and for land nobody owns. It asks nothing of the
/// map mode or of what the player knows.
/// </summary>
public static class CMapPicture
{
	/// <summary>What a subject's land keeps of its played overlord's colour.</summary>
	public const double SubjectShade = 0.75;

	private static readonly CColor Black = new(0f, 0f, 0f);

	/// <summary>A province's colour by the original's rule.</summary>
	public static CColor Colour(bool land, CCountryTag? owner, CCountryTag? controller, bool playersOnly,
		Func<CCountryTag, CColor> colourOf, Func<CCountryTag, bool> played, Func<CCountryTag, CCountryTag?> overlordOf)
	{
		if (!land)
		{
			return CColor.White;
		}
		if (!playersOnly)
		{
			return controller is { } holder ? colourOf(holder) : Black;
		}
		// The original reads the controller where the province has an owner, and never asks whether there is one.
		if (owner is null || controller is not { } held)
		{
			return Black;
		}
		if (played(held))
		{
			return colourOf(held);
		}
		if (overlordOf(held) is { } overlord && played(overlord))
		{
			CColor full = colourOf(overlord);
			return new CColor((float)(full.R * SubjectShade), (float)(full.G * SubjectShade), (float)(full.B * SubjectShade));
		}
		return Black;
	}

	/// <summary>A part of a colour as its byte: times 255 and cut towards nought, as the original makes it.</summary>
	public static byte Part(float part)
	{
		return (byte)Math.Clamp((int)(part * 255.0), 0, 255);
	}

	/// <summary>The picture's pixels, rows from the top down, blue, green, red - from the raster, whose rows count north.</summary>
	public static byte[] Pixels(int width, int height, ReadOnlySpan<ushort> raster, CColor[] colourOfProvince)
	{
		var bgr = new byte[colourOfProvince.Length * 3];
		for (int province = 0; province < colourOfProvince.Length; province++)
		{
			bgr[province * 3] = Part(colourOfProvince[province].B);
			bgr[province * 3 + 1] = Part(colourOfProvince[province].G);
			bgr[province * 3 + 2] = Part(colourOfProvince[province].R);
		}
		var pixels = new byte[width * height * 3];
		for (int y = 0; y < height; y++)
		{
			ReadOnlySpan<ushort> row = raster.Slice((height - 1 - y) * width, width);
			int at = y * width * 3;
			for (int x = 0; x < width; x++, at += 3)
			{
				int province = row[x];
				if (province < colourOfProvince.Length)
				{
					pixels[at] = bgr[province * 3];
					pixels[at + 1] = bgr[province * 3 + 1];
					pixels[at + 2] = bgr[province * 3 + 2];
				}
				else
				{
					pixels[at] = pixels[at + 1] = pixels[at + 2] = 255;
				}
			}
		}
		return pixels;
	}

	/// <summary>The picture of a game's map as it stands.</summary>
	public static byte[] Make(CGameState state, CMap map, bool playersOnly)
	{
		var colours = new CColor[Math.Min(map.ProvinceCount, state.Provinces.Count)];
		colours[0] = CColor.White;
		for (int id = 1; id < colours.Length; id++)
		{
			CProvince province = state.Provinces[id];
			colours[id] = Colour(!map.IsSea(id), province.Owner, province.Controller, playersOnly,
				tag => state.Country(tag)?.Color ?? Black, state.Played.Contains, tag => state.Country(tag)?.Overlord);
		}
		return Pixels(map.Width, map.Height, map.Raster, colours);
	}
}
