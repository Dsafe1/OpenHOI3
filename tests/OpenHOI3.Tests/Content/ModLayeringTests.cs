using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Checksum;
using OpenHOI3.Content.Mods;
using OpenHOI3.Content.Settings;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// Mods laid over a fixture install as the original lays them: a descriptor found and matched as <c>-mod=</c> names it,
/// its folder in front of <c>tfh/</c> and the root, a folder its <c>replace_path</c> names listing only what the mod or
/// one in front of it holds, and its player folder below the Documents folder's (<c>docs/mechanics/mods.md</c>, section 2).
/// </summary>
public class ModLayeringTests : IDisposable
{
	private readonly string _root;
	private readonly string _documents;

	public ModLayeringTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "openhoi3-mods-" + Guid.NewGuid().ToString("N"));
		_documents = Path.Combine(_root, "Documents", "Hearts of Iron III");
		Directory.CreateDirectory(Path.Combine(_root, "game", "interface"));
		Directory.CreateDirectory(Path.Combine(_root, "game", "gfx"));
		Directory.CreateDirectory(Path.Combine(_root, "game", "localisation"));
		Directory.CreateDirectory(_documents);
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

	private string Game => Path.Combine(_root, "game");

	private void Write(string relative, string content)
	{
		string path = Path.Combine(Game, relative.Replace('/', Path.DirectorySeparatorChar));
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, content);
	}

	/// <summary>A mod descriptor tfh/mod/&lt;file&gt;.mod for a folder tfh/mod/&lt;file&gt;, with the lines given after its name and path.</summary>
	private void Descriptor(string file, params string[] lines)
	{
		Write($"tfh/mod/{file}.mod", $"name = \"{file}\"\npath = \"tfh/mod/{file}\"\n" + string.Join("\n", lines) + "\n");
		Directory.CreateDirectory(Path.Combine(Game, "tfh", "mod", file));
	}

	private CDirectorySettings Select(params string[] values)
	{
		var warnings = new List<string>();
		IReadOnlyList<CMod> mods = CModReader.Select(Game, _documents, values, warnings.Add);
		Assert.Empty(warnings);
		return CDirectorySettings.At(Game, mods, _documents);
	}

	/// <summary>A mod "Test" with a player folder "TestUser" and the lines given, selected.</summary>
	private CDirectorySettings WithMod(params string[] lines)
	{
		Descriptor("Test", new[] { "user_dir = \"TestUser\"" }.Concat(lines).ToArray());
		return Select("mod/Test.mod");
	}

	private CDirectorySettings Plain => CDirectorySettings.At(Game, null, _documents);

	[Fact]
	public void TheModFileIsReadAsTheOriginalReadsIt()
	{
		Write("tfh/mod/Test.mod", "name = \"Test Mod\"\npath = \"tfh/mod/Test\"\nuser_dir = \"Old\"\nuser_dir = \"TestUser\"\n"
			+ "replace_path = \"history\\provinces\\africa\"\nreplace_path = \"events/\"\ndependencies = { \"Base Mod\" \"Other\" }\n"
			+ "extend = \"x\"\nsteam_id = \"1\"\n");
		var warnings = new List<string>();

		CMod mod = CModReader.Read(Game, Path.Combine(Game, "tfh", "mod", "Test.mod"), "mod/Test.mod", warnings.Add)!;

		Assert.Equal("Test Mod", mod.Name);
		Assert.Equal(Path.Combine(Game, "tfh", "mod", "Test"), mod.Folder);
		// A key given twice keeps its last; replace_path adds each, '\' made '/' and nothing else changed.
		Assert.Equal("TestUser", mod.UserDir);
		Assert.Equal(new[] { "history/provinces/africa", "events/" }, mod.ReplacePaths);
		Assert.Equal(new[] { "Base Mod", "Other" }, mod.Dependencies);
		// The old 'extend' is no key of the original's, which drops it: here it is reported.
		Assert.Contains(warnings, w => w.Contains("'extend' is not a key"));
		Assert.DoesNotContain(warnings, w => w.Contains("'steam_id' is not"));
	}

	[Fact]
	public void ADescriptorWithNoPathIsRefused()
	{
		Write("tfh/mod/Old.mod", "name = \"Old\"\nextend = \"Old\"\nreplace = \"events\"\n");
		var warnings = new List<string>();

		Assert.Null(CModReader.Read(Game, Path.Combine(Game, "tfh", "mod", "Old.mod"), "mod/Old.mod", warnings.Add));
		Assert.Contains(warnings, w => w.StartsWith("Incorrect MOD descriptor: \"Old.mod\""));
		Assert.Empty(CModReader.Select(Game, _documents, new[] { "mod/Old.mod" }, _ => { }));
	}

	[Fact]
	public void DescriptorsAreListedFromThePlayersFolderTfhAndTheInstallTheFirstCopyWinning()
	{
		Write("mod/Root.mod", "");
		Write("mod/Both.mod", "root");
		Write("tfh/mod/Both.mod", "tfh");
		File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(_documents, "mod")).FullName, "Mine.mod"), "");

		List<(string VirtualPath, string File)> found = CModReader.Descriptors(Game, _documents);

		Assert.Equal(new[] { "mod/Mine.mod", "mod/Both.mod", "mod/Root.mod" }, found.Select(d => d.VirtualPath));
		Assert.Equal("tfh", File.ReadAllText(found[1].File));
	}

	[Fact]
	public void AValueIsMatchedByteForByteAndOneThatMatchesNothingPlaysTheInstall()
	{
		Descriptor("Test");
		var warnings = new List<string>();

		Assert.Single(CModReader.Select(Game, _documents, new[] { "mod/Test.mod" }, warnings.Add));
		Assert.Empty(CModReader.Select(Game, _documents, new[] { "mod/test.mod", "tfh/mod/Test.mod", "Test" }, warnings.Add));
		Assert.Equal(3, warnings.Count);
		Assert.Contains("'mod/Test.mod'", warnings[0]);
	}

	[Fact]
	public void TheDevelopersNameMeansTheDescriptorOfThatFile()
	{
		Descriptor("BlackICE GitHub");

		Assert.Equal("mod/BlackICE GitHub.mod", CModReader.VirtualPathFor(Game, _documents, "blackice github"));
		Assert.Equal("mod/BlackICE GitHub.mod", CModReader.VirtualPathFor(Game, _documents, "BlackICE GitHub.mod"));
		Assert.Equal("mod/Other.mod", CModReader.VirtualPathFor(Game, _documents, "Other"));
		Assert.NotNull(Plain.WithMod("BlackICE GitHub", _ => { }));
		Assert.Null(Plain.WithMod("Missing", _ => { }));
	}

	[Fact]
	public void WithoutAModTheLayersAreTheExpansionAndTheRoot()
	{
		Assert.Empty(Plain.Mods);
		Assert.Equal(new[] { Path.Combine(Game, "tfh"), Game }, Plain.Layers);
		Assert.Equal(_documents, Plain.UserFolder);
	}

	[Fact]
	public void TheModsCopyWinsOverTheExpansionAndTheBase()
	{
		Write("interface/core.gfx", "base");
		Write("tfh/interface/core.gfx", "expansion");
		Write("tfh/mod/Test/interface/core.gfx", "mod");
		Write("interface/only_base.gfx", "base only");
		CDirectorySettings directories = WithMod();

		Assert.Equal("mod", File.ReadAllText(directories.Resolve("interface\\core.gfx")));
		Assert.Equal("base only", File.ReadAllText(directories.Resolve("interface/only_base.gfx")));
		// Listed once each, the mod's copy, in the order of the names and not of the layers.
		Assert.Equal(new[] { "mod", "base only" }, directories.ListFiles("interface", "*.gfx").Select(File.ReadAllText));
	}

	[Fact]
	public void ATextureIsLookedForInTheModFirst()
	{
		Write("gfx/interface/button.dds", "base");
		Write("tfh/mod/Test/gfx/interface/button.dds", "mod");
		CDirectorySettings directories = WithMod();

		Assert.Equal("mod", File.ReadAllText(directories.ResolveTexture("gfx\\interface\\button.tga")!));
	}

	[Fact]
	public void AReplacedFolderListsOnlyTheModsFiles()
	{
		Write("events/base.txt", "base");
		Write("events/shared.txt", "base");
		Write("tfh/events/expansion.txt", "expansion");
		Write("tfh/mod/Test/events/mod.txt", "mod");
		Write("tfh/mod/Test/events/shared.txt", "mod");
		Write("decisions/kept.txt", "base");
		CDirectorySettings directories = WithMod("replace_path = \"events\"");

		Assert.Equal(new[] { "mod.txt", "shared.txt" }, directories.ListFiles("events", "*.txt").Select(Path.GetFileName));
		// A folder it does not name is laid over as before.
		Assert.Equal(new[] { "kept.txt" }, directories.ListFiles("decisions", "*.txt").Select(Path.GetFileName));
		Assert.True(directories.IsReplaced("events"));
		Assert.False(Plain.IsReplaced("events"));
	}

	[Fact]
	public void AFileOpenedByItsNameInAReplacedFolderIsStillFoundInTheInstall()
	{
		Write("common/only_base.txt", "base");
		CDirectorySettings directories = WithMod("replace_path = \"common\"");

		Assert.Empty(directories.ListFiles("common", "*.txt"));
		Assert.Equal("base", File.ReadAllText(directories.Resolve("common/only_base.txt")));
	}

	[Fact]
	public void AReplacedFolderIsMatchedExactlyItsTrailingSlashAndCaseWithIt()
	{
		Write("events/base.txt", "base");
		Write("decisions/base.txt", "base");
		Write("history/provinces/Mongolia/5 - Urga.txt", "base");
		Write("history/provinces/china/6 - Peking.txt", "base");
		CDirectorySettings directories = WithMod("replace_path = \"events/\"", "replace_path = \"Decisions\"",
			"replace_path = \"history\\provinces\\Mongolia\"", "replace_path = \"history\\provinces\\China\"");

		Assert.Single(directories.ListFiles("events", "*.txt"));
		Assert.Single(directories.ListFiles("decisions", "*.txt"));
		// "Mongolia" as the folder is written; "China" is not "china", so that folder is still the base game's.
		Assert.Equal(new[] { "china/6 - Peking.txt" },
			directories.ListFilesRecursive("history/provinces", "*.txt").Select(f => f.RelativePath));
	}

	[Fact]
	public void ReplacingAFolderDoesNotReplaceTheFoldersInsideIt()
	{
		Write("history/provinces/1 - Top.txt", "base");
		Write("history/provinces/asia/2 - Asia.txt", "base");
		Write("history/provinces/soviet/3 - Soviet.txt", "base");
		Write("tfh/mod/Test/history/provinces/bice_x/4 - Mod.txt", "mod");
		CDirectorySettings directories = WithMod("replace_path = \"history\\provinces\"",
			"replace_path = \"history\\provinces\\asia\"");

		Assert.Equal(new[] { "bice_x/4 - Mod.txt", "soviet/3 - Soviet.txt" },
			directories.ListFilesRecursive("history/provinces", "*.txt").Select(f => f.RelativePath));
	}

	[Fact]
	public void AModWhosePathHasABackslashReplacesNothing()
	{
		Write("events/base.txt", "base");
		Write("tfh/mod/Test.mod", "name = \"Test\"\npath = \"tfh\\mod\\Test\"\nreplace_path = \"events\"\n");
		Directory.CreateDirectory(Path.Combine(Game, "tfh", "mod", "Test"));
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(Game, CModReader.Select(Game, _documents, new[] { "mod/Test.mod" }, warnings.Add), _documents);

		Assert.Equal(CModRules.BackslashedPathDisablesReplace, directories.ListFiles("events", "*.txt").Length == 1);
		Assert.Contains(warnings, w => w.Contains("backslash"));
	}

	[Fact]
	public void SeveralModsAreOrderedByTheirDependenciesThenByName()
	{
		Descriptor("Zed", "user_dir = \"z\"");
		Descriptor("Alpha", "user_dir = \"a\"", "dependencies = { \"Base\" }");
		Descriptor("Base", "user_dir = \"b\"");
		Write("tfh/mod/Base/events/base_mod.txt", "base mod");
		Write("tfh/mod/Alpha/events/alpha.txt", "alpha");
		Write("tfh/mod/Zed/decisions/zed.txt", "zed");

		CDirectorySettings directories = Select("mod/Zed.mod", "mod/Base.mod", "mod/Alpha.mod");

		// Base is raised above Alpha, its dependent, so it is mounted first and stands at the back; Alpha and Zed, at -1000,
		// are mounted greater name first, so Alpha ends in front.
		Assert.Equal(new[] { "Alpha", "Zed", "Base" }, directories.Mods.Select(m => m.Name));
		Assert.Equal(Path.Combine(_documents, "b_z_a"), directories.UserFolder);
		Assert.Equal(Path.Combine(Game, "tfh", "mod", "Alpha"), directories.Layers[0]);
	}

	[Fact]
	public void AFolderTheFrontModReplacesHidesTheModsBehindIt()
	{
		Descriptor("Alpha", "replace_path = \"events\"");
		Descriptor("Beta", "replace_path = \"decisions\"");
		Write("tfh/mod/Alpha/events/alpha.txt", "alpha");
		Write("tfh/mod/Beta/events/beta.txt", "beta");
		Write("tfh/mod/Alpha/decisions/alpha.txt", "alpha");
		Write("tfh/mod/Beta/decisions/beta.txt", "beta");
		Write("decisions/base.txt", "base");

		CDirectorySettings directories = Select("mod/Alpha.mod", "mod/Beta.mod");

		Assert.Equal(new[] { "alpha.txt" }, directories.ListFiles("events", "*.txt").Select(Path.GetFileName));
		// Beta replaces decisions: what is in it or in front of it - Alpha - stays.
		Assert.Equal(new[] { "alpha.txt", "beta.txt" }, directories.ListFiles("decisions", "*.txt").Select(Path.GetFileName));
	}

	[Fact]
	public void TheModsPlayerFolderIsBelowTheBaseGames()
	{
		CDirectorySettings directories = WithMod();

		Assert.Equal(Path.Combine(_documents, "TestUser"), directories.UserFolder);
		Assert.Equal(_documents, directories.DocumentsFolder);
		Assert.Equal(Path.Combine(_documents, "TestUser", "settings.txt"), CUserSettingsReader.DefaultPath(directories));
		Assert.Equal(Path.Combine(_documents, "settings.txt"), CUserSettingsReader.DefaultPath(Plain));
		Assert.Equal(Path.Combine(_documents, "TestUser", "save games"), directories.UserAndLayerFolders("save games").First());
		// The player's folder is not a layer of game files unless CModRules says so (docs/compatibility.md, entry 28).
		Assert.Equal(CModRules.UserFolderIsLayer, directories.Layers.Contains(directories.UserFolder));
	}

	[Fact]
	public void TheSearchPathIsThePlayersFolderTheModThenTheInstall()
	{
		CDirectorySettings directories = WithMod("replace_path = \"events\"");

		(string[] mounts, IReadOnlyDictionary<string, int> limits) = directories.SearchPath();

		// With a mod the base game's Documents folder is not mounted at all.
		Assert.Equal(new[] { Path.Combine(_documents, "TestUser"), Path.Combine(Game, "tfh", "mod", "Test"), Path.Combine(Game, "tfh"), Game },
			mounts);
		Assert.Equal(1, limits["events"]);
		(string[] plain, IReadOnlyDictionary<string, int> none) = Plain.SearchPath();
		Assert.Equal(new[] { _documents, Path.Combine(Game, "tfh"), Game }, plain);
		Assert.Empty(none);
	}

	[Fact]
	public void TheChecksumListsAReplacedFolderFromTheModsMountsAlone()
	{
		Write("events/base.txt", "base");
		Write("tfh/mod/Test/events/mod.txt", "mod");
		CDirectorySettings directories = WithMod("replace_path = \"events\"");

		CChecksumSearchPath search = CChecksumSearchPath.For(directories);

		Assert.True(search.MayList("events", "events/mod.txt"));
		Assert.False(search.MayList("events", "events/base.txt"));
	}

	[Fact]
	public void TheCommandLineIsCutAtEveryDashAsTheOriginalCutsIt()
	{
		(List<string> mods, string? userDir) = CModSwitch.Parse("ignored -mod=mod/BlackICE GitHub.mod  -exclude_dlc=x -Mod=mod/No.mod "
			+ "-mod= -mod=\"mod/Quoted.mod\" -userdir=D:\\Games\\HoI -mod=mod/My-Mod.mod");

		// Quotes are kept, the prefix is matched with case, an empty value is passed over, and a '-' cuts a value.
		Assert.Equal(new[] { "mod/BlackICE GitHub.mod", "\"mod/Quoted.mod\"", "mod/My" }, mods);
		Assert.Equal("D:\\Games\\HoI", userDir);
		Assert.Empty(CModSwitch.Parse("mod=mod/First.mod").Mods);
	}

	[Fact]
	public void TheVersionLabelNamesTheModPlayed()
	{
		Assert.Equal("OpenHOI3 (OXLS)    Mod: BlackICE GitHub", OpenHOI3.Interface.Screens.CVersionText.Label(1599539514, "BlackICE GitHub"));
		Assert.Equal("OpenHOI3 (GSKH)", OpenHOI3.Interface.Screens.CVersionText.Label(1499173604));
	}

	[Fact]
	public void AFolderIsListedByItsNamesSortedByByteWithCase()
	{
		Write("events/b.txt", "base");
		Write("events/Zulu.txt", "base");
		Write("tfh/events/a.txt", "expansion");
		Write("tfh/mod/Test/events/c.txt", "mod");
		Write("tfh/mod/Test/events/B.txt", "mod");
		Write("events/upper.TXT", "base");
		Write("events/café.txt", "base");

		CDirectorySettings directories = WithMod();

		// Not the base layer first: every name over the layers once, upper case before lower, as strcmp sorts them. A tail
		// in another case is no match, and a name with a character past 0x7F is left out, both as the original's lister.
		// b.txt and B.txt are two names, and the file system opens both from the frontmost copy, the mod's B.txt.
		string[] listed = directories.ListFiles("events", "*.txt");
		Assert.Equal(new[] { "B.txt", "Zulu.txt", "a.txt", "B.txt", "c.txt" }, listed.Select(Path.GetFileName));
		Assert.Equal(new[] { "mod", "base", "expansion", "mod", "mod" }, listed.Select(File.ReadAllText));
	}

	[Fact]
	public void TheHistoryIsListedASubfolderAtATimeInTheOrderOfTheirNames()
	{
		Write("history/provinces/africa/2 - B.txt", "base");
		Write("history/provinces/Mongolia/1 - A.txt", "base");
		Write("history/provinces/asia-x/3 - C.txt", "base");
		Write("history/provinces/asia/4 - D.txt", "base");
		Write("tfh/history/provinces/asia/4 - D.txt", "expansion");

		CDirectorySettings.LayeredFile[] files = Plain.ListFilesRecursive("history/provinces", "*.txt");

		// The subfolders by byte - Mongolia before africa, asia before asia-x - and each folder's files after it.
		Assert.Equal(new[] { "Mongolia/1 - A.txt", "africa/2 - B.txt", "asia/4 - D.txt", "asia-x/3 - C.txt" },
			files.Select(f => f.RelativePath));
		Assert.Equal("expansion", File.ReadAllText(files[2].AbsolutePath));
	}

	[Fact]
	public void TheDeveloperSwitchGivesNames()
	{
		Assert.Equal(new[] { "BlackICE GitHub", "B" }, CModSwitch.DeveloperNames(new[] { "--mod=BlackICE GitHub", "--country=GER", "--mod=", "--mod=B" }));
		Assert.True(CModSwitch.Is("--mod=A"));
		Assert.False(CModSwitch.Is("--mode=terrain"));
	}
}
