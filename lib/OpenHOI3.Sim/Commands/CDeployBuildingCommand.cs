using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A finished building put in a province from the deploy menu: the original's <c>CDeployUnitCommand</c> with a building's
/// deployment and no target. Valid while the deployment is there; it asks nothing of the province - the click that
/// posts it is the only check. See <c>docs/mechanics/buildinglines.md</c>, section 6.
/// </summary>
public sealed class CDeployBuildingCommand : CCommand
{
	public CDeployBuildingCommand(CCountryTag country, int deployment, int province)
	{
		Country = country;
		Deployment = deployment;
		Province = province;
	}

	public CCountryTag Country { get; }

	public int Deployment { get; }

	public int Province { get; }

	public override bool MayExecute(CGameState state)
	{
		return Find(state) is not null && Province > 0 && Province < state.Provinces.Count;
	}

	public override void Execute(CGameState state)
	{
		if (state.Country(Country) is { } country && Find(state) is { } deployment)
		{
			CBuildingLines.Place(state, country, deployment, state.Province(Province));
		}
	}

	private CBuildingDeployment? Find(CGameState state)
	{
		return state.Country(Country)?.BuildingDeployments.Find(each => each.Id == Deployment);
	}
}
