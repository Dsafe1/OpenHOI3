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
	/// <c>ForeignMinister_Tick(minister)</c>: a country's foreign minister's day - its decisions, calls to arms, transit
	/// rights, wars, peace, influence and alignment - whose offers go to the state's outbox or its proposal. A failure is
	/// reported. See <c>docs/mechanics/foreignminister.md</c>, section 1.
	/// </summary>
	void RunForeign(CGameState state, CCountry country)
	{
	}

	/// <summary>
	/// <c>ForeignMinister_EvaluateDecision(minister, decision, scope)</c>: what a country's foreign minister makes of a
	/// decision open to it, a whole number to the nearest - above nought takes it - or null where the call failed or did
	/// not answer a number, with why in <paramref name="error"/>: reported by the tick it ends, not here. See
	/// <c>docs/mechanics/foreignminister.md</c>, section 2.
	/// </summary>
	int? EvaluateDecision(CGameState state, CCountry country, Script.CDecision decision, Script.CEventScope scope,
		out string? error)
	{
		error = null;
		return null;
	}

	/// <summary>
	/// <c>ForeignMinister_OnWar(minister, joiner, enemy, war)</c>, told to a country's foreign minister of a new pair at
	/// war. A failure is reported. See <c>docs/mechanics/foreignminister.md</c>, section 8.
	/// </summary>
	void ForeignOnWar(CGameState state, CCountry country, CCountryTag joiner, CCountryTag enemy, Wars.CWar war)
	{
	}

	/// <summary>
	/// <c>TechMinister_Tick(minister, sliders, research)</c>: a country's tech minister's day - its leadership shares set and its
	/// free research slots filled - whose commands go to the state's outbox. See <c>docs/mechanics/ministers.md</c>,
	/// section 2.
	/// </summary>
	void RunTech(CGameState state, CCountry country, bool sliders = true, bool research = true)
	{
	}

	/// <summary>
	/// <c>ProductionMinister_Tick(minister)</c>: a country's production minister's day - convoys, units and buildings
	/// ordered - whose commands go to the state's outbox. See <c>docs/mechanics/ministers.md</c>, section 4.
	/// </summary>
	void RunProduction(CGameState state, CCountry country)
	{
	}

	/// <summary>
	/// <c>BalanceProductionSliders(ai, country, level, lendLease, ...)</c> at the end of a country's day, handed each share's
	/// need over its IC: level 0 and no lend-lease for the AI, the player's slider automation level and his lend-lease share
	/// for his own; false where the call failed, which leaves the rest of the country's day. See
	/// <c>docs/mechanics/ministers.md</c>, section 5.
	/// </summary>
	bool BalanceProductionSliders(CGameState state, CCountry country, int level = 0, double lendLease = 0) => true;

	/// <summary><c>BalanceLendLeaseSliders(ai, country, tags, values)</c>, for a country that gives lend-lease to anyone.</summary>
	void BalanceLendLeaseSliders(CGameState state, CCountry country)
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
