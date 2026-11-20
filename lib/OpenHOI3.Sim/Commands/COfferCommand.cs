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

	/// <summary>A proposal a diplomat would wait for is thrown away at its post while one is on his way (agreements.md, section 1).</summary>
	public override bool MayPost(CGameState state)
	{
		return !COfferRules.RefusedAtPost(state, Offer);
	}

	public override void Execute(CGameState state)
	{
		if (!COfferRules.IsValid(state, Offer) || state.Country(Offer.Actor) is not { } actor
			|| state.Country(Offer.Recipient) is not { } recipient)
		{
			return;
		}
		// Every proposal but a call to arms sends a diplomat (agreements.md, section 1; war.md, section 6).
		if (Offer.Stage == EOfferStage.Proposed && Offer.Kind != EOfferKind.CallAlly && !(Offer.Kind == EOfferKind.Expedition && !Offer.Value))
		{
			CGameState.Status(actor, recipient).DiplomatUntil = Offer.Date + COfferRules.DiplomatHours;
		}
		CPacts.Apply(state, Offer);
		var queued = Offer with { Queued = state.TotalHours };
		(Offer.Stage == EOfferStage.Proposed ? recipient : actor).PendingOffers.Add(queued);
	}
}
