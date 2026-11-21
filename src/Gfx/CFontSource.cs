using Godot;
using OpenHOI3.Content.Gui;

namespace OpenHOI3.Gfx;

/// <summary>
/// What a font file is made from, worked out once by <see cref="CGuiFonts"/> - so that the font is made from it and
/// the log can say of every font which way it went without making the large ones (<c>docs/mechanics/interface.md</c>,
/// section 6).
/// </summary>
public sealed class CFontSource
{
	public required CFontSourceKind Kind { get; init; }

	/// <summary>The descriptor, <c>gfx/fonts/&lt;name&gt;.fnt</c> in whichever layer has it; null where none has.</summary>
	public CFontDescriptor? Descriptor { get; init; }

	/// <summary>The descriptor's path, or empty.</summary>
	public string DescriptorPath { get; init; } = "";

	/// <summary>Whether a glyph texture was found and decoded.</summary>
	public bool HasTexture { get; init; }

	/// <summary>How tall the glyph texture's capital H stands, in its pixels; nought without a texture or an H.</summary>
	public int CapHeight { get; init; }

	/// <summary>The colour the original's glyphs were painted in.</summary>
	public Color Ink { get; init; } = Colors.White;

	/// <summary>The supplied typeface and its path, for <see cref="CFontSourceKind.Supplied"/>.</summary>
	public Font? Supplied { get; init; }
	public string SuppliedPath { get; init; } = "";

	/// <summary>The glyph texture's coverage, for <see cref="CFontSourceKind.OwnGlyphs"/> until its atlas is built.</summary>
	public CGlyphPage? Page { get; set; }
}
