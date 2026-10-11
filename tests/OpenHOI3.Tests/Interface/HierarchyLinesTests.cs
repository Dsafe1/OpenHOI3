using System;
using System.Collections.Generic;
using System.Numerics;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The lines of a selected unit's hierarchy, against <c>docs/mechanics/mapunits.md</c>,
/// section 8: an army in province 1, a corps under it in 2, two divisions under the corps
/// in 3 and in 2, and a division straight under the army in 4.
/// </summary>
public class HierarchyLinesTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static readonly Dictionary<int, Vector2> Points = new()
	{
		[1] = new Vector2(100, 100), [2] = new Vector2(130, 100), [3] = new Vector2(130, 140),
		[4] = new Vector2(100, 60), [5] = new Vector2(990, 100),
	};

	private static Vector2 PointOf(int province) => Points[province];

	private static CUnit Unit(EUnitKind kind, int location, CUnit? parent, int brigades = 1)
	{
		var unit = new CUnit(kind, Ger) { Location = location, Parent = parent };
		var type = new CSubUnitType(kind == EUnitKind.Navy ? "destroyer" : "infantry_brigade",
			kind == EUnitKind.Navy ? EUnitBranch.Naval : EUnitBranch.Land);
		for (int index = 0; index < brigades; index++)
		{
			unit.SubUnits.Add(new CSubUnit(type) { Unit = unit });
		}
		parent?.Children.Add(unit);
		return unit;
	}

	private static (CUnit Army, CUnit Corps, CUnit Far, CUnit Near, CUnit Direct) Hierarchy()
	{
		CUnit army = Unit(EUnitKind.Army, 1, null);
		CUnit corps = Unit(EUnitKind.Corps, 2, army);
		CUnit far = Unit(EUnitKind.Division, 3, corps, brigades: 3);
		CUnit near = Unit(EUnitKind.Division, 2, corps, brigades: 3);
		CUnit direct = Unit(EUnitKind.Division, 4, army);
		return (army, corps, far, near, direct);
	}

	[Fact]
	public void EveryUnitUnderTheSelectedOneIsJoinedToItsOwnCommander()
	{
		(CUnit army, _, _, _, _) = Hierarchy();

		List<CHierarchyLine> lines = CHierarchyLines.Build(army, PointOf, mapWidth: 1000);

		// The army has nobody over it; the division in its corps' own province has no line.
		Assert.Equal(3, lines.Count);
		Assert.Equal((new Vector2(130, 100), new Vector2(100, 100)), (lines[0].From, lines[0].To));   // the corps to the army
		Assert.Equal((new Vector2(130, 140), new Vector2(130, 100)), (lines[1].From, lines[1].To));   // a division to its corps
		Assert.Equal((new Vector2(100, 60), new Vector2(100, 100)), (lines[2].From, lines[2].To));    // a division to the army
		Assert.All(lines, line => Assert.False(line.IsBroken));
	}

	[Fact]
	public void ALinesColourSaysHowFarUnderTheSelectedUnitItsUnitIs()
	{
		(CUnit army, CUnit corps, CUnit far, _, CUnit direct) = Hierarchy();
		CUnit brigade = Unit(EUnitKind.Division, 5, far);

		Assert.Equal(CHierarchyLines.Under, CHierarchyLines.ColourOf(corps, army));
		Assert.Equal(CHierarchyLines.Under, CHierarchyLines.ColourOf(direct, army));
		Assert.Equal(CHierarchyLines.TwoUnder, CHierarchyLines.ColourOf(far, army));
		Assert.Equal(CHierarchyLines.Further, CHierarchyLines.ColourOf(brigade, army));

		// The selected unit's own line, up to its commander, is the green one.
		List<CHierarchyLine> lines = CHierarchyLines.Build(corps, PointOf, mapWidth: 1000);
		Assert.Equal(CHierarchyLines.Own, lines[0].Colour);
		Assert.Equal(new Vector2(100, 100), lines[0].To);
		Assert.Equal(CHierarchyLines.Under, lines[1].Colour);
		Assert.Equal(new CColor(0f, 120 / 255f, 0f), CHierarchyLines.Own);
		Assert.Equal(new CColor(20 / 255f, 50 / 255f, 200 / 255f), CHierarchyLines.Under);
	}

	[Fact]
	public void ALineIsAsWideAsItsUnitsRank()
	{
		(CUnit army, CUnit corps, CUnit far, _, CUnit direct) = Hierarchy();

		List<CHierarchyLine> lines = CHierarchyLines.Build(army, PointOf, mapWidth: 1000);

		Assert.Equal(0.6f, lines[0].HalfWidth, 5);     // a corps: the third mark
		Assert.Equal(0.4f, lines[1].HalfWidth, 5);     // a division of several brigades: the second
		Assert.Equal(0.2f, lines[2].HalfWidth, 5);     // a division of one: the first
		Assert.Equal(3, CCounterText.SizeFrame(corps));
		Assert.Equal(2, CCounterText.SizeFrame(far));
		Assert.Equal(1, CCounterText.SizeFrame(direct));
	}

	[Fact]
	public void AUnitOutOfRadioRangeHasABrokenDarkRedLineWhereverItStands()
	{
		(CUnit army, CUnit corps, CUnit far, _, _) = Hierarchy();

		List<CHierarchyLine> lines = CHierarchyLines.Build(army, PointOf, mapWidth: 1000, unit => unit == far);

		Assert.Equal(CHierarchyLines.Under, lines[0].Colour);
		Assert.False(lines[0].IsBroken);
		Assert.Equal(CHierarchyLines.OutOfReach, lines[1].Colour);
		Assert.True(lines[1].IsBroken);
		Assert.Same(corps, far.Parent);
	}

	[Fact]
	public void ALineGoesTheShortWayRoundTheMap()
	{
		CUnit army = Unit(EUnitKind.Army, 1, null);
		Unit(EUnitKind.Division, 5, army);                 // at x 990 of a map 1000 wide: 110 to the west

		CHierarchyLine line = CHierarchyLines.Build(army, PointOf, mapWidth: 1000)[0];

		Assert.Equal(new Vector2(990, 100), line.From);
		Assert.Equal(new Vector2(1100, 100), line.To);
	}

	[Fact]
	public void AFleetHasNoLineAndNeitherHasAUnitOfNoPlace()
	{
		CUnit army = Unit(EUnitKind.Army, 1, null);
		Unit(EUnitKind.Navy, 3, army);
		Unit(EUnitKind.Division, 0, army);

		Assert.Empty(CHierarchyLines.Build(army, PointOf, mapWidth: 1000));
	}

	[Fact]
	public void ALinesCornersStandItsHalfWidthEitherSideAndItsPictureRepeatsByItsLength()
	{
		var line = new CHierarchyLine(new Vector2(10, 10), new Vector2(10, 50), 0.4f, CHierarchyLines.Under, false);
		Span<Vector2> corners = stackalloc Vector2[4];

		Assert.True(CHierarchyLines.Corners(line, corners, out float pictures));

		Assert.Equal(new Vector2(9.6f, 10), corners[0]);
		Assert.Equal(new Vector2(10.4f, 10), corners[1]);
		Assert.Equal(new Vector2(9.6f, 50), corners[2]);
		Assert.Equal(new Vector2(10.4f, 50), corners[3]);
		// Forty long and 0.8 wide: fifty widths, a fifth of a picture each.
		Assert.Equal(10f, pictures, 4);

		var none = new CHierarchyLine(new Vector2(10, 10), new Vector2(10, 10), 0.4f, CHierarchyLines.Under, false);
		Assert.False(CHierarchyLines.Corners(none, corners, out _));
	}
}
