using System;
using System.IO;
using OpenHOI3.Content;
using OpenHOI3.Content.Map;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// The map cache's stamp and where its files are found, as <c>docs/mechanics/mapdraw.md</c>, section 2.1, has them: a stale
/// cache is the wrong map drawn without an error, so the stamp's arithmetic and the order of the folders are pinned down.
/// </summary>
public class MapCacheTests : IDisposable
{
	private readonly string _root;
	private readonly string _user;

	public MapCacheTests()
	{
		string temp = Path.Combine(Path.GetTempPath(), "openhoi3-tests-" + Guid.NewGuid().ToString("N"));
		_root = Path.Combine(temp, "install");
		_user = Path.Combine(temp, "user");
		Write(_root, "tfh/map/default.map", "provinces = \"provinces.bmp\"\npositions = \"positions.txt\"\n");
		Write(_root, "tfh/map/provinces.bmp", "pixels");
		Write(_root, "map/positions.txt", "1 = { }");
	}

	public void Dispose()
	{
		try
		{
			Directory.Delete(Path.GetDirectoryName(_root)!, recursive: true);
		}
		catch (IOException)
		{
			// A locked file must not fail the test that already passed.
		}
		GC.SuppressFinalize(this);
	}

	private static void Write(string root, string relative, string content)
	{
		string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, content);
	}

	private static void WriteStamp(string root, uint stamp)
	{
		string path = Path.Combine(root, "map", "cache", "time");
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllBytes(path, BitConverter.GetBytes(stamp));
	}

	private CDirectorySettings Settings => CDirectorySettings.At(_root);

	[Fact]
	public void ChecksumSumsBothWays()
	{
		// a = 1, 3, 6; b = 1, 4, 10.
		Assert.Equal(0x0006000Au, CMapCache.Checksum(new byte[] { 1, 2, 3 }));
	}

	[Fact]
	public void ChecksumKeepsASumThatLandsOn65535()
	{
		// 257 bytes of 255 bring a to 65535 exactly, which a single subtraction leaves alone and a remainder would make nought.
		byte[] bytes = new byte[257];
		Array.Fill(bytes, (byte)255);
		Assert.Equal(0xFFFFu, CMapCache.Checksum(bytes) >> 16);
		// One more passes it and comes back to 255, where a remainder would give 255 too, from nought.
		bytes = new byte[258];
		Array.Fill(bytes, (byte)255);
		Assert.Equal(255u, CMapCache.Checksum(bytes) >> 16);
	}

	[Fact]
	public void StampSumsDefaultMapAndTheFilesItNamesThroughTheLayers()
	{
		uint expected = unchecked(CMapCache.Checksum(Path.Combine(_root, "tfh", "map", "default.map"))
			+ CMapCache.Checksum(Path.Combine(_root, "map", "positions.txt"))
			+ CMapCache.Checksum(Path.Combine(_root, "tfh", "map", "provinces.bmp")));
		// terrain.txt, terrain.bmp, definition.csv and adjacencies.csv are not there and count nought.
		Assert.Equal(expected, CMapCache.Stamp(Settings));
	}

	[Fact]
	public void CacheWithTheMapsStampIsCurrent()
	{
		WriteStamp(Path.Combine(_root, "tfh"), CMapCache.Stamp(Settings));
		Write(_root, "tfh/map/cache/quad_index.bin", "x");
		CMapCache cache = CMapCache.Open(Settings, _user, _ => throw new InvalidOperationException());
		Assert.True(cache.IsCurrent);
		Assert.Equal(Path.Combine(_root, "tfh", "map", "cache", "quad_index.bin"), cache.Find("quad_index.bin"));
	}

	[Fact]
	public void CacheWithAnotherStampIsStaleAndSaysSo()
	{
		WriteStamp(Path.Combine(_root, "tfh"), CMapCache.Stamp(Settings) + 1);
		string? said = null;
		CMapCache cache = CMapCache.Open(Settings, _user, problem => said = problem);
		Assert.False(cache.IsCurrent);
		Assert.Contains("not read", said);
		Assert.Empty(CQuadCache.Load(cache, _ => throw new InvalidOperationException()));
	}

	[Fact]
	public void CacheWithNoStampIsStale()
	{
		Write(_root, "tfh/map/cache/quad_index.bin", "x");
		Assert.False(CMapCache.Open(Settings, _user, _ => { }).IsCurrent);
	}

	[Fact]
	public void PlayersFolderComesBeforeTheInstall()
	{
		// The player's stamp is the one asked, as the original resolves map/cache/time; the install's is wrong here.
		WriteStamp(_user, CMapCache.Stamp(Settings));
		WriteStamp(Path.Combine(_root, "tfh"), 0);
		Write(_user, "map/cache/map.bin", "user");
		Write(_root, "tfh/map/cache/map.bin", "install");
		Write(_root, "tfh/map/cache/all_quads.bin", "install");
		CMapCache cache = CMapCache.Open(Settings, _user, _ => throw new InvalidOperationException());
		Assert.True(cache.IsCurrent);
		Assert.Equal(Path.Combine(_user, "map", "cache", "map.bin"), cache.Find("map.bin"));
		Assert.Equal(Path.Combine(_root, "tfh", "map", "cache", "all_quads.bin"), cache.Find("all_quads.bin"));
		Assert.Null(cache.Find("border_adj.bin"));
	}
}
