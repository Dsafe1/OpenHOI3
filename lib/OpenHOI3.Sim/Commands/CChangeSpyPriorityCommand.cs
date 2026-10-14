using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// The priority the actor's free spies are placed in the target by - the target the actor itself for its home - set as
/// it is given, with no test of its range. See <c>docs/mechanics/espionage.md</c>, section 3.
/// </summary>
public sealed class CChangeSpyPriorityCommand : CCommand
{
	public CChangeSpyPriorityCommand(CCountryTag actor, CCountryTag target, int priority)
	{
		Actor = actor;
		Target = target;
		Priority = priority;
	}

	public CCountryTag Actor { get; }

	public CCountryTag Target { get; }

	public int Priority { get; }

	public override bool MayExecute(CGameState state) => state.Country(Actor) is not null && state.Country(Target) is not null;

	public override void Execute(CGameState state)
	{
		if (state.Country(Actor) is { } actor && state.Country(Target) is { } target)
		{
			CGameState.Presence(actor, target).Priority = Priority;
		}
	}
}
