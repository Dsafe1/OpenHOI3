using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Aid;

/// <summary>
/// One receiver of a country's lend-lease: its weight in the day's sharing out, 500 when it begins and as
/// the player or the AI sets it after, and the share of what is sent that arrives, in thousandths - 1000
/// over land, a convoy's efficiency by sea, nought with neither. See <c>docs/mechanics/aid.md</c>,
/// section 3.
/// </summary>
public sealed class CLendLeaseGift
{
	public const long StartingWeight = 500;

	public CLendLeaseGift(CCountryTag receiver)
	{
		Receiver = receiver;
	}

	public CCountryTag Receiver { get; }

	public long Weight { get; set; } = StartingWeight;

	public long Effectiveness { get; set; }
}
