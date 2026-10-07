using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Sim;

namespace OpenHOI3.Gfx;

/// <summary>
/// Builds the country shields the front end shows for selectable nations.
///
/// The game declares these as a <c>maskedShieldType</c>: a frame texture, a mask
/// texture and a shader that composites a country's flag between them. The mask that
/// ships with the thin shield is solid white in every channel, so it changes nothing,
/// and the frame carries the shape in its alpha. That reduces the whole thing to
/// scaling the flag and blending the frame over it, which is done once per country on
/// the CPU and cached rather than needing a shader.
/// </summary>
public sealed class CShields
{
	private const string FramePath = "gfx/interface/shield_frame.tga";

	private readonly CTextureManager _textures;
	private readonly Dictionary<string, Texture2D?> _cache =
		new(StringComparer.OrdinalIgnoreCase);

	private Image? _frame;
	private bool _frameLoaded;

	public CShields(CTextureManager textures)
	{
		_textures = textures;
	}

	/// <summary>Size of a shield, taken from the frame texture.</summary>
	public Vector2I Size => _frame is null ? new Vector2I(66, 51)
		: new Vector2I(_frame.GetWidth(), _frame.GetHeight());

	/// <summary>The shield for a country, or null when its flag is missing.</summary>
	public Texture2D? Build(CCountry country)
	{
		if (_cache.TryGetValue(country.Tag.Value, out Texture2D? cached))
		{
			return cached;
		}

		Texture2D? shield = Compose(country);
		_cache[country.Tag.Value] = shield;
		return shield;
	}

	private Texture2D? Compose(CCountry country)
	{
		Image? flag = _textures.LoadImage(country.FlagFile);
		if (flag is null)
		{
			return null;
		}

		Image? frame = Frame();
		if (frame is null)
		{
			// Without the frame the flag alone is still a usable shield.
			return ImageTexture.CreateFromImage(flag);
		}

		// Flags are 93x64 and shields 66x51, so the flag is squeezed to fit. The
		// frame's border hides the edges where the aspect change shows.
		Image scaled = Image.CreateEmpty(frame.GetWidth(), frame.GetHeight(), false,
			Image.Format.Rgba8);
		flag.Convert(Image.Format.Rgba8);
		scaled.BlitRect(flag, new Rect2I(Vector2I.Zero, flag.GetSize()), Vector2I.Zero);
		scaled.Resize(frame.GetWidth(), frame.GetHeight(), Image.Interpolation.Bilinear);

		Image composed = Image.CreateEmpty(frame.GetWidth(), frame.GetHeight(), false,
			Image.Format.Rgba8);
		composed.BlitRect(scaled, new Rect2I(Vector2I.Zero, scaled.GetSize()), Vector2I.Zero);
		composed.BlendRect(frame, new Rect2I(Vector2I.Zero, frame.GetSize()), Vector2I.Zero);

		return ImageTexture.CreateFromImage(composed);
	}

	private Image? Frame()
	{
		if (_frameLoaded)
		{
			return _frame;
		}
		_frameLoaded = true;
		_frame = _textures.LoadImage(FramePath);
		if (_frame is not null)
		{
			_frame.Convert(Image.Format.Rgba8);
		}
		else
		{
			CDiagnostics.Warn($"Shield frame missing: {FramePath}");
		}
		return _frame;
	}
}
