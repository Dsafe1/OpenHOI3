using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country telling one of its fleets in a battle at sea to fall back: what the battle
/// window's retreat button does for a fleet, the original's move order to "retreat". The
/// province is where a beaten fleet would go, and with none nothing happens. Then it is a
/// move order there: a fleet that has not counted long enough keeps the province and starts
/// its count, its ships opening the distance, and sets off once the count has run; one that
/// has is given its way at once. See <c>docs/mechanics/navalcombat.md</c>, section 6, and
/// <c>docs/mechanics/combatview.md</c>, section 12.
/// </summary>
public sealed class CBreakAwayCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly int _unit;

	public CBreakAwayCommand(CCountryTag country, int unit)
	{
		_country = country;
		_unit = unit;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is { } country && Find(country) is { Kind: EUnitKind.Navy, IsFighting: true };
	}

	public override void Execute(CGameState state)
	{
		CUnit fleet = Find(state.Country(_country)!)!;
		int to = CNavalRetreat.FindRetreatProvince(state, fleet);
		if (to <= 0)
		{
			return;
		}
		// The rest is the move order's own, with the way replacing the one it had.
		new CMoveUnitCommand(_country, _unit, to).Execute(state);
	}

	private CUnit? Find(CCountry country)
	{
		foreach (CUnit unit in country.AllUnits())
		{
			if (unit.Id == _unit)
			{
				return unit;
			}
		}
		return null;
	}
}
