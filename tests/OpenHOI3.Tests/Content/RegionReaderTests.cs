using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// The reading of <c>map/region.txt</c>, against <c>docs/mechanics/aircombat.md</c>,
/// section 2. The text is written here in the file's own shape and is not a copy of it.
/// </summary>
public class RegionReaderTests
{
	private readonly List<string> _warnings = new();

	private List<CRegion> Read(string text)
	{
		return CRegionReader.Read(ClausewitzParser.Parse(text), 100, _warnings.Add);
	}

	[Fact]
	public void Regions_keep_the_files_order_and_their_provinces_theirs()
	{
		List<CRegion> regions = Read(@"
			sea_region = { 12 10 11 }
			coast_region = {
				peace
				3 2
			}
		");

		Assert.Equal(new[] { "sea_region", "coast_region" }, new[] { regions[0].Name, regions[1].Name });
		Assert.Equal(new[] { 12, 10, 11 }, regions[0].Provinces);
		Assert.False(regions[0].UseForPeace);
		Assert.Equal(new[] { 3, 2 }, regions[1].Provinces);
		Assert.True(regions[1].UseForPeace);
		Assert.Empty(_warnings);
	}

	[Fact]
	public void An_entry_that_is_no_province_is_reported_and_left_out()
	{
		List<CRegion> regions = Read("edge_region = { 5 500 war }");

		Assert.Equal(new[] { 5 }, regions[0].Provinces);
		Assert.Single(_warnings);
	}
}
