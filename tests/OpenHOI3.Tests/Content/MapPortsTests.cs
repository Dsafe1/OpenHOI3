using System;
using System.Collections.Generic;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// Which provinces have a port and how the places of the table of distances by sea are
/// numbered, against <c>docs/mechanics/fleetsandair.md</c>, section 2. Six provinces on a
/// strip one pixel high:
///
///     x:  0      1      2      3       4      5
///         1 land 2 land 3 sea  4 sea   5 lake 6 sea, listed as a lake
/// </summary>
public class MapPortsTests
{
	private static readonly bool[] IsSea = { false, false, false, true, true, true, true };
	private static readonly string[] Continents = { "", "europe", "europe", "", "", "", CMapPorts.LakeContinent };

	private static CProvinceEdge Edge(int to)
	{
		return new CProvinceEdge(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.One);
	}

	// 5 touches only land; 6 touches the sea and is a lake by name.
	private static readonly CProvinceEdge[][] Edges =
	{
		Array.Empty<CProvinceEdge>(),
		new[] { Edge(2) },
		new[] { Edge(1), Edge(3), Edge(5) },
		new[] { Edge(2), Edge(4) },
		new[] { Edge(3), Edge(6) },
		new[] { Edge(2) },
		new[] { Edge(4) },
	};

	private static CMapPorts.Result Build(params CPortPlacement[] ports)
	{
		return CMapPorts.Build(IsSea.Length, id => IsSea[id], (x, y) => y == 0 && x >= 0 && x < 6 ? x + 1 : 0,
			Edges, ports, Continents);
	}

	[Fact]
	public void AProvinceHasAPortWhereItsNavalBaseLiesOnAPixelOfTheSea()
	{
		CMapPorts.Result result = Build(new CPortPlacement(2, 2.6f, 0.4f), new CPortPlacement(1, 1.5f, 0f));

		Assert.Equal(3, result.PortSea[2]);          // the pixel under it is province 3's
		Assert.Equal(0, result.PortSea[1]);          // on land: no port, though the sea is next door
	}

	[Fact]
	public void APortCannotOpenOntoALake()
	{
		CMapPorts.Result result = Build(new CPortPlacement(2, 4.5f, 0f), new CPortPlacement(1, 5.5f, 0f));

		Assert.Equal(0, result.PortSea[2]);          // 5 touches no other sea
		Assert.Equal(0, result.PortSea[1]);          // 6 is listed under the continent "lake"
	}

	[Fact]
	public void ThePortsAreNumberedFirstInTheFilesOrderAndThenTheSeasById()
	{
		CMapPorts.Result result = Build(new CPortPlacement(2, 2f, 0f), new CPortPlacement(1, 3f, 0f));

		Assert.Equal(new[] { -1, 1, 0, 2, 3, -1, -1 }, result.SeaIndex);
		Assert.Equal(4, result.Count);
		Assert.Equal(4, result.PortSea[1]);
	}

	[Fact]
	public void ALakeIsASeaThatTouchesNoSeaOrIsListedAsOne()
	{
		CMapPorts.Result result = Build();

		Assert.Equal(new[] { -1, -1, -1, 0, 1, -1, -1 }, result.SeaIndex);
		Assert.True(CMapPorts.TouchesNoSea(5, id => IsSea[id], Edges));
		Assert.False(CMapPorts.TouchesNoSea(6, id => IsSea[id], Edges));
	}
}
