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
/// flag between them. Both masks the base game ships - the thin shield's and the top
/// bar flag's - are solid white in every channel, so they change nothing, and the other
/// texture carries the frame or the shading in its alpha. That reduces the whole thing
/// to scaling the flag and blending that texture over it, which is done once per
/// country and sprite on the CPU and cached rather than needing a shader. A mask that
/// did cut something would be ignored: <c>docs/mechanics/interface.md</c>, section 5.
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
		flag.Convert(Image.Format.Rgba8);

		Image? overlay = Overlay(sprite);
		if (overlay is null)
		{
			// Without the overlay the flag alone is still a usable shield.
			return ImageTexture.CreateFromImage(flag);
		}

		// The overlay sets the size. Flags are 93x64; the thin shield is 66x51, so the
		// flag is squeezed to fit and the frame's border hides where that shows.
		if (flag.GetSize() != overlay.GetSize())
		{
			flag.Resize(overlay.GetWidth(), overlay.GetHeight(), Image.Interpolation.Bilinear);
		}
		Image composed = Image.CreateEmpty(overlay.GetWidth(), overlay.GetHeight(), false, Image.Format.Rgba8);
		composed.BlitRect(flag, new Rect2I(Vector2I.Zero, flag.GetSize()), Vector2I.Zero);
		composed.BlendRect(overlay, new Rect2I(Vector2I.Zero, overlay.GetSize()), Vector2I.Zero);
		return ImageTexture.CreateFromImage(composed);
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
			overlay.Convert(Image.Format.Rgba8);
		}
		_overlays[sprite.Name] = overlay;
		return overlay;
	}
}
