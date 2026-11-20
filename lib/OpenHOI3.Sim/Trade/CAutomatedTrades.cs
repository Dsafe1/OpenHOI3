using System.Collections.Generic;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Trade;

/// <summary>
/// The trades offered to a player under automate-trade, answered each hour: every proposal that makes
/// a route is scored as the AI scores one and rolled - **with the C runtime's own <c>rand()</c>, seeded
/// to 1 at every call**, so the rolls are the same each hour: 47, 0, 56, 24, 68... A roll below the score
/// accepts, the first only - a later trade the roll would accept is kept for the next hour - and any
/// other roll declines; there is no cut at 30 here. Whatever is not a proposal that makes a route is
/// dropped. See <c>docs/mechanics/trade.md</c>, section 12.
/// </summary>
public static class CAutomatedTrades
{
	public static void Answer(CGameState state, CCountry country)
	{
		if (country.PendingOffers.Count == 0)
		{
			return;
		}
		var rand = new CRuntimeRand(1);
		var kept = new List<COffer>();
		bool accepted = false;
		foreach (COffer offer in country.PendingOffers)
		{
			if (offer.Kind != EOfferKind.Trade)
			{
				kept.Add(offer);
				continue;
			}
			if (offer.Stage != EOfferStage.Proposed || !offer.Value)
			{
				continue;
			}
			int score = CPactPasses.Score(state, offer);
			int roll = rand.Next() * rand.Next() % 100;
			if (roll < score)
			{
				if (accepted)
				{
					kept.Add(offer);
					continue;
				}
				accepted = true;
				state.Outbox.Add(new COfferCommand(offer with { Stage = EOfferStage.Accepted }));
			}
			else
			{
				state.Outbox.Add(new COfferCommand(offer with { Stage = EOfferStage.Declined }));
			}
		}
		// Through the step, so that a machine playing the AI back has the list as this one leaves it.
		state.DoInStep(new CKeepOffersCommand(country.Tag, kept));
	}
}
