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

	/// <summary>The <c>.gfx</c> file that declared it and the line its block begins on, for a report of a fault in it (<c>docs/mechanics/logs.md</c>, section 4).</summary>
	public string File { get; init; } = "";
	public int Line { get; init; }

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

	/// <summary>
	/// For a <c>weatherBillboardType</c>, every <c>textureFile</c> in the file's order: the cloud, then the rain or snow
	/// under it - or the lightning, for a storm. See <c>docs/mechanics/weather.md</c>, section 6.
	/// </summary>
	public System.Collections.Generic.IReadOnlyList<string> TextureFiles { get; init; } = System.Array.Empty<string>();

	/// <summary>
	/// <c>effect</c>: for weather, <c>RainTech</c> or <c>SnowTech</c> - the technique of its effect file it is drawn with, and the
	/// key its effect is kept under, so every type of one <c>effect</c> draws through the first such type's file.
	/// </summary>
	public string Effect { get; init; } = "";

	/// <summary><c>effectFile</c>, as written: <c>gfx\\fx\\weather_billboard.fx</c> for the weather.</summary>
	public string EffectFile { get; init; } = "";
}
