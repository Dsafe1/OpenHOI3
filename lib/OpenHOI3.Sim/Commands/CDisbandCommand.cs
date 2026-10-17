using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A unit disbanded: the player told where it is his and has brigades, its owner given back its strength's share of the
/// men it cost, a theatre-level unit's theatre dropped where it holds no province, and the unit taken out of the game.
/// The AI's theatre layer posts it for a theatre-level unit no theatre takes (<c>docs/mechanics/theatres.md</c>, section
/// 9); the player's disband button is not built (<c>selection.md</c>).
/// </summary>
public sealed class CDisbandCommand : CCommand
{
	private readonly int _unit;

	public CDisbandCommand(int unit)
	{
		_unit = unit;
	}

	public override bool MayExecute(CGameState state)
	{
		return Find(state) is not null;
	}

	public override void Execute(CGameState state)
	{
		if (Find(state) is not { } unit || state.Country(unit.Owner) is not { } owner)
		{
			return;
		}
		if (state.Played.Contains(owner.Tag) && unit.SubUnits.Count > 0)
		{
			state.RaisePactMessage(new Agreements.CPactMessage("DISBAND", "", "", owner.Tag, owner.Tag)
			{
				Told = owner.Tag,
				Values = new[] { ("UNIT", unit.Name) },
			});
		}
		long strength = 0;
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			strength += brigade.Strength.Raw;
		}
		long most = CUnitTotals.Sum(unit, CSubUnitType.MaxStrength).Raw;
		long men = CUnitTotals.Sum(unit, CSubUnitStats.BuildCostManpower).Raw;
		long share = most == 0 ? -1 : strength * CFixedPoint.Scale / most;
		owner.Manpower = CFixedPoint.Max(CFixedPoint.Zero, owner.Manpower + new CFixedPoint(share * men / CFixedPoint.Scale));
		if (unit.Kind == EUnitKind.Theatre && CTheatres.FindFor(state, unit) is { } theatre && theatre.AllProvinces.Count == 0)
		{
			owner.Strategy.Theatres.Remove(theatre);
			CTheatreProvinces.Delete(state, theatre);
		}
		CTheatres.Remove(state, unit);
	}

	private CUnit? Find(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.Id == _unit && !unit.IsRemoved)
				{
					return unit;
				}
			}
		}
		return null;
	}
}
