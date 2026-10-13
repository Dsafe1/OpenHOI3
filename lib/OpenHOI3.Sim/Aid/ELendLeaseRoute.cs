namespace OpenHOI3.Sim.Aid;

/// <summary>
/// How lend-lease would go from one country to another, by the original's numbers; or that it may not.
/// See <c>docs/mechanics/aid.md</c>, section 3.
/// </summary>
public enum ELendLeaseRoute
{
	Refused = 0,
	Convoy = 1,
	Neighbour = 2,
	Teleport = 3,
}
