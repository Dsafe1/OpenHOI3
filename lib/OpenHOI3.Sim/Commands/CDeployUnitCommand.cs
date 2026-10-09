using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country putting one of its finished units on the map: in a province, or with a
/// unit already in the field. It is refused for a unit that is not waiting, for a
/// province the unit may not be put in, and for a unit it may not join. See
/// <c>docs/mechanics/buildqueue.md</c>, section 7.
/// </summary>
public sealed class CDeployUnitCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly int _unit;
	private readonly int _province;
	private readonly int _target;

	/// <param name="target">The number of the unit to join, or 0 to stand alone in the province.</param>
	public CDeployUnitCommand(CCountryTag country, int unit, int province, int target = 0)
	{
		_country = country;
		_unit = unit;
		_province = province;
		_target = target;
	}

	public override bool MayExecute(CGameState state)
	{
		if (state.Country(_country) is not { } country || Find(country) is not { } unit)
		{
			return false;
		}
		if (_target == 0)
		{
			return CDeployment.MayPlace(state, country, unit, _province);
		}
		return FindTarget(country) is { } target && CDeployment.MayJoin(state, country, unit, target);
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_country)!;
		CUnit unit = Find(country)!;
		if (_target != 0)
		{
			CDeployment.Join(country, unit, FindTarget(country)!);
		}
		else
		{
			CDeployment.Place(country, unit, _province);
		}
	}

	private CUnit? Find(CCountry country)
	{
		return country.Deployments.Find(unit => unit.Id == _unit);
	}

	private CUnit? FindTarget(CCountry country)
	{
		foreach (CUnit unit in country.AllUnits())
		{
			if (unit.Id == _target)
			{
				return unit;
			}
		}
		return null;
	}
}
