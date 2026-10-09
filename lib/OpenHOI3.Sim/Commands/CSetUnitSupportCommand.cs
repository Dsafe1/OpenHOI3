using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country switching, for one of its units, whether it is reinforced, whether it is
/// upgraded, or whether it comes before the others for both: the three checkboxes of the
/// unit's panel. See <c>docs/mechanics/unitpanel.md</c>, section 7.
/// </summary>
public sealed class CSetUnitSupportCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly int _unit;
	private readonly EUnitSupport _what;
	private readonly bool _on;

	public CSetUnitSupportCommand(CCountryTag country, int unit, EUnitSupport what, bool on)
	{
		_country = country;
		_unit = unit;
		_what = what;
		_on = on;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is { } country && Find(country) is not null;
	}

	public override void Execute(CGameState state)
	{
		CUnit unit = Find(state.Country(_country)!)!;
		switch (_what)
		{
			case EUnitSupport.Priority:
				unit.IsPrioritised = _on;
				break;
			case EUnitSupport.Reinforcement:
				unit.MayReinforce = _on;
				break;
			case EUnitSupport.Upgrade:
				unit.MayUpgrade = _on;
				break;
		}
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
