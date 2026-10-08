using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Research;

/// <summary>
/// A condition out of a technology's <c>allow</c> block: one test, or a group of them.
/// It is the small part of the script's condition language that technologies use; the
/// whole of it arrives with events and decisions.
/// </summary>
public sealed class CResearchCondition
{
	public CResearchCondition(EResearchCondition kind, string name = "", CFixedPoint amount = default)
	{
		Kind = kind;
		Name = name;
		Amount = amount;
	}

	public EResearchCondition Kind { get; }

	/// <summary>The technology, category or building asked about.</summary>
	public string Name { get; }

	public CFixedPoint Amount { get; }

	/// <summary>What a group holds.</summary>
	public List<CResearchCondition> Children { get; } = new();

	public bool Holds(CGameState state, CCountry country)
	{
		switch (Kind)
		{
			case EResearchCondition.TechnologyLevel:
				return country.TechnologyLevels.GetValueOrDefault(Name) >= Amount.ToInt();
			case EResearchCondition.Ability:
				return country.Ability(Name) >= Amount;
			case EResearchCondition.Ports:
				return CountPorts(state, country) >= Amount.ToInt();
			case EResearchCondition.OwnsProvinceWithBuilding:
				return OwnsProvinceWith(state, country, Name);
			case EResearchCondition.Any:
				foreach (CResearchCondition child in Children)
				{
					if (child.Holds(state, country))
					{
						return true;
					}
				}
				return false;
			case EResearchCondition.None:
				foreach (CResearchCondition child in Children)
				{
					if (child.Holds(state, country))
					{
						return false;
					}
				}
				return true;
			default:
				foreach (CResearchCondition child in Children)
				{
					if (!child.Holds(state, country))
					{
						return false;
					}
				}
				return true;
		}
	}

	private static int CountPorts(CGameState state, CCountry country)
	{
		CBuilding? navalBase = FindBuilding(state, "naval_base");
		if (navalBase is null)
		{
			return 0;
		}
		int ports = 0;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller is { } controller && SameTag(controller, country.Tag)
				&& province.BuildingLevel(navalBase) > CFixedPoint.Zero)
			{
				ports++;
			}
		}
		return ports;
	}

	private static bool OwnsProvinceWith(CGameState state, CCountry country, string buildingKey)
	{
		CBuilding? building = FindBuilding(state, buildingKey);
		if (building is null)
		{
			return false;
		}
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner is { } owner && SameTag(owner, country.Tag)
				&& province.BuildingLevel(building) > CFixedPoint.Zero)
			{
				return true;
			}
		}
		return false;
	}

	private static CBuilding? FindBuilding(CGameState state, string key)
	{
		foreach (CBuilding building in state.Buildings)
		{
			if (building.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
			{
				return building;
			}
		}
		return null;
	}

	private static bool SameTag(CCountryTag a, CCountryTag b)
	{
		return a.Value.Equals(b.Value, StringComparison.OrdinalIgnoreCase);
	}
}
