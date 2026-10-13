using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// For trying things out: sets a country's neutrality, there and then, and works its effective neutrality out
/// again - so that a war may be declared on a new game, where nobody may (<c>docs/mechanics/war.md</c>,
/// section 5). Nothing in a game posts it.
/// </summary>
public sealed class CSetNeutralityCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly CFixedPoint _neutrality;

	public CSetNeutralityCommand(CCountryTag country, CFixedPoint neutrality)
	{
		_country = country;
		_neutrality = neutrality;
	}

	public override bool MayExecute(CGameState state) => state.Country(_country) is not null;

	public override void Execute(CGameState state)
	{
		if (state.Country(_country) is { } country)
		{
			country.Neutrality = CFixedPoint.Max(_neutrality, CFixedPoint.Zero);
			CNeutrality.UpdateEffective(state, country);
		}
	}
}
