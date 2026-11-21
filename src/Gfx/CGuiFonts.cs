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
		var read = new List<(string FileName, ClausewitzNode Root)>();
		// Each line passed over is a line of the log at its place, said once (docs/mechanics/logs.md, section 4).
		var paths = new Dictionary<string, string>(StringComparer.Ordinal);
		var said = new HashSet<string>(StringComparer.Ordinal);
		void PassedOver(string file, ClausewitzNode line)
		{
			string report = CDiagnostics.Located(file, line.Line,
				$"the line '{line.Key ?? line.Value}' of a font's colours is not a letter and three numbers and was passed over");
			if (said.Add(report))
			{
				CDiagnostics.Problem(report);
			}
		}
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
			read.Add((Path.GetFileName(file), root));
			paths[Path.GetFileName(file)] = file;
			foreach (ClausewitzNode container in root.All("bitmapfonts"))
			{
				foreach (ClausewitzNode entry in container.All("bitmapfont"))
				{
					var font = new CGuiFont
					{
						Name = entry.GetString("name"),
						FontName = entry.GetString("fontname"),
						File = file,
						Line = entry.Get("fontname")?.Line ?? entry.Line,
						Color = entry.GetColor("color", CColor.White).ToGodot(),
						Effect = !string.Equals(entry.GetString("effect"), "no", StringComparison.OrdinalIgnoreCase),
						OwnColours = CFontColourTable.Own(entry, line => PassedOver(file, line)),
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
		registry._shared = CFontColourTable.Shared(read, (fileName, line) => PassedOver(paths[fileName], line));
		return registry;
	}

	/// <summary>What an escape letter stands for in a font with no colour of its own for it.</summary>
	private CFontColourTable _shared = new();

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
				// The shared table is the bitmap fonts'; whether the original's Direct3D font draws escapes from it was not
				// read - ours.
				trueType.SharedColours = _shared;
				_fonts[name] = trueType;
				return trueType;
			}
			font = new CGuiFont { Name = name, FontName = name };
			_fonts[name] = font;
		}
		font.SharedColours = _shared;
		if (font.Font is null)
		{
			// Fonts that share a descriptor share its typeface and its size.
			CGuiFont? made = Make(font.FontName, font.File, font.Line);
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

	/// <summary>The install and mods, as their layers, whose fonts the log has had - one report a process each.</summary>
	private static readonly HashSet<string> Reported = new(StringComparer.Ordinal);

	/// <summary>
	/// Writes into the run's log every font file - each <c>bitmapfont</c> entry's <c>fontname</c> and every descriptor in
	/// <c>gfx/fonts</c> - and what it is drawn from: its own glyphs as distance fields, a typeface supplied beside it, or a
	/// shipped stand-in at the size that fits the original's widths, with why and how far narrowed; and every
	/// <c>font = { }</c> entry's stand-in. Once a process for an install and its mods, however often the interface is read.
	/// Every font but the large ones is made for it, which is cheap; a large one's distance fields are built only when it
	/// is first drawn. <c>docs/mechanics/interface.md</c>, section 6.
	/// </summary>
	public void Report()
	{
		if (!Reported.Add(string.Join("|", _directories.Layers)))
		{
			return;
		}
		var clock = System.Diagnostics.Stopwatch.StartNew();
		var files = new SortedDictionary<string, List<CGuiFont>>(StringComparer.OrdinalIgnoreCase);
		foreach (CGuiFont entry in _fonts.Values)
		{
			if (entry.File.Length == 0 || entry.FontName.Length == 0)
			{
				continue;
			}
			if (!files.TryGetValue(entry.FontName, out List<CGuiFont>? named))
			{
				files[entry.FontName] = named = new List<CGuiFont>();
			}
			named.Add(entry);
		}
		foreach (string path in _directories.ListFiles("gfx/fonts", "*.fnt"))
		{
			files.TryAdd(Path.GetFileNameWithoutExtension(path), new List<CGuiFont>());
		}

		int width = 0;
		foreach (string name in files.Keys)
		{
			width = Math.Max(width, name.Length);
		}
		var lines = new List<(CFontSourceKind Kind, string Text)>();
		foreach ((string name, List<CGuiFont> named) in files)
		{
			named.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
			CFontSource source = Source(name);
			CGuiFont? made = source.Kind == CFontSourceKind.OwnGlyphs ? null
				: named.Count > 0 ? Make(name, named[0].File, named[0].Line) : Make(name);
			string what = source.Kind switch
			{
				CFontSourceKind.OwnGlyphs => $"its own glyphs as distance fields, capital H {source.CapHeight} px, "
					+ $"line {source.Descriptor!.LineHeight}",
				CFontSourceKind.Supplied => $"the typeface {_directories.Relative(source.SuppliedPath)} beside it"
					+ (made is null ? "" : $", size {made.Size}"),
				_ => StandIn(name, source, made),
			};
			string where = source.DescriptorPath.Length > 0 ? _directories.Relative(source.DescriptorPath) : "no descriptor";
			string by = named.Count == 0 ? "no bitmapfont entry"
				: string.Join(", ", named.ConvertAll(entry => entry.Name));
			lines.Add((source.Kind, $"{Label(source.Kind),-10} {name.PadRight(width)}  {what}; {where}; named by {by}"));
		}

		var trueType = new List<string>();
		foreach (CTrueTypeFont entry in _trueType.Values)
		{
			CGuiFont? made = Find(entry.Name);
			_shipped.For(entry.FontName, bold: false, italic: false, out _, out string file);
			trueType.Add(made is null
				? $"font={{}}    {entry.Name}: not made - '{entry.FontName}' at height {entry.Height}"
				: $"font={{}}    {entry.Name}: stand-in {file} for '{entry.FontName}', height {entry.Height}, size {made.Size}");
		}
		trueType.Sort(StringComparer.OrdinalIgnoreCase);

		int Count(CFontSourceKind kind) => lines.FindAll(line => line.Kind == kind).Count;
		GD.Print($"Fonts: {files.Count} font files - {Count(CFontSourceKind.OwnGlyphs)} drawn from their own glyphs, "
			+ $"{Count(CFontSourceKind.Supplied)} in a typeface supplied beside them, "
			+ $"{Count(CFontSourceKind.StandIn)} in a shipped stand-in; "
			+ $"{trueType.Count} font = {{ }} entries, each a stand-in ({clock.ElapsedMilliseconds} ms):");
		foreach (CFontSourceKind kind in new[] { CFontSourceKind.OwnGlyphs, CFontSourceKind.Supplied, CFontSourceKind.StandIn })
		{
			foreach ((CFontSourceKind lineKind, string text) in lines)
			{
				if (lineKind == kind)
				{
					GD.Print("  " + text);
				}
			}
		}
		foreach (string text in trueType)
		{
			GD.Print("  " + text);
		}
	}

	private static string Label(CFontSourceKind kind) => kind switch
	{
		CFontSourceKind.OwnGlyphs => "own glyphs",
		CFontSourceKind.Supplied => "supplied",
		_ => "stand-in",
	};

	/// <summary>A stand-in font for the report: the shipped file, the size and narrowing it is drawn at, and why it is one.</summary>
	private string StandIn(string name, CFontSource source, CGuiFont? made)
	{
		string why = source.Descriptor is null ? "no descriptor"
			: !source.HasTexture ? "no glyph texture"
			: source.CapHeight == 0 ? "no capital H in its glyphs"
			: $"capital H {source.CapHeight} px, under {CDistanceFieldAtlas.LeastCapHeight}";
		if (made is null || !_standIns.TryGetValue(name, out (string File, bool SameWidths) standIn))
		{
			return $"not made, the shipped typeface missing ({why})";
		}
		string face = source.Descriptor?.Face is { Length: > 0 } named ? $"'{named}'" : "no typeface named";
		string narrowed = made.Narrowing < 1f ? $", narrowed to {made.Narrowing * 100f:0}%" : "";
		string widths = standIn.SameWidths ? ", the original's widths" : "";
		return $"{standIn.File} for {face}, size {made.Size}{narrowed}{widths} - {why}";
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

	/// <summary>A font file made a font, a fault in it reported at <paramref name="namedIn"/> and <paramref name="line"/>, where the name is written.</summary>
	private CGuiFont? Make(string fontName, string namedIn = "", int line = 0)
	{
		if (_files.TryGetValue(fontName, out CGuiFont? cached))
		{
			return cached;
		}

		CFontSource source = Source(fontName);
		CFontDescriptor? descriptor = source.Descriptor;
		if (source is { Kind: CFontSourceKind.OwnGlyphs, Descriptor: { } own, Page: { } page })
		{
			// Large enough to be drawn from its own glyphs, in the descriptor's own pixels.
			CDistanceFieldAtlas atlas = CDistanceFieldAtlas.Build(own, page);
			source.Page = null;
			int size = own.LineHeight;
			var drawn = new CGuiFont
			{
				Name = fontName,
				FontName = fontName,
				Font = CDistanceFieldFont.Make(own, atlas, size),
				Color = source.Ink,
				Size = size,
				Ascent = own.Base,
				LineHeight = own.LineHeight,
			};
			_files[fontName] = drawn;
			return drawn;
		}

		bool sameWidths = false;
		Font? face = source.Supplied;
		if (face is null)
		{
			if (descriptor is null)
			{
				CDiagnostics.Problem(namedIn, line, $"The font '{fontName}' has no descriptor in gfx/fonts and no typeface beside one");
			}
			face = _shipped.For(descriptor?.Face ?? "", descriptor is { Bold: true }, descriptor is { Italic: true },
				out sameWidths, out string standIn);
			_standIns[fontName] = (standIn, sameWidths);
		}

		CGuiFont? made = null;
		if (face is not null)
		{
			made = new CGuiFont { Name = fontName, FontName = fontName, Font = face, Color = source.Ink };
			Measure(made, descriptor, sameWidths);
		}
		_files[fontName] = made;
		return made;
	}

	/// <summary>Which shipped typeface each stand-in font file was given, and whether its letters are the original's widths.</summary>
	private readonly Dictionary<string, (string File, bool SameWidths)> _standIns = new(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, CFontSource> _sources = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// What a font file is made from, in the order <see cref="CGuiFonts"/> gives: a typeface beside its descriptor, else its
	/// own glyphs where its capital H stands at least <see cref="CDistanceFieldAtlas.LeastCapHeight"/> pixels, else a stand-in.
	/// </summary>
	private CFontSource Source(string fontName)
	{
		if (_sources.TryGetValue(fontName, out CFontSource? known))
		{
			return known;
		}
		string descriptorPath = _directories.Resolve($"gfx/fonts/{fontName}.fnt");
		bool hasDescriptor = File.Exists(descriptorPath);
		CFontDescriptor? descriptor = hasDescriptor ? CFontDescriptor.Load(descriptorPath) : null;
		Image? texture = GlyphTexture(fontName);
		Font? supplied = Supplied(fontName, out string suppliedPath);

		CGlyphPage? page = texture is null ? null : Page(texture);
		int capHeight = descriptor is not null && page is not null ? CDistanceFieldAtlas.CapHeight(descriptor, page) : 0;
		CFontSourceKind kind = supplied is not null ? CFontSourceKind.Supplied
			: descriptor is { LineHeight: > 0 } && page is not null && CDistanceFieldAtlas.Suits(descriptor, page)
				? CFontSourceKind.OwnGlyphs
				: CFontSourceKind.StandIn;
		var source = new CFontSource
		{
			Kind = kind,
			Descriptor = descriptor,
			DescriptorPath = hasDescriptor ? descriptorPath : "",
			HasTexture = texture is not null,
			CapHeight = capHeight,
			Ink = Ink(texture),
			Supplied = supplied,
			SuppliedPath = suppliedPath,
			Page = kind == CFontSourceKind.OwnGlyphs ? page : null,
		};
		_sources[fontName] = source;
		return source;
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

	/// <summary>The glyph texture's coverage, its alpha, which the distance fields and the capital H's height are read from.</summary>
	private static CGlyphPage Page(Image texture)
	{
		byte[] pixels = texture.GetData();
		var alpha = new byte[pixels.Length / 4];
		for (int i = 0; i < alpha.Length; i++)
		{
			alpha[i] = pixels[4 * i + 3];
		}
		return new CGlyphPage(texture.GetWidth(), texture.GetHeight(), alpha);
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
	private Font? Supplied(string fontName, out string path)
	{
		foreach (string extension in new[] { ".ttf", ".otf" })
		{
			path = _directories.Resolve($"gfx/fonts/{fontName}{extension}");
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
			CDiagnostics.Problem(path, 0, $"Could not load the typeface ({error})");
		}
		path = "";
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
