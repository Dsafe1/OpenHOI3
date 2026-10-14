using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Countries;

/// <summary>The install's Lua with the hourly trading left out, for the politics and espionage trials: it takes most of an hour's time.</summary>
internal sealed class CPoliticsOnly : IAiScripts
{
	private readonly IAiScripts _scripts;

	public CPoliticsOnly(IAiScripts scripts)
	{
		_scripts = scripts;
	}

	public int? NonHistoricalRelation(CGameState state, CCountry country, CCountry other) =>
		_scripts.NonHistoricalRelation(state, country, other);

	public int DiploScore(CGameState state, string function, CCountry actor, CCountry recipient, CCountry observer) =>
		_scripts.DiploScore(state, function, actor, recipient, observer);

	public int OfferTradeScore(CGameState state, COffer offer) => _scripts.OfferTradeScore(state, offer);

	public int LicenceScore(CGameState state, COffer offer) => _scripts.LicenceScore(state, offer);

	public void RunLaws(CGameState state, CCountry country) => _scripts.RunLaws(state, country);

	public void RunPolitics(CGameState state, CCountry country) => _scripts.RunPolitics(state, country);

	public void RunIntelligence(CGameState state, CCountry country) => _scripts.RunIntelligence(state, country);
}
