using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// The answer to an event: a person's choice, an event that answered itself, or the AI's. It runs
/// the option's effects in the event's scope, the scope's state of chance first set back to its
/// seed, and takes the event off the open ones. It is dropped where the event no longer exists; it
/// asks neither the trigger nor whether the event was open. See <c>docs/mechanics/events.md</c>,
/// section 7.
/// </summary>
public sealed class CSelectEventOptionCommand : CCommand
{
	public CSelectEventOptionCommand(CEvent answered, CEventScope scope, int option)
	{
		Event = answered;
		Scope = scope;
		Option = option;
	}

	public CEvent Event { get; }

	public CEventScope Scope { get; }

	public int Option { get; }

	public override bool MayExecute(CGameState state)
	{
		foreach (CEvent known in state.Events)
		{
			if (known == Event)
			{
				return true;
			}
		}
		return false;
	}

	public override void Execute(CGameState state)
	{
		state.OpenEvents.RemoveAll(open => open.Event == Event && open.Scope.Country == Scope.Country);
		// Its message is raised before the option runs, so its text tells of the game before it.
		state.RaiseEventAnswered(Event, Scope, Option);
		Scope.ResetToSeed();
		if (Option >= 0 && Option < Event.Options.Count)
		{
			CEffects.Run(state, Event.Options[Option].Effects, Scope);
		}
	}
}
