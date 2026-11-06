using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content.Map;

namespace OpenHOI3.Tests.Content;

/// <summary>map/trees.txt and terrain.txt's trees, as <c>docs/mechanics/mapdraw.md</c>, section 4, has the original read them.</summary>
public class TreeDefinitionTests : IDisposable
{
	private readonly string _folder = Path.Combine(Path.GetTempPath(), "openhoi3-tests-" + Guid.NewGuid().ToString("N"));

	public TreeDefinitionTests()
	{
		Directory.CreateDirectory(_folder);
	}

	public void Dispose()
	{
		try
		{
			Directory.Delete(_folder, recursive: true);
		}
		catch (IOException)
		{
			// A locked file must not fail the test that already passed.
		}
		GC.SuppressFinalize(this);
	}

	private string Write(string name, string text)
	{
		string path = Path.Combine(_folder, name);
		File.WriteAllText(path, text);
		return path;
	}

	[Fact]
	public void TreeTypesInFileOrderWithTheirDensityDefaultingToOne()
	{
		var warnings = new List<string>();
		List<CTreeType> types = CTreeReader.Read(Write("trees.txt",
			"woods = { texture = \"terrain/trees/woods.dds\" tree_density = 0.7 }\n"
			+ "forest = { texture = \"terrain/trees/forest.dds\" }\n"
			+ "jungle = { texture = \"terrain/trees/jungle.dds\" tree_density = 7.0 size = 3 }\n"), warnings.Add);
		Assert.Equal(new[] { "woods", "forest", "jungle" }, types.ConvertAll(t => t.Name));
		Assert.Equal(new[] { 0.7f, 1f, 7f }, types.ConvertAll(t => t.Density));
		Assert.Equal("terrain/trees/forest.dds", types[1].Texture);
		// A key nothing reads is reported.
		Assert.Contains(warnings, w => w.Contains("size"));
	}

	[Fact]
	public void TerrainEntryGivesEachIndexItsTreeAndRow()
	{
		CTerrainReader.Definition definition = CTerrainReader.ReadDefinition(Write("terrain.txt",
			"categories = { forest = { } plains = { } }\n"
			+ "forest_a = { type = forest color = { 10 11 12 13 14 } tree = forest }\n"
			+ "forest_b = { type = forest color = { 20 } tree = woods tree_density = 2 }\n"
			+ "plain = { type = plains color = { 30 } }\n"), _ => { });
		// No tree_density: 4 less the index's place in its colour list, never below nought.
		Assert.Equal(new[] { 4, 3, 2, 1, 0 }, new[] { 10, 11, 12, 13, 14 }.Select(i => definition.TreeRowOfIndex![i]).ToArray());
		Assert.Equal("forest", definition.TreeOfIndex![12]);
		// Given, it is the row's d as it stands.
		Assert.Equal(2, definition.TreeRowOfIndex![20]);
		Assert.Equal("woods", definition.TreeOfIndex![20]);
		Assert.Null(definition.TreeOfIndex![30]);
	}
}
