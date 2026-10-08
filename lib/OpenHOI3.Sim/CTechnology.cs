using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim;

/// <summary>
/// One technology, as a file under <c>technologies/</c> declares it. Each level a
/// country has of it is worth the same again of every effect. What researching it takes
/// is in <c>docs/mechanics/research.md</c>, section 1.
/// </summary>
public sealed class CTechnology
{
	public CTechnology(string name, int maxLevel, CModifier effectPerLevel)
	{
		Name = name;
		MaxLevel = maxLevel;
		EffectPerLevel = effectPerLevel;
	}

	public string Name { get; }

	/// <summary>The highest level the file allows, or 0 when it does not say.</summary>
	public int MaxLevel { get; }

	public CModifier EffectPerLevel { get; }

	/// <summary>A whole number that what a level costs comes from.</summary>
	public int Difficulty { get; init; }

	/// <summary>The year level 1 is due.</summary>
	public int StartYear { get; init; }

	/// <summary>The year level 2 is due, whatever the key is called.</summary>
	public int FirstOffset { get; init; }

	/// <summary>The years added for each level past 2. Nothing makes it a single-level technology.</summary>
	public int AdditionalOffset { get; init; }

	/// <summary>Whether the country's modifier for nuclear research counts towards its bonus.</summary>
	public bool IsNuclear { get; init; }

	/// <summary>The categories whose ability makes it cheaper, each with its weight.</summary>
	public IReadOnlyList<(string Category, CFixedPoint Weight)> ResearchBonusFrom { get; init; } =
		Array.Empty<(string, CFixedPoint)>();

	/// <summary>The category that gains when a level is finished, or an empty string.</summary>
	public string OnCompletion { get; init; } = "";

	/// <summary>What a level of it adds to a category's monthly decay modifier, by category.</summary>
	public IReadOnlyList<(string Category, CFixedPoint Amount)> Decay { get; init; } =
		Array.Empty<(string, CFixedPoint)>();

	/// <summary>What must hold for it to be researched at all, or null for nothing.</summary>
	public CResearchCondition? Allow { get; init; }

	public bool IsSingleLevel => AdditionalOffset == 0;

	/// <summary>The highest level there is to research: what the file says, or one when it does not.</summary>
	public int HighestLevel => MaxLevel > 0 ? MaxLevel : 1;

	public override string ToString()
	{
		return Name;
	}
}

/// <summary>What one line of a technology's <c>allow</c> block asks.</summary>
public enum EResearchCondition
{
	/// <summary>The country has that level of a technology, or more.</summary>
	TechnologyLevel,

	/// <summary>Its ability in a category is that much, or more.</summary>
	Ability,

	/// <summary>It controls that many provinces with a naval base, or more.</summary>
	Ports,

	/// <summary>A province it owns has the building.</summary>
	OwnsProvinceWithBuilding,

	All,
	Any,
	None,
}

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
