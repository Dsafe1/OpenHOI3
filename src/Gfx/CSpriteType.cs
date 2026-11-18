using System;
using Godot;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Gfx;

/// <summary>
/// A sprite declaration from a <c>.gfx</c> file: the <c>GFX_*</c> name the GUI refers
/// to, plus the texture backing it.
/// </summary>
public sealed class CSpriteType
{
	/// <summary>The declaring key, e.g. <c>spriteType</c> or <c>corneredTileSpriteType</c>.</summary>
	public string Kind { get; init; } = "spriteType";

	public string Name { get; init; } = "";

	/// <summary>Game-relative, as written in the file.</summary>
	public string TextureFile { get; init; } = "";

	/// <summary>
	/// A masked shield has two textures and none of its own picture: what is drawn over
	/// the flag, and the mask the flag is cut with.
	/// </summary>
	public string OverlayFile { get; init; } = "";
	public string MaskFile { get; init; } = "";

	/// <summary>The effect the sprite is drawn through, as written: a masked shield's <c>maskedflag.fx</c>, a progress bar's <c>progress.fx</c>.</summary>
	public string EffectFile { get; init; } = "";

	/// <summary>A progress bar's <c>color</c> and <c>colortwo</c>: what a bar with no pictures fills its part and the rest with.</summary>
	public OpenHOI3.Sim.CColor Colour { get; init; }
	public OpenHOI3.Sim.CColor ColourTwo { get; init; }

	/// <summary>A <c>scrollingSprite</c>'s <c>step</c>: how many frames its picture is slid through, a step a frame.</summary>
	public int Step { get; init; } = 1;

	/// <summary>A picture slid along itself by its frame: the original's <c>scrollingSprite</c>, the minimap's day and night.</summary>
	public bool IsScrolling => Kind.Equals("scrollingSprite", StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// How many frames the texture holds, side by side. A frame is the texture's width
	/// divided by this, whatever the texture's resolution.
	/// </summary>
	public int FrameCount { get; init; } = 1;

	/// <summary>
	/// The <c>size</c> a declaration gives: the cell of a cornered tile, the size of a
	/// progress bar. Zero for ordinary sprites, which take theirs from the texture.
	/// </summary>
	public Vector2 TileSize { get; init; }

	/// <summary>How much of each edge of a cornered tile is kept unstretched.</summary>
	public Vector2 Border { get; init; }

	/// <summary>Never takes a click: what is under it does.</summary>
	public bool AlwaysTransparent { get; init; }

	/// <summary>
	/// <c>transparencecheck</c>: a button drawn with it lets a click through where its frame's pixel has no alpha
	/// (<c>docs/mechanics/interface.md</c>, section 5).
	/// </summary>
	public bool TransparenceCheck { get; init; }

	/// <summary><c>clicksound</c>: what a button drawn with it sounds where the button names no sound of its own.</summary>
	public string ClickSound { get; init; } = "";

	public bool IsTiled => Kind.Equals("corneredTileSpriteType", StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// A <c>tileSpriteType</c>: a quad of <see cref="TileSize"/>, its texture repeated the declared size over its own, in
	/// integer division, each way (<c>CTileSprite</c>; <c>docs/mechanics/interface.md</c>, section 5).
	/// </summary>
	public bool IsTileStrip => Kind.Equals("tileSpriteType", StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// A plain picture, a <c>spriteType</c>: a button drawn with one shows no words of its own, whatever its file or code
	/// gives it (<c>docs/mechanics/interface.md</c>, section 2).
	/// </summary>
	public bool IsPlain => Kind.Equals("spriteType", StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// A <c>textSpriteType</c>: its picture is the texture a text is rendered into, so a button on one with no font draws
	/// nothing (<c>docs/mechanics/interface.md</c>, section 2).
	/// </summary>
	public bool IsTextSprite => Kind.Equals("textSpriteType", StringComparison.OrdinalIgnoreCase);

	/// <summary>Draws a country's flag, not a picture of its own.</summary>
	public bool IsMaskedShield => Kind.Equals("maskedShieldType", StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// A bar that fills from the left: <see cref="OverlayFile"/> is drawn over the part
	/// that is filled and <see cref="MaskFile"/> over the rest, in a box of <see cref="TileSize"/>.
	/// </summary>
	public bool IsProgressBar => Kind.Equals("progressbartype", StringComparison.OrdinalIgnoreCase);

	/// <summary>A pie chart: its <c>size</c> alone, the slices given by the screen that shows it.</summary>
	public bool IsPieChart => Kind.Equals("PieChartType", StringComparison.OrdinalIgnoreCase);

	public bool IsLineChart => Kind.Equals("LineChartType", StringComparison.OrdinalIgnoreCase);

	/// <summary>A line chart's line, across, and whether it has an outline.</summary>
	public float LineWidth { get; init; } = 1f;

	public bool Outline { get; init; }

	/// <summary>A pie chart's <c>size</c>.</summary>
	public int PieSize { get; init; }

	/// <summary>A progress bar's <c>horizontal</c>: one that is not fills from its foot upwards.</summary>
	public bool Horizontal { get; init; } = true;

	public static CSpriteType FromNode(string kind, ClausewitzNode node)
	{
		return new CSpriteType
		{
			Kind = kind,
			Name = node.GetString("name"),
			TextureFile = node.GetString("textureFile"),
			OverlayFile = node.GetString("textureFile1"),
			MaskFile = node.GetString("textureFile2"),
			EffectFile = node.GetString("effectFile"),
			Colour = node.GetInterfaceColor("color"),
			ColourTwo = node.GetInterfaceColor("colortwo"),
			Step = Math.Max(1, node.GetInt("step", 1)),
			FrameCount = Math.Max(1, node.GetInt("noOfFrames", 1)),
			TileSize = node.GetVector2("size").ToGodot(),
			Border = node.GetVector2("borderSize").ToGodot(),
			AlwaysTransparent = node.GetBool("allwaystransparent"),
			TransparenceCheck = node.GetBool("transparencecheck"),
			ClickSound = node.GetString("clicksound"),
			PieSize = kind.Equals("PieChartType", StringComparison.OrdinalIgnoreCase) ? node.GetInt("size") : 0,
			Horizontal = !node.GetString("horizontal").Equals("no", StringComparison.OrdinalIgnoreCase),
			LineWidth = node.GetFloat("linewidth", 1f),
			Outline = node.GetBool("outline"),
		};
	}
}
