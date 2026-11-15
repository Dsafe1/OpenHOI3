using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Trade;

/// <summary>
/// The trading of the hour, for the countries whose hour of the day it is: the original's list of countries - the null
/// country first, then the file's - cut in 24 runs of a 24th of their number, the last hour taking what is left over, so
/// each country trades once a day; the rebels and a country owning nothing are passed over. A played country under
/// automate-trade first has the trades offered to it answered (<see cref="CAutomatedTrades"/>). Then the install's Lua
/// trades for every country nobody plays and for a played one under automate-trade: each draws on the game's generator -
/// one in six it proposes trades, and failing that one in ten it looks again at those it has. Nothing while a game is
/// being made. See <c>docs/mechanics/trade.md</c>, sections 9 and 12.
///
/// The draws are made in the countries' order and the scripts are then run together, as the original
/// runs them on its worker threads (<see cref="Ai.IAiScripts.RunTrades"/>). That changes nothing: a
/// script only reads the state and posts to the outbox, which is filled in the countries' order. The
/// one thing that changes the state on the way, a player's automate-trade, is run in its place between
/// the countries before it and those after.
/// </summary>
public static class CTradeAi
{
	public static void RunHourly(CGameState state)
	{
		if (state.IsBeingMade)
		{
			return;
		}
		var runs = new List<(CCountry Country, bool Propose)>();
		// The original's index is ours plus one, its null country standing first (reversing/findings/FINDINGS-liveread.md,
		// section 104).
		int total = state.Countries.Count + 1;
		int share = total / CGameState.HoursPerDay;
		int hour = (int)(state.TotalHours % CGameState.HoursPerDay);
		int first = share * hour;
		int end = hour == CGameState.HoursPerDay - 1 ? total : first + share;
		COwnedCounts owned = COwnedCounts.Of(state);
		foreach (CCountry country in state.Countries)
		{
			int index = country.Index + 1;
			if (index < first || index >= end || country.Tag.Value == Revolts.CRebelFactions.Rebels
				|| owned.Count(country.Tag) <= 0 || country.AiPaused)
			{
				continue;
			}
			if (state.Played.Contains(country.Tag))
			{
				// The Lua trades for a played country under Automate Trade, or whose diplomacy is handed to the AI
				// (frontend.md, section 4); only Automate Trade answers the offers made to it.
				bool automated = state.AutomateTrade.Contains(country.Tag);
				if (!automated && !state.Automation.Has(Settings.EAutomation.Diplomacy))
				{
					continue;
				}
				if (automated)
				{
					RunScripts(state, runs);
					CAutomatedTrades.Answer(state, country);
				}
			}
			if (state.AiScripts is null)
			{
				continue;
			}
			if (state.AiRandom.Next(6, country.Tag, CRandom.TradeKind) == 0)
			{
				runs.Add((country, true));
			}
			else if (state.AiRandom.Next(10, country.Tag, CRandom.TradeKind) == 0)
			{
				runs.Add((country, false));
			}
		}
		RunScripts(state, runs);
	}

	/// <summary>Runs the scripts of the countries gathered so far, and forgets them.</summary>
	private static void RunScripts(CGameState state, List<(CCountry Country, bool Propose)> runs)
	{
		if (runs.Count == 0 || state.AiScripts is not { } scripts)
		{
			runs.Clear();
			return;
		}
		// What the scripts read that is otherwise made on first use is made now, before they read it side by
		// side: each is made again only where the state has moved on, which a script cannot do.
		long started = System.Diagnostics.Stopwatch.GetTimestamp();
		COwnedCounts.Of(state);
		CCountryFacts.Of(state);
		Supply.CSupplyNetworks.Refresh(state);
		_ = state.NavalDistances;
		_ = state.Positions;
		_ = state.ControlledCount(default);
		long made = System.Diagnostics.Stopwatch.GetTimestamp();
		scripts.RunTrades(state, runs);
		if (state.Profile is { } profile)
		{
			profile.Add("  of which the caches made first", made - started);
			profile.Add("  of which the scripts", System.Diagnostics.Stopwatch.GetTimestamp() - made);
		}
		runs.Clear();
	}
}
