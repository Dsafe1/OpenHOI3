using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// Which unit types a country may build, and how many more of each. It is what fills
/// the lists of the windows a unit is ordered from, and what refuses an order. See
/// <c>docs/mechanics/builders.md</c>, section 2.
/// </summary>
public static class CUnitAvailability
{
	public const string IsBuildable = "is_buildable";
	public const string Active = "active";
	public const string MinimumOfType = "minimum_of_type";
	public const string MaxPercentageOfType = "max_percentage_of_type";
	public const string BrigadesInDivision = "BRIGADES_IN_DIVISION";

	/// <summary>What a technology's <c>division_size</c> adds up to.</summary>
	public const string DivisionSize = "division_size";

	/// <summary>A type with no ration may be built without end, which the original writes as this many.</summary>
	public const long Unlimited = 1000000;

	/// <summary>A division is of two brigades at the least.</summary>
	public const int FewestBrigades = 2;

	/// <summary>Whether a type's own file lets it be built at all.</summary>
	public static bool IsBuildableType(CSubUnitType type)
	{
		return !(type.Flags.TryGetValue(IsBuildable, out bool buildable) && !buildable);
	}

	/// <summary>
	/// Whether a country has the use of a type: one its file does not hold back with
	/// <c>active = no</c>, or that a technology the country has a level of names in
	/// <c>activate_unit</c>.
	/// </summary>
	public static bool IsUnlocked(CGameState state, CCountry country, CSubUnitType type)
	{
		if (!(type.Flags.TryGetValue(Active, out bool active) && !active))
		{
			return true;
		}
		foreach (CTechnology technology in state.Technologies.Values)
		{
			if (technology.ActivatesUnits.Count == 0 || country.TechnologyLevels.GetValueOrDefault(technology.Name) < 1)
			{
				continue;
			}
			foreach (string key in technology.ActivatesUnits)
			{
				if (key.Equals(type.Key, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>Whether a type is the country's to use where its file names who may.</summary>
	public static bool IsUsableBy(CCountry country, CSubUnitType type)
	{
		if (type.UsableBy.Count == 0)
		{
			return true;
		}
		foreach (string tag in type.UsableBy)
		{
			if (tag.Equals(country.Tag.Value, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Whether a type is one the country has the use of: unlocked, and its own to use.
	/// A country that has been given every unit to try has the use of them all.
	/// </summary>
	public static bool MayUse(CGameState state, CCountry country, CSubUnitType type)
	{
		return country.EveryUnitUnlocked || (IsUnlocked(state, country, type) && IsUsableBy(country, type));
	}

	/// <summary>
	/// How many more of a type the country may have: none of one it has not unlocked or
	/// may not use, no end of one with no ration, and otherwise what its ration leaves -
	/// the greater of its fixed number and its share of every brigade on the map of the
	/// type's group (<see cref="CUnitTypeGroup"/>), less those of the type the country has,
	/// is building and has waiting to be placed - the original's
	/// <c>CountMaxUnitsStillBuildable</c>, which the production script asks too.
	/// </summary>
	public static long StillBuildable(CGameState state, CCountry country, CSubUnitType type)
	{
		if (!MayUse(state, country, type))
		{
			return 0;
		}
		long least = type.Stats[MinimumOfType].Raw / CFixedPoint.Scale;
		long share = type.Stats[MaxPercentageOfType].Raw;
		if (least < 1 && share < 1)
		{
			return Unlimited;
		}

		// Every brigade on the map of the type's group, and of the type itself (FINDINGS-ministers.md C.5).
		CSubUnitType group = CUnitTypeGroup.Of(state, type);
		long all = 0;
		long have = 0;
		foreach (CUnit unit in country.AllUnits())
		{
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				if (CUnitTypeGroup.Of(state, subUnit.Type) != group)
				{
					continue;
				}
				all++;
				if (subUnit.Type == type)
				{
					have++;
				}
			}
		}
		foreach (CConstruction construction in country.Constructions)
		{
			foreach (CBrigadeOrder order in construction.Brigades)
			{
				if (order.Type == type)
				{
					have += construction.Size;
				}
			}
		}
		foreach (CUnit unit in country.Deployments)
		{
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				if (subUnit.Type == type)
				{
					have++;
				}
			}
		}

		long left = Math.Max(least - have, all * share / CFixedPoint.Scale - have);
		return Math.Max(0, left);
	}

	/// <summary>
	/// The types of a branch a country may order now, in the order the game holds them:
	/// buildable, and with at least one more allowed.
	/// </summary>
	public static List<CSubUnitType> Buildable(CGameState state, CCountry country, EUnitBranch branch)
	{
		var types = new List<CSubUnitType>();
		foreach (CSubUnitType type in state.SubUnitTypes.Values)
		{
			if (type.Branch == branch && IsBuildableType(type) && StillBuildable(state, country, type) >= 1)
			{
				types.Add(type);
			}
		}
		return types;
	}

	/// <summary>The most brigades a division of the country's may have.</summary>
	public static int MostBrigades(CGameState state, CCountry country)
	{
		long thousandths = state.Defines[CManpower.MilitarySection, BrigadesInDivision].Raw
			+ country.Technology[DivisionSize].Raw;
		return (int)(thousandths / CFixedPoint.Scale);
	}

	/// <summary>
	/// Whether a design may be ordered as a division, so many at once: from two brigades
	/// to the most the country may put in one, every type buildable, and none taking
	/// more of its type than the country may still have.
	/// </summary>
	public static bool MayBuildDivision(CGameState state, CCountry country, IReadOnlyList<CSubUnitType> design, int divisions)
	{
		if (design.Count > MostBrigades(state, country) || design.Count < FewestBrigades)
		{
			return false;
		}
		return MayBuildEach(state, country, design, divisions);
	}

	/// <summary>
	/// Whether every type of a list is buildable and has room for as many as the list
	/// holds of it, so many times over.
	/// </summary>
	public static bool MayBuildEach(CGameState state, CCountry country, IReadOnlyList<CSubUnitType> types, int times)
	{
		var counts = new Dictionary<CSubUnitType, int>();
		foreach (CSubUnitType type in types)
		{
			if (!IsBuildableType(type))
			{
				return false;
			}
			counts[type] = counts.GetValueOrDefault(type) + 1;
		}
		foreach ((CSubUnitType type, int count) in counts)
		{
			if (StillBuildable(state, country, type) - (long)count * times < 0)
			{
				return false;
			}
		}
		return true;
	}
}
