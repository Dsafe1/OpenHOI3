using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Espionage;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A coup attempted, from the intelligence screen: <c>from</c> is the target and <c>to</c> the instigator, the
/// original's names the other way round from what they suggest. Nothing is re-checked here. See
/// <c>docs/mechanics/espionage.md</c>, section 11.
/// </summary>
public sealed class CPerformCoupCommand : CCommand
{
	public CPerformCoupCommand(CCountryTag from, CCountryTag to)
	{
		From = from;
		To = to;
	}

	/// <summary>The target.</summary>
	public CCountryTag From { get; }

	/// <summary>The instigator.</summary>
	public CCountryTag To { get; }

	public override bool MayExecute(CGameState state) => state.Country(From) is not null && state.Country(To) is not null;

	public override void Execute(CGameState state)
	{
		if (state.Country(From) is { } target && state.Country(To) is { } instigator)
		{
			CCoup.Perform(state, instigator, target);
		}
	}
}
