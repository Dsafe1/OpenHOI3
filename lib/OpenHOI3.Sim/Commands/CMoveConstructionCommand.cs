using System.Collections.Generic;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country moving a line of its build queue: to the front, to the back, or one place
/// either way. The order is the whole of a line's priority - the production share pays
/// for the first line first. See <c>docs/mechanics/buildqueue.md</c>, section 6.
/// </summary>
public sealed class CMoveConstructionCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly int _construction;
	private readonly EQueueMove _move;

	public CMoveConstructionCommand(CCountryTag country, int construction, EQueueMove move)
	{
		_country = country;
		_construction = construction;
		_move = move;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is { } country
			&& country.Constructions.Exists(construction => construction.Id == _construction);
	}

	public override void Execute(CGameState state)
	{
		List<CConstruction> queue = state.Country(_country)!.Constructions;
		int at = queue.FindIndex(construction => construction.Id == _construction);
		int to = _move switch
		{
			EQueueMove.First => 0,
			EQueueMove.Earlier => at - 1,
			EQueueMove.Later => at + 1,
			_ => queue.Count - 1,
		};

		// A line already at the end it is sent towards stays where it is.
		if (to < 0 || to >= queue.Count || to == at)
		{
			return;
		}
		CConstruction moved = queue[at];
		queue.RemoveAt(at);
		queue.Insert(to, moved);
	}
}
