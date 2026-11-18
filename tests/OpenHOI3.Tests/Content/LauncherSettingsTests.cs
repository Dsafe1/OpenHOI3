using System;
using System.IO;
using OpenHOI3.Content.Launch;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// What the launcher remembers and where it looks for an install (<c>docs/launcher.md</c>, section 6): its file read back
/// as written, a path's backslashes and a user name's letters with it; Steam's list of libraries; the places looked in.
/// </summary>
public sealed class LauncherSettingsTests : IDisposable
{
	private readonly LauncherFixture _fixture = new();

	public void Dispose() => _fixture.Dispose();

	[Fact]
	public void TheFileIsReadBackAsWritten()
	{
		string file = Path.Combine(_fixture.Root, "appdata", "OpenHOI3", "launcher.txt");
		var settings = new CLauncherSettings(@"C:\Users\Jürgen\Spiele\Hearts of Iron 3", new[] { "mod/BlackICE GitHub.mod", "mod/Sub-mod.mod" });
		CLauncherSettingsFile.Write(file, settings);
		CLauncherSettings read = CLauncherSettingsFile.Read(file);
		Assert.Equal(settings.Install, read.Install);
		Assert.Equal(settings.Mods, read.Mods);
	}

	[Fact]
	public void NoFileIsAFirstRun()
	{
		CLauncherSettings read = CLauncherSettingsFile.Read(Path.Combine(_fixture.Root, "none.txt"));
		Assert.Null(read.Install);
		Assert.Empty(read.Mods);
	}

	[Fact]
	public void SteamsLibrariesAreItsPathLines()
	{
		const string vdf = "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n"
			+ "\t\t\"label\"\t\t\"\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"D:\\\\SteamLibrary\"\n\t\t\"apps\"\n\t\t{\n\t\t\t\"25890\"\t\t\"1\"\n\t\t}\n\t}\n}\n";
		Assert.Equal(new[] { @"C:\Program Files (x86)\Steam", @"D:\SteamLibrary" }, CInstallSearch.LibraryPaths(vdf));
	}

	[Fact]
	public void TheRememberedInstallIsLookedInFirstAndTheFirstInstallTaken()
	{
		string launcher = Path.Combine(_fixture.Root, "launcher");
		Directory.CreateDirectory(launcher);
		var candidates = CInstallSearch.Candidates(Path.Combine(_fixture.Root, "gone"), launcher, new[] { _fixture.Root });
		Assert.Equal(Path.Combine(_fixture.Root, "gone"), candidates[0]);
		Assert.Equal(launcher, candidates[1]);
		Assert.Equal(Path.Combine(_fixture.Root, "steamapps", "common", CInstallSearch.SteamFolder), candidates[^1]);
		Assert.Null(CInstallSearch.Find(candidates));
		Assert.Equal(Path.GetFullPath(_fixture.Game), CInstallSearch.Find(new[] { Path.Combine(_fixture.Root, "gone"), _fixture.Game }));
	}
}
