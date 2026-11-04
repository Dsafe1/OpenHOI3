using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Gui;
using OpenHOI3.Sim;

namespace OpenHOI3.Gfx;

/// <summary>
/// The fonts the interface files name, drawn as outlines and not from the original's
/// glyph textures. See <c>docs/mechanics/interface.md</c>, section 6.
///
/// A font is still declared and named exactly as the original's files do it. What
/// changes is where its letters come from, in this order:
/// a <c>.ttf</c> or <c>.otf</c> of the font's name beside its descriptor, which is how
/// a mod supplies a typeface; else the shipped typeface that stands for the one the
/// descriptor says its glyphs were made from. Nothing is taken from the machine.
/// </summary>
public sealed class CGuiFonts
{
	/// <summary>The size a typeface is measured at to find the size to draw it at.</summary>
	private const int ProbeSize = 200;

	/// <summary>A typeface is never narrowed to less than this much of its own width.</summary>
	private const float LeastNarrowing = 0.7f;

	/// <summary>The size of a font that has no descriptor to take one from.</summary>
	private const int DefaultSize = 14;

	private readonly CDirectorySettings _directories;
	private readonly CShippedTypefaces _shipped = new();
	private readonly Dictionary<string, CGuiFont> _fonts = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, CGuiFont?> _files = new(StringComparer.OrdinalIgnoreCase);

	private CGuiFonts(CDirectorySettings directories)
	{
		_directories = directories;
	}

	public int Count => _fonts.Count;

	public static CGuiFonts Load(CDirectorySettings directories)
	{
		var registry = new CGuiFonts(directories);
		foreach (string file in directories.ListFiles("interface", "*.gfx"))
		{
			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(file);
			}
			catch (Exception)
			{
				continue;                             // already reported by the sprite pass
			}
			foreach (ClausewitzNode container in root.All("bitmapfonts"))
			{
				foreach (ClausewitzNode entry in container.All("bitmapfont"))
				{
					var font = new CGuiFont
					{
						Name = entry.GetString("name"),
						FontName = entry.GetString("fontname"),
						Color = entry.GetColor("color", CColor.White).ToGodot(),
					};
					if (font.Name.Length != 0)
					{
						// Later declarations win, matching the layer load order.
						registry._fonts[font.Name] = font;
					}
				}
			}
		}
		return registry;
	}

	/// <summary>
	/// Resolves a font as named by the GUI. Names without a <c>bitmapfont</c> entry are
	/// treated as direct file names, which is how several of the original's fonts are
	/// written.
	/// </summary>
	public CGuiFont? Find(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		if (!_fonts.TryGetValue(name, out CGuiFont? font))
		{
			font = new CGuiFont { Name = name, FontName = name };
			_fonts[name] = font;
		}
		if (font.Font is null)
		{
			// Fonts that share a descriptor share its typeface and its size.
			CGuiFont? made = Make(font.FontName);
			if (made is null)
			{
				return null;
			}
			font.Font = made.Font;
			font.Color *= made.Color;
			font.Size = made.Size;
			font.Narrowing = made.Narrowing;
			font.FixedAdvance = made.FixedAdvance;
			font.GlyphWidth = made.GlyphWidth;
			font.Ascent = made.Ascent;
			font.LineHeight = made.LineHeight;
		}
		return font;
	}

	private CGuiFont? Make(string fontName)
	{
		if (_files.TryGetValue(fontName, out CGuiFont? cached))
		{
			return cached;
		}

		string descriptorPath = _directories.Resolve($"gfx/fonts/{fontName}.fnt");
		CFontDescriptor? descriptor = File.Exists(descriptorPath) ? CFontDescriptor.Load(descriptorPath) : null;

		bool sameWidths = false;
		Font? face = Supplied(fontName);
		if (face is null)
		{
			if (descriptor is null)
			{
				CDiagnostics.Warn($"The font '{fontName}' has no descriptor in gfx/fonts and no typeface beside one");
			}
			face = _shipped.For(descriptor?.Face ?? "", descriptor is { Bold: true }, descriptor is { Italic: true },
				out sameWidths);
		}

		CGuiFont? made = null;
		if (face is not null)
		{
			made = new CGuiFont { Name = fontName, FontName = fontName, Font = face, Color = Ink(fontName) };
			Measure(made, descriptor, sameWidths);
		}
		_files[fontName] = made;
		return made;
	}

	/// <summary>
	/// The colour the original's glyphs were painted in: the average of its glyph
	/// texture, counting each pixel by how solid it is. White when there is no texture.
	/// The texture is read for this and for nothing else.
	/// </summary>
	private Color Ink(string fontName)
	{
		string? path = _directories.ResolveTexture($"gfx/fonts/{fontName}.tga");
		if (path is null)
		{
			return Colors.White;
		}
		var image = new Image();
		Error error = Path.GetExtension(path).Equals(".dds", StringComparison.OrdinalIgnoreCase)
			? image.LoadDdsFromBuffer(File.ReadAllBytes(path))
			: image.LoadTgaFromBuffer(File.ReadAllBytes(path));
		if (error != Error.Ok || image.IsEmpty())
		{
			return Colors.White;
		}
		if (image.IsCompressed())
		{
			image.Decompress();
		}
		image.Convert(Image.Format.Rgba8);

		byte[] pixels = image.GetData();
		double red = 0, green = 0, blue = 0, solid = 0;
		for (int index = 0; index + 3 < pixels.Length; index += 4)
		{
			double alpha = pixels[index + 3];
			red += pixels[index] * alpha;
			green += pixels[index + 1] * alpha;
			blue += pixels[index + 2] * alpha;
			solid += alpha;
		}
		return solid <= 0 ? Colors.White
			: new Color((float)(red / solid / 255.0), (float)(green / solid / 255.0), (float)(blue / solid / 255.0));
	}

	/// <summary>A typeface file of the font's own name beside its descriptor, in whichever layer has one.</summary>
	private Font? Supplied(string fontName)
	{
		foreach (string extension in new[] { ".ttf", ".otf" })
		{
			string path = _directories.Resolve($"gfx/fonts/{fontName}{extension}");
			if (!File.Exists(path))
			{
				continue;
			}
			var file = new FontFile();
			Error error = file.LoadDynamicFont(path);
			if (error == Error.Ok)
			{
				file.AllowSystemFallback = false;
				return file;
			}
			CDiagnostics.Warn($"Could not load the typeface '{path}' ({error})");
		}
		return null;
	}

	/// <summary>
	/// Finds the size to draw a typeface at, and takes the line's height and where its
	/// letters stand from the descriptor.
	///
	/// A typeface whose letters are as wide as the original's is drawn at the size that
	/// makes text as wide as the original's glyphs made it, which is the original's
	/// size. Any other has letters of another width, so it is sized by the height of a
	/// line instead, and where that comes out wider than the original it is narrowed to
	/// fit the boxes the interface files give it.
	///
	/// The descriptor's own size figure is not used: two copies of one font in the wild
	/// give different sizes for the same glyphs.
	/// </summary>
	private static void Measure(CGuiFont font, CFontDescriptor? descriptor, bool sameWidths)
	{
		Font face = font.Font!;
		string reference = descriptor?.Measurable() ?? "";
		float probed = reference.Length == 0 ? 0f : Width(face, reference, ProbeSize);
		if (descriptor is null || probed <= 0f)
		{
			font.Size = DefaultSize;
			font.Ascent = face.GetAscent(DefaultSize);
			font.LineHeight = face.GetHeight(DefaultSize);
			return;
		}

		float wanted = descriptor.Width(reference);
		float probedHeight = face.GetHeight(ProbeSize);
		if (sameWidths || descriptor.LineHeight <= 0 || probedHeight <= 0f)
		{
			font.Size = Mathf.Max(1, Mathf.RoundToInt(ProbeSize * wanted / probed));
		}
		else
		{
			font.Size = Mathf.Max(1, Mathf.RoundToInt(ProbeSize * descriptor.LineHeight / probedHeight));
			float narrowing = wanted / Width(face, reference, font.Size);
			if (narrowing < 1f)
			{
				font.Narrowing = Mathf.Max(narrowing, LeastNarrowing);
			}
		}
		font.Ascent = descriptor.Base > 0 ? descriptor.Base : face.GetAscent(font.Size);
		font.LineHeight = descriptor.LineHeight > 0 ? descriptor.LineHeight : face.GetHeight(font.Size);
		font.FixedAdvance = descriptor.FixedAdvance;
		font.GlyphWidth = font.FixedAdvance > 0 ? descriptor.GlyphWidth : null;
	}

	private static float Width(Font face, string text, int size)
	{
		return face.GetStringSize(text, HorizontalAlignment.Left, -1f, size).X;
	}
}
