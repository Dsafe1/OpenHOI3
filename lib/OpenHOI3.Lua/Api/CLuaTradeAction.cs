using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Trade;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// A <c>CTradeAction</c> a script made: its actor and recipient, whether it makes or ends a route, the
/// hour it was made - which stays its date when posted - and the route it carries, at first from the
/// actor to the recipient with nothing either way. See <c>docs/mechanics/trade.md</c>, section 9.
/// </summary>
public sealed class CLuaTradeAction
{
	public CLuaTradeAction(CCountryTag actor, CCountryTag recipient, long date)
	{
		Actor = actor;
		Recipient = recipient;
		Date = date;
		Route = new CTradeRoute(actor, recipient, CTradeTerms.None, date);
	}

	public CCountryTag Actor { get; }

	public CCountryTag Recipient { get; }

	public long Date { get; }

	public bool Value { get; set; } = true;

	/// <summary>The route it carries: made again on each change, so one a script holds is as it was.</summary>
	public CTradeRoute Route { get; set; }

	/// <summary>
	/// The start of a standing route <c>SetRoute</c> copied, which the original's copy keeps and a save writes with the
	/// offer; none for a route the action made itself.
	/// </summary>
	public long? RouteStart { get; set; }

	/// <summary>The offer it stands for, proposed.</summary>
	public COffer Offer => new(EOfferKind.Trade, Actor, Recipient, EOfferStage.Proposed, Value, Date)
	{
		Deal = new CTradeDeal(Route.From, Route.To, Route.Terms) { Start = RouteStart },
	};
}
