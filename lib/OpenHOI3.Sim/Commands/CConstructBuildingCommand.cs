using System;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A building ordered: the original's <c>CConstructBuildingCommand</c>. Valid for a real building, and with a province
/// where the building may go there; with none - the production screen's order - always. In a province it joins the
/// last line of the building there, up to what <c>max_level</c> leaves; otherwise it makes a new line of the size asked
/// at the end of the queue. Nothing is paid and nothing is said. See <c>docs/mechanics/buildinglines.md</c>, section 2.
/// </summary>
public sealed class CConstructBuildingCommand : CCommand
{
	public CConstructBuildingCommand(CCountryTag country, string building, int province, int amount)
	{
		Country = country;
		Building = building;
		Province = province;
		Amount = amount;
	}

	public CCountryTag Country { get; }

	/// <summary>The building, by its key.</summary>
	public string Building { get; }

	/// <summary>The province, or 0 for a building to be placed when it is done.</summary>
	public int Province { get; }

	public int Amount { get; }

	public override bool MayExecute(CGameState state)
	{
		if (Find(state) is not { } building || state.Country(Country) is not { } country || Province < 0
			|| Province >= state.Provinces.Count)
		{
			return false;
		}
		return Province == 0 || CBuildingLines.IsAllowed(state, country, building, state.Province(Province));
	}

	public override void Execute(CGameState state)
	{
		if (Find(state) is not { } building || state.Country(Country) is not { } country)
		{
			return;
		}
		CConstruction? found = null;
		long level = 0;
		if (Province != 0)
		{
			level = state.Province(Province).BuildingMaxLevel(building).Raw / CFixedPoint.Scale;
			foreach (CConstruction line in country.Constructions)
			{
				if (line.Building == building && line.Location == Province)
				{
					level += line.Size;
					found = line;
				}
			}
		}
		if (found is not null)
		{
			// Joined to the last line there, as far as max_level leaves room, and not priced again.
			long add = Amount + level > building.MaxLevel ? building.MaxLevel - level : Amount;
			found.Size += (int)add;
		}
		else
		{
			found = new CConstruction(country.Tag) { Id = ++state.LastUnitId, Building = building, Size = Amount, Location = Province };
			country.Constructions.Add(found);
			CBuildQueue.Recalculate(country, found);
		}
		CBuildQueue.UpdateCost(country);
	}

	private CBuilding? Find(CGameState state)
	{
		foreach (CBuilding building in state.Buildings)
		{
			if (building.Key.Equals(Building, StringComparison.OrdinalIgnoreCase))
			{
				return building;
			}
		}
		return null;
	}
}
