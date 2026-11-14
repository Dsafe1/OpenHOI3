using System;
using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;

/// <summary>
/// A look, and not a check, since a save keeps no country's level of a resource: every strategic resource a country is
/// given out of another's province, as the game stands, and which of them come by the coastlines' link alone - the
/// capitals on different continents (<c>docs/mechanics/modifiers.md</c>, section 6). Printed by <c>firstdays</c> and
/// <c>load</c>.
/// </summary>
internal static class CStrategicResourceLook
{
	public static void Print(CGameState state)
	{
		var reach = new CCoastlineReach(state);
		HashSet<string> landowners = CManpower.Landowners(state);
		var given = new List<string>();
		var bySea = new List<string>();
		foreach (CProvince province in state.Provinces)
		{
			if (province.StrategicResource.Length == 0 || state.Country(province.Controller) is not { } holder)
			{
				continue;
			}
			foreach (CCountry country in state.Countries)
			{
				if (ReferenceEquals(country, holder) || !landowners.Contains(country.Tag.Value)
					|| !CStrategicResources.GetsFrom(state, country, holder, reach))
				{
					continue;
				}
				string line = $"{country.Tag} {province.StrategicResource} from {holder.Tag} ({province.Id})";
				string here = state.Province(country.ActingCapital).Continent;
				string there = state.Province(holder.ActingCapital).Continent;
				if (here.Length > 0 && here.Equals(there, StringComparison.OrdinalIgnoreCase))
				{
					given.Add(line);
				}
				else
				{
					bySea.Add(line);
				}
			}
		}
		Console.WriteLine($"strategic resources given by another country, {state.CoastlineRegions.Count} coastlines read: " +
			$"{given.Count} by a capital on one continent{(given.Count > 0 ? ": " + string.Join("; ", given) : "")}");
		Console.WriteLine($"  {bySea.Count} by the coastlines alone{(bySea.Count > 0 ? ": " + string.Join("; ", bySea) : "")}");
	}
}
