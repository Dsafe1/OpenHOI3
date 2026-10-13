using OpenHOI3.Sim.Agreements;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// An offer made or answered: nothing where it is not valid; a proposal sends the actor's diplomat
/// for seven days; the offer takes effect for its stage and value; and it goes on the recipient's
/// list of offers if it is a proposal, else on the actor's. See <c>docs/mechanics/agreements.md</c>,
/// section 1.
/// </summary>
public sealed class COfferCommand : CCommand
{
	public COfferCommand(COffer offer)
	{
		Offer = offer;
	}

	public COffer Offer { get; }

	public override void Execute(CGameState state)
	{
		if (!COfferRules.IsValid(state, Offer) || state.Country(Offer.Actor) is not { } actor
			|| state.Country(Offer.Recipient) is not { } recipient)
		{
			return;
		}
		if (Offer.Stage == EOfferStage.Proposed)
		{
			CGameState.Status(actor, recipient).DiplomatUntil = Offer.Date + COfferRules.DiplomatHours;
		}
		CPacts.Apply(state, Offer);
		var queued = Offer with { Queued = state.TotalHours };
		(Offer.Stage == EOfferStage.Proposed ? recipient : actor).PendingOffers.Add(queued);
	}
}
