using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Gfx;

/// <summary>
/// One <c>bitmapfont</c> entry from a <c>.gfx</c> file. The logical name is what the
/// GUI asks for; the font name selects a file in <c>gfx/fonts</c>, and the colour
/// tints it - several logical fonts differ only by colour.
/// </summary>
public sealed class CBitmapFont
{
	public string Name { get; init; } = "";
	public string FontName { get; init; } = "";
	public Color Color { get; init; } = Colors.White;
	public Font? Font { get; set; }
	public int Size { get; set; }
}

/// <summary>
/// The game's fonts, as AngelCode BMFont <c>.fnt</c> descriptors with <c>.tga</c>
/// atlas pages. Godot reads that format directly, so no glyph parsing is needed here.
/// </summary>
public sealed class CBitmapFonts
{
	private readonly CDirectorySettings _directories;
	private readonly Dictionary<string, CBitmapFont> _fonts = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, Font?> _files = new(StringComparer.OrdinalIgnoreCase);

	private CBitmapFonts(CDirectorySettings directories)
	{
		_directories = directories;
	}

	public int Count => _fonts.Count;

	public static CBitmapFonts Load(CDirectorySettings directories)
	{
		var registry = new CBitmapFonts(directories);
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
					var font = new CBitmapFont
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
	/// treated as direct file names, which is how several front end fonts are written.
	/// </summary>
	public CBitmapFont? Find(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		if (!_fonts.TryGetValue(name, out CBitmapFont? font))
		{
			font = new CBitmapFont { Name = name, FontName = name };
			_fonts[name] = font;
		}
		font.Font ??= LoadFontFile(font.FontName);
		return font.Font is null ? null : font;
	}

	private Font? LoadFontFile(string fontName)
	{
		if (_files.TryGetValue(fontName, out Font? cached))
		{
			return cached;
		}

		Font? loaded = null;
		string path = _directories.Resolve($"gfx/fonts/{fontName}.fnt");
		if (File.Exists(path))
		{
			var file = new FontFile();
			Error error = file.LoadBitmapFont(path);
			if (error == Error.Ok)
			{
				AttachTexturePage(file, fontName);
				loaded = file;
			}
			else
			{
				CDiagnostics.Warn($"Could not load font '{fontName}' ({error})");
			}
		}
		else
		{
			CDiagnostics.Warn($"Font file missing: {path}");
		}

		_files[fontName] = loaded;
		return loaded;
	}

	/// <summary>
	/// Supplies the glyph atlas by hand.
	///
	/// The game's descriptors say <c>pages=1</c> but omit the <c>page</c> line that
	/// names the atlas file, because the original loader assumed the image shares the
	/// descriptor's base name. A standards-compliant reader like Godot's therefore
	/// ends up with glyph rectangles but no texture, and every glyph renders blank.
	/// </summary>
	private void AttachTexturePage(FontFile file, string fontName)
	{
		string? texturePath = _directories.ResolveTexture($"gfx/fonts/{fontName}.tga");
		if (texturePath is null)
		{
			CDiagnostics.Warn($"No glyph atlas alongside font '{fontName}'");
			return;
		}

		var image = new Image();
		Error error = image.LoadTgaFromBuffer(File.ReadAllBytes(texturePath));
		if (error != Error.Ok)
		{
			CDiagnostics.Warn($"Could not decode glyph atlas '{texturePath}' ({error})");
			return;
		}

		var sizeKey = new Vector2I(file.FixedSize, 0);
		file.SetTextureImage(0, sizeKey, 0, image);
	}
}
