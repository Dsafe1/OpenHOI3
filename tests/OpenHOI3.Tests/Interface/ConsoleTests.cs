using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Console;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

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
	public void AnUnknownDebugWordAnswersNothing()
	{
		(CConsoleRunner runner, _, _) = World();
		Assert.Equal(new CConsoleReply(true, ""), runner.Run("debug nonsense"));
		Assert.False(runner.Run("debug").Ok);
	}
}
