namespace OpenHOI3.Content.Map;

/// <summary>
/// A <c>billboardType</c> of the interface's <c>.gfx</c> files: a flat picture on the map
/// that always faces the viewer. The one read for is <c>counter_arrow</c>, the pointer
/// beside the counter of a unit on the move. Lengths are in map units. See
/// <c>docs/mechanics/mapunits.md</c>, section 2.
/// </summary>
public sealed class CBillboardType
{
	public required string Name { get; init; }

	/// <summary><c>textureFile</c>: a strip of <see cref="Frames"/> pictures side by side.</summary>
	public string TextureFile { get; init; } = "";

	/// <summary><c>scale</c>: half the side of the square one picture is drawn in.</summary>
	public float Scale { get; init; } = 1f;

	/// <summary>
	/// <c>offset</c>: how far from the thing it belongs to the picture is drawn, in the
	/// direction it is turned to.
	/// </summary>
	public float Offset { get; init; }

	/// <summary><c>noOfFrames</c>.</summary>
	public int Frames { get; init; } = 1;

	/// <summary><c>font</c>: what a billboard that carries a number writes it in, or an empty string.</summary>
	public string Font { get; init; } = "";
}
