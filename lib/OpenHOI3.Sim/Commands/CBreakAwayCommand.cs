using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country telling one of its fleets in a battle at sea to break away, with nowhere
/// named to go: what the battle window's retreat button does for a fleet. The fleet
/// starts its count and its ships open the distance; it is out when the battle's own
/// roll to break off next passes with the count run. See
/// <c>docs/mechanics/navalcombat.md</c>, section 6.
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
		CNavalDisengage.Defer(Find(state.Country(_country)!)!, 0);
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
