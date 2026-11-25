using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// An enhanced game's record of finished battles, against <c>docs/mechanics/combatreports.md</c>: what a report
/// holds of a battle as it ends, that a faithful game keeps none, a country's tally over a stretch of hours, its
/// last battles, and the record in a save.
/// </summary>
public class CombatReportTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Ita = new("ITA");
	private static readonly CCountryTag Pol = new("POL");

	private static CUnit Unit(CCountryTag owner, int id) => new(EUnitKind.Air, owner) { Id = id, Location = 1 };

	private static CGameState State(bool enhanced)
	{
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false) }, System.Array.Empty<CCountry>());
		if (enhanced)
		{
			state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(System.Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
		}
		return state;
	}

	[Fact]
	public void AReportHoldsBothSidesTheirLossesAndMenAndTheSideLeftStandingWon()
	{
		CGameState state = State(enhanced: true);
		var combat = new CCombat(7, ECombatKind.Land);
		combat.Attacker.Add(Unit(Ger, 1));
		combat.Attacker.Add(Unit(Ita, 2));
		CUnit polish = Unit(Pol, 3);
		combat.Defender.Add(polish);
		combat.Attacker.Losses = new CFixedPoint(21900);
		combat.Defender.Losses = new CFixedPoint(40500);
		// A side's men: each type's tally cut to whole ones, one at nought or below left out.
		combat.Attacker.Size.Clear();
		combat.Attacker.Size["infantry_brigade"] = new CFixedPoint(25700_900);
		combat.Attacker.Size["artillery_brigade"] = new CFixedPoint(1000_500);
		combat.Attacker.Size["empty"] = new CFixedPoint(-5000);
		combat.Defender.Size.Clear();
		combat.Defender.Size["infantry_brigade"] = new CFixedPoint(9000_000);
		// The defender is beaten: it has no country left in the fight, and is named all the same.
		combat.Defender.Remove(state, polish);

		state.CombatReports.Record(state, combat);

		Assert.Equal(new CCombatReport(state.TotalHours, ECombatKind.Land, 7, Ger, Pol, 21900, 26700, 40500, 9000, ECombatWinner.Attacker),
			Assert.Single(state.CombatReports.Entries));
	}

	[Fact]
	public void ABombingHasNoWinnerAndAFaithfulGameKeepsNoRecord()
	{
		var bombing = new CCombat(7, ECombatKind.GroundBombing);
		bombing.Attacker.Add(Unit(Ger, 1));
		bombing.Defender.AddCountry(Pol);

		CGameState enhanced = State(enhanced: true);
		enhanced.CombatReports.Record(enhanced, bombing);
		Assert.Equal(ECombatWinner.Nobody, Assert.Single(enhanced.CombatReports.Entries).Winner);

		CGameState faithful = State(enhanced: false);
		faithful.CombatReports.Record(faithful, bombing);
		Assert.Empty(faithful.CombatReports.Entries);
	}

	private static CCombatReports Sample()
	{
		var reports = new CCombatReports();
		reports.Add(new CCombatReport(100, ECombatKind.Land, 1, Ger, Pol, 1000, 10, 3000, 20, ECombatWinner.Attacker));
		reports.Add(new CCombatReport(200, ECombatKind.Land, 2, Pol, Ger, 500, 30, 700, 40, ECombatWinner.Attacker));
		reports.Add(new CCombatReport(300, ECombatKind.GroundBombing, 3, Ita, Ger, 0, 0, 250, 0, ECombatWinner.Nobody));
		reports.Add(new CCombatReport(400, ECombatKind.Air, 4, Ita, Pol, 10, 1, 20, 2, ECombatWinner.Defender));
		return reports;
	}

	[Fact]
	public void ATallyCountsACountrysBattlesBetweenTwoHoursBothAmongThem()
	{
		CCombatReports reports = Sample();

		CCombatTally all = reports.Tally(Ger, 0, 1000);
		Assert.Equal(3, all.Combats);
		Assert.Equal(1, all.Won);
		Assert.Equal(1, all.Lost);
		Assert.Equal(1, all.AsAttacker);
		Assert.Equal(2, all.AsDefender);
		// Its own side's losses, and the other side's.
		Assert.Equal(1000 + 700 + 250, all.Losses);
		Assert.Equal(3000 + 500 + 0, all.Kills);

		// The ends are among them; a battle outside is not; one of another kind is not where a kind is named.
		Assert.Equal(2, reports.Tally(Ger, 100, 200).Combats);
		Assert.Equal(1, reports.Tally(Ger, 101, 200).Combats);
		Assert.Equal(2, reports.Tally(Ger, 0, 1000, ECombatKind.Land).Combats);
		Assert.Equal(0, reports.Tally(Ger, 0, 1000, ECombatKind.Naval).Combats);
		// A bombing counts as a battle and as neither won nor lost.
		CCombatTally bombing = reports.Tally(Ger, 0, 1000, ECombatKind.GroundBombing);
		Assert.Equal((1, 0, 0), (bombing.Combats, bombing.Won, bombing.Lost));
	}

	[Fact]
	public void TheLastBattlesAreACountrysOwnTheNewestFirst()
	{
		CCombatReports reports = Sample();

		Assert.Equal(new long[] { 300, 200 }, reports.Last(Ger, 2).Select(report => report.Hour));
		Assert.Equal(new long[] { 400, 300 }, reports.Last(Ita, 16).Select(report => report.Hour));
	}

	[Fact]
	public void AnEnhancedSaveHoldsTheRecordAndAFaithfulOneNone()
	{
		CGameState state = State(enhanced: false);
		foreach (CCombatReport report in Sample().Entries)
		{
			state.CombatReports.Add(report);
		}
		// A side with no country, as a battle saved with one may have.
		state.CombatReports.Add(new CCombatReport(500, ECombatKind.NavalBombing, 9, null, Ger, 0, 0, 0, 0, ECombatWinner.Nobody));

		var faithful = new CSaveTextWriter();
		CSaveCombatReports.Write(faithful, state);
		Assert.Equal("", faithful.Text);

		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(System.Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
		var writer = new CSaveTextWriter();
		CSaveCombatReports.Write(writer, state);
		// One block, a year in it for each year a battle ended in: the year, how many, and the letters.
		ClausewitzNode block = Assert.Single(ClausewitzParser.Parse(writer.Text).Children);
		Assert.Equal(CSaveCombatReports.BlockKey, block.Key);
		Assert.All(block.Children, year => Assert.Equal("y", year.Key));
		Assert.Equal(state.CombatReports.Entries.Count, block.Children.Sum(year => int.Parse(year.Children[1].Value!)));
		Assert.DoesNotContain("GER", writer.Text);

		CGameState loaded = State(enhanced: true);
		var report2 = new CSaveReport();
		CSaveCombatReports.ReadBlock(loaded, block, report2);
		Assert.Equal(state.CombatReports.Entries, loaded.CombatReports.Entries);
		Assert.Empty(report2.UnreadKeys);

		// A year whose letters are no block is reported and left out.
		CSaveCombatReports.ReadBlock(loaded, ClausewitzParser.Parse("openhoi_combat_reports = { y = { 1936 2 AAAA } y = { 1936 } }").Children[0], report2);
		Assert.Equal(state.CombatReports.Entries.Count, loaded.CombatReports.Entries.Count);
		Assert.Equal(2, report2.UnreadKeys.Values.Sum());
	}

	[Fact]
	public void ASaveOfBeforeThePackingIsStillReadABattleAKey()
	{
		CGameState state = State(enhanced: true);
		foreach (CCombatReport report in Sample().Entries)
		{
			state.CombatReports.Add(report);
		}
		state.CombatReports.Add(new CCombatReport(500, ECombatKind.NavalBombing, 9, null, Ger, 0, 0, 0, 0, ECombatWinner.Nobody));
		string old = string.Concat(state.CombatReports.Entries.Select(CSaveCombatReports.OldText));
		Assert.Contains("100 0 1 GER POL 1000 10 3000 20 1", old);
		Assert.Contains("500 5 9 --- GER 0 0 0 0 0", old);

		CGameState loaded = State(enhanced: true);
		var report2 = new CSaveReport();
		foreach (ClausewitzNode node in ClausewitzParser.Parse(old).Children)
		{
			Assert.Equal(CSaveCombatReports.Key, node.Key);
			CSaveCombatReports.Read(loaded, node, report2);
		}
		Assert.Equal(state.CombatReports.Entries, loaded.CombatReports.Entries);
		Assert.Empty(report2.UnreadKeys);

		// A report that is not ten words, or names a kind there is none of, is reported and left out.
		CSaveCombatReports.Read(loaded, ClausewitzParser.Parse("openhoi_combat_report = { 1 2 3 }").Children[0], report2);
		CSaveCombatReports.Read(loaded, ClausewitzParser.Parse("openhoi_combat_report = { 1 9 3 GER POL 0 0 0 0 0 }").Children[0], report2);
		Assert.Equal(state.CombatReports.Entries.Count, loaded.CombatReports.Entries.Count);
		Assert.Equal(2, report2.UnreadKeys.Values.Sum());
	}

	[Fact]
	public void ABlockOfReportsComesBackExactAndIsAFractionOfTheText()
	{
		// Figures of every size and sign a report may hold, a side with no country, and hours out of order.
		var reports = new System.Collections.Generic.List<CCombatReport>
		{
			new(16959413, ECombatKind.Land, 7425, Ger, Pol, 57500, 82800, 946600, 54025, ECombatWinner.Attacker),
			new(16959413, ECombatKind.NavalBombing, 1, null, Ger, 0, 0, long.MaxValue, -5, ECombatWinner.Nobody),
			new(16959000, ECombatKind.Naval, 14000, Pol, null, 1, 2, 3, 4, ECombatWinner.Defender),
		};
		Assert.Equal(reports, CCombatReportBlock.Unpack(CCombatReportBlock.Pack(reports)));
		Assert.Empty(CCombatReportBlock.Unpack(CCombatReportBlock.Pack(System.Array.Empty<CCombatReport>()))!);
		Assert.Null(CCombatReportBlock.Unpack(new byte[] { 1, 2, 3, 4 }));
		Assert.Null(CCombatReportBlock.Unpack(System.Array.Empty<byte>()));

		// A year of battles as a war has them: packed, a small part of what a key a battle came to.
		var random = new System.Random(3);
		var year = new System.Collections.Generic.List<CCombatReport>();
		long hour = 1941L * CSaveCombatReports.HoursInYear;
		for (int index = 0; index < 5000; index++)
		{
			hour += random.Next(0, 3);
			year.Add(new CCombatReport(hour, (ECombatKind)random.Next(0, 3), random.Next(1, 14000), index % 2 == 0 ? Ger : Pol,
				index % 2 == 0 ? Pol : Ger, random.Next(0, 900000), random.Next(0, 90000), random.Next(0, 900000), random.Next(0, 90000),
				(ECombatWinner)random.Next(0, 3)));
		}
		byte[] packed = CCombatReportBlock.Pack(year);
		Assert.Equal(year, CCombatReportBlock.Unpack(packed));
		Assert.True(CSaveStatistics.Letters(packed).Length * 3 < year.Sum(report => CSaveCombatReports.OldText(report).Length));

		// The years apart, each in the record's order.
		year.Add(new CCombatReport(1942L * CSaveCombatReports.HoursInYear + 5, ECombatKind.Land, 3, Ger, Pol, 1, 1, 1, 1, ECombatWinner.Nobody));
		var years = CSaveCombatReports.Years(year).ToList();
		Assert.Equal(new long[] { 1941, 1942 }, years.Select(one => one.Year));
		Assert.Equal(5000, years[0].Reports.Count);
		Assert.Single(years[1].Reports);
	}
}
