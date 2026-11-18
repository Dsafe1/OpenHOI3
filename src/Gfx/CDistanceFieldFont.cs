using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Interface.Text;

namespace OpenHOI3.Gfx;

/// <summary>
/// A large font of the original's drawn from its own glyphs: its <see cref="CDistanceFieldAtlas"/> handed to Godot as a
/// multichannel distance-field font whose three channels are the one field, so that the median its shader takes is that
/// field. Laid out in the descriptor's own pixels at a size of the line's height: the advances, the baseline and the
/// line are the original's (<c>docs/mechanics/interface.md</c>, section 6).
/// </summary>
public static class CDistanceFieldFont
{
	public static FontFile Make(CFontDescriptor descriptor, CDistanceFieldAtlas atlas, int size)
	{
		var font = new FontFile
		{
			MultichannelSignedDistanceField = true,
			MsdfPixelRange = CDistanceFieldAtlas.Range,
			MsdfSize = size,
			AllowSystemFallback = false,
		};
		var key = new Vector2I(size, 0);

		byte[] pixels = new byte[atlas.Field.Length * 3];
		for (int i = 0; i < atlas.Field.Length; i++)
		{
			pixels[3 * i] = pixels[3 * i + 1] = pixels[3 * i + 2] = atlas.Field[i];
		}
		font.SetTextureImage(0, key, 0, Image.CreateFromData(atlas.Width, atlas.Height, false, Image.Format.Rgb8, pixels));
		font.SetCacheAscent(0, size, descriptor.Base);
		font.SetCacheDescent(0, size, descriptor.LineHeight - descriptor.Base);

		var given = new HashSet<int>();
		foreach (CDistanceFieldGlyph glyph in atlas.Glyphs)
		{
			int index = Character(glyph.Code);
			given.Add(index);
			font.SetGlyphAdvance(0, size, index, new Vector2(glyph.Advance, 0));
			if (glyph.Width > 0)
			{
				font.SetGlyphOffset(0, key, index, new Vector2(glyph.XOffset, glyph.YOffset - descriptor.Base));
				font.SetGlyphSize(0, key, index, new Vector2(glyph.Width, glyph.Height));
				font.SetGlyphUVRect(0, key, index, new Rect2(glyph.AtlasX, glyph.AtlasY, glyph.Width, glyph.Height));
				font.SetGlyphTextureIdx(0, key, index, 0);
			}
		}
		// A character the descriptor does not give moves the pen nothing and draws nothing, as in the original's.
		for (int code = 1; code < 256; code++)
		{
			int index = Character(code);
			if (given.Add(index))
			{
				font.SetGlyphAdvance(0, size, index, Vector2.Zero);
			}
		}
		return font;
	}

	/// <summary>The character a descriptor's number is drawn as: its Windows-1252 byte's.</summary>
	private static int Character(int code)
	{
		return code is > 0 and < 256 ? CWindows1252.ToDisplay(((char)code).ToString())[0] : code;
	}
}
