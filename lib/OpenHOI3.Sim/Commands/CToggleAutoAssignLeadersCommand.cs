using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// Turns a country's <c>auto_assign_leaders</c> the other way: the checkbox of the
/// window a leader is chosen in, for the country being played. See
/// <c>docs/mechanics/leaders.md</c>, section 12.
/// </summary>
public sealed class CToggleAutoAssignLeadersCommand : CCommand
{
	private readonly CCountryTag _country;

	public CToggleAutoAssignLeadersCommand(CCountryTag country)
	{
		_country = country;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is not null;
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_country)!;
		country.AutoAssignLeaders = !country.AutoAssignLeaders;
	}
}
