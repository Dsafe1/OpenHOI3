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
	/// <c>textureFile</c>: loaded by the original and never drawn - <see cref="BodyTextureFile"/> is. The base game's
	/// picture is the arrow twice, one over the other - above, its outline, which is how the part of the way still ahead
	/// is drawn; below, the same filled, for the part already walked. In each the right half is the head and the middle
	/// column the shaft.
	/// </summary>
	public string TextureFile { get; init; } = "";

	/// <summary>
	/// <c>bodytexture</c>: the picture the arrow is drawn with - <c>textureFile</c> is loaded and never drawn
	/// (<c>reversing/findings/FINDINGS-movement.md</c>, section 11.7). The base game names the same file for both.
	/// </summary>
	public string BodyTextureFile { get; init; } = "";

	/// <summary><c>effect</c>, the arrow's effect file as written - which counts only for the first type loaded.</summary>
	public string Effect { get; init; } = "";

	/// <summary><c>size</c>. Read; how wide the band is drawn does not come from it.</summary>
	public float Size { get; init; }
}
