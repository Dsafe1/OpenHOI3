using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// For trying things out: puts two countries at war with each other, there and then, and nobody else.
/// It is not a declaration of war (<c>docs/mechanics/war.md</c>, section 5) - nothing is asked, nobody
/// is called in and nothing is charged - which is what the savegame tool's trials want. The two join
/// the war as any joiner does, and their leave to run a debt with each other is cleared as a
/// declaration clears it. See <c>docs/mechanics/landcombat.md</c>, section 10.
/// </summary>
public sealed class CStartWarCommand : CCommand
{
	private readonly CCountryTag _attacker;
	private readonly CCountryTag _defender;

	public CStartWarCommand(CCountryTag attacker, CCountryTag defender)
	{
		_attacker = attacker;
		_defender = defender;
	}

	public override bool MayExecute(CGameState state)
	{
		return _attacker != _defender && state.Country(_attacker) is not null && state.Country(_defender) is not null
			&& CWarSides.Between(state, _attacker, _defender) is null;
	}

	public override void Execute(CGameState state)
	{
		var war = new CWar($"{_attacker}-{_defender}", state.Date) { Action = state.TotalHours };
		state.Wars.Add(war);
		CWarSides.Join(state, war, _attacker, attacking: true);
		CWarSides.Join(state, war, _defender, attacking: false);
		if (state.Country(_attacker) is { } attacker && state.Country(_defender) is { } defender)
		{
			CGameState.Status(attacker, defender).AllowDebts = false;
			CGameState.Status(defender, attacker).AllowDebts = false;
		}
	}
}
