using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Interface.Screens;

/// <summary>A row of the diplomacy panel's list of influence: the country, and what it says beside it.</summary>
public sealed record CDriftRow(CCountry Country, string Name, string Influence);
