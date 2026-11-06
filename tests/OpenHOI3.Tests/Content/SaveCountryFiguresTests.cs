using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Aid;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// A country's pools of goods, lend-lease and strategic warfare in a save, both ways, as
/// <c>docs/mechanics/savegame.md</c>, section 15, has them.
/// </summary>
public class SaveCountryFiguresTests
{
	private static CGameState World()
	{
		var rebels = new CCountry { Tag = new CCountryTag("REB"), Name = "Rebels", DefinitionFile = "" };
		var britain = new CCountry { Tag = new CCountryTag("ENG"), Name = "Britain", DefinitionFile = "" };
		var america = new CCountry { Tag = new CCountryTag("USA"), Name = "America", DefinitionFile = "" };
		return new CGameState(new CDate(1941, 7, 16), new[] { new CProvince(0, "", isSea: false) }, new[] { rebels, britain, america });
	}

	[Fact]
	public void Every_pool_is_written_whatever_it_holds_in_the_originals_order()
	{
		CGameState state = World();
		CCountry britain = state.Country(new CCountryTag("ENG"))!;
		britain.HomeProduced[EGoods.Money] = CFixedPoint.FromInt(6);
		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		writer.Begin("ENG");

		CSaveLedgers.Write(writer, britain);
		writer.End();

		Assert.Equal(
			"ENG=\r\n{\r\n\tpool=\r\n\t{\r\n\t}\r\n\thome=\r\n\t{\r\n\t\tmoney=6.000\r\n\t}\r\n\tusage=\r\n\t{\r\n\t}\r\n\tto=\r\n\t{\r\n\t}\r\n" +
			"\tback=\r\n\t{\r\n\t}\r\n\tconvoyed_out=\r\n\t{\r\n\t}\r\n\ttraded_for=\r\n\t{\r\n\t}\r\n\ttraded_for_sans_allied=\r\n\t{\r\n\t}\r\n" +
			"\ttraded_away=\r\n\t{\r\n\t}\r\n\ttraded_away_sans_allied=\r\n\t{\r\n\t}\r\n}\r\n",
			writer.Text);
	}

	[Fact]
	public void Lend_lease_names_a_country_by_its_number_plus_one_and_is_written_only_while_there_is_any()
	{
		CGameState state = World();
		CCountry britain = state.Country(new CCountryTag("ENG"))!;
		CCountry america = state.Country(new CCountryTag("USA"))!;
		america.LendLeaseGiving.Add(new CLendLeaseGift(britain.Tag) { Weight = 305000, Effectiveness = 1000 });
		britain.LendLeaseReceived.Add(new CLendLeaseReceipt(america.Tag) { Amount = 22082 });
		britain.LendLeaseIncome = 22082;
		var writer = new CSaveTextWriter();
		writer.OpenRoot();

		CSaveLendLease.Write(writer, state, state.Country(new CCountryTag("REB"))!);
		CSaveLendLease.Write(writer, state, america);
		CSaveLendLease.Write(writer, state, britain);

		// The null country is number 0, so the rebels, first here, are 1, Britain 2 and America 3.
		Assert.Equal(
			"lendlease_targets=\r\n{\r\n2 }\r\nlendlease_distributions=\r\n{\r\n305.000 }\r\nlendlease_effectiveness=\r\n{\r\n1.000 }\r\n" +
			"lend_lease_from=\r\n{\r\n3 }\r\nlend_lease_from_values=\r\n{\r\n22.082 }\r\nlend_lease_income=22.082\r\n",
			writer.Text);

		var report = new CSaveReport();
		CSaveLendLease.Read(state, britain, ClausewitzParser.Parse(
			"ENG=\r\n{\r\n\tlend_lease_from=\r\n\t{\r\n3 9 \t}\r\n\tlend_lease_from_values=\r\n\t{\r\n5.407 1.000 \t}\r\n" +
			"\tlend_lease_income=5.407\r\n}\r\n").Get("ENG")!, report);
		CLendLeaseReceipt receipt = Assert.Single(britain.LendLeaseReceived);
		Assert.Equal((america.Tag, 5407L), (receipt.Giver, receipt.Amount));
		Assert.Empty(britain.LendLeaseGiving);
		Assert.Single(report.UnreadKeys);
	}

	[Fact]
	public void Strategic_warfare_is_read_whole_and_written_with_its_entries_at_column_0()
	{
		CGameState state = World();
		CCountry britain = state.Country(new CCountryTag("ENG"))!;
		ClausewitzNode node = ClausewitzParser.Parse(
			"strategic_warfare=\r\n{\r\n\talliance=\r\n\t{\r\n\t}\r\n\r\n\tconvoy=\r\n\t{\r\n\"1936.3.24.0\"=\"-0.093\"\r\n" +
			"\"1941.7.15.17\"=\"-0.024\"\r\n\t}\r\n\r\n\tground_bombing=\r\n\t{\r\n\t}\r\n\r\n}\r\n").Get("strategic_warfare")!;

		CSaveStrategicWarfare.Read(britain, node);

		// An entry years old is kept - the day's prune takes it - and the impact is the sum.
		Assert.Equal(2, britain.StrategicWarfare.Convoys.Count);
		Assert.Equal(-117, britain.StrategicWarfare.ConvoyImpact);
		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		CSaveStrategicWarfare.Write(writer, britain);
		Assert.Equal(
			"strategic_warfare=\r\n{\r\n\talliance=\r\n\t{\r\n\t}\r\n\r\n\tconvoy=\r\n\t{\r\n\"1936.3.24.0\"=\"-0.093\"\r\n" +
			"\"1941.7.15.17\"=\"-0.024\"\r\n\t}\r\n\r\n\tground_bombing=\r\n\t{\r\n\t}\r\n\r\n}\r\n",
			writer.Text);
	}
}
