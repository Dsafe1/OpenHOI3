using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Map;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// map.bin's list of the rivers' sources and its test by date, as <c>docs/mechanics/mapdraw.md</c>, section 4, and
/// <c>docs/compatibility.md</c>, entry 20, have them: the list read where the file is the map's, and the bitmap's own sources
/// where it is not - the river bitmap among the files it is tested against, which the original leaves out.
/// </summary>
public class MapBinTests : IDisposable
{
	private static readonly string[] Files =
	{
		"positions.txt", "provinces.bmp", "terrain.bmp", "definition.csv", "adjacencies.csv", "terrain.txt", "continent.txt",
		"region.txt", "coastlines.txt", "rivers.bmp",
	};

	private readonly string _root;
	private readonly DateTime _old = new(2025, 9, 4, 16, 0, 0, DateTimeKind.Utc);

	/// <summary>A river raster of 3 by 2: sources at (2, 0) and (1, 1), by rows from the south.</summary>
	private readonly byte[] _rivers = { 255, 255, 0, 255, 0, 255 };

	public MapBinTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "openhoi3-tests-" + Guid.NewGuid().ToString("N"));
		Write("tfh/map/default.map", "provinces = \"provinces.bmp\"\n");
		foreach (string file in Files)
		{
			Write("tfh/map/" + file, "x");
		}
		// The cache's stamp is the map files', so that the rest of the cache is current.
		Write("tfh/map/cache/time", "");
		File.WriteAllBytes(Path.Combine(_root, "tfh", "map", "cache", "time"), BitConverter.GetBytes(CMapCache.Stamp(Settings)));
		WriteMapBin((7, 8));
		foreach (string file in Files.Append("default.map"))
		{
			File.SetLastWriteTimeUtc(Path.Combine(_root, "tfh", "map", file), _old);
		}
		File.SetLastWriteTimeUtc(MapBinPath, _old.AddMinutes(10));
	}

	public void Dispose()
	{
		try
		{
			Directory.Delete(_root, recursive: true);
		}
		catch (IOException)
		{
			// A locked file must not fail the test that already passed.
		}
		GC.SuppressFinalize(this);
	}

	private CDirectorySettings Settings => CDirectorySettings.At(_root);

	private string MapBinPath => Path.Combine(_root, "tfh", "map", "cache", "map.bin");

	private void Write(string relative, string content)
	{
		string path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, content);
	}

	/// <summary>map.bin: the head, the count, the pairs, and a raster that does not matter here.</summary>
	private void WriteMapBin(params (int X, int Y)[] sources)
	{
		var bytes = new List<byte>(new byte[CMapBin.Head]);
		bytes.AddRange(BitConverter.GetBytes(sources.Length));
		foreach ((int x, int y) in sources)
		{
			bytes.AddRange(BitConverter.GetBytes(x));
			bytes.AddRange(BitConverter.GetBytes(y));
		}
		bytes.AddRange(new byte[12]);
		File.WriteAllBytes(MapBinPath, bytes.ToArray());
	}

	private (IReadOnlyList<(int X, int Y)> Sources, List<string> Problems) Sources()
	{
		var problems = new List<string>();
		CMapCache cache = CMapCache.Open(Settings, null, problems.Add);
		IReadOnlyList<(int X, int Y)> sources = CMapBin.RiverSources(cache, _rivers, 3, 2, problems.Add);
		return (sources, problems);
	}

	[Fact]
	public void CurrentFileGivesItsOwnList()
	{
		(IReadOnlyList<(int X, int Y)> sources, List<string> problems) = Sources();
		Assert.Equal(new[] { (7, 8) }, sources);
		Assert.Empty(problems);
	}

	[Fact]
	public void BuilderTakesIndexZeroPixelsByRowsFromTheSouth()
	{
		Assert.Equal(new[] { (2, 0), (1, 1) }, CMapBin.BitmapSources(_rivers, 3, 2));
	}

	[Theory]
	[InlineData("rivers.bmp")]
	[InlineData("coastlines.txt")]
	[InlineData("provinces.bmp")]
	public void FileNewerThanTheCacheMakesTheBuildersList(string file)
	{
		// rivers.bmp the original leaves out (ours); the others are its own nine.
		File.SetLastWriteTimeUtc(Path.Combine(_root, "tfh", "map", file), _old.AddMinutes(20));
		(IReadOnlyList<(int X, int Y)> sources, List<string> problems) = Sources();
		Assert.Equal(new[] { (2, 0), (1, 1) }, sources);
		Assert.Contains(problems, p => p.Contains(file) && p.Contains("newer"));
	}

	[Fact]
	public void MissingSourceMakesTheBuildersList()
	{
		File.Delete(Path.Combine(_root, "tfh", "map", "region.txt"));
		Assert.Equal(new[] { (2, 0), (1, 1) }, Sources().Sources);
	}

	[Fact]
	public void FileThatDoesNotReadMakesTheBuildersList()
	{
		// The head and a count of nine sources, and nothing after it.
		var bytes = new byte[CMapBin.Head + 4];
		BitConverter.GetBytes(9).CopyTo(bytes, CMapBin.Head);
		File.WriteAllBytes(MapBinPath, bytes);
		File.SetLastWriteTimeUtc(MapBinPath, _old.AddMinutes(10));
		(IReadOnlyList<(int X, int Y)> sources, List<string> problems) = Sources();
		Assert.Equal(new[] { (2, 0), (1, 1) }, sources);
		Assert.Contains(problems, p => p.Contains("does not read"));
	}
}
