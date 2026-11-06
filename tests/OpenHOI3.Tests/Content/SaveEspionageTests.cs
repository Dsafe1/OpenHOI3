using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Espionage;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Content;

/// <summary>A country's spies in a save, both ways, as <c>docs/mechanics/savegame.md</c>, section 13, has them.</summary>
public class SaveEspionageTests
{
	private static CGameState World()
	{
		var rebels = new CCountry { Tag = new CCountryTag("REB"), Name = "Rebels", DefinitionFile = "" };
		var germany = new CCountry { Tag = new CCountryTag("GER"), Name = "Germany", DefinitionFile = "" };
		return new CGameState(new CDate(1936, 1, 7), new[] { new CProvince(0, "", isSea: false) }, new[] { rebels, germany });
	}

	[Fact]
	public void The_lists_have_the_placeholders_entry_first_and_then_one_a_country()
	{
		CGameState state = World();
		CCountry germany = state.Country(new CCountryTag("GER"))!;
		germany.FreeSpies = new CFixedPoint(494);
		CSpyPresence atHome = CGameState.Presence(germany, germany);
		atHome.Priority = 1;
		atHome.Level = 10000;
		atHome.SetMission((int)ESpyMission.CounterEspionage, 1);
		Assert.True(CSaveValues.TryHours("1936.1.19.5", out long changed));
		atHome.LastMissionChange = changed;
		atHome.CovertOpsPoints = 2500;
		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		writer.Begin("GER");

		CSaveEspionage.Write(writer, state, germany);
		writer.End();

		// The dates unquoted, the null date where never; nine mission weights a country, the placeholder's first.
		Assert.Equal(
			"GER=\r\n{\r\n\tspies=0.494\r\n\tspy_priority=\r\n\t{\r\n0 0 1 \t}\r\n\tspy_allocation=\r\n\t{\r\n0.000 0.000 10.000 \t}\r\n" +
			"\tspy_date=\r\n\t{\r\n1.1.1.0 1.1.1.0 1936.1.19.5 \t}\r\n" +
			"\tspy_mission=\r\n\t{\r\n0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1 0 0 0 0 0 0 0 0 \t}\r\n" +
			"\tcovert_ops_points=\r\n\t{\r\n0.000 0.000 2.500 \t}\r\n}\r\n",
			writer.Text);
	}

	[Fact]
	public void A_saves_spies_are_read_by_each_countrys_place_after_the_placeholder()
	{
		CGameState state = World();
		CCountry germany = state.Country(new CCountryTag("GER"))!;
		ClausewitzNode node = ClausewitzParser.Parse(
			"GER=\r\n{\r\n\tspies=1.250\r\n\tspy_priority=\r\n\t{\r\n0 3 1 \t}\r\n\tspy_allocation=\r\n\t{\r\n0.000 0.500 7.000 \t}\r\n" +
			"\tspy_date=\r\n\t{\r\n1.1.1.0 1938.8.3.5 1.1.1.0 \t}\r\n" +
			"\tspy_mission=\r\n\t{\r\n0 0 0 0 0 0 0 0 0 0 2 0 0 0 0 0 0 2 \t}\r\n}\r\n").Get("GER")!;

		CSaveEspionage.Read(state, germany, node);

		CSpyPresence inRebels = CGameState.Presence(germany, state.Country(new CCountryTag("REB"))!);
		CSpyPresence atHome = CGameState.Presence(germany, germany);
		Assert.Equal(1250, germany.FreeSpies.Raw);
		Assert.Equal(3, inRebels.Priority);
		Assert.Equal(500, inRebels.Level);
		Assert.True(CSaveValues.TryHours("1938.8.3.5", out long changed));
		Assert.Equal(changed, inRebels.LastMissionChange);
		Assert.Equal(new[] { 0, 2, 0, 0, 0, 0, 0, 0, 2 }, inRebels.Missions);
		// The shares are worked out from the weights; a list that ends early leaves the rest nought.
		Assert.Equal(500, inRebels.Shares[1]);
		Assert.Null(atHome.LastMissionChange);
		Assert.Equal(new int[9], atHome.Missions);
		Assert.Equal(0, atHome.CovertOpsPoints);
		Assert.Equal(7000, atHome.Level);
	}
}
