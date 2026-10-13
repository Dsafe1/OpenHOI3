using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// One province a peace moves: the country that owns it and loses it, and the one it goes to. See
/// <c>docs/mechanics/war.md</c>, section 11.
/// </summary>
public sealed record CPeaceTransfer(int Province, CCountryTag Loser, CCountryTag Gainer);
