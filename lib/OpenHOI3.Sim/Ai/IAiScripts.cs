using System.Collections.Generic;
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
	/// What the seller's AI makes of an offer to buy a licence, by <c>DiploScore_LicenceTechnology</c>, handed
	/// the licence itself; 0 on a failure (<c>docs/mechanics/aid.md</c>, section 4).
	/// </summary>
	int LicenceScore(CGameState state, Agreements.COffer offer) => 0;

	/// <summary>
	/// <c>ProposeTrades(ai, tag)</c> where <paramref name="propose"/>, else
	/// <c>EvalutateExistingTrades(ai, tag)</c>: a country's AI trading for the hour, whose offers go
	/// to the state's outbox. See <c>docs/mechanics/trade.md</c>, section 9.
	/// </summary>
	void RunTrade(CGameState state, CCountry country, bool propose)
	{
	}

	/// <summary>
	/// <c>Laws(country)</c>: a country's laws looked at once by its AI, as a new game does for every country, whose
	/// changes go to the state's outbox. See <c>docs/mechanics/politics.md</c>, section 12.
	/// </summary>
	void RunLaws(CGameState state, CCountry country)
	{
	}

	/// <summary>
	/// <c>PoliticsMinister_Tick(minister)</c>: a country's politics minister's day - mobilising, its laws, its
	/// cabinet, its puppets and liberations, each by a draw - whose commands go to the state's outbox. See
	/// <c>docs/mechanics/politics.md</c>, section 12.
	/// </summary>
	void RunPolitics(CGameState state, CCountry country)
	{
	}

	/// <summary>
	/// <c>IntelligenceMinister_Tick(minister)</c>: a country's intelligence minister's day - one day in nine, its spies'
	/// priorities and missions at home and abroad - whose commands go to the state's outbox. See
	/// <c>docs/mechanics/espionage.md</c>, section 15.
	/// </summary>
	void RunIntelligence(CGameState state, CCountry country)
	{
	}

	/// <summary>
	/// The hour's trading of several countries at once, as the original runs it on its worker threads:
	/// each <see cref="RunTrade"/> in turn unless the scripts can run them side by side. Whichever way,
	/// what they post reaches the outbox in the order of <paramref name="runs"/>, and they change nothing
	/// else of the state. See <c>docs/mechanics/trade.md</c>, section 9.
	/// </summary>
	void RunTrades(CGameState state, IReadOnlyList<(CCountry Country, bool Propose)> runs)
	{
		foreach ((CCountry country, bool propose) in runs)
		{
			RunTrade(state, country, propose);
		}
	}
}
