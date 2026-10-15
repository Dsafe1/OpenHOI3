using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Construction;

/// <summary>
/// A finished building waiting in the deploy menu to be put in a province: what a building line ordered with no
/// province makes. See <c>docs/mechanics/buildinglines.md</c>, section 6.
/// </summary>
public sealed class CBuildingDeployment
{
	public CBuildingDeployment(int id, CCountryTag country, CBuilding building)
	{
		Id = id;
		Country = country;
		Building = building;
	}

	public int Id { get; }

	public CCountryTag Country { get; }

	public CBuilding Building { get; }

	public override string ToString() => $"{Country} {Building.Key}";
}
