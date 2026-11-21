using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Launch;
using OpenHOI3.Content.Rules;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Rules;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// How a mod's data turns the enhanced mode on, against <c>docs/enhanced.md</c>, <i>The shape</i>: the file's being
/// there, the fixes it goes without, what else it says reported; the folder an enhanced game's saves go to; and the
/// two keys of ours an enhanced save holds, which a faithful one does not.
/// </summary>
public class RulesReaderTests : IDisposable
{
	private readonly string _root = Path.Combine(Path.GetTempPath(), "openhoi3-rules-" + Guid.NewGuid().ToString("N"));

	public RulesReaderTests()
	{
		Directory.CreateDirectory(Path.Combine(_root, "common"));
		Directory.CreateDirectory(Path.Combine(_root, "tfh", "common"));
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

	private void WriteFile(string text)
	{
		File.WriteAllText(Path.Combine(_root, "tfh", "common", "openhoi_enhanced.txt"), text);
	}

	private static CGameState State()
	{
		return new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", false) }, Array.Empty<CCountry>());
	}

	[Fact]
	public void WithNoFileAGameIsTheFaithfulOneAndItsSavesTheOriginalsFolder()
	{
		Assert.False(CRulesReader.IsEnhanced(Settings));
		Assert.False(CRulesReader.Read(Settings).Enhanced);
		Assert.Equal("save games", CSaveFiles.FolderFor(Settings));
	}

	[Fact]
	public void TheFileBeingThereMakesAnEnhancedGameWithEveryFixAndItsSavesAFolderOfTheirOwn()
	{
		WriteFile("# nothing but a comment\n");
		Assert.True(CRulesReader.IsEnhanced(Settings));
		CRules rules = CRulesReader.Read(Settings);
		Assert.True(rules.Enhanced);
		Assert.True(rules.Has(EFix.MinisterTechDecay));
		Assert.Equal("save games/openhoi", CSaveFiles.FolderFor(Settings));
	}

	[Fact]
	public void TheFixesAModDoesNotWantAreNamedAndWhatElseItSaysIsReported()
	{
		var warnings = new List<string>();
		CRules rules = CRulesReader.Parse(
			"without = { minister_tech_decay no_such_fix }\nwith = { minister_tech_decay }\nwithout = yes\n", "test", warnings.Add);
		Assert.True(rules.Enhanced);
		Assert.False(rules.Has(EFix.MinisterTechDecay));
		Assert.Equal(3, warnings.Count);
		Assert.Contains("no_such_fix", warnings[0]);
		Assert.Contains("with", warnings[1]);
		Assert.Contains("without", warnings[2]);
	}

	[Fact]
	public void AnEnhancedSaveHoldsItsRulesAndItsEngineAndAFaithfulOneNeither()
	{
		CGameState faithful = State();
		var writer = new CSaveTextWriter();
		CSaveTop.WriteRules(writer, faithful);
		Assert.Equal("", writer.Text);

		CGameState enhanced = State();
		enhanced.Rules.CopyFrom(new CRules(new[] { EFix.MinisterTechDecay }));
		enhanced.EngineVersion = "openhoi3-test";
		CSaveTop.WriteRules(writer, enhanced);
		Assert.Contains("openhoi_rules=\"enhanced without minister_tech_decay\"", writer.Text);
		Assert.Contains("openhoi_engine=\"openhoi3-test\"", writer.Text);

		// Read into a game the folders made faithful, the save's rules stand.
		CGameState loaded = State();
		var report = new CSaveReport();
		foreach (ClausewitzNode node in ClausewitzParser.Parse(writer.Text).Children)
		{
			Assert.True(CSaveTop.Read(loaded, node, report));
		}
		Assert.Equal(enhanced.Rules.Text, loaded.Rules.Text);
		Assert.Equal("openhoi3-test", loaded.SavedByEngine);
		Assert.Empty(report.UnreadKeys);
	}

	[Fact]
	public void AnEnginesVersionIsReadFromTheFileBesideItsProgram()
	{
		string folder = Path.Combine(_root, "engine");
		Directory.CreateDirectory(folder);
		string program = Path.Combine(folder, "OpenHOI3.exe");
		Assert.Equal("", CEngineFile.VersionBeside(program));
		File.WriteAllText(program, "");
		File.WriteAllText(Path.Combine(folder, CEngineFile.FileName), CEngineFile.Text("openhoi3-test", "OpenHOI3.exe", 1));
		Assert.Equal("openhoi3-test", CEngineFile.VersionBeside(program));
	}
}
