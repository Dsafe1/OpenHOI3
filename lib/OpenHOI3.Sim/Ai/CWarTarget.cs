using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// A war a country's AI has prepared: against whom, the decision taken in its place when it is ready, whether it is to be
/// a limited war, the hour it was first prepared and the value the script gave. See
/// <c>docs/mechanics/foreignminister.md</c>, section 4.
/// </summary>
public sealed class CWarTarget
{
	public CWarTarget(CCountryTag target, Script.CDecision? decision, bool limited, long hour)
	{
		Target = target;
		Decision = decision;
		Limited = limited;
		Hour = hour;
	}

	public CCountryTag Target { get; }

	/// <summary>The decision posted when the war is ready, in place of the war; none for a war to be declared.</summary>
	public Script.CDecision? Decision { get; }

	public bool Limited { get; }

	/// <summary>The hour it was first prepared, which a later call for the same target does not move.</summary>
	public long Hour { get; }

	/// <summary>The value the script last gave, a single-precision number kept by its bits; nothing reads it here.</summary>
	public int ValueBits { get; set; }
}
