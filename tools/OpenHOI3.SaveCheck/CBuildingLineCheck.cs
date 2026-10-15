using System;
using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

/// <summary>
/// The building lines of the build queue beside the save's: <c>docs/mechanics/buildinglines.md</c>, section 3.
///
/// What a line costs a day and the days it takes are worked out under the practicals, laws and ministers the save
/// shows, and set beside what the save keeps. A line is priced as it is made and again on each day the country finishes
/// a line, so a practical that has moved since then is a disagreement that says nothing about the price.
/// </summary>
internal static class CBuildingLineCheck
{
	private const string Block = "building_construction";

	public static void Compare(CGameState state, IEnumerable<(CCountry Country, ClausewitzNode Earlier, ClausewitzNode Later)> pairs,
		Action<string, bool, Func<string>> compare)
	{
		foreach ((CCountry country, ClausewitzNode before, ClausewitzNode _) in pairs)
		{
			foreach (ClausewitzNode line in before.Children)
			{
				if (!line.IsBlock || !Block.Equals(line.Key, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				// The save numbers a building from one, in the file's order.
				int index = line.GetInt("building") - 1;
				if (index < 0 || index >= state.Buildings.Count)
				{
					compare("building lines: each names a building", false,
						() => $"{country.Tag.Value}: building {line.GetString("building")}");
					continue;
				}
				CBuilding building = state.Buildings[index];
				var ours = new CConstruction(country.Tag) { Building = building };
				CBuildingLines.Price(country, ours);

				CFixedPoint cost = Number(line.GetString("cost"));
				CFixedPoint duration = Number(line.GetString("duration"));
				string who = $"{country.Tag.Value} {building.Key} in {line.GetString("location")}";
				string facts = $"({building.OnCompletion} {country.Ability(building.OnCompletion)})";
				compare("building lines: what a line costs a day, under the save's practicals and laws", cost == ours.Cost,
					() => $"{who}: save {cost}, ours {ours.Cost}  {facts}");
				compare("building lines: the days a line takes, under the save's practicals and laws", duration == ours.Duration,
					() => $"{who}: save {duration}, ours {ours.Duration}  {facts}");
			}
		}
	}

	private static CFixedPoint Number(string? text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}
}
