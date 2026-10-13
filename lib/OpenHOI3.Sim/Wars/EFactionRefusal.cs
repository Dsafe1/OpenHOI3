namespace OpenHOI3.Sim.Wars;

/// <summary>Why a Join Faction or Invite to Faction row is greyed, in the order they are asked (<c>factions.md</c>, section 3).</summary>
public enum EFactionRefusal
{
	None,
	Self,
	EnRoute,
	Puppet,
	Influence,
	Neutrality,
	Distance,
	War,
}
