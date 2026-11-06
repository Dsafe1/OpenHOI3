using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Tests.Content;

/// <summary>A country's politics and research queue in a save, both ways, as <c>docs/mechanics/savegame.md</c>, section 12, has them.</summary>
public class SavePoliticsTests
{
	private static (CGameState State, CCountry Country) World()
	{
		var group = new CIdeologyGroup("all", 0, 0);
		group.IdeologyOrder.AddRange(new[] { "national_socialist", "social_democrat" });
		group.Ideologies.UnionWith(group.IdeologyOrder);
		var germany = new CCountry { Tag = new CCountryTag("GER"), Name = "Germany", DefinitionFile = "" };
		germany.Ministers[1] = new CMinister(1, "Adolf Hitler");
		germany.Ministers[33] = new CMinister(33, "Hermann Göring");
		var state = new CGameState(new CDate(1936, 1, 7), new[] { new CProvince(0, "", isSea: false) }, new[] { germany },
			technologies: new[] { new CTechnology("tank_engine", 5, new CModifier("tank_engine")) })
		{
			IdeologyGroups = new[] { group },
		};
		return (state, germany);
	}

	private static ClausewitzNode Country(string body)
	{
		return ClausewitzParser.Parse("GER=\r\n{\r\n" + body + "}\r\n").Get("GER")!;
	}

	[Fact]
	public void A_saves_politics_are_read_into_the_country()
	{
		(CGameState state, CCountry germany) = World();
		var report = new CSaveReport();
		ClausewitzNode node = Country(
			"\tresearch=tank_engine\r\n\tresearch=jet_engine\r\n\tlast_election=\"1936.1.1.0\"\r\n\tlast_surrender=\"1.1.1.0\"\r\n" +
			"\telection=yes\r\n\tduration=60\r\n\tspiescaught=9\r\n" +
			"organization=\r\n\t{\r\n\t\tdata=\r\n\t\t{\r\n0.000 39.200 13.154 \t\t}\r\n\t}\r\n" +
			"popularity=\r\n\t{\r\n\t\tdata=\r\n\t\t{\r\n0.000 63.100 36.900 \t\t}\r\n\t}\r\n" +
			"\tgovernment=national_socialism\r\n\tministers={33 1 7 }\r\n\tcabinet=\r\n\t{\r\n0 7 1 0 7 0 \t}\r\n");

		CSavePolitics.Read(state, germany, node, report);

		// A technology of no name known and a minister the file gives no number to are left out and reported; the null
		// date is never; the figures are made at the ideologies' count; the cabinet's whole list is kept behind.
		Assert.Equal(new[] { "tank_engine" }, germany.ResearchQueue);
		Assert.Equal(new CDate(1936, 1, 1), germany.LastElection);
		Assert.True(germany.LastSurrender < 0);
		Assert.Equal(60, germany.ElectionDuration);
		Assert.Equal(9, germany.SpiesCaught);
		Assert.Equal(new long[] { 0, 39200, 13154 }, germany.Organisation.Values);
		Assert.Equal(new long[] { 0, 63100, 36900 }, germany.Popularity.Values);
		Assert.Equal("national_socialism", germany.GovernmentType);
		Assert.Equal(new[] { 33, 1 }, germany.PossibleMinisters.Select(minister => minister.Id));
		Assert.Equal(new[] { 0, 7, 1, 0, 7, 0 }, germany.CabinetLoaded);
		Assert.Equal(2, report.UnreadKeys.Count);
	}

	[Fact]
	public void A_countrys_politics_are_written_in_the_originals_layout()
	{
		(CGameState _, CCountry germany) = World();
		germany.ResearchQueue.Add("tank_engine");
		germany.LastElection = new CDate(1936, 1, 1);
		germany.ElectionDuration = 48;
		germany.Popularity = new CPartyFigures(3);
		germany.Popularity.Values[1] = 63100;
		germany.Organisation = new CPartyFigures(3);
		germany.Cabinet = new[] { 0, 7, 1 };
		germany.CabinetLoaded = new[] { 0, 8, 0 };
		germany.PossibleMinisters.Add(germany.Ministers[33]);
		germany.PossibleMinisters.Add(germany.Ministers[1]);
		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		writer.Begin("GER");

		CSavePolitics.WriteResearch(writer, germany);
		CSavePolitics.WriteDates(writer, germany);
		CSavePolitics.WriteParties(writer, germany);
		CSavePolitics.WriteGovernment(writer, germany);
		CSavePolitics.WriteMinisters(writer, germany);
		CSavePolitics.WriteCabinet(writer, germany);
		writer.End();

		// Never surrendered is the null date and election is always yes; the party keys stand at column 0; no government
		// writes nothing; the pool is one line; the cabinet a load gave follows the seats.
		Assert.Equal(
			"GER=\r\n{\r\n\tresearch=tank_engine\r\n\tlast_election=\"1936.1.1.0\"\r\n\tlast_surrender=\"1.1.1.0\"\r\n\telection=yes\r\n" +
			"\tduration=48\r\norganization=\r\n\t{\r\n\t\tdata=\r\n\t\t{\r\n0.000 0.000 0.000 \t\t}\r\n\t}\r\n" +
			"popularity=\r\n\t{\r\n\t\tdata=\r\n\t\t{\r\n0.000 63.100 0.000 \t\t}\r\n\t}\r\n\tministers={33 1 }\r\n" +
			"\tcabinet=\r\n\t{\r\n0 7 1 0 8 0 \t}\r\n}\r\n",
			writer.Text);
	}

	[Fact]
	public void An_empty_pool_is_written_as_two_braces()
	{
		(CGameState _, CCountry germany) = World();
		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		writer.Begin("REB");

		CSavePolitics.WriteMinisters(writer, germany);

		Assert.EndsWith("\tministers={}\r\n", writer.Text);
	}
}
