using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The conditions whose key is a name rather than a keyword - a government position, a building,
/// a technology, a unit type, a law group: <c>docs/mechanics/triggers.md</c>, sections 5 and 7.
/// </summary>
public static class CNamedConditions
{
	public static bool Holds(CGameState state, CTrigger trigger, CEventScope scope)
	{
		CCountry? country = state.Country(scope.Country);
		long wanted = CConditions.WholeOf(trigger.Argument.Amount);
		switch (trigger.Kind)
		{
			case ETriggerKind.GovernmentPosition:
				return country is not null && country.Government.TryGetValue(trigger.Key, out CMinister? holder)
					&& holder.Id == wanted;
			case ETriggerKind.Building:
				return Built(state, CConditions.Here(state, scope), trigger.Key) >= wanted;
			case ETriggerKind.Technology:
				return (country?.TechnologyLevels.GetValueOrDefault(trigger.Key) ?? 0) >= wanted;
			case ETriggerKind.UnitType:
				// The type is compared with each brigade's own copy of it, which is never the same.
				return 0 >= wanted;
			case ETriggerKind.LawGroup:
				return country is not null && country.Laws.TryGetValue(trigger.Key, out CLaw? law)
					&& law.Name.Equals(trigger.Argument.Text, StringComparison.Ordinal);
			default:
				throw new InvalidOperationException($"'{trigger.Key}' is no name a condition answers");
		}
	}

	/// <summary>The level of the building built in the province, in whole levels; nought for a name no building has.</summary>
	public static long Built(CGameState state, CProvince province, string key)
	{
		foreach (CBuilding building in state.Buildings)
		{
			if (building.Key.Equals(key, StringComparison.Ordinal))
			{
				return CConditions.WholeOf(province.BuildingMaxLevel(building));
			}
		}
		return 0;
	}
}
