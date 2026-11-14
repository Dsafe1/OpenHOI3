using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// What two countries hold of each other, the offers on a country's list and the casus belli among the pacts, read from a
/// save and written back as the original writes them (<c>docs/mechanics/savegame.md</c>, section 19).
/// </summary>
public class SaveStatusAndOffersTests
{
	private static CGameState State()
	{
		CCountry Country(string tag) => new() { Tag = new CCountryTag(tag), Name = tag, DefinitionFile = "" };
		return new CGameState(new CDate(1940, 5, 23),
			new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Amsterdam", isSea: false) },
			new[] { Country("GER"), Country("HOL"), Country("ITA"), Country("ENG"), Country("ROM"), Country("GRE") });
	}

	private static CCountryTag T(string tag) => new(tag);

	[Fact]
	public void A_pair_block_keeps_its_dates_and_debt_and_reports_a_key_it_does_not_know()
	{
		const string Saved =
			"HOL=\r\n{\r\n\tvalue=87\r\n\tthreat=0.390\r\n\tmilitary_access=yes\r\n\tlast_send_diplomat=\"1940.5.30.15\"\r\n" +
			"\tnext_wargoal_date=\"1940.8.9.16\"\r\n\tlast_war=\"1939.11.30.19\"\r\n\tdebtaction_action=yes\r\n\tdebt=79.655\r\n" +
			"\tdailyrepay=0.004\r\n}\r\n";
		CGameState state = State();
		CDiplomacyStatus status = CGameState.Status(state.Country(T("GER"))!, state.Country(T("HOL"))!);
		var report = new CSaveReport();

		CSaveStatus.Read(state, status, ClausewitzParser.Parse(Saved).Get("HOL")!, report);

		Assert.True(status.AllowDebts);
		Assert.Equal(79_655, status.Debt);
		Assert.True(CSaveValues.TryHours("1940.5.30.15", out long diplomat));
		Assert.Equal(diplomat, status.DiplomatUntil);
		Assert.Empty(report.UnreadKeys);
		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		CSaveStatus.Write(writer, state, status, "HOL");
		Assert.Equal(Saved, writer.Text);

		// A pair with nothing between them writes the relation and the threat alone; a key nobody names is reported.
		CDiplomacyStatus other = CGameState.Status(state.Country(T("GER"))!, state.Country(T("ITA"))!);
		CSaveStatus.Read(state, other, ClausewitzParser.Parse("ITA={ value=10 threat=0.000 truce=yes }").Get("ITA")!, report);
		Assert.Null(other.LastWar);
		Assert.Equal(long.MinValue, other.NextWarGoal);
		Assert.Equal(1, report.UnreadKeys[("pair", "truce")]);
	}

	[Fact]
	public void The_offers_on_a_list_come_back_each_under_its_kinds_key_with_a_cancellations_route_and_its_start()
	{
		const string Saved =
			"\tinfluence=\r\n\t{\r\n\t\ttype=0\r\n\t\tactor=\"GER\"\r\n\t\trecipient=\"HOL\"\r\n\t\tdate=\"1940.5.23.15\"\r\n" +
			"\t\tvalue=yes\r\n\t\tlast_command_date=\"1940.5.23.15\"\r\n\t}\r\n" +
			"\ttrade_action=\r\n\t{\r\n\t\ttype=0\r\n\t\tactor=\"ROM\"\r\n\t\trecipient=\"HOL\"\r\n\t\tdate=\"1940.5.23.7\"\r\n" +
			"\t\tvalue=no\r\n\t\tlast_command_date=\"1940.5.23.8\"\r\n\t\ttrade=\r\n\t\t{\r\n\t\t\tto=\"HOL\"\r\n\t\t\tfrom=\"ROM\"\r\n" +
			"\t\t\ttrade_from=\r\n\t\t\t{\r\n0.000 0.000 0.000 0.000 0.000 0.000 0.500 \t\t\t}\r\n" +
			"\t\t\ttrade_to=\r\n\t\t\t{\r\n0.000 0.000 0.064 0.000 0.000 0.000 0.000 \t\t\t}\r\n" +
			"\t\t\tfirst=\"ROM\"\r\n\t\t\tsecond=\"HOL\"\r\n\t\t\tstart_date=\"1940.1.1.22\"\r\n\t\t}\r\n\t}\r\n" +
			"\tcallaction=\r\n\t{\r\n\t\ttype=2\r\n\t\tactor=\"ENG\"\r\n\t\trecipient=\"HOL\"\r\n\t\tdate=\"1940.5.22.0\"\r\n" +
			"\t\tvalue=yes\r\n\t\tlast_command_date=\"1940.5.22.0\"\r\n\t\tversus=\"GER\"\r\n\t}\r\n";
		CGameState state = State();
		CCountry holland = state.Country(T("HOL"))!;
		ClausewitzNode block = ClausewitzParser.Parse("HOL=\r\n{\r\n" + Saved + "}\r\n").Get("HOL")!;
		var report = new CSaveReport();

		foreach (ClausewitzNode offer in block.Children)
		{
			Assert.True(CSaveOffers.IsOffer(offer.Key!));
			CSaveOffers.Read(state, holland, offer, new CSaveIds(), report);
		}

		Assert.Empty(report.UnreadKeys);
		Assert.Equal(new[] { EOfferKind.Influence, EOfferKind.Trade, EOfferKind.CallAlly }, holland.PendingOffers.Select(o => o.Kind));
		COffer cancel = holland.PendingOffers[1];
		Assert.False(cancel.Value);
		Assert.Equal(EOfferStage.Proposed, cancel.Stage);
		Assert.Equal(T("ROM"), cancel.Deal!.From);
		Assert.Equal(EOfferStage.Accepted, holland.PendingOffers[2].Stage);
		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		writer.Begin("HOL");
		CSaveOffers.Write(writer, state, holland);
		writer.End();
		Assert.Equal("HOL=\r\n{\r\n" + Saved + "}\r\n", writer.Text);
	}

	[Fact]
	public void An_added_war_goal_waiting_for_its_answer_is_written_and_not_read_back_as_the_original()
	{
		Assert.False(CSaveOffers.IsOffer("add_war_goal"));
		Assert.True(CSaveOffers.IsOffer("declarewar"));
	}

	[Fact]
	public void A_casus_belli_stands_among_the_pacts_in_the_order_it_was_given_its_null_type_by_the_originals_key()
	{
		const string Saved =
			"diplomacy=\r\n{\r\n" +
			"\talliance=\r\n\t{\r\n\t\tfirst=\"GER\"\r\n\t\tsecond=\"ITA\"\r\n\t\tstart_date=\"1939.5.22.0\"\r\n\t}\r\n" +
			"\tcasus_belli=\r\n\t{\r\n\t\ttype=\"nocasusbelli\"\r\n\t\tfirst=\"ITA\"\r\n\t\tsecond=\"ENG\"\r\n\t\tstart_date=\"1940.6.25.17\"\r\n\t}\r\n" +
			"\tguarantee=\r\n\t{\r\n\t\tfirst=\"ENG\"\r\n\t\tsecond=\"GRE\"\r\n\t\tstart_date=\"1940.7.1.0\"\r\n\t}\r\n" +
			"}\r\n";
		CGameState state = State();
		var report = new CSaveReport();

		CSaveDiplomacy.Read(state, ClausewitzParser.Parse(Saved).Get("diplomacy")!, report);

		Assert.Empty(report.UnreadKeys);
		CCasusBelli held = Assert.Single(CGameState.Status(state.Country(T("ITA"))!, state.Country(T("ENG"))!).CasusBelli);
		Assert.Equal(CCasusBelliType.Null.Index, held.Type);
		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		CSaveDiplomacy.Write(writer, state);
		Assert.Equal(Saved, writer.Text);

		// One given in play is dated the hour and goes after what stands.
		CCasusBellis.Give(state, state.Country(T("GRE"))!, state.Country(T("ITA"))!);
		writer = new CSaveTextWriter();
		writer.OpenRoot();
		CSaveDiplomacy.Write(writer, state);
		Assert.EndsWith(
			"\tcasus_belli=\r\n\t{\r\n\t\ttype=\"nocasusbelli\"\r\n\t\tfirst=\"GRE\"\r\n\t\tsecond=\"ITA\"\r\n\t\tstart_date=\"1940.5.23.0\"\r\n\t}\r\n}\r\n",
			writer.Text);
	}
}
