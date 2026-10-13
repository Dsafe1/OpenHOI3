using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Modifiers;

/// <summary>
/// Rebuilds every province's modifier from what currently applies to it: its buildings,
/// and the two static modifiers for land that lies apart from its controller or is not
/// its owner's by right. See <c>docs/mechanics/modifiers.md</c>, section 3.
///
/// The total is cleared and summed again every time, like a country's.
/// </summary>
public static class CProvinceModifiers
{
	/// <summary>The static modifiers the engine applies to a province by name.</summary>
	public const string Overseas = "overseas";
	public const string NonCore = "non_core";

	public static void Rebuild(CGameState state)
	{
		CModifier? overseas = state.FindStaticModifier(Overseas);
		CModifier? nonCore = state.FindStaticModifier(NonCore);

		// A province is in its controller's home block when it can be walked to from the
		// capital through land that country controls.
		var joined = new bool[state.Provinces.Count];
		foreach (CCountry country in state.Countries)
		{
			foreach (int id in CResources.ProvincesJoinedToCapital(state, country))
			{
				joined[id] = true;
			}
		}

		foreach (CProvince province in state.Provinces)
		{
			CModifier total = province.Modifier;
			total.Clear();
			if (province.Id == 0 || province.IsSea)
			{
				continue;
			}

			foreach (CBuilding building in state.Buildings)
			{
				CFixedPoint level = province.BuildingLevel(building);
				if (level != CFixedPoint.Zero && building.ModifierKey.Length != 0)
				{
					total.Add(building.ModifierKey, level * building.ModifierValue);
				}
			}

			if (overseas is not null && !joined[province.Id] && IsOnAnotherContinent(state, province))
			{
				total.Add(overseas);
			}
			if (nonCore is not null && IsNotItsOwnersByRight(province))
			{
				total.Add(nonCore);
			}

			// What events gave it, each until its time has passed (step 5; ours where it is
			// dropped, docs/mechanics/effects.md section 6).
			province.EventModifiers.RemoveAll(given => given.HasEnded(state));
			foreach (CTimedModifier given in province.EventModifiers)
			{
				total.Add(given.Modifier);
			}
		}
	}

	/// <summary>Whether the province is on a different continent from its controller's capital.</summary>
	private static bool IsOnAnotherContinent(CGameState state, CProvince province)
	{
		if (state.Country(province.Controller) is not { } controller)
		{
			return false;
		}
		CProvince capital = state.Province(controller.ActingCapital);
		return capital.Id != 0 && !capital.Continent.Equals(province.Continent, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Whether its owner holds it without a claim to it. Land somebody else holds is not
	/// counted: the occupier's policy is what applies there.
	/// </summary>
	private static bool IsNotItsOwnersByRight(CProvince province)
	{
		if (province.Owner is not { } owner || province.Controller is not { } controller
			|| !owner.Value.Equals(controller.Value, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		foreach (CCountryTag core in province.Cores)
		{
			if (core.Value.Equals(owner.Value, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}
		return true;
	}
}
