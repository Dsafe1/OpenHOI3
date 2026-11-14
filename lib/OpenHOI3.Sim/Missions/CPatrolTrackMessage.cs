using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// What a patrol that takes up a new fleet to shadow tells its country's player, <c>PATROLTRACK</c>: the sea province both
/// are in, the patrol's country and the shadowed fleet's, the patrol by its number, and the two fleets' names. Its words
/// are made where it is shown. See <c>docs/mechanics/navalmissions.md</c>, section 3.1.
/// </summary>
/// <param name="Told">The patrol's country; nobody else's player is told. The message's first country.</param>
/// <param name="Province">Where the patrol is, which is where the fleet it shadows is.</param>
/// <param name="Other">The shadowed fleet's country, the message's second.</param>
/// <param name="Patrol">The patrol's number, which the message carries though its Goto goes to the province.</param>
/// <param name="PatrolName">The patrol's name, <c>$OUR_FLEET$</c>.</param>
/// <param name="OtherName">The shadowed fleet's name, <c>$OTHERS$</c>.</param>
public sealed record CPatrolTrackMessage(CCountryTag Told, int Province, CCountryTag Other, int Patrol, string PatrolName,
	string OtherName)
{
	public const string Type = "PATROLTRACK";
}
