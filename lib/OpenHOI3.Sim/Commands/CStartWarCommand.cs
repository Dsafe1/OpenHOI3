using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// For trying things out: puts two countries at war with each other, there and then.
/// It is not a declaration of war - nothing is asked of neutrality, of a casus belli,
/// of allies or of factions, and nobody is called in - which is diplomacy's and comes
/// with it. Until then this is the only way a country other than the two the start
/// date has at war can fight. See <c>docs/mechanics/landcombat.md</c>, section 10.
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
		if (_attacker == _defender || state.Country(_attacker) is null || state.Country(_defender) is null)
		{
			return false;
		}
		foreach (CWar war in state.Wars)
		{
			if ((war.Attackers.Contains(_attacker) && war.Defenders.Contains(_defender))
				|| (war.Attackers.Contains(_defender) && war.Defenders.Contains(_attacker)))
			{
				return false;
			}
		}
		return true;
	}

	public override void Execute(CGameState state)
	{
		var war = new CWar($"{_attacker}-{_defender}", state.Date);
		war.Attackers.Add(_attacker);
		war.Defenders.Add(_defender);
		state.Wars.Add(war);
	}
}
