using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country keeping its selected units as one of its ten selection groups: Ctrl and a digit. The group is replaced
/// whole by the units' numbers in the order given. See <c>docs/mechanics/selection.md</c>, section 7.
/// </summary>
public sealed class CSetSelectionGroupCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly int _group;
	private readonly int[] _units;

	public CSetSelectionGroupCommand(CCountryTag country, int group, IEnumerable<int> units)
	{
		_country = country;
		_group = group;
		_units = new List<int>(units).ToArray();
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is not null && _group >= 0 && _group < CCountry.SelectionGroupCount;
	}

	public override void Execute(CGameState state)
	{
		List<int> group = state.Country(_country)!.SelectionGroups[_group];
		group.Clear();
		group.AddRange(_units);
	}
}
