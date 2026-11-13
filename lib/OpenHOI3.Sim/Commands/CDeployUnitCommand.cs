using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Supply;
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
	private readonly bool _dropped;

	/// <param name="target">The number of the unit to join, or 0 to stand alone in the province.</param>
	/// <param name="dropped">
	/// A line dropped on a row of the order of battle browser: the command then asks nothing of the target, as the
	/// original's asks nothing, and the unit is put where the original's join puts it - the target's province, or the
	/// nearest of the home area where it may go - or, where there is none, stays waiting (<c>mapunits.md</c>, section 6).
	/// </param>
	public CDeployUnitCommand(CCountryTag country, int unit, int province, int target = 0, bool dropped = false)
	{
		_country = country;
		_unit = unit;
		_province = province;
		_target = target;
		_dropped = dropped;
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
		if (FindTarget(country) is not { } target)
		{
			return false;
		}
		return _dropped ? CDeployment.PlaceToJoin(state, country, unit, target) != 0 : CDeployment.MayJoin(state, country, unit, target);
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_country)!;
		CUnit unit = Find(country)!;

		// A land unit comes with its days of supplies, out of the stockpile.
		int province = _target == 0 ? _province
			: _dropped ? CDeployment.PlaceToJoin(state, country, unit, FindTarget(country)!)
			: FindTarget(country)!.Location;
		if (CMovementRules.IsLandUnit(unit) && !state.Province(province).IsSea)
		{
			CSupplyCarry.Bring(state, country, unit, from: 0, province);
		}

		if (_target != 0)
		{
			CDeployment.Join(state, country, unit, FindTarget(country)!, _dropped ? province : 0);
		}
		else
		{
			CDeployment.Place(state, country, unit, _province);
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
