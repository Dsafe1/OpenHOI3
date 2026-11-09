using System.Linq;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// Every unit of a country left with no leader: what the confirmation of the leader
/// window's "remove all" posts, for the country being played. The leaders taken off throw
/// away nothing and the units lose no organisation. The original passes over a unit lent
/// to another country, and none is lent here. See <c>docs/mechanics/leaders.md</c>,
/// section 12.
/// </summary>
public sealed class CRemoveAllLeadersCommand : CCommand
{
	private readonly CCountryTag _country;

	public CRemoveAllLeadersCommand(CCountryTag country)
	{
		_country = country;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is not null;
	}

	public override void Execute(CGameState state)
	{
		foreach (CUnit unit in new List<CUnit>(state.Country(_country)!.AllUnits()).Where(unit => unit.Lender is null))
		{
			CLeaders.SetLeader(state, unit, null, true, false);
		}
	}
}
