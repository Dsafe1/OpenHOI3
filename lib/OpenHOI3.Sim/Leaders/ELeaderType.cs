namespace OpenHOI3.Sim.Leaders;

/// <summary>
/// What a leader commands, in the original's order: a general, an admiral or an air
/// marshal. The number is what a trait's <c>allowed_leader</c> and the gainable traits
/// are looked up by. See <c>docs/mechanics/leaders.md</c>, sections 1 and 4.
/// </summary>
public enum ELeaderType
{
	Land = 0,
	Sea = 1,
	Air = 2,
}
