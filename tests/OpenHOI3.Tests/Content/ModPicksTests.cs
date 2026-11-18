using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Launch;
using OpenHOI3.Content.Mods;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// The launcher's list of mods and its ticks (<c>docs/launcher.md</c>, section 4): a dependency ticked with what needs
/// it, a dependent unticked with what it needs, a remembered choice made whole, and a refused descriptor listed with
/// its reason and never played.
/// </summary>
public sealed class ModPicksTests : IDisposable
{
	private readonly LauncherFixture _fixture = new();

	public ModPicksTests()
	{
		_fixture.Descriptor("Base");
		_fixture.Descriptor("Middle", "dependencies = { \"Base\" }");
		_fixture.Descriptor("Top", "dependencies = { \"Middle\" }");
		_fixture.Descriptor("Other");
	}

	public void Dispose() => _fixture.Dispose();

	private static CMod Mod(List<CMod> mods, string file) => mods.Single(mod => mod.VirtualPath == $"mod/{file}.mod");

	private static string[] Names(List<CMod> mods, IReadOnlySet<string> picks) =>
		CModPicks.Chosen(mods, picks).Select(mod => mod.Name).ToArray();

	[Fact]
	public void TickingAModTicksWhatItDependsOnThroughEveryStep()
	{
		List<CMod> mods = _fixture.Mods();
		HashSet<string> picks = CModPicks.Set(mods, Array.Empty<string>(), Mod(mods, "Top"), on: true);
		Assert.Equal(new[] { "Base", "Middle", "Top" }, Names(mods, picks));
	}

	[Fact]
	public void UntickingAModUnticksWhatDependsOnIt()
	{
		List<CMod> mods = _fixture.Mods();
		HashSet<string> picks = CModPicks.Set(mods, Array.Empty<string>(), Mod(mods, "Top"), on: true);
		picks = CModPicks.Set(mods, picks, Mod(mods, "Other"), on: true);
		picks = CModPicks.Set(mods, picks, Mod(mods, "Base"), on: false);
		Assert.Equal(new[] { "Other" }, Names(mods, picks));
	}

	[Fact]
	public void UntickingADependentLeavesWhatItNeeds()
	{
		List<CMod> mods = _fixture.Mods();
		HashSet<string> picks = CModPicks.Set(mods, Array.Empty<string>(), Mod(mods, "Top"), on: true);
		picks = CModPicks.Set(mods, picks, Mod(mods, "Top"), on: false);
		Assert.Equal(new[] { "Base", "Middle" }, Names(mods, picks));
	}

	[Fact]
	public void ARememberedChoiceIsMadeWholeAndWhatIsGoneIsDropped()
	{
		List<CMod> mods = _fixture.Mods();
		HashSet<string> picks = CModPicks.Restore(mods, new[] { "mod/Middle.mod", "mod/Gone.mod" });
		Assert.Equal(new[] { "Base", "Middle" }, Names(mods, picks));
	}

	[Fact]
	public void ADependencyNoDescriptorHasIsSaid()
	{
		_fixture.Descriptor("Lonely", "dependencies = { \"Nowhere\" \"Base\" }");
		List<CMod> mods = _fixture.Mods();
		Assert.Equal(new[] { "Nowhere" }, CModPicks.Missing(mods, Mod(mods, "Lonely")));
		Assert.Empty(CModPicks.Missing(mods, Mod(mods, "Top")));
	}

	[Fact]
	public void ARefusedDescriptorIsListedWithItsReasonAndNotPlayable()
	{
		System.IO.File.WriteAllText(System.IO.Path.Combine(_fixture.Game, "tfh", "mod", "Old.mod"), "name = \"Old\"\nextend = \"Old\"\n");
		CModEntry old = _fixture.Entries().Single(entry => entry.VirtualPath == "mod/Old.mod");
		Assert.Null(old.Mod);
		Assert.Contains("Not a mod Their Finest Hour plays", old.Refusal);
		Assert.Empty(old.Problems);
		Assert.DoesNotContain(_fixture.Mods(), mod => mod.Name == "Old");
	}

	[Fact]
	public void AKeyNoModFileHasIsLeftToTheLogAndAnotherProblemSaidOnce()
	{
		_fixture.Descriptor("Noisy", "extend = \"a\"", "extend = \"b\"", "archive = \"Noisy.zip\"");
		CModEntry noisy = _fixture.Entries().Single(entry => entry.VirtualPath == "mod/Noisy.mod");
		Assert.NotNull(noisy.Mod);
		Assert.Null(noisy.Refusal);
		Assert.Contains("is not read here", Assert.Single(noisy.Problems));
	}

	[Fact]
	public void OnlyTheModsOfTfhAreOffered()
	{
		// The install's own mod folder holds the base game's expansions, and the player's folder no mod of TFH's.
		System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_fixture.Game, "mod"));
		System.IO.File.WriteAllText(System.IO.Path.Combine(_fixture.Game, "mod", "Expansion.mod"), "name = \"Expansion\"\npath = \"mod/Expansion\"\n");
		System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_fixture.Documents, "mod"));
		System.IO.File.WriteAllText(System.IO.Path.Combine(_fixture.Documents, "mod", "Mine.mod"), "name = \"Mine\"\npath = \"tfh/mod/Base\"\n");
		List<CModEntry> entries = _fixture.Entries();
		Assert.Equal(new[] { "mod/Base.mod", "mod/Middle.mod", "mod/Other.mod", "mod/Top.mod" }, entries.Select(entry => entry.VirtualPath));
	}

	[Fact]
	public void OfTwoDescriptorsOfOneNameTheFirstIsPlayable()
	{
		System.IO.File.WriteAllText(System.IO.Path.Combine(_fixture.Game, "tfh", "mod", "Zcopy.mod"),
			"name = \"Base\"\npath = \"tfh/mod/Base\"\n");
		List<CModEntry> entries = _fixture.Entries();
		Assert.NotNull(entries.Single(entry => entry.VirtualPath == "mod/Base.mod").Mod);
		CModEntry copy = entries.Single(entry => entry.VirtualPath == "mod/Zcopy.mod");
		Assert.Null(copy.Mod);
		Assert.Contains("comes first", copy.Refusal);
	}
}
