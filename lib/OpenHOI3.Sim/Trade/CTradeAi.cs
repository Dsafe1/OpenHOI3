namespace OpenHOI3.Sim.Trade;

/// <summary>
/// The trading of the hour. A played country under automate-trade first has the trades offered to it
/// answered (<see cref="CAutomatedTrades"/>). Then the install's Lua trades for every country nobody
/// plays and for a played one under automate-trade: each draws on the game's generator - one in six it
/// proposes trades, and failing that one in ten it looks again at those it has. Nothing while a game is
/// being made. See <c>docs/mechanics/trade.md</c>, sections 9 and 12.
/// </summary>
public static class CTradeAi
{
	public static void RunHourly(CGameState state)
	{
		if (state.IsBeingMade)
		{
			return;
		}
		foreach (Countries.CCountry country in state.Countries)
		{
			if (state.Played.Contains(country.Tag))
			{
				if (!state.AutomateTrade.Contains(country.Tag))
				{
					continue;
				}
				CAutomatedTrades.Answer(state, country);
			}
			if (state.AiScripts is not { } scripts)
			{
				continue;
			}
			if (state.Random.Next(6) == 0)
			{
				scripts.RunTrade(state, country, propose: true);
			}
			else if (state.Random.Next(10) == 0)
			{
				scripts.RunTrade(state, country, propose: false);
			}
		}
	}
}
