using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What a test's handful of provinces needs before the land around a capital counts as
/// one piece: infrastructure, since a province with one level or none is in no area, and
/// a way to each neighbour. See <c>docs/mechanics/supply.md</c>, section 2.
/// </summary>
public static class TestRoads
{
	/// <summary>The infrastructure building, at an index no test's own buildings use.</summary>
	public static readonly CBuilding Infra = new(9, "infra", 10, CSupplyFigures.Infrastructure, CFixedPoint.Parse("0.1"));

	/// <summary>
	/// Gives every land province full infrastructure, where it has none, and every
	/// province a plain way to each of its neighbours, where it has no ways at all.
	/// </summary>
	public static void Pave(IEnumerable<CProvince> provinces)
	{
		foreach (CProvince province in provinces)
		{
			if (province.Id > 0 && !province.IsSea && province.BuildingLevel(Infra) == CFixedPoint.Zero)
			{
				province.SetBuildingLevel(Infra, CFixedPoint.FromInt(10));
			}
			if (province.Edges.Count == 0 && province.Neighbours.Count > 0)
			{
				var edges = new List<CProvinceEdge>();
				foreach (int neighbour in province.Neighbours)
				{
					edges.Add(new CProvinceEdge(neighbour, EProvinceEdgeKind.Plain, 0, CFixedPoint.One));
				}
				province.Edges = edges;
			}
		}
	}
}
