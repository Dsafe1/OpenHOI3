using System.Diagnostics;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Countries;

/// <summary>The install's Lua with the hourly trading and the trade scores timed, for the trade trial.</summary>
internal sealed class CTimedTrade : IAiScripts
{
	private readonly IAiScripts _scripts;

	public CTimedTrade(IAiScripts scripts)
	{
		_scripts = scripts;
	}

	public Stopwatch Trading { get; } = new();

	public Stopwatch Scoring { get; } = new();

	public int Runs { get; private set; }

	public int Scores { get; private set; }

	public int? NonHistoricalRelation(CGameState state, CCountry country, CCountry other) =>
		_scripts.NonHistoricalRelation(state, country, other);

	public int DiploScore(CGameState state, string function, CCountry actor, CCountry recipient, CCountry observer) =>
		_scripts.DiploScore(state, function, actor, recipient, observer);

	public int OfferTradeScore(CGameState state, COffer offer)
	{
		Scores++;
		bool outer = !Scoring.IsRunning;
		if (outer)
		{
			Scoring.Start();
		}
		try
		{
			return _scripts.OfferTradeScore(state, offer);
		}
		finally
		{
			if (outer)
			{
				Scoring.Stop();
			}
		}
	}

	public void RunTrade(CGameState state, CCountry country, bool propose)
	{
		Runs++;
		Trading.Start();
		_scripts.RunTrade(state, country, propose);
		Trading.Stop();
	}
}
