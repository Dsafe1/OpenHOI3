namespace OpenHOI3.Content.Map;

/// <summary>
/// An <c>arrowType</c> of the interface's <c>.gfx</c> files: the band drawn on the map
/// along the way a selected unit is going. <c>moveArrow</c> is the one for a march. See
/// <c>docs/mechanics/movement.md</c>, section 7.
/// </summary>
public sealed class CArrowType
{
	public required string Name { get; init; }

	/// <summary>
	/// <c>textureFile</c>: the arrow's picture, twice, one over the other - above, its
	/// outline, which is how the part of the way still ahead is drawn; below, the same
	/// filled, for the part already walked. In each the right half is the head and the
	/// middle column the shaft.
	/// </summary>
	public string TextureFile { get; init; } = "";

	/// <summary><c>bodytexture</c>. The base game names the same file.</summary>
	public string BodyTextureFile { get; init; } = "";

	/// <summary><c>size</c>. Read; how wide the band is drawn does not come from it.</summary>
	public float Size { get; init; }
}
