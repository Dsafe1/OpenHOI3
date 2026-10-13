using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Agreements;

/// <summary>
/// One country's offer to another: its kind, who makes it and to whom, its stage, whether it makes
/// something (yes) or ends it (no), and the hour it was made. <see cref="Queued"/> is the hour it was
/// last put on a country's list of offers, which the 96-hour drop counts from. See
/// <c>docs/mechanics/agreements.md</c>, section 1.
/// </summary>
public sealed record COffer(EOfferKind Kind, CCountryTag Actor, CCountryTag Recipient, EOfferStage Stage, bool Value, long Date)
{
	public long Queued { get; init; }
}
