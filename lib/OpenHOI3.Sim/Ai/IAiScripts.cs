using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// What the simulation asks of the install's Lua, which the original's AI is written in. The
/// simulation references nothing, so it states here what it asks and the Lua project answers it.
/// See <c>docs/mechanics/lua.md</c>.
/// </summary>
public interface IAiScripts
{
	/// <summary>
	/// <c>AI_GenerateNonHistoricalRelation(country, other)</c>: the relation a new game gives
	/// <paramref name="country"/> towards <paramref name="other"/> where nothing else has, in whole
	/// points - or null where the script has no such function or failed, which it reports.
	/// </summary>
	int? NonHistoricalRelation(CGameState state, CCountry country, CCountry other);

	/// <summary>
	/// <c>DiploScore_&lt;kind&gt;(ai, actor, recipient, observer)</c>: what the recipient's AI
	/// makes of an offer, the Lua function named by <paramref name="function"/>. 0 where the call
	/// fails, which it reports - the offer refused, as in the original. See
	/// <c>docs/mechanics/lua.md</c>, section 6.
	/// </summary>
	int DiploScore(CGameState state, string function, CCountry actor, CCountry recipient, CCountry observer);

	/// <summary>
	/// <c>DiploScore_OfferTrade(ai, actor, recipient, observer, action, tradedFrom, tradedTo)</c>: what
	/// the recipient's AI makes of an offer to trade, handed the offer itself and its two blocks. 0
	/// where the call fails. See <c>docs/mechanics/trade.md</c>, section 9.
	/// </summary>
	int OfferTradeScore(CGameState state, Agreements.COffer offer) => 0;

	/// <summary>
	/// <c>ProposeTrades(ai, tag)</c> where <paramref name="propose"/>, else
	/// <c>EvalutateExistingTrades(ai, tag)</c>: a country's AI trading for the hour, whose offers go
	/// to the state's outbox. See <c>docs/mechanics/trade.md</c>, section 9.
	/// </summary>
	void RunTrade(CGameState state, CCountry country, bool propose)
	{
	}
}
