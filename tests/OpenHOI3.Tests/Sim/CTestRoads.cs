using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Infrastructure for a test's map. A land unit stands only in land that is in an area, which wants more than one level
/// of infrastructure (<c>docs/mechanics/movement.md</c>, section 3), so a map a land unit is to move on has to have some.
/// </summary>
internal static class CTestRoads
{
	/// <summary>The infrastructure building, at an index none of the tests' own buildings takes.</summary>
	public static readonly CBuilding Infrastructure = new(31, "infra", 10, CSupplyFigures.Infrastructure, CFixedPoint.Parse("0.1"));

	/// <summary>A test's buildings with the infrastructure beside them.</summary>
	public static IReadOnlyList<CBuilding> With(params CBuilding[] buildings)
	{
		return buildings.Append(Infrastructure).ToArray();
	}

	/// <summary>Five levels of the infrastructure given in every land province that has none, and the state handed back.</summary>
	public static CGameState Paved(this CGameState state, CBuilding? building = null)
	{
		building ??= Infrastructure;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id > 0 && !province.IsSea && province.BuildingMaxLevel(building) == CFixedPoint.Zero)
			{
				province.SetBuildingLevel(building, CFixedPoint.FromInt(5));
			}
		}
		return state;
	}
}
