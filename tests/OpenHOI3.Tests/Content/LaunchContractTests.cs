using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Launch;
using OpenHOI3.Content.Mods;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// The command line between the launcher and an engine (<c>docs/launcher.md</c>, section 5): everything after
/// <c>--</c>, the install named and checked, each mod by its virtual path - read back by the engine's own readings into
/// the same install and mods, a mod whose file name holds a '-' among them, which the original's own line cannot name.
/// </summary>
public sealed class LaunchContractTests : IDisposable
{
	private readonly LauncherFixture _fixture = new();

	public void Dispose() => _fixture.Dispose();

	[Fact]
	public void EverythingGoesAfterTheDoubleDash()
	{
		List<string> arguments = CLaunchContract.Arguments(@"C:\Games\Hearts of Iron 3", new[] { "mod/A.mod", "mod/B.mod" });
		Assert.Equal(new[] { "--", @"--install=C:\Games\Hearts of Iron 3", "--mod=mod/A.mod", "--mod=mod/B.mod" }, arguments);
	}

	[Fact]
	public void TheEngineReadsBackTheInstallAndTheMods()
	{
		_fixture.Descriptor("Black-ICE");
		_fixture.Descriptor("Plain");
		List<string> arguments = CLaunchContract.Arguments(_fixture.Game, new[] { "mod/Black-ICE.mod", "mod/Plain.mod" });
		string[] user = arguments.Skip(1).ToArray();

		string install = CLaunchContract.Install(user)!;
		Assert.NotNull(CDirectorySettings.Named(install));
		List<string> values = CModSwitch.DeveloperNames(user)
			.Select(name => CModReader.VirtualPathFor(install, _fixture.Documents, name)).ToList();
		var warnings = new List<string>();
		IReadOnlyList<CMod> mods = CModReader.Select(install, _fixture.Documents, values, warnings.Add);
		Assert.Empty(warnings);
		Assert.Equal(new[] { "Black-ICE", "Plain" }, mods.Select(mod => mod.Name).OrderBy(name => name, StringComparer.Ordinal));
	}

	[Fact]
	public void TheInstallSwitchTakesQuotesOffAndTheLastOneCounts()
	{
		Assert.Equal(@"D:\HOI3", CLaunchContract.Install(new[] { "--install=C:\\x", "--install=\"D:\\HOI3\"" }));
		Assert.Null(CLaunchContract.Install(new[] { "--install=", "--mod=x" }));
		Assert.True(CLaunchContract.Is(" --install=C:\\x"));
		Assert.False(CLaunchContract.Is("--mod=x"));
	}

	[Fact]
	public void AFolderThatIsNoInstallIsNotTaken()
	{
		Assert.Null(CDirectorySettings.Named(_fixture.Root));
		Assert.NotNull(CDirectorySettings.Named(_fixture.Game));
	}

	[Fact]
	public void TheEngineFileWrittenIsTheEngineRead()
	{
		string folder = _fixture.Engine(System.IO.Path.Combine(_fixture.Root, "engine"), "normal-2026.11.18-abc123+");
		CEngine engine = CEngineFile.Read(folder)!;
		Assert.Equal("normal-2026.11.18-abc123+", engine.Version);
		Assert.Equal(CLaunchContract.Version, engine.Contract);
		Assert.EndsWith("OpenHOI3.exe", engine.Executable);
	}

	[Fact]
	public void ALaunchersOwnEngineIsInItsFolderOrBesideItAsAModShipsThem()
	{
		// A mod's folder with its launcher and its engine side by side: the launcher's own engine is the mod's.
		string mod = System.IO.Path.Combine(_fixture.Root, "shipped");
		string launcher = System.IO.Path.Combine(mod, CEngineFile.LauncherFolderName);
		System.IO.Directory.CreateDirectory(launcher);
		string inside = System.IO.Path.Combine(launcher, CEngineFile.FolderName);
		// With neither, the folder inside is named, for what is said of it.
		Assert.Equal(inside, CEngineFile.OwnFolder(launcher));

		string beside = _fixture.Engine(System.IO.Path.Combine(mod, CEngineFile.FolderName), "openhoi3-1.0");
		Assert.Equal(beside, CEngineFile.OwnFolder(launcher));
		Assert.Equal(beside, CEngineFile.OwnFolder(launcher + System.IO.Path.DirectorySeparatorChar));

		// One of its own inside comes first.
		_fixture.Engine(inside, "openhoi3-2.0");
		Assert.Equal(inside, CEngineFile.OwnFolder(launcher));
	}
}
