using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Espionage;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// One mission's weight on the actor's presence in the target, set as it is given where the mission is one of the
/// nine, and the hour of the presence's last mission change stamped whatever happened. No rule on which mission may go
/// where is asked. See <c>docs/mechanics/espionage.md</c>, section 3.
/// </summary>
public sealed class CChangeSpyMissionCommand : CCommand
{
	public CChangeSpyMissionCommand(CCountryTag actor, CCountryTag target, int mission, int priority)
	{
		Actor = actor;
		Target = target;
		Mission = mission;
		Priority = priority;
	}

	public CCountryTag Actor { get; }

	public CCountryTag Target { get; }

	/// <summary>The mission's number, 0 to 8; any other only stamps the hour.</summary>
	public int Mission { get; }

	/// <summary>Its weight, 0 to 3 in practice.</summary>
	public int Priority { get; }

	public override bool MayExecute(CGameState state) => state.Country(Actor) is not null && state.Country(Target) is not null;

	public override void Execute(CGameState state)
	{
		if (state.Country(Actor) is not { } actor || state.Country(Target) is not { } target)
		{
			return;
		}
		CSpyPresence presence = CGameState.Presence(actor, target);
		// The original's test is signed, and a negative mission would write before its array; nothing posts one, and here
		// it only stamps the hour.
		if (Mission >= 0 && Mission < CSpyPresence.MissionCount)
		{
			presence.SetMission(Mission, Priority);
		}
		presence.LastMissionChange = state.TotalHours;
	}
}
