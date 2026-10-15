using OpenHOI3.Content.Map;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// An edge's bearing, against <c>docs/mechanics/weather.md</c>, section 3: thousandths of a turn from due west, the
/// map's y counting north, worked in single precision and floored.
/// </summary>
public class EdgeBearingTests
{
	private static int Bearing(int toX, int toY) => CMapGraph.Bearing(new CMapPosition(100, 100), new CMapPosition(toX, toY));

	[Fact]
	public void East_is_500_north_250_south_750_and_due_west_1000()
	{
		Assert.Equal(500, Bearing(110, 100));
		Assert.Equal(250, Bearing(100, 110));
		Assert.Equal(750, Bearing(100, 90));
		Assert.Equal(1000, Bearing(90, 100));
	}

	[Fact]
	public void Only_a_hair_north_of_west_is_0()
	{
		Assert.Equal(0, Bearing(-900, 101));
		Assert.Equal(999, Bearing(-900, 95));
	}
}
