namespace OpenHOI3.Sim.Aid;

/// <summary>
/// What a licence is for: a unit type by its key, how many a run, how many runs at once, and the money
/// paid for each unit. See <c>docs/mechanics/aid.md</c>, section 4.
/// </summary>
public sealed record CLicenceTerms(string UnitType, int Serial, int Parallel, CFixedPoint Money)
{
	/// <summary>The fee: money a unit, times the runs' units, times the runs, each product truncated in thousandths.</summary>
	public long Fee => Money.Raw * (Serial * 1000L) / 1000 * (Parallel * 1000L) / 1000;
}
