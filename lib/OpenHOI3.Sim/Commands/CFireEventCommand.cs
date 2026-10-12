using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Events;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// Fires an event for a country, or for a province of its, without asking its trigger: a trial by
/// hand, as the original's console's <c>event</c> command is. See <c>docs/mechanics/events.md</c>,
/// section 4.
/// </summary>
public sealed class CFireEventCommand : CCommand
{
	private readonly int _event;
	private readonly CCountryTag _country;
	private readonly int _province;

	public CFireEventCommand(int eventId, CCountryTag country, int province = 0)
	{
		_event = eventId;
		_country = country;
		_province = province;
	}

	public override bool MayExecute(CGameState state)
	{
		return Find(state) is not null && state.Country(_country) is not null;
	}

	public override void Execute(CGameState state)
	{
		CEvent fired = Find(state)!;
		// A province's event is the province's alone; a country's has the country.
		CEventScope scope = _province != 0
			? CEventScope.Start(null, _province, randomState: (uint)state.Random.Next())
			: CEventScope.Start(_country, randomState: (uint)state.Random.Next());
		CEventFiring.Fire(state, fired, scope);
	}

	private CEvent? Find(CGameState state)
	{
		foreach (CEvent known in state.Events)
		{
			if (known.Id == _event)
			{
				return known;
			}
		}
		return null;
	}
}
