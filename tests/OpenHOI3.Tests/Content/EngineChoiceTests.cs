using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Launch;
using OpenHOI3.Content.Mods;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// Which engine a choice of mods is played on (<c>docs/launcher.md</c>, sections 2 to 4): the launcher's own for the base
/// game and for every mod naming none, the one a mod ships where it names one, a submod taking its dependency's, two
/// different ones refused - and what an engine's <c>engine.txt</c> must say.
/// </summary>
public sealed class EngineChoiceTests : IDisposable
{
	private readonly LauncherFixture _fixture = new();

	public void Dispose() => _fixture.Dispose();

	[Fact]
	public void TheBaseGameIsPlayedOnTheLaunchersOwnEngine()
	{
		CEngine own = _fixture.Own();
		CEngineChoiceResult choice = CEngineChoice.Choose(new List<CMod>(), own);
		Assert.Same(own, choice.Engine);
		Assert.Null(choice.Problem);
	}

	[Fact]
	public void AModNamingNoEngineIsPlayedOnTheLaunchersOwn()
	{
		_fixture.Descriptor("Plain");
		CEngine own = _fixture.Own();
		Assert.Same(own, CEngineChoice.Choose(_fixture.Chosen("Plain"), own).Engine);
	}

	[Fact]
	public void ALauncherWithoutAnEngineOfItsOwnSaysSo()
	{
		CEngineChoiceResult choice = CEngineChoice.Choose(new List<CMod>(), own: null);
		Assert.Null(choice.Engine);
		Assert.Contains("no engine of its own", choice.Problem);
	}

	[Fact]
	public void TheEngineKeyIsReadAndNotReported()
	{
		_fixture.Descriptor("Enhanced", "engine = \"enhanced-1.0\"");
		CModEntry entry = Assert.Single(_fixture.Entries());
		Assert.Empty(entry.Problems);
		Assert.Equal("enhanced-1.0", entry.Mod!.Engine);
	}

	[Fact]
	public void AModsEngineIsFoundInItsFolder()
	{
		_fixture.Descriptor("Enhanced", "engine = \"enhanced-1.0\"");
		string folder = _fixture.ModEngine("Enhanced", "enhanced-1.0");
		CEngineChoiceResult choice = CEngineChoice.Choose(_fixture.Chosen("Enhanced"), _fixture.Own());
		Assert.Equal(Path.GetFullPath(folder), choice.Engine!.Folder);
		Assert.Equal(Path.Combine(Path.GetFullPath(folder), "OpenHOI3.exe"), choice.Engine.Executable);
	}

	[Fact]
	public void ASubmodIsPlayedOnTheEngineItsDependencyShips()
	{
		_fixture.Descriptor("Enhanced", "engine = \"enhanced-1.0\"");
		_fixture.ModEngine("Enhanced", "enhanced-1.0");
		_fixture.Descriptor("Submod", "dependencies = { \"Enhanced\" }");
		List<CMod> chosen = _fixture.Chosen("Submod");
		Assert.Equal(2, chosen.Count);
		Assert.Equal("enhanced-1.0", CEngineChoice.Choose(chosen, _fixture.Own()).Engine!.Version);
	}

	[Fact]
	public void ModsMadeForTwoEnginesCannotBePlayedTogether()
	{
		_fixture.Descriptor("One", "engine = \"enhanced-1.0\"");
		_fixture.ModEngine("One", "enhanced-1.0");
		_fixture.Descriptor("Two", "engine = \"enhanced-2.0\"");
		_fixture.ModEngine("Two", "enhanced-2.0");
		CEngineChoiceResult choice = CEngineChoice.Choose(_fixture.Chosen("One", "Two"), _fixture.Own());
		Assert.Null(choice.Engine);
		Assert.Contains("different engines", choice.Problem);
	}

	[Fact]
	public void AnEngineOfAnotherVersionInTheModsFolderIsNotTaken()
	{
		_fixture.Descriptor("Enhanced", "engine = \"enhanced-1.0\"");
		_fixture.ModEngine("Enhanced", "enhanced-0.9");
		CEngineChoiceResult choice = CEngineChoice.Choose(_fixture.Chosen("Enhanced"), _fixture.Own());
		Assert.Null(choice.Engine);
		Assert.Contains("wants engine enhanced-1.0", choice.Problem);
	}

	[Fact]
	public void TheLaunchersOwnEngineServesAModNamingItsVersion()
	{
		_fixture.Descriptor("Pinned", "engine = \"normal-7\"");
		CEngine own = _fixture.Own("normal-7");
		Assert.Same(own, CEngineChoice.Choose(_fixture.Chosen("Pinned"), own).Engine);
	}

	[Fact]
	public void AnEngineOfAnotherContractIsRefused()
	{
		_fixture.Descriptor("Enhanced", "engine = \"enhanced-1.0\"");
		_fixture.ModEngine("Enhanced", "enhanced-1.0", contract: CLaunchContract.Version + 1);
		CEngineChoiceResult choice = CEngineChoice.Choose(_fixture.Chosen("Enhanced"), _fixture.Own());
		Assert.Null(choice.Engine);
		Assert.Contains("command line of version", choice.Problem);
	}

	[Fact]
	public void AnEngineFileMustNameAProgramThatIsThere()
	{
		string folder = Path.Combine(_fixture.Root, "broken");
		_fixture.Engine(folder, "normal-1");
		File.Delete(Path.Combine(folder, "OpenHOI3.exe"));
		var problems = new List<string>();
		Assert.Null(CEngineFile.Read(folder, problems.Add));
		Assert.Contains("not there", Assert.Single(problems));
	}

	[Fact]
	public void AFolderWithNoEngineFileHasNoEngine()
	{
		var problems = new List<string>();
		Assert.Null(CEngineFile.Read(Path.Combine(_fixture.Root, "nothing"), problems.Add));
		Assert.Contains(CEngineFile.FileName, Assert.Single(problems));
	}
}
