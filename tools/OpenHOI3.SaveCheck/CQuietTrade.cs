using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Countries;

/// <summary>The install's Lua with the hourly trading left out, for timing the trade trial against.</summary>
internal sealed class CQuietTrade : IAiScripts
{
	private readonly IAiScripts _scripts;

	public CQuietTrade(IAiScripts scripts)
	{
		_scripts = scripts;
	}

	public int? NonHistoricalRelation(CGameState state, CCountry country, CCountry other) =>
		_scripts.NonHistoricalRelation(state, country, other);

	public int DiploScore(CGameState state, string function, CCountry actor, CCountry recipient, CCountry observer) =>
		_scripts.DiploScore(state, function, actor, recipient, observer);

	public int OfferTradeScore(CGameState state, COffer offer) => _scripts.OfferTradeScore(state, offer);

	public void RunTrade(CGameState state, CCountry country, bool propose)
	{
	}
}
