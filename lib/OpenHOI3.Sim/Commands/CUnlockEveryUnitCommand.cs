using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// For trying things out: gives a country the use of every unit type that can be built
/// at all, or takes that away again. With it a carrier and its air groups can be
/// ordered in 1936, and a brigade its file keeps to another country.
///
/// It lifts the two locks - the technology a type waits for, and <c>usable_by</c> - and
/// nothing else: a type its file says cannot be built still cannot, a ration still
/// holds, and the country still pays. The original has no such command; its nearest is
/// a console. See <c>docs/mechanics/builders.md</c>, section 9.
/// </summary>
public sealed class CUnlockEveryUnitCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly bool _unlocked;

	public CUnlockEveryUnitCommand(CCountryTag country, bool unlocked = true)
	{
		_country = country;
		_unlocked = unlocked;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is not null;
	}

	public override void Execute(CGameState state)
	{
		state.Country(_country)!.EveryUnitUnlocked = _unlocked;
	}
}
