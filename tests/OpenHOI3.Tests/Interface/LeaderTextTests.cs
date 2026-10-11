using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>What a unit's panel says of its leader: <c>docs/mechanics/unitpanel.md</c>, section 3.</summary>
public class LeaderTextTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static CGameState State(out CUnit army)
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false) }, new[] { germany });
		army = new CUnit(EUnitKind.Army, Ger) { Id = 1, Name = "1. Armee" };
		germany.Units.Add(army);
		return state;
	}

	private static string Localise(string key) => key == CUnitPanelText.NoLeaderKey ? "No Leader" : key;

	[Fact]
	public void AUnitWithNoLeaderSaysSo()
	{
		CGameState state = State(out CUnit army);

		Assert.Equal("No Leader", CUnitPanelText.Leader(state, army, Localise));
		Assert.Null(CUnitPanelText.LeaderPortrait(army));
		Assert.Equal("1. Armee", CUnitPanelText.ChildName(army));
	}

	[Fact]
	public void ALeaderWithoutTheRankIsNamedInRed()
	{
		CGameState state = State(out CUnit army);
		var leader = new CLeader(798) { Name = "von Rundstedt", Rank = 1, Picture = "L798" };
		CLeaders.SetLeader(state, army, leader, false);

		Assert.Equal("§Rvon Rundstedt", CUnitPanelText.Leader(state, army, Localise));
		leader.Rank = 2;
		Assert.Equal("von Rundstedt", CUnitPanelText.Leader(state, army, Localise));
		Assert.Equal("gfx/pictures/portraits/L798.tga", CUnitPanelText.LeaderPortrait(army));
		Assert.Equal("1. Armee (von Rundstedt)", CUnitPanelText.ChildName(army));
	}
}
