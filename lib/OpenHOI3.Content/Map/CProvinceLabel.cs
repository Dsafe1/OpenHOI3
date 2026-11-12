namespace OpenHOI3.Content.Map;

/// <summary>
/// Where and how a province's name is written on the near map, as <c>positions.txt</c> gives it: <c>text_position</c> as
/// written, not cut - and where the file has none, <see cref="Given"/> false and the centre of the bounding box
/// <see cref="FallbackSouth"/> map pixels south, the point the original sets before it reads the file
/// (<c>reversing/findings/FINDINGS-provincenamepoint.md</c>) - <c>text_rotation</c> in radians, 0 where not given, and
/// <c>text_scale</c>, 1 where not given (<c>reversing/findings/FINDINGS-farmap.md</c>, B.7.1;
/// <c>docs/mechanics/mapdraw.md</c>, section 4).
/// </summary>
public readonly record struct CProvinceLabel(float X, float Y, bool Given, float Rotation, float Scale)
{
	public static readonly CProvinceLabel Default = new(0, 0, false, 0, 1);

	/// <summary>
	/// How far south of the bounding box's centre a name stands where the file gives no <c>text_position</c>, in map pixels
	/// whatever its scale [exe]. The centre alone put Bautzen, Lauban, Sagar and the others 1.9 to 2 pixels north of the
	/// original's names in the maintainer's screenshot, 2026-11-12.
	/// </summary>
	public const float FallbackSouth = 2f;
}
