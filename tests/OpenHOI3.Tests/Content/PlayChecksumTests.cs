using System;
using System.IO;
using System.Text;
using OpenHOI3.Content;
using OpenHOI3.Content.Checksum;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// This engine's own checksum (docs/mechanics/checksum.md, section 8): each thing the original's figure lets by changes
/// this one, and what changes nothing a game does leaves it alone.
/// </summary>
public class PlayChecksumTests : IDisposable
{
	private readonly string _root;
	private readonly string _user;

	public PlayChecksumTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "openhoi3-tests-" + Guid.NewGuid().ToString("N"));
		_user = Path.Combine(_root, "user");
		Directory.CreateDirectory(Path.Combine(_root, "tfh"));
		Directory.CreateDirectory(_user);
		Write("common/defines.lua", "defines = {}");
		Write("events/a.txt", "one");
		Write("events/b.txt", "two");
		Write("script/ai.lua", "return 1");
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

	private string Label(string engine = "normal-1", string rules = "")
	{
		(byte[] hash, _) = CPlayChecksum.Compute(
			CChecksumSearchPath.ForPlay(CDirectorySettings.At(_root, null, _user)), engine, rules);
		return CPlayChecksum.Label(hash);
	}

	/// <summary>
	/// What lies in the folders and is no file a game reads does not count: two installs of the same game were refused
	/// each other for a Thumbs.db, an archive, three .bak files and a map cache under another name.
	/// </summary>
	[Fact]
	public void AMachinesOwnFilesInTheFoldersDoNotChangeIt()
	{
		string before = Label();
		Write("map/Thumbs.db", "a machine's own");
		Write("map/map.rar", "kept by a player");
		Write("map/_cache/adjacencies.bin", "a cache under another name");
		Write("map/_cache/time", "1");
		// A tile picture of that cache is of a kind that counts, and lies in no folder of the map a game reads.
		Write("map/_cache/quad_textures/quad_texture_1000.dds", "a cache's tile picture");
		Write("history/provinces/holland/1851 - Haarlem.txt.bak", "kept by a player");
		Assert.Equal(before, Label());
		Write("map/definition.csv", "read by the game");
		string withMap = Label();
		Assert.NotEqual(before, withMap);
		Write("map/terrain/colormap.dds", "the terrain's picture");
		Assert.NotEqual(withMap, Label());
	}

	[Fact]
	public void TheLabelCarriesThePlayChecksumAfterTheOriginalsLetters()
	{
		Assert.Equal("OpenHOI3 (GSKH) 3F9A-01C7", OpenHOI3.Interface.Screens.CVersionText.Label(1499173604, null, "3F9A-01C7"));
		Assert.Equal("OpenHOI3 (GSKH) 3F9A-01C7    Mod: X", OpenHOI3.Interface.Screens.CVersionText.Label(1499173604, "X", "3F9A-01C7"));
	}

	[Fact]
	public void TheSameFilesGiveTheSameFigureAndEveryFileIsCounted()
	{
		(byte[] hash, int files) = CPlayChecksum.Compute(
			CChecksumSearchPath.ForPlay(CDirectorySettings.At(_root, null, _user)), "normal-1");
		Assert.Equal(4, files);
		Assert.Equal(32, hash.Length);
		Assert.Equal(Label(), Label());
		Assert.Matches("^[0-9A-F]{4}-[0-9A-F]{4}$", Label());
	}

	[Fact]
	public void TwoFilesExchangedChangeIt()
	{
		string before = Label();
		Write("events/a.txt", "two");
		Write("events/b.txt", "one");
		Assert.NotEqual(before, Label());
	}

	[Fact]
	public void AnEmptyFileAddedChangesIt()
	{
		string before = Label();
		Write("events/c.txt", "");
		Assert.NotEqual(before, Label());
	}

	[Fact]
	public void AFileMovedToAnotherFolderChangesIt()
	{
		string before = Label();
		File.Delete(Path.Combine(_root, "events", "b.txt"));
		Write("decisions/b.txt", "two");
		Assert.NotEqual(before, Label());
	}

	[Fact]
	public void TheAisLuaIsPartOfIt()
	{
		string before = Label();
		Write("script/ai.lua", "return 2");
		Assert.NotEqual(before, Label());
	}

	[Fact]
	public void TheEngineAndTheRulesArePartOfIt()
	{
		string before = Label();
		Assert.NotEqual(before, Label(engine: "normal-2"));
		Assert.NotEqual(before, Label(rules: "no_payback"));
	}

	[Fact]
	public void WhatChangesNothingAGameDoesIsLeftOut()
	{
		string before = Label();
		Write("interface/topbar.gui", "guiTypes = {}");
		Write("localisation/text.csv", "KEY;Text;");
		Write("gfx/flags/GER.tga", "picture");
		Write("map/cache/time", "stamp");
		Write("events/.svn/entries", "x");
		// The player's own folder is no layer of game files here.
		Write("user/events/a.txt", "another");
		Assert.Equal(before, Label());
	}

	[Fact]
	public void ALayerInFrontStandsInForTheFileBehindIt()
	{
		string before = Label();
		Write("tfh/events/a.txt", "one of tfh");
		Assert.NotEqual(before, Label());
	}
}
