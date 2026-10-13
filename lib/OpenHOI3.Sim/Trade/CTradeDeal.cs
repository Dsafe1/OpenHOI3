using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Trade;

/// <summary>
/// The route an offer to trade carries: its <c>from</c>, its <c>to</c> and its terms. A new offer's
/// is the actor's and the recipient's; a cancellation carries a copy of the route it ends, which it
/// is matched with whoever cancels. See <c>docs/mechanics/trade.md</c>, sections 1 and 2.
/// </summary>
public sealed record CTradeDeal(CCountryTag From, CCountryTag To, CTradeTerms Terms);
