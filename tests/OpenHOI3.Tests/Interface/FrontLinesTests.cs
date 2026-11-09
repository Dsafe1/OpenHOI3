using System.Collections.Generic;
using System.Numerics;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>The black dashed line of a headquarters under an AI, against <c>docs/mechanics/mapunits.md</c>, section 11.</summary>
public class FrontLinesTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	/// <summary>Two provinces of Germany's in a row beside two of Poland's: a straight front between.</summary>
	private static (CGameState State, CUnit Unit) World(CCountryTag beyond)
	{
		CProvince Province(int id, CCountryTag holder, params int[] beside)
		{
			var province = new CProvince(id, "p" + id, isSea: false) { Owner = holder, Controller = holder, CentreX = id % 10 * 10, CentreY = id / 10 * 10 };
			province.Edges = System.Array.ConvertAll(beside, to => new CProvinceEdge(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.Zero));
			return province;
		}
		var provinces = new List<CProvince> { new(0, "", isSea: false) };
		for (int id = 1; id < 24; id++)
		{
			provinces.Add(id switch
			{
				11 => Province(11, Ger, 12, 21),
				12 => Province(12, Ger, 11, 21, 22),
				21 => Province(21, beyond, 11, 12, 22),
				22 => Province(22, beyond, 12, 21),
				_ => new CProvince(id, "", isSea: false),
			});
		}
		var state = new CGameState(new CDate(1936, 1, 1), provinces, new[]
		{
			new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" },
			new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" },
		});
		var unit = new CUnit(EUnitKind.Theatre, Ger);
		unit.Plan.Area.AddRange(new[] { 11, 12 });
		return (state, unit);
	}

	private static Vector2 Centre(CGameState state, int id) => new(state.Province(id).CentreX, state.Province(id).CentreY);

	[Fact]
	public void TheLineRunsHalfWayBetweenTheAreaAndTheForeignLandBesideIt()
	{
		(CGameState state, CUnit unit) = World(Pol);
		List<CFrontLines.CStrip> strips = CFrontLines.Build(state, unit, id => Centre(state, id));
		Assert.Single(strips);
		Assert.All(strips[0].Positions, point => Assert.InRange(point.Y, 8f, 23f));
	}

	[Fact]
	public void LandOfTheSameCountryIsNoFront()
	{
		(CGameState state, CUnit unit) = World(Ger);
		Assert.Empty(CFrontLines.Build(state, unit, id => Centre(state, id)));
	}

	[Fact]
	public void TheStripIsSixWideAndCountsItsLengthAlong()
	{
		CFrontLines.CStrip strip = CFrontLines.Strip(new List<Vector2> { new(0, 0), new(10, 0), new(20, 0) });
		Assert.Equal(6, strip.Positions.Length);
		Assert.Equal(new Vector2(0, 3), strip.Positions[0]);
		Assert.Equal(new Vector2(0, -3), strip.Positions[1]);
		Assert.Equal(new Vector2(10, 0), strip.Uvs[2]);
		Assert.Equal(new Vector2(20, 1), strip.Uvs[5]);
		Assert.Equal(new ushort[] { 0, 1, 2, 1, 3, 2, 2, 3, 4, 3, 5, 4 }, strip.Indices);
	}

	[Fact]
	public void CuttingCornersMakesTwoPointsOfEachButTheEnds()
	{
		List<Vector2> cut = CFrontLines.CutCorners(new List<Vector2> { new(0, 0), new(4, 0), new(4, 4) });
		Assert.Equal(new[] { new Vector2(0, 0), new Vector2(3, 0), new Vector2(4, 1), new Vector2(4, 4) }, cut);
	}

	[Fact]
	public void RelaxingLeavesTheTwoPointsAtEachEndAndFourPointsAlone()
	{
		var four = new List<Vector2> { new(0, 0), new(1, 0), new(1, 1), new(2, 1) };
		Assert.Equal(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(2, 1) }, CFrontLines.Relax(new List<Vector2>(four)));
		var five = new List<Vector2> { new(0, 0), new(1, 0), new(2, 0), new(2, 1), new(2, 2) };
		List<Vector2> relaxed = CFrontLines.Relax(five);
		Assert.Equal(new Vector2(1, 0), relaxed[1]);
		Assert.Equal(new Vector2(2, 1), relaxed[3]);
		Assert.NotEqual(new Vector2(2, 0), relaxed[2]);
	}
}
