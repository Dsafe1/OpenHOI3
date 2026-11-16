using System;
using System.IO;
using System.Text;
using OpenHOI3.Content;
using OpenHOI3.Content.Checksum;
using OpenHOI3.Content.Map;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// The original's file checksum (docs/mechanics/checksum.md): which files go in, which copy of each, and the letters.
/// A wrong rule gives a plausible figure and different letters, so each rule is pinned on a fixture install.
/// </summary>
public class FileChecksumTests : IDisposable
{
	private readonly string _root;
	private readonly string _user;

	public FileChecksumTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "openhoi3-tests-" + Guid.NewGuid().ToString("N"));
		_user = Path.Combine(_root, "user");
		Directory.CreateDirectory(Path.Combine(_root, "tfh"));
		Directory.CreateDirectory(_user);
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

	private void Write(string relative, string content)
	{
		string path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, content, Encoding.Latin1);
	}

	private static uint Sum(string content)
	{
		return CMapCache.Checksum(Encoding.Latin1.GetBytes(content));
	}

	private static readonly uint VersionTerm = CFileChecksum.SignedChecksum(Encoding.ASCII.GetBytes(CFileChecksum.VersionName));

	private (int Checksum, int Files) Compute()
	{
		return CFileChecksum.Compute(CChecksumSearchPath.For(CDirectorySettings.At(_root, null, _user)));
	}

	private static int Expected(params string[] contents)
	{
		uint total = VersionTerm;
		foreach (string content in contents)
		{
			total += Sum(content);
		}
		return unchecked((int)total);
	}

	[Theory]
	[InlineData(1499173604, "GSKH")]
	[InlineData(-410340735, "ZAUD")]
	[InlineData(-779044967, "ZHBH")]
	[InlineData(-829062603, "FZSY")]
	[InlineData(-2035974651, "JWQX")]
	public void TheLettersAreTheOriginalsForEveryFigureItLogged(int checksum, string letters)
	{
		Assert.Equal(letters, CFileChecksum.Letters(checksum));
	}

	[Fact]
	public void AnInstallWithNothingSummedGivesTheVersionNamesTermAlone()
	{
		Assert.Equal((unchecked((int)VersionTerm), 0), Compute());
	}

	[Fact]
	public void AStringsBytesAreTakenSigned()
	{
		Assert.Equal(CMapCache.Checksum(Encoding.ASCII.GetBytes("HoI3 v4.02")), VersionTerm);
		Assert.Equal(0xFFFFFF80u, CFileChecksum.SignedChecksum(new byte[] { 0x80 }));
	}

	[Fact]
	public void ANameInTwoLayersIsSummedOnceFromTheFirst()
	{
		Write("tfh/events/a.txt", "expansion");
		Write("events/a.txt", "base");
		Write("events/b.txt", "only base");

		Assert.Equal((Expected("expansion", "only base"), 2), Compute());
	}

	[Fact]
	public void TheExtensionIsMatchedWithItsCase()
	{
		Write("events/a.txt", "kept");
		Write("events/b.TXT", "left out");
		Write("events/c.txt.bak", "left out");

		Assert.Equal(Expected("kept"), Compute().Checksum);
	}

	[Fact]
	public void OnlyTheFoldersOfTheListGoInAndOnlyTheirOwnSubfoldersRecurse()
	{
		Write("history/provinces/europe/1 - A.txt", "province");
		Write("history/countries/old/GER.txt", "not recursed");
		Write("common/other/x.txt", "not summed");
		Write("common/countries/Germany.txt", "country");
		Write("interface/core.gui", "not summed");
		Write("localisation/text.csv", "not summed");

		Assert.Equal((Expected("province", "country"), 2), Compute());
	}

	[Fact]
	public void UnitsAndCommonGoInWithBothExtensions()
	{
		Write("common/defines.lua", "lua");
		Write("common/buildings.txt", "txt");
		Write("units/infantry_brigade.txt", "unit");
		Write("units/country/GER.lua", "country lua");

		Assert.Equal(Expected("lua", "txt", "unit", "country lua"), Compute().Checksum);
	}

	[Fact]
	public void TheMapsThreeFilesComeFromWhereverTheSearchPathFindsThem()
	{
		Write("map/default.map", "map");
		Write("user/map/cache/time", "stamp");
		Write("user/map/cache/adjacencies.bin", "edges");
		Write("map/provinces.bmp", "not summed");

		Assert.Equal((Expected("map", "stamp", "edges"), 3), Compute());
	}

	[Fact]
	public void SvnFoldersAndNamesPastAsciiAreLeftOut()
	{
		Write("battleplans/historical/GER/a.pln", "plan");
		Write("battleplans/.svn/b.pln", "left out");
		Write("cgm/é.txt", "left out");
		Write("cgm/a.txt", "cgm");

		Assert.Equal(Expected("plan", "cgm"), Compute().Checksum);
	}

	[Fact]
	public void AFolderNamedLikeAFileIsNotSummed()
	{
		Directory.CreateDirectory(Path.Combine(_root, "events", "folder.txt"));
		Write("events/a.txt", "kept");

		Assert.Equal((Expected("kept"), 1), Compute());
	}

	[Fact]
	public void AReplacedFolderListsOnlyTheModsFiles()
	{
		Write("mod/events/a.txt", "mod");
		Write("events/b.txt", "base, replaced");
		Write("mod/decisions/c.txt", "mod decision");
		Write("decisions/d.txt", "base decision");
		var search = new CChecksumSearchPath(new[] { Path.Combine(_root, "mod"), _root }, new[] { "events" }, replaceLimit: 0);

		Assert.Equal(Expected("mod", "mod decision", "base decision"), CFileChecksum.Compute(search).Checksum);
	}
}
