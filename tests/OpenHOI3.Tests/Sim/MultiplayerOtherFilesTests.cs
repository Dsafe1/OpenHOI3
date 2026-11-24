using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Checksum;
using OpenHOI3.Content.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim.Multiplayer;
using OpenHOI3.Sim.Settings;
using Xunit;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The host's rule on other files (<c>docs/multiplayer.md</c>, section 15): a machine whose play checksum is not the
/// host's let in by its base, the host's list of files sent before its answer, and the two lists set beside each other.
/// </summary>
public class MultiplayerOtherFilesTests
{
	private static readonly string[] Host =
	{
		"format\tOpenHOI3 play checksum 2", "engine\t1.2", "rules\t",
		"common/defines.lua\t100\tAAAA", "events/a.txt\t20\tBBBB", "history/x.txt\t5\tCCCC", "checksum\tFFFF",
	};

	private static readonly string[] Here =
	{
		"format\tOpenHOI3 play checksum 2", "engine\t1.2", "rules\t",
		"common/defines.lua\t101\tAAAB", "events/a.txt\t20\tBBBB", "units/new.txt\t7\tDDDD", "checksum\tEEEE",
	};

	[Fact]
	public void ATokenIsThePlayChecksumAndItsBaseAfterASlash()
	{
		Assert.Equal("AB/CD", CPlayToken.Of("AB", "CD"));
		Assert.Equal("AB", CPlayToken.Of("AB", ""));
		Assert.Equal("AB", CPlayToken.Play("AB/CD"));
		Assert.Equal("CD", CPlayToken.Base("AB/CD"));
		Assert.Equal("AB", CPlayToken.Play("AB"));
		Assert.Equal("", CPlayToken.Base("AB"));
	}

	[Fact]
	public void AMachineOfOtherFilesIsLetInOnlyByTheRuleAndOnlyWhereItsBaseIsTheHosts()
	{
		var told = new List<string>();
		// The same play checksum: let in as ever, nothing sent before.
		CAdmission same = CHostSession.Admit(CClientSession.JoinLine("AAAA/B1", "Ann"), "AAAA/B1", told.Add, true, Host);
		Assert.Equal(new CAdmission("Ann", false), same);
		Assert.Equal(new[] { "y " }, told);

		// Another, the rule off: the host's list first, then refused with the host's token.
		told.Clear();
		CAdmission refused = CHostSession.Admit(CClientSession.JoinLine("CCCC/B1", "Ann"), "AAAA/B1", told.Add, false, Host);
		Assert.Null(refused.Name);
		Assert.Equal("n AAAA/B1", told[^1]);
		Assert.All(told.Take(told.Count - 1), line => Assert.StartsWith("f ", line));
		Assert.Equal(Host, CFileListWire.Read(told.Take(told.Count - 1).Select(line => line[2..])));

		// The rule on, the base the host's: the list, and let in with the word that says so.
		told.Clear();
		CAdmission let = CHostSession.Admit(CClientSession.JoinLine("CCCC/B1", "Ann Lee"), "AAAA/B1", told.Add, true, Host);
		Assert.Equal(new CAdmission("Ann Lee", true), let);
		Assert.Equal("y files", told[^1]);
		Assert.StartsWith("f ", told[0]);

		// The rule on, but another engine, rules or original checksum - or a machine that names no base: refused.
		told.Clear();
		Assert.Null(CHostSession.Admit(CClientSession.JoinLine("CCCC/B2", "Ann"), "AAAA/B1", told.Add, true, null).Name);
		Assert.Equal(new[] { "n AAAA/B1" }, told);
		told.Clear();
		Assert.Null(CHostSession.Admit(CClientSession.JoinLine("CCCC", "Ann"), "AAAA/B1", told.Add, true, null).Name);
		Assert.Null(CHostSession.Admit(CClientSession.JoinLine("CCCC", "Ann"), "AAAA", told.Add, true, null).Name);
	}

	[Fact]
	public void TwoListsSetBesideEachOtherSayWhatDiffersIsHereAloneAndIsTheHostsAlone()
	{
		CFileListDifference other = CFileListDifference.Compare(Here, Host);
		Assert.Equal(new[] { "common/defines.lua" }, other.Changed);
		Assert.Equal(new[] { "units/new.txt" }, other.OnlyHere);
		Assert.Equal(new[] { "history/x.txt" }, other.OnlyHost);
		Assert.Empty(other.Settings);
		Assert.Equal(3, other.Count);
		Assert.Equal("1 differing; 1 only here; 1 only on the host: common/defines.lua, units/new.txt and 1 more", other.Summary(2));
		Assert.Equal(new[]
		{
			"differing (1):", "  common/defines.lua", "only on this machine (1):", "  units/new.txt",
			"only on the host (1):", "  history/x.txt",
		}, other.Report());

		// The engine and the rules are said by what each machine has; the checksum's own line is not a file.
		CFileListDifference engines = CFileListDifference.Compare(
			new[] { "engine\t1.2", "rules\tenhanced", "checksum\tA" }, new[] { "engine\t1.3", "rules\t", "checksum\tB" });
		Assert.Equal(new[] { "engine: here 1.2, host 1.3", "rules: here enhanced, host none" }, engines.Settings);
		Assert.Equal(2, engines.Count);

		Assert.Equal(0, CFileListDifference.Compare(Host, Host).Count);
		Assert.Equal("no file differs", CFileListDifference.Compare(Host, Host).Summary(3));
	}

	[Fact]
	public void AListTravelsInPiecesAndIsReadBackAsItWas()
	{
		// Long enough for more than one piece: hashes do not deflate.
		var random = new System.Random(7);
		List<string> list = Enumerable.Range(0, 6000)
			.Select(index => $"events/file{index}.txt\t{index}\t{random.NextInt64():X16}{random.NextInt64():X16}").ToList();
		List<string> lines = CFileListWire.Lines(list);
		Assert.True(lines.Count > 1);
		Assert.All(lines, line => Assert.StartsWith("f ", line));
		Assert.Equal(list, CFileListWire.Read(lines.Select(line => line[2..])));
		Assert.Null(CFileListWire.Read(new[] { "not base64 !" }));
	}

	[Fact]
	public void TheBaseIsOfTheEngineTheRulesAndTheOriginalsChecksum()
	{
		string one = CPlayChecksum.Base("1.2", "", 1499173604);
		Assert.Equal(16, one.Length);
		Assert.Equal(one, CPlayChecksum.Base("1.2", "", 1499173604));
		Assert.NotEqual(one, CPlayChecksum.Base("1.3", "", 1499173604));
		Assert.NotEqual(one, CPlayChecksum.Base("1.2", "enhanced", 1499173604));
		Assert.NotEqual(one, CPlayChecksum.Base("1.2", "", -410340735));
	}

	[Fact]
	public void TheRuleIsOffUntilChosenAndCopiedWithTheOthers()
	{
		var chosen = new CGamePlaySettings();
		Assert.False(chosen.AllowOtherFiles);
		chosen.AllowOtherFiles = true;
		var game = new CGamePlaySettings();
		game.CopyFrom(chosen);
		Assert.True(game.AllowOtherFiles);
	}

	[Fact]
	public void TheEnginesOwnRulesWindowHasTheOneRuleAndWordsWhereNoTableHasItsKeys()
	{
		var problems = new List<string>();
		CGuiWindowType? window = CGuiTypes.Parse(CLobbyRulesText.FallbackWindow, problems.Add, CLobbyRulesText.FallbackFile)
			.FindWindow("openhoi_lobby_rules");
		Assert.NotNull(window);
		Assert.Empty(problems);
		Assert.Equal(new[] { "background", "title_bar", "title", "other_files", "other_files_label", "other_files_help" },
			window!.Children.Select(child => child.Name));

		// A table that has the key says it; one that has not gives the key back, and the words stand in.
		Assert.Equal("Allow other files", CLobbyRulesText.Text(key => key, CLobbyRulesText.OtherFilesKey, CLobbyRulesText.OtherFiles));
		Assert.Equal("Andere Dateien", CLobbyRulesText.Text(_ => "Andere Dateien", CLobbyRulesText.OtherFilesKey, CLobbyRulesText.OtherFiles));
	}
}
