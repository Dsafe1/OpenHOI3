using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Aid;

/// <summary>
/// One giver of a country's lend-lease and what has come from it and is not yet made IC, in thousandths
/// of IC. See <c>docs/mechanics/aid.md</c>, section 3.
/// </summary>
public sealed class CLendLeaseReceipt
{
	public CLendLeaseReceipt(CCountryTag giver)
	{
		Giver = giver;
	}

	public CCountryTag Giver { get; }

	public long Amount { get; set; }
}
