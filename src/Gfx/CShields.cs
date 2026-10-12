using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Gfx;

/// <summary>
/// Builds a country's flag as a <c>maskedShieldType</c> sprite shows it.
///
/// The game declares these with two textures and a shader that composites a country's
/// flag between them: the flag takes the mask's size and its alpha, and the other
/// texture, which carries the frame or the shading in its own alpha, goes over it. The
/// thin shield's mask and the top bar flag's are solid, so there it comes to scaling
/// the flag and blending the frame over it; the small round shield's mask is a disc.
/// It is done once per country and sprite on the CPU and cached rather than needing a
/// shader: <c>docs/mechanics/interface.md</c>, section 5.
/// </summary>
public sealed class CShields
{
	private readonly CTextureManager _textures;
	private readonly Dictionary<string, Texture2D?> _cache = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, Image?> _overlays = new(StringComparer.OrdinalIgnoreCase);

	public CShields(CTextureManager textures)
	{
		_textures = textures;
	}

	/// <summary>The flag of a country as that sprite draws it, or null when its flag is missing.</summary>
	public Texture2D? Build(CCountry country, CSpriteType sprite)
	{
		string key = sprite.Name + "/" + country.Tag.Value;
		if (_cache.TryGetValue(key, out Texture2D? cached))
		{
			return cached;
		}

		Texture2D? shield = Compose(country, sprite);
		_cache[key] = shield;
		return shield;
	}

	private Texture2D? Compose(CCountry country, CSpriteType sprite)
	{
		Image? flag = _textures.LoadImage(country.FlagFile);
		if (flag is null)
		{
			return null;
		}
		ToRgba(flag);

		Image? overlay = Overlay(sprite);
		if (overlay is null)
		{
			// Without the overlay the flag alone is still a usable shield.
			return ImageTexture.CreateFromImage(flag);
		}

		// The mask sets the flag's size and cuts it: the flag is squeezed to the mask's
		// size and is as see-through as the mask is at each point, as the original's own
		// shader has it. The thin shield's mask is its overlay's size and cuts nothing;
		// the small round one's is 40 by 26 with a disc in it, under an overlay of 32 by
		// 32. The flag goes in the middle of the overlay, which sets the picture's size.
		Image? mask = sprite.MaskFile.Length == 0 ? null : Mask(sprite);
		Vector2I flagSize = mask?.GetSize() ?? overlay.GetSize();
		if (flag.GetSize() != flagSize)
		{
			flag.Resize(flagSize.X, flagSize.Y, Image.Interpolation.Bilinear);
		}
		if (mask is not null)
		{
			for (int y = 0; y < flagSize.Y; y++)
			{
				for (int x = 0; x < flagSize.X; x++)
				{
					Color colour = flag.GetPixel(x, y);
					colour.A = mask.GetPixel(x, y).A;
					flag.SetPixel(x, y, colour);
				}
			}
		}
		Image composed = Image.CreateEmpty(overlay.GetWidth(), overlay.GetHeight(), false, Image.Format.Rgba8);
		Vector2I at = (overlay.GetSize() - flagSize) / 2;
		composed.BlitRect(flag, new Rect2I(Vector2I.Zero, flagSize), at);
		composed.BlendRect(overlay, new Rect2I(Vector2I.Zero, overlay.GetSize()), Vector2I.Zero);
		return ImageTexture.CreateFromImage(composed);
	}

	private Image? Mask(CSpriteType sprite)
	{
		string key = sprite.Name + "/mask";
		if (_overlays.TryGetValue(key, out Image? cached))
		{
			return cached;
		}
		Image? mask = _textures.LoadImage(sprite.MaskFile);
		if (mask is not null)
		{
			ToRgba(mask);
		}
		_overlays[key] = mask;
		return mask;
	}

	/// <summary>
	/// An image made plain RGBA to be drawn into: a <c>.dds</c> comes compressed - the diplomacy screen's
	/// small shields do - and is unpacked first, which converting cannot do.
	/// </summary>
	private static void ToRgba(Image image)
	{
		if (image.IsCompressed())
		{
			image.Decompress();
		}
		image.Convert(Image.Format.Rgba8);
	}

	private Image? Overlay(CSpriteType sprite)
	{
		if (_overlays.TryGetValue(sprite.Name, out Image? cached))
		{
			return cached;
		}
		Image? overlay = sprite.OverlayFile.Length == 0 ? null : _textures.LoadImage(sprite.OverlayFile);
		if (overlay is null)
		{
			CDiagnostics.Warn($"The sprite '{sprite.Name}' has no texture to draw over a flag");
		}
		else
		{
			ToRgba(overlay);
		}
		_overlays[sprite.Name] = overlay;
		return overlay;
	}
}
