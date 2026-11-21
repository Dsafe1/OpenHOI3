namespace OpenHOI3.Gfx;

/// <summary>Where a font file's letters come from (<see cref="CFontSource"/>; <c>docs/mechanics/interface.md</c>, section 6).</summary>
public enum CFontSourceKind
{
	/// <summary>A <c>.ttf</c> or <c>.otf</c> of the font's name beside its descriptor: a mod's own typeface.</summary>
	Supplied,

	/// <summary>Its own glyph texture made into distance fields: a capital H of at least 38 pixels.</summary>
	OwnGlyphs,

	/// <summary>A shipped typeface standing for the one its descriptor names, at the size that fits the original's widths.</summary>
	StandIn,
}
