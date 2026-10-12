using OpenHOI3.Sim.Events;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A decision taken, by its name, in a scope: asked again just before it runs - <c>potential</c> and
/// <c>allow</c> - then its effects run and the country's modifier is made again. Nothing records it,
/// so it may be taken again. See <c>docs/mechanics/events.md</c>, section 9.
/// </summary>
public sealed class CExecuteDecisionCommand : CCommand
{
	public CExecuteDecisionCommand(string decision, CEventScope scope)
	{
		Decision = decision;
		Scope = scope;
	}

	public string Decision { get; }

	public CEventScope Scope { get; }

	public override bool MayExecute(CGameState state)
	{
		return CDecisions.Find(state, Decision) is { } decision && CDecisions.MayTake(state, decision, Scope);
	}

	public override void Execute(CGameState state)
	{
		if (CDecisions.Find(state, Decision) is not { } decision)
		{
			return;
		}
		// Its message goes out before its effects run.
		state.RaiseDecisionTaken(decision, Scope);
		Scope.ResetToSeed();
		CEffects.Run(state, decision.Effects, Scope);
		if (Scope.Province != 0)
		{
			CProvinceModifiers.Rebuild(state);
		}
		if (state.Country(Scope.Country) is { } country)
		{
			CCountryModifiers.RebuildOne(state, country);
		}
	}
}
