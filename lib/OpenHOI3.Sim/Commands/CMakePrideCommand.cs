using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A ship made the pride of its country's fleet, the old one's mark taken off and the redesignation's dissent added. It
/// tests nothing else, as the original's does: the card's button is what keeps a ship that may not be one from it. See
/// <c>docs/mechanics/brigadecard.md</c>, section 4.
/// </summary>
public sealed class CMakePrideCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _ship;

	public CMakePrideCommand(CCountryTag owner, int ship)
	{
		_owner = owner;
		_ship = ship;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_owner) is not null && CUnitFinder.FindSubUnit(state, _owner, _ship) is not null;
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_owner)!;
		CSubUnit ship = CUnitFinder.FindSubUnit(state, _owner, _ship)!;
		if (CPride.Find(country) is { } old)
		{
			long dissent = country.Dissent.Raw + CPride.Dissent(state.Defines, ship, old).Raw;
			country.Dissent = new CFixedPoint(System.Math.Max(0, dissent));
			old.IsPride = false;
		}
		ship.IsPride = true;
	}
}
