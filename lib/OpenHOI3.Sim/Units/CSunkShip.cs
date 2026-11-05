using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// A ship that has left the game, as the list of them keeps it: whose it was - none where a save named a country
/// that does not exist - and the ship as it was when it went. See <see cref="CSunkShips"/>.
/// </summary>
public sealed record CSunkShip(CCountryTag? Owner, CSubUnit Ship);
