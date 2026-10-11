namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the window a leader is chosen in sorts its list by, numbered as the original
/// numbers them. The name is ascending and the other three descending. See
/// <c>docs/mechanics/leaders.md</c>, section 12.
/// </summary>
public enum ELeaderSort
{
	Name = 0,
	Skill = 1,
	Traits = 2,
	Rank = 3,
}
