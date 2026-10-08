using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country putting one of its finished units in a province. It is refused for a unit
/// that is not waiting, and for a province the unit may not be put in. See
/// <c>docs/mechanics/buildqueue.md</c>, section 7.
/// </summary>
public sealed class CDeployUnitCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly int _unit;
	private readonly int _province;

	public CDeployUnitCommand(CCountryTag country, int unit, int province)
	{
		_country = country;
		_unit = unit;
		_province = province;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is { } country
			&& Find(country) is { } unit
			&& CDeployment.MayPlace(state, country, unit, _province);
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_country)!;
		CDeployment.Place(country, Find(country)!, _province);
	}

	private CUnit? Find(CCountry country)
	{
		return country.Deployments.Find(unit => unit.Id == _unit);
	}
}
