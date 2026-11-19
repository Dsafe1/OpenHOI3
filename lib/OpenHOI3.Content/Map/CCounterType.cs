using System.Numerics;
using OpenHOI3.Sim;

namespace OpenHOI3.Content.Map;

/// <summary>
/// A <c>CounterType</c> of the interface's <c>.gfx</c> files: how one kind of unit
/// counter is drawn on the map. The base game declares two, <c>unit_counter</c> for a
/// far view and <c>unit_counter_close</c> for a near one. Lengths are in map units, one
/// to a pixel of the province bitmap. See <c>docs/mechanics/mapunits.md</c>, section 2.
/// </summary>
public sealed class CCounterType
{
	public required string Name { get; init; }

	/// <summary>The <c>.gfx</c> file that declared it and the line its block begins on, for a report of a fault in it (<c>docs/mechanics/logs.md</c>, section 4).</summary>
	public string File { get; init; } = "";
	public int Line { get; init; }

	/// <summary><c>textureFile</c>: the plate.</summary>
	public string BackgroundFile { get; init; } = "";

	/// <summary><c>textureFile2</c>: red where the owner's colour goes, blue and green for the ring of a selected one.</summary>
	public string MaskFile { get; init; } = "";

	/// <summary><c>textureFile3</c>: a strip of <see cref="Frames"/> pictures of a size, X to XXXXXX.</summary>
	public string SizeFile { get; init; } = "";

	/// <summary><c>textureFile4</c>. Read and not drawn.</summary>
	public string ArrowFile { get; init; } = "";

	/// <summary><c>noOfFrames</c>.</summary>
	public int Frames { get; init; } = 1;

	/// <summary><c>scale</c>: half a counter's height, before its own scale.</summary>
	public float Scale { get; init; } = 1f;

	/// <summary><c>level_scale</c>: how much a counter grows for each step of size.</summary>
	public float LevelScale { get; init; }

	/// <summary><c>font</c>, by the name the interface files give it.</summary>
	public string Font { get; init; } = "";

	/// <summary><c>size</c>: the height of the two figures, in tenths of <see cref="Scale"/>.</summary>
	public float TextSize { get; init; }

	/// <summary>Whether a <c>shieldtype</c> was given: the counter carries its owner's flag.</summary>
	public bool HasFlag { get; init; }

	/// <summary><c>shieldtype</c>'s first two numbers, in <see cref="Scale"/>s.</summary>
	public float FlagWidth { get; init; }

	public float FlagHeight { get; init; }

	/// <summary><c>offset</c>: where the flag goes, in tenths of <see cref="Scale"/>; x east, z north.</summary>
	public Vector3 FlagOffset { get; init; }

	/// <summary><c>offset2</c>: where the two figures go.</summary>
	public Vector3 FiguresOffset { get; init; }

	/// <summary><c>offset3</c>: where the name goes.</summary>
	public Vector3 NameOffset { get; init; }

	/// <summary><c>color1</c> and <c>color2</c>: two of the colours a selected counter's ring may be.</summary>
	public CColor Color1 { get; init; } = CColor.White;

	public CColor Color2 { get; init; } = CColor.White;
}
