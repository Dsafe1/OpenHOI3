namespace OpenHOI3.Content.Map;

/// <summary>
/// A <c>projectionType</c> of the interface's <c>.gfx</c> files: a picture laid flat on the map from above through the
/// install's <c>gfx/FX/project.fx</c>, which grows, pulses, spins and fades by these figures. The three the original has
/// are the marks of a unit: <c>selection_projection</c>, which the original lays under a selected unit only where
/// <c>alwaysCounters</c> is off and which is therefore never made here, and <c>legal_selection_projection</c> and
/// <c>illegal_selection_projection</c>, which a right click leaves on the province ordered. Each key's default is the
/// original's (<c>reversing/findings/FINDINGS-movement.md</c>, section 13.1). Lengths in map units, times in seconds.
/// See <c>docs/mechanics/movement.md</c>, section 7.
/// </summary>
public sealed class CProjectionType
{
	/// <summary>The effect every projection is drawn through, whatever its declaration says - it has no key for one [exe].</summary>
	public const string EffectFile = "project.fx";

	/// <summary>The technique of <see cref="EffectFile"/> it is drawn with.</summary>
	public const string Technique = "tec0";

	public required string Name { get; init; }

	/// <summary><c>textureFile</c>: the picture, laid square.</summary>
	public string TextureFile { get; init; } = "";

	/// <summary><c>size</c>: the side of the square the picture covers once it has grown. 1 unless said.</summary>
	public float Size { get; init; } = 1f;

	/// <summary><c>spin</c>: how fast it turns, in turns of 3.14 radians a second. Nought unless said.</summary>
	public float Spin { get; init; }

	/// <summary><c>pulsating</c>: whether its alpha pulses once it has grown.</summary>
	public bool Pulsating { get; init; }

	/// <summary><c>pulseLowest</c>: the alpha the pulse falls to. 0.4 unless said.</summary>
	public float PulseLowest { get; init; } = 0.4f;

	/// <summary><c>pulseSpeed</c>: the pulse's speed, in half-turns of 3.14 radians a second. 3 unless said.</summary>
	public float PulseSpeed { get; init; } = 3f;

	/// <summary><c>additative</c>: drawn by adding its colour, ONE / ONE, where otherwise it is mixed by its alpha.</summary>
	public bool Additive { get; init; }

	/// <summary><c>expanding</c>: the seconds it takes to grow from a tenth of a unit to <see cref="Size"/>. Nought, at once.</summary>
	public float Expanding { get; init; }

	/// <summary><c>duration</c>: the seconds it stays before it fades. Nought: for good, until taken away.</summary>
	public float Duration { get; init; }

	/// <summary><c>fadeout</c>: the seconds it takes to fade after <see cref="Duration"/>. Nought: gone at once.</summary>
	public float Fadeout { get; init; }
}
