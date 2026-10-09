using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Map;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;

namespace OpenHOI3.Map;

/// <summary>
/// One of the two kinds of counter - the far one or the near one - with the pictures
/// its type names, and every counter of that kind painted so far: one picture for each
/// country's colour, unit type and size that has been on screen.
/// </summary>
public sealed class CCounterKind
{
	/// <summary>The picture a counter carries when its unit type has none of its own.</summary>
	private const string Unknown = "unknown";

	private readonly CPixels _background;
	private readonly CPixels _mask;
	private readonly CPixels _sizes;
	private readonly string _family;
	private readonly Dictionary<(uint Colour, string Key, int Size), Texture2D> _painted = new();
	private readonly Dictionary<string, CPixels?> _pictures = new(StringComparer.OrdinalIgnoreCase);

	private CCounterKind(CCounterType type, CPixels background, CPixels mask, CPixels sizes, string family)
	{
		Type = type;
		_background = background;
		_mask = mask;
		_sizes = sizes;
		_family = family;
		Aspect = (float)background.Width / background.Height;
	}

	public CCounterType Type { get; }

	/// <summary>The plate's width over its height.</summary>
	public float Aspect { get; }

	/// <summary>What the figures are written in, or null when no file declares the font the type names.</summary>
	public CGuiFont? Font { get; private init; }

	/// <summary>The ring of a selected counter, to be drawn over it.</summary>
	public Texture2D? Ring { get; private init; }

	/// <summary>Whether a counter of this kind has its unit's name written on it: the near one does.</summary>
	public bool CarriesName { get; private init; }

	/// <summary>
	/// Makes the kind from its declaration, or answers null - with a line in the log -
	/// when there is no declaration or one of its three pictures cannot be read.
	/// </summary>
	public static CCounterKind? Create(CGuiContext gui, CCounterType? type, string family, bool carriesName)
	{
		if (type is null)
		{
			CDiagnostics.Warn("map: a counter type is not declared, so that kind of counter is not drawn");
			return null;
		}
		CPixels? background = Pixels(gui, type.BackgroundFile);
		CPixels? mask = Pixels(gui, type.MaskFile);
		CPixels? sizes = Pixels(gui, type.SizeFile);
		if (background is null || mask is null || sizes is null)
		{
			CDiagnostics.Warn($"map: counter type '{type.Name}' lacks one of its three pictures and is not drawn");
			return null;
		}
		CGuiFont? font = gui.Fonts.Find(type.Font);
		if (font is null && type.Font.Length != 0)
		{
			CDiagnostics.Warn($"map: counter type '{type.Name}' names the font '{type.Font}', which no file declares");
		}
		return new CCounterKind(type, background, mask, sizes, family)
		{
			Font = font,
			Ring = Texture(CCounterPainter.Ring(mask)),
			CarriesName = carriesName,
		};
	}

	/// <summary>The picture of a counter: painted the first time it is asked for and kept.</summary>
	public Texture2D? Texture(CGuiContext gui, CMapCounter counter)
	{
		(uint, string, int) key = (Pack(counter.Colour), counter.TypeKey, counter.Size);
		if (!_painted.TryGetValue(key, out Texture2D? texture))
		{
			CPixels painted = CCounterPainter.Paint(_background, _mask, _sizes, Type.Frames,
				Picture(gui, counter.TypeKey), counter.Colour, counter.Size);
			texture = Texture(painted);
			_painted[key] = texture;
		}
		return texture;
	}

	/// <summary>
	/// A unit type's picture in this kind's family of sprites, and where it has none the
	/// family's picture for a unit that is not known.
	/// </summary>
	private CPixels? Picture(CGuiContext gui, string typeKey)
	{
		if (!_pictures.TryGetValue(typeKey, out CPixels? picture))
		{
			CSpriteType? sprite = typeKey.Length == 0 ? null : gui.Sprites.Find(_family + typeKey);
			sprite ??= gui.Sprites.Find(_family + Unknown);
			picture = sprite is null ? null : Pixels(gui, sprite.TextureFile);
			_pictures[typeKey] = picture;
		}
		return picture;
	}

	private static CPixels? Pixels(CGuiContext gui, string file)
	{
		if (file.Length == 0 || gui.Textures.LoadImage(file) is not { } image)
		{
			return null;
		}
		if (image.IsCompressed())
		{
			image.Decompress();
		}
		image.Convert(Image.Format.Rgba8);
		image.ClearMipmaps();                               // only the picture itself, not its smaller copies
		return new CPixels(image.GetWidth(), image.GetHeight(), image.GetData());
	}

	private static Texture2D Texture(CPixels pixels)
	{
		Image image = Image.CreateFromData(pixels.Width, pixels.Height, false, Image.Format.Rgba8, pixels.Rgba);
		image.GenerateMipmaps();
		return ImageTexture.CreateFromImage(image);
	}

	private static uint Pack(CColor colour)
	{
		static uint Part(float value) => (uint)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
		return (Part(colour.R) << 24) | (Part(colour.G) << 16) | (Part(colour.B) << 8) | Part(colour.A);
	}
}
