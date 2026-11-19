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
/// The fonts the interface files name, drawn so that they stay sharp on a scaled canvas.
/// See <c>docs/mechanics/interface.md</c>, section 6.
///
/// A font is still declared and named exactly as the original's files do it. What
/// changes is where its letters come from, in this order:
/// a <c>.ttf</c> or <c>.otf</c> of the font's name beside its descriptor, which is how
/// a mod supplies a typeface; else, for a large font, its own glyph texture made into
/// distance fields (<see cref="CDistanceFieldAtlas"/>); else the shipped typeface that
/// stands for the one the descriptor says its glyphs were made from. Nothing is taken
/// from the machine.
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

	/// <summary>The <c>font = { }</c> entries, by name: fonts the original makes from an installed typeface.</summary>
	private readonly Dictionary<string, CTrueTypeFont> _trueType = new(StringComparer.Ordinal);

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
						Effect = !string.Equals(entry.GetString("effect"), "no", StringComparison.OrdinalIgnoreCase),
					};
					if (font.Name.Length != 0)
					{
						// Later declarations win, matching the layer load order.
						registry._fonts[font.Name] = font;
					}
				}
			}
			CTrueTypeFontReader.Read(root, registry._trueType);
		}
		return registry;
	}

	/// <summary>
	/// Resolves a font as named by the GUI. A name with a <c>bitmapfont</c> entry is that; one with a <c>font = { }</c>
	/// entry instead is the typeface it names at its height (<see cref="MakeTrueType"/>); any other is treated as a direct
	/// file name, which is how several of the original's fonts are written. Which of the two entries the original asks
	/// first for an interface element was not read - no name of the base game's has both.
	/// </summary>
	public CGuiFont? Find(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		if (!_fonts.TryGetValue(name, out CGuiFont? font))
		{
			if (_trueType.TryGetValue(name, out CTrueTypeFont? entry) && MakeTrueType(entry) is { } trueType)
			{
				_fonts[name] = trueType;
				return trueType;
			}
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

	/// <summary>
	/// A <c>font = { }</c> entry as a font to draw with: the shipped typeface that stands for its <c>fontname</c>, at the
	/// size whose cell - from the top of its tallest letter to the foot of its lowest - is the entry's <c>height</c>, as
	/// Windows takes a positive height and the original hands it to Direct3D (<c>reversing/findings/FINDINGS-loadingscreen.md</c>,
	/// section 2), a line being that cell. Its colour is the entry's own. Null where no typeface stands for it.
	/// </summary>
	private CGuiFont? MakeTrueType(CTrueTypeFont entry)
	{
		Font? face = _shipped.For(entry.FontName, bold: false, italic: false, out _);
		if (face is null || entry.Height <= 0)
		{
			return null;
		}
		int size = Mathf.Max(1, Mathf.RoundToInt(ProbeSize * entry.Height / face.GetHeight(ProbeSize)));
		return new CGuiFont
		{
			Name = entry.Name,
			FontName = entry.FontName,
			TrueType = true,
			Font = face,
			Color = entry.Color.ToGodot(),
			Size = size,
			Ascent = face.GetAscent(size),
			LineHeight = entry.Height,
		};
	}

	/// <summary>A font file's descriptor, <c>gfx/fonts/&lt;name&gt;.fnt</c> in whichever layer has it, or null.</summary>
	public CFontDescriptor? Descriptor(string fontName)
	{
		if (!_descriptors.TryGetValue(fontName, out CFontDescriptor? descriptor))
		{
			string path = _directories.Resolve($"gfx/fonts/{fontName}.fnt");
			descriptor = File.Exists(path) ? CFontDescriptor.Load(path) : null;
			_descriptors[fontName] = descriptor;
		}
		return descriptor;
	}

	private readonly Dictionary<string, CFontDescriptor?> _descriptors = new(StringComparer.OrdinalIgnoreCase);

	private CGuiFont? Make(string fontName)
	{
		if (_files.TryGetValue(fontName, out CGuiFont? cached))
		{
			return cached;
		}

		string descriptorPath = _directories.Resolve($"gfx/fonts/{fontName}.fnt");
		CFontDescriptor? descriptor = File.Exists(descriptorPath) ? CFontDescriptor.Load(descriptorPath) : null;

		bool sameWidths = false;
		Image? texture = GlyphTexture(fontName);
		Font? face = Supplied(fontName);
		if (face is null && descriptor is { LineHeight: > 0 } && texture is not null && Atlas(descriptor, texture) is { } atlas)
		{
			// Large enough to be drawn from its own glyphs, in the descriptor's own pixels.
			int size = descriptor.LineHeight;
			var own = new CGuiFont
			{
				Name = fontName,
				FontName = fontName,
				Font = CDistanceFieldFont.Make(descriptor, atlas, size),
				Color = Ink(texture),
				Size = size,
				Ascent = descriptor.Base,
				LineHeight = descriptor.LineHeight,
			};
			_files[fontName] = own;
			return own;
		}
		if (face is null)
		{
			if (descriptor is null)
			{
				CDiagnostics.Problem($"The font '{fontName}' has no descriptor in gfx/fonts and no typeface beside one");
			}
			face = _shipped.For(descriptor?.Face ?? "", descriptor is { Bold: true }, descriptor is { Italic: true },
				out sameWidths);
		}

		CGuiFont? made = null;
		if (face is not null)
		{
			made = new CGuiFont { Name = fontName, FontName = fontName, Font = face, Color = Ink(texture) };
			Measure(made, descriptor, sameWidths);
		}
		_files[fontName] = made;
		return made;
	}

	/// <summary>The font's glyph texture, <c>gfx/fonts/&lt;name&gt;.tga</c> in whichever layer has it, as RGBA; null where none decodes.</summary>
	private Image? GlyphTexture(string fontName)
	{
		string? path = _directories.ResolveTexture($"gfx/fonts/{fontName}.tga");
		if (path is null)
		{
			return null;
		}
		var image = new Image();
		Error error = Path.GetExtension(path).Equals(".dds", StringComparison.OrdinalIgnoreCase)
			? image.LoadDdsFromBuffer(File.ReadAllBytes(path))
			: image.LoadTgaFromBuffer(File.ReadAllBytes(path));
		if (error != Error.Ok || image.IsEmpty())
		{
			return null;
		}
		if (image.IsCompressed())
		{
			image.Decompress();
		}
		image.Convert(Image.Format.Rgba8);
		return image;
	}

	/// <summary>
	/// The font's glyphs as distance fields, where its capital H stands at least
	/// <see cref="CDistanceFieldAtlas.LeastCapHeight"/> pixels; null for a smaller font, which a stand-in draws.
	/// </summary>
	private static CDistanceFieldAtlas? Atlas(CFontDescriptor descriptor, Image texture)
	{
		byte[] pixels = texture.GetData();
		var alpha = new byte[pixels.Length / 4];
		for (int i = 0; i < alpha.Length; i++)
		{
			alpha[i] = pixels[4 * i + 3];
		}
		var page = new CGlyphPage(texture.GetWidth(), texture.GetHeight(), alpha);
		return CDistanceFieldAtlas.Suits(descriptor, page) ? CDistanceFieldAtlas.Build(descriptor, page) : null;
	}

	/// <summary>
	/// The colour the original's glyphs were painted in: the average of its glyph
	/// texture, counting each pixel by how solid it is. White when there is no texture.
	/// </summary>
	private static Color Ink(Image? texture)
	{
		if (texture is null)
		{
			return Colors.White;
		}
		byte[] pixels = texture.GetData();
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
			CDiagnostics.Problem($"Could not load the typeface '{path}' ({error})");
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
