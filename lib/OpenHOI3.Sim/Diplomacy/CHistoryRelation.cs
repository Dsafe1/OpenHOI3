using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Diplomacy;

/// <summary>
/// One <c>relation</c> entry of <c>history/diplomacy</c>: <see cref="First"/>'s relation towards
/// <see cref="Second"/> from <see cref="Start"/>, and back to 0 from <see cref="End"/>.
/// </summary>
public sealed record CHistoryRelation(CCountryTag First, CCountryTag Second, CFixedPoint Value, CDate Start, CDate? End);
