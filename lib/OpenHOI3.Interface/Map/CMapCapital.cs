using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// One capital marker on the map: the province it stands in, whose acting capital that is, and where it stands - the
/// province's city point, in map pixels with y counting north, as <c>positions.txt</c> writes it.
/// </summary>
public readonly record struct CMapCapital(int Province, CCountryTag Owner, float X, float Y);
