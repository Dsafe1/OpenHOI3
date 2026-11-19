using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// The reading of <c>map/coastlines.txt</c>, against <c>docs/mechanics/modifiers.md</c>, section 6. The text is
/// written here in the file's own shape and is not a copy of it.
/// </summary>
public class CoastlinesReaderTests
{
	private readonly List<string> _warnings = new();

	private static readonly CRegion[] Regions =
	{
		new("north_sea_region", new[] { 1 }, false),
		new("pacific_coast_region", new[] { 2 }, false),
		new("baltic_sea_region", new[] { 3 }, false),
		new("atlantic_coast_region", new[] { 4 }, false),
	};

	private List<int> Read(string text)
	{
		return CCoastlinesReader.Read(ClausewitzParser.Parse(text), Regions, _warnings.Add);
	}

	[Fact]
	public void The_regions_named_are_taken_in_the_order_of_region_txt()
	{
		List<int> coastlines = Read(@"
			strategic_resource_coastlines = {
				atlantic_coast_region
				pacific_coast_region
			}
		");

		Assert.Equal(new[] { 1, 3 }, coastlines);
		Assert.Empty(_warnings);
	}

	[Fact]
	public void A_name_is_matched_as_written_and_one_that_is_no_region_is_reported()
	{
		List<int> coastlines = Read("strategic_resource_coastlines = { Atlantic_Coast_Region pacific_coast_region }");

		Assert.Equal(new[] { 1 }, coastlines);
		Assert.Single(_warnings);
		Assert.Contains("Atlantic_Coast_Region", _warnings[0]);
	}

	[Fact]
	public void A_region_named_twice_is_taken_once_and_the_original_warns_of_it()
	{
		List<int> coastlines = Read("strategic_resource_coastlines = { baltic_sea_region baltic_sea_region }");

		Assert.Equal(new[] { 2 }, coastlines);
		Assert.Single(_warnings);
	}

	[Fact]
	public void The_last_block_stands_and_any_other_key_is_reported()
	{
		List<int> coastlines = Read(@"
			strategic_resource_coastlines = { north_sea_region }
			coasts = { pacific_coast_region }
			strategic_resource_coastlines = { baltic_sea_region }
		");

		Assert.Equal(new[] { 2 }, coastlines);
		Assert.Single(_warnings);
		Assert.Contains("coasts", _warnings[0]);
	}

	[Fact]
	public void No_block_no_coastlines()
	{
		Assert.Empty(Read(""));
		Assert.Empty(_warnings);
	}

	[Fact]
	public void Each_name_that_is_no_region_is_reported_at_its_own_line()
	{
		CCoastlinesReader.Read(ClausewitzParser.Parse("strategic_resource_coastlines = {\n\tnorth_sea_region\n\tnorth_sea\n\tbaltic\n}"),
			Regions, _warnings.Add, "map/coastlines.txt");

		Assert.Equal(new[] { "map/coastlines.txt:3: no region is called north_sea", "map/coastlines.txt:4: no region is called baltic" },
			_warnings);
	}
}
