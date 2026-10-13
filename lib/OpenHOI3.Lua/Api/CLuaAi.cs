using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// A country's AI as a script is handed it - <c>CAI</c>, the original's <c>CEU3AI</c>. Every country
/// has one there, the player's included; here it is the country it speaks for.
/// </summary>
public sealed class CLuaAi
{
	public CLuaAi(CCountry country)
	{
		Country = country;
	}

	public CCountry Country { get; }
}
