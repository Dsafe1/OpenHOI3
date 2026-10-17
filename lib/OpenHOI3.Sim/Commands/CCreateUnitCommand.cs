using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A new, empty fleet or air group with the number given, in a province and based at its base, put under the
/// headquarters given - as the original's agent attaches a unit it asked for as soon as it is made. The AI's agents post
/// it as they split a fleet or an air group (<c>docs/mechanics/agents.md</c>, section 5.4).
/// </summary>
public sealed class CCreateUnitCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _id;
	private readonly EUnitKind _kind;
	private readonly int _province;
	private readonly int _base;
	private readonly int _headquarters;

	public CCreateUnitCommand(CCountryTag owner, int id, EUnitKind kind, int province, int baseProvince, int headquarters)
	{
		_owner = owner;
		_id = id;
		_kind = kind;
		_province = province;
		_base = baseProvince;
		_headquarters = headquarters;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_owner) is not null && _province > 0 && _province < state.Provinces.Count
			&& _kind is EUnitKind.Navy or EUnitKind.Air;
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_owner)!;
		var unit = new CUnit(_kind, country.Tag) { Id = _id, Location = _province, Base = _base };
		country.Units.Add(unit);
		state.ForgetPositions();
		if (CUnitFinder.Find(state, _owner, _headquarters) is { } headquarters)
		{
			CAgentUnits.Attach(state, unit, headquarters);
		}
	}
}
