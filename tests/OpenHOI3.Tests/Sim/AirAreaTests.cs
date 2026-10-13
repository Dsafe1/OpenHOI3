using System;
using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The area of an air mission as the orders window makes it, against
/// <c>docs/mechanics/aircombat.md</c>, section 2, on a star of Polish provinces about a
/// middle one, ten pixels out each way - forty as a range measures it:
///
///                 NW   N   NE
///             W ------ C ------ E           Berlin, Germany's airfield, far to the west
///                      S
///
/// C is joined to each of the five and W to Berlin.
/// </summary>
public class AirAreaTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Berlin = 1;
	private const int C = 2;
	private const int N = 3;
	private const int E = 4;
	private const int S = 5;
	private const int W = 6;
	private const int NE = 7;
	private const int NW = 8;
	private const int Far = 9;

	private static readonly CBuilding AirBase = new(0, "air_base", 10, "", CFixedPoint.Zero);

	private static CProvinceEdge Edge(int to) => new(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.FromInt(10));

	private static CProvince Land(int id, CCountryTag owner, int x, int y, params int[] edges)
	{
		var province = new CProvince(id, "P" + id, isSea: false)
		{
			Owner = owner, Controller = owner, CentreX = x, CentreY = y,
			Edges = Array.ConvertAll(edges, Edge),
		};
		return province;
	}

	private static CGameState State(out CCountry germany, params (string Name, int[] Provinces)[] regions)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		var berlin = Land(Berlin, Ger, 60, 100, W);
		berlin.SetBuildingLevel(AirBase, CFixedPoint.FromInt(4));
		var list = new List<CRegion>();
		foreach ((string name, int[] provinces) in regions)
		{
			list.Add(new CRegion(name, provinces, false));
		}
		var state = new CGameState(new CDate(1936, 6, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				berlin,
				Land(C, Pol, 100, 100, N, E, S, W, NE, NW),
				Land(N, Pol, 100, 110, C),
				Land(E, Pol, 110, 100, C),
				Land(S, Pol, 100, 90, C),
				Land(W, Pol, 90, 100, C, Berlin),
				Land(NE, Pol, 110, 110, C),
				Land(NW, Pol, 90, 110, C),
				Land(Far, Pol, 300, 100),
			},
			new[] { germany, poland }, buildings: new[] { AirBase }, wars: new[] { war })
		{
			MapWidth = 1000,
			MapHeight = 1000,
			Regions = list,
		};
		for (int index = 0; index < list.Count; index++)
		{
			foreach (int id in list[index].Provinces)
			{
				state.Provinces[id].Regions.Add(index);
			}
		}
		return state;
	}

	private static CUnit Fighters(CGameState state, CCountry germany, string range = "1000")
	{
		var type = new CSubUnitType("interceptor", EUnitBranch.Air);
		type.Stats.Values.Set(CUnitOrders.Range, CFixedPoint.Parse(range));
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(100));
		var unit = new CUnit(EUnitKind.Air, Ger) { Id = ++state.LastUnitId, Location = Berlin, Base = Berlin, Name = "wing" };
		var wing = new CSubUnit(type) { Id = ++state.LastUnitId, Unit = unit, Strength = CFixedPoint.FromInt(100), Name = "w" };
		wing.Stats.CopyFrom(type.Stats);
		unit.SubUnits.Add(wing);
		germany.Units.Add(unit);
		return unit;
	}

	private static CAirArea Area(CGameState state, CCountry germany, CUnit unit, EAirAreaShape shape)
	{
		var area = new CAirArea(state, germany, unit, EUnitOrder.AirSuperiority, C, shape);
		area.SetShape(shape);
		return area;
	}

	private const float Turn = (float)(2 * Math.PI);

	[Fact]
	public void ABearingIsClockwiseFromNorth()
	{
		CGameState state = State(out _);
		CProvince middle = state.Province(C);
		float Of(int id) => (float)(CAirArea.Bearing(middle, state.Province(id)) % (2 * Math.PI));

		Assert.Equal(0f, Of(N), 4);
		Assert.Equal(Turn / 8, Of(NE), 4);
		Assert.Equal(Turn / 4, Of(E), 4);
		Assert.Equal(Turn / 2, Of(S), 4);
		Assert.Equal(Turn * 3 / 4, Of(W), 4);
	}

	[Fact]
	public void AnArcThatStartsBelowNorthLosesItsPartWestOfNorth()
	{
		// Pointed just east of north, half a width either side wider than the angle.
		float angle = 0.05f * Turn / 2;
		float half = 0.3f * Turn / 2;
		Assert.True(CAirArea.IsWithinArc(0.1f * Turn / 2, angle, half));
		Assert.False(CAirArea.IsWithinArc(1.75f * Turn / 2, angle, half));

		// The same arc pointed just west of north keeps both sides, its end going round.
		Assert.True(CAirArea.IsWithinArc(1.75f * Turn / 2, 1.95f * Turn / 2, half));
		Assert.True(CAirArea.IsWithinArc(0.1f * Turn / 2, 1.95f * Turn / 2, half));
	}

	[Fact]
	public void APointIsTheWindowsProvinceAndShiftAddsOthersOnce()
	{
		CGameState state = State(out CCountry germany);
		CAirArea area = Area(state, germany, Fighters(state, germany), EAirAreaShape.Point);

		Assert.Equal(new[] { C }, area.Provinces);
		area.Add(E);
		area.Add(E);
		Assert.Equal(new[] { C, E }, area.Provinces);
	}

	[Fact]
	public void ACircleOfNothingIsItsMiddleAndAimingItReachesTheProvinceClicked()
	{
		CGameState state = State(out CCountry germany);
		CAirArea area = Area(state, germany, Fighters(state, germany), EAirAreaShape.Circle);
		Assert.Equal(new[] { C }, area.Provinces);

		// Forty to E: the four neighbours forty off, not the corners at fifty-six.
		area.SetSecondTarget(E, false);
		Assert.Equal(CFixedPoint.FromInt(40), area.Range);
		Assert.Equal(new[] { C, N, E, S, W }, area.Provinces);

		// Shift takes one off and puts it back.
		area.SetSecondTarget(S, true);
		Assert.Equal(new[] { C, N, E, W }, area.Provinces);
		area.SetSecondTarget(S, true);
		Assert.Equal(new[] { C, N, E, W, S }, area.Provinces);

		// The window's own province does nothing.
		area.SetSecondTarget(C, false);
		Assert.Equal(CFixedPoint.FromInt(40), area.Range);
	}

	[Fact]
	public void AimingIsNoFurtherThanTheUnitCanFly()
	{
		CGameState state = State(out CCountry germany);
		CAirArea area = Area(state, germany, Fighters(state, germany, "200"), EAirAreaShape.Circle);

		area.SetSecondTarget(Far, false);
		Assert.Equal(CFixedPoint.FromInt(200), area.Range);
	}

	[Fact]
	public void AConeKeepsWhatLiesInItsArc()
	{
		CGameState state = State(out CCountry germany);
		CAirArea area = Area(state, germany, Fighters(state, germany), EAirAreaShape.Cone);
		area.SetRange(CFixedPoint.FromInt(60));

		// East, three tenths of a half turn either side: north-east and east, not north or south.
		area.SetWidth(0.6f * Turn / 2);
		area.SetAngle(Turn / 4);
		Assert.Equal(new[] { C, E, NE }, area.Provinces);

		// Aimed at north-west: north-west and north and west, each within the arc's quarter turn.
		area.SetWidth(Turn / 4 + 0.01f);
		area.SetSecondTarget(NW, false);
		Assert.Equal(new[] { C, N, W, NW }, area.Provinces);
	}

	[Fact]
	public void ARegionIsTheFirstThatListsTheProvince()
	{
		CGameState state = State(out CCountry germany, ("first", new[] { E, C, N }), ("second", new[] { C, S }));
		CUnit unit = Fighters(state, germany);
		CAirArea area = Area(state, germany, unit, EAirAreaShape.Region);

		Assert.Equal(new[] { E, C, N }, area.Provinces);
		area.AddRegionOf(S);
		Assert.Equal(new[] { E, C, N, S }, area.Provinces);
	}

	[Fact]
	public void AMissionsStartComesWithItsCommand()
	{
		CGameState state = State(out CCountry germany);
		CUnit unit = Fighters(state, germany);
		long start = state.TotalHours + 30;

		new CSetUnitOrderCommand(Ger, unit.Id, EUnitOrder.AirSuperiority, C, provinces: new[] { C, E },
			time: CMission.ByNight, endHour: start + 100, startHour: start).Execute(state);

		Assert.Equal(start, unit.Mission.StartHour);
		Assert.Equal(start + 100, unit.Mission.EndHour);
		Assert.Equal(CMission.ByNight, unit.Mission.Time);
		Assert.Equal(new[] { C, E }, unit.Mission.Provinces);
	}
}
