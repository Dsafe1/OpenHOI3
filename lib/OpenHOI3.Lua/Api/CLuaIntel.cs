using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// A <c>CAIIntel</c> a script made: what one country knows of another's strength, its two figures worked out
/// as it is made. See <c>docs/mechanics/lua.md</c>, section 7.
/// </summary>
public sealed class CLuaIntel
{
	public CLuaIntel(CCountryTag us, CCountryTag them, float factor, float uncertainty)
	{
		Us = us;
		Them = them;
		Factor = factor;
		Uncertainty = uncertainty;
	}

	public CCountryTag Us { get; }

	public CCountryTag Them { get; }

	/// <summary>How well we know them, 1 for ten spies and no counter-intelligence.</summary>
	public float Factor { get; }

	/// <summary>How far a guess at their hidden units may stray, 0.2 to 1.</summary>
	public float Uncertainty { get; }
}
