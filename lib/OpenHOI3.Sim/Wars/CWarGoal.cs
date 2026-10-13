using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// A war goal: its casus belli type, the country holding it and the one it is against, and - where the
/// type asks for them - a third country and a region. Two goals are the same where all five agree. See
/// <c>docs/mechanics/war.md</c>, section 3.
/// </summary>
public sealed record CWarGoal(CCasusBelliType Type, CCountryTag Actor, CCountryTag Recipient)
{
	public CCountryTag? Country { get; init; }

	public CRegion? Region { get; init; }

	public override string ToString() => $"{Type.Name} {Actor}->{Recipient}" + (Country is { } third ? $" {third}" : "")
		+ (Region is { } region ? $" {region.Name}" : "");
}
