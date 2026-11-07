namespace OpenHOI3.Content.Map;

/// <summary>
/// Where and how a province's name is written on the near map, as <c>positions.txt</c> gives it: <c>text_position</c> as
/// written, not cut - the centre of the bounding box and <see cref="Given"/> false where the file has none
/// (<c>reversing/findings/FINDINGS-movement.md</c>; the original's screenshot of 2026-11-07 names such provinces where the
/// centre is) - <c>text_rotation</c> in radians, 0 where not given, and <c>text_scale</c>, 1 where not given
/// (<c>reversing/findings/FINDINGS-farmap.md</c>, B.7.1; <c>docs/mechanics/mapdraw.md</c>, section 4).
/// </summary>
public readonly record struct CProvinceLabel(float X, float Y, bool Given, float Rotation, float Scale)
{
	public static readonly CProvinceLabel Default = new(0, 0, false, 0, 1);
}
