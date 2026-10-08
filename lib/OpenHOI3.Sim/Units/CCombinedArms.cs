using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// The combined arms bonus of a set of brigades, as the division designer shows it. See
/// <c>docs/mechanics/builders.md</c>, section 4.
/// </summary>
public static class CCombinedArms
{
	/// <summary>
	/// Which groups count for these brigades, one answer for each group of the game in
	/// its file's order. A group counts when one of the brigades is in it and one of
	/// them - the same or another - is in a base group.
	/// </summary>
	public static bool[] Counted(CGameState state, IEnumerable<CSubUnitType> brigades)
	{
		var present = new bool[state.CombinedArmsGroups.Count];
		bool anyBase = false;
		foreach (CSubUnitType type in brigades)
		{
			int index = IndexOf(state, type.UnitGroup);
			if (index < 0)
			{
				continue;
			}
			present[index] = true;
			anyBase |= state.CombinedArmsGroups[index].IsBase;
		}
		if (!anyBase)
		{
			Array.Clear(present);
		}
		return present;
	}

	/// <summary>
	/// The bonus: for each group that counts, its own value and what the country's
	/// technology adds to it.
	/// </summary>
	public static CFixedPoint Bonus(CGameState state, CCountry country, IEnumerable<CSubUnitType> brigades)
	{
		bool[] counted = Counted(state, brigades);
		long bonus = 0;
		for (int index = 0; index < counted.Length; index++)
		{
			if (counted[index])
			{
				CCombinedArmsGroup group = state.CombinedArmsGroups[index];
				bonus += group.Value.Raw + country.Technology[group.Name].Raw;
			}
		}
		return new CFixedPoint(bonus);
	}

	/// <summary>
	/// The number of a type's group, counted from one in the file's order, or nothing
	/// for a type in no group. The lists a brigade is picked from colour each line by it.
	/// </summary>
	public static int GroupNumber(CGameState state, CSubUnitType type)
	{
		return IndexOf(state, type.UnitGroup) + 1;
	}

	private static int IndexOf(CGameState state, string group)
	{
		if (group.Length == 0)
		{
			return -1;
		}
		for (int index = 0; index < state.CombinedArmsGroups.Count; index++)
		{
			if (state.CombinedArmsGroups[index].Name.Equals(group, StringComparison.OrdinalIgnoreCase))
			{
				return index;
			}
		}
		return -1;
	}
}
