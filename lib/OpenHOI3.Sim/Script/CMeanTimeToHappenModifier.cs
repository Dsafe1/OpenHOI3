namespace OpenHOI3.Sim.Script;

/// <summary>
/// A <c>modifier</c> of a mean time to happen: a factor, to three places, and the conditions
/// under which it is applied, ANDed. See <c>docs/mechanics/script.md</c>, section 4.
/// </summary>
public sealed class CMeanTimeToHappenModifier
{
	public CMeanTimeToHappenModifier(CFixedPoint factor, CTrigger condition)
	{
		Factor = factor;
		Condition = condition;
	}

	/// <summary>One where none is written.</summary>
	public CFixedPoint Factor { get; }

	public CTrigger Condition { get; }
}
