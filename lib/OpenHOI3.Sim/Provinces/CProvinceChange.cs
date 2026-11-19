using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// One change to a province made in play, as its history keeps it: the hour, the key - <c>controller</c>, <c>owner</c>,
/// <c>add_core</c> and <c>remove_core</c> with a country, <c>manpower</c> and <c>leadership</c> with what it is now. A save
/// writes each as a dated block of its own (<c>docs/mechanics/savegame.md</c>, section 4).
/// </summary>
public sealed record CProvinceChange(long Hour, string Key, CCountryTag? Country, CFixedPoint Amount);
