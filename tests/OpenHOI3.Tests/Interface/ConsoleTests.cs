using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Console;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>The console's table and its answers, against <c>docs/mechanics/console.md</c>.</summary>
public class ConsoleTests
{
	private sealed class CHost : IConsoleHost
	{
		public CGameState State { get; init; } = null!;

		public CCountry? Player { get; init; }

		public IReadOnlyList<int> SelectedProvinces { get; set; } = new List<int>();

		public bool TooltipsOn { get; set; } = true;

		public bool TooltipDebug { get; set; }

		public bool ContinentPainting { get; set; }

		public string Clipboard { get; private set; } = "";

		public CUnit? SelectedUnit { get; set; }

		public bool TakesCommands { get; set; } = true;

		public int RenderLayers { get; set; } = 0xFFFF;

		public int ModeLayers { get; set; } = 0xFFBE;

		public bool Fullscreen { get; set; }

		public bool Wireframe { get; set; }

		public int Remade { get; private set; }

		public bool EffectExists(string name) => name == "river.fx";

		public void ShowCountryPlayed() => Remade++;

		public void ReloadInterface() => Remade++;

		/// <summary>The song <c>nextsong</c> answers, as a host's music would pick it.</summary>
		public string Song { get; set; } = "";

		public string NextSong() => Song;

		public void RefreshMapColours()
		{
		}

		public void WriteDetailedLog(string name)
		{
		}

		public void Post(CCommand command)
		{
			command.Execute(State);
		}

		public void CopyToClipboard(string text) => Clipboard = text;

		public void SetLeastHeight(int height)
		{
		}

		public void Log(string line)
		{
		}
	}

	private static (CConsoleRunner Runner, CHost Host, CCountry Germany) World()
	{
		var germany = new CCountry { Tag = new CCountryTag("GER"), Name = "Germany", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Berlin", isSea: false) }, new[] { germany });
		var host = new CHost { State = state, Player = germany };
		return (new CConsoleRunner(host), host, germany);
	}

	[Fact]
	public void TheTableHasEveryCommandAndHelpLeavesOutTheDevelopers()
	{
		Assert.Equal(53, CConsoleTable.Commands.Length);
		string list = CConsoleRunner.ListAll();
		Assert.DoesNotContain("crash", list);
		Assert.Contains("instantconstruction(instantproduction,inc)", list);
	}

	[Fact]
	public void AWordIsFoundWithItsCaseAndByItsAliases()
	{
		(CConsoleRunner runner, _, _) = World();
		Assert.Equal(new CConsoleReply(false, "Unknown command"), runner.Run("MANPOWER 5"));
		Assert.Equal(new CConsoleReply(true, "Added 5 manpower."), runner.Run("manpower 5"));
	}

	[Fact]
	public void NextSongSaysWhatItPlaysAndNothingWhereNoneCouldBePicked()
	{
		(CConsoleRunner runner, var host, _) = World();
		host.Song = "music/bigarmy.mp3";
		Assert.Equal(new CConsoleReply(true, "Now playing music/bigarmy.mp3"), runner.Run("nextsong"));
		host.Song = "";
		Assert.Equal(new CConsoleReply(true, "Now playing "), runner.Run("nextsong"));
		Assert.Equal(new CConsoleReply(false, "Invalid arguments count."), runner.Run("nextsong 2"));
	}

	[Fact]
	public void ADevelopersRowIsRefusedAndStillDescribed()
	{
		(CConsoleRunner runner, _, _) = World();
		Assert.False(runner.Run("crash").Ok);
		Assert.Equal("crash\n - Simulate crashing the game", runner.Run("help crash").Text);
	}

	[Fact]
	public void ANumberThatIsNoneReadsAsNought()
	{
		Assert.Equal(0, CConsoleRunner.Atoi("abc"));
		Assert.Equal(12, CConsoleRunner.Atoi("12x"));
		Assert.Equal(-3, CConsoleRunner.Atoi("-3"));
	}

	[Fact]
	public void ManpowerIsAddedInThousandsAndStopsAtNought()
	{
		(CConsoleRunner runner, _, CCountry germany) = World();
		runner.Run("addmanpower 3");
		Assert.Equal(CFixedPoint.FromInt(3), germany.Manpower);
		runner.Run("addmanpower -9");
		Assert.Equal(CFixedPoint.Zero, germany.Manpower);
	}

	[Fact]
	public void NationalUnityIsHeldBetweenOneAndAHundred()
	{
		(CConsoleRunner runner, _, CCountry germany) = World();
		runner.Run("setnationalunity GER 400");
		Assert.Equal(CFixedPoint.FromInt(100), germany.NationalUnity);
	}

	[Fact]
	public void InstantResearchTurnsOnAndOff()
	{
		(CConsoleRunner runner, CHost host, _) = World();
		Assert.Equal("Instant research ON", runner.Run("inr").Text);
		Assert.True(host.State.InstantResearch);
		Assert.Equal("Instant research OFF", runner.Run("inr").Text);
		Assert.Equal(new CConsoleReply(false, "Invalid arguments."), runner.Run("inr 1"));
	}

	[Fact]
	public void CopyprovCopiesTheSelection()
	{
		(CConsoleRunner runner, CHost host, _) = World();
		Assert.False(runner.Run("copyprov").Ok);
		host.SelectedProvinces = new List<int> { 3, 7 };
		Assert.Equal("2 provinces copied to clipboard.", runner.Run("copyprov").Text);
		Assert.Equal("3 7", host.Clipboard);
	}

	[Fact]
	public void TheAiIsPausedOneCountryAtATimeOrAll()
	{
		(CConsoleRunner runner, _, CCountry germany) = World();
		Assert.Equal("AI is paused for:\nNone", runner.Run("aipause").Text);
		Assert.Equal("Germany AI is now Paused", runner.Run("aipause GER").Text);
		Assert.True(germany.AiPaused);
		Assert.Equal("AI is paused for:\nGER, ", runner.Run("aipause").Text);
		Assert.Equal("AI Unpaused for all", runner.Run("aipause all").Text);
		Assert.False(germany.AiPaused);
		Assert.Equal("AI Paused for all", runner.Run("aipause all").Text);
		Assert.Equal("Invalid country tag", runner.Run("aipause XXX").Text);
		Assert.Equal("Please specify country tag, or \"all\" argument", runner.Run("aipause GER all").Text);
	}

	[Fact]
	public void TheDateIsWrittenAndNothingElseRuns()
	{
		(CConsoleRunner runner, CHost host, _) = World();
		Assert.Equal("Date changed.", runner.Run("date 1939.9.1.5").Text);
		Assert.Equal(new CDate(1939, 9, 1), host.State.Date);
		Assert.Equal(5, host.State.Hour);
		Assert.Equal("Date changed.", runner.Run("date 1940.2.3").Text);
		Assert.Equal(0, host.State.Hour);
		Assert.False(runner.Run("date nonsense").Ok);
		Assert.Equal(new CDate(1940, 2, 3), host.State.Date);
	}

	[Fact]
	public void TagNeedsACountryWithLandAndAGameThatTakesCommands()
	{
		(CConsoleRunner runner, CHost host, _) = World();
		Assert.Equal("Please specify the country tag", runner.Run("tag").Text);
		Assert.Equal("Such country does not exist.", runner.Run("tag XXX").Text);
		Assert.Equal("Such country does not exist.", runner.Run("tag GER").Text);
		Assert.Equal(0, host.Remade);
	}

	[Fact]
	public void RenderListsTheLayersByBothMasksAndNamesOneToTurnOver()
	{
		(CConsoleRunner runner, CHost host, _) = World();
		string list = runner.Run("debug render").Text;
		Assert.StartsWith("----------------------------------\nRender options:\n\t\tTrees:\tno\n\t\tBorders:\tyes\n\t\tText:\tyes\n", list);
		Assert.EndsWith("\t\tEffects:\tyes\n----------------------------------\n", list);
		Assert.Equal(new CConsoleReply(true, ""), runner.Run("debug render borders"));
		Assert.Equal(0xFFFD, host.RenderLayers);
		Assert.Contains("\t\tBorders:\tno\n", runner.Run("debug render").Text);
	}

	[Fact]
	public void DebugSpyYesmenAndTheZoomAnswerAsTheOriginals()
	{
		(CConsoleRunner runner, CHost host, _) = World();
		Assert.Equal("Superior espionage turned ON.", runner.Run("debug spy").Text);
		Assert.True(host.State.SpyCheat);
		Assert.Equal("AI now always responds favorably", runner.Run("debug yesmen").Text);
		Assert.Equal("AI now uses normal responses.", runner.Run("debug yesmen").Text);
		Assert.Equal("Zoom limit set to 5.000", runner.Run("debug minzoom 5").Text);
		Assert.Equal("Please specify zoomlevel value.", runner.Run("debug minzoom").Text);
	}

	[Fact]
	public void ReloadfxTakesItsFourNamesOrAFileThatIsThere()
	{
		(CConsoleRunner runner, CHost host, _) = World();
		Assert.Equal("Map reloaded", runner.Run("reloadfx map").Text);
		Assert.Equal("river.fx reloaded", runner.Run("reloadfx river.fx").Text);
		Assert.Equal(new CConsoleReply(false, "Reload failed"), runner.Run("reloadfx none.fx"));
		Assert.Equal(2, host.Remade);
	}

	[Fact]
	public void TeleportWantsAUnitSelected()
	{
		(CConsoleRunner runner, _, _) = World();
		Assert.Equal("Please specify the province id.", runner.Run("teleport").Text);
		Assert.Equal("Select transportable unit first!", runner.Run("teleport 1").Text);
	}

	[Fact]
	public void AnUnknownDebugWordAnswersNothing()
	{
		(CConsoleRunner runner, _, _) = World();
		Assert.Equal(new CConsoleReply(true, ""), runner.Run("debug nonsense"));
		Assert.False(runner.Run("debug").Ok);
	}
}
