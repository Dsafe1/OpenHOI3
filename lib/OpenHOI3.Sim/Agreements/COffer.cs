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

	/// <summary>An offer to trade's route (<c>docs/mechanics/trade.md</c>); none for any other kind.</summary>
	public Trade.CTradeDeal? Deal { get; init; }

	/// <summary>The theory an offer to share technology shares (<c>aid.md</c>, section 2); empty for the rest.</summary>
	public string Category { get; init; } = "";

	/// <summary>What a licence is for (<c>aid.md</c>, section 4); none for the rest, or a licence with nothing chosen.</summary>
	public Aid.CLicenceTerms? Licence { get; init; }

	/// <summary>The war goal a declaration or an added goal carries (<c>war.md</c>, sections 3 and 5); none for the rest.</summary>
	public Wars.CWarGoal? WarGoal { get; init; }

	/// <summary>The unit an expeditionary force sends or takes back (<c>expeditions.md</c>); 0 for the rest, or for one not chosen yet.</summary>
	public int Unit { get; init; }

	/// <summary>A withdrawal of an expeditionary force by its lender, where otherwise the host gives it back.</summary>
	public bool Recall { get; init; }

	/// <summary>The enemy a call to arms is against (<c>war.md</c>, section 6); none for the rest.</summary>
	public CCountryTag? Versus { get; init; }
}
