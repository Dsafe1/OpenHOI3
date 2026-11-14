using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Theatres;

namespace OpenHOI3.Tests.Content;

/// <summary>A country's AI strategy and theatres in a save, both ways, as <c>docs/mechanics/savegame.md</c>, section 16, has them.</summary>
public class SaveAiStrategyTests
{
	private static CGameState World()
	{
		var germany = new CCountry { Tag = new CCountryTag("GER"), Name = "Germany", DefinitionFile = "" };
		var france = new CCountry { Tag = new CCountryTag("FRA"), Name = "France", DefinitionFile = "" };
		return new CGameState(new CDate(1936, 1, 7), new[] { new CProvince(0, "", isSea: false) }, new[] { germany, france });
	}

	[Fact]
	public void A_strategy_is_written_with_its_lists_and_theatres_and_the_two_nothing_holds_empty()
	{
		CGameState state = World();
		CCountry germany = state.Country(new CCountryTag("GER"))!;
		CAiStrategy strategy = germany.Strategy;
		strategy.IsMade = true;
		strategy.Personality = EAiPersonality.Military;
		strategy.DefendProvinces.Append(2613, 220);
		strategy.Threat.Append(new CCountryTag("FRA"), 0);
		strategy.MilitaryAccess.Append(new CCountryTag("SOV"), 3035);
		strategy.MaxSubunits = 920;
		strategy.LandShare = CFixedPoint.Parse("0.797");
		strategy.AirShare = CFixedPoint.Parse("0.05");
		strategy.NavalShare = CFixedPoint.Parse("0.153");
		strategy.ArmorBias = CFixedPoint.Parse("0.33");
		var theatre = new CTheatre(5, germany.Tag) { Headquarters = 0 };
		var front = new CFront { Enemy = new CCountryTag("FRA") };
		front.Provinces.AddRange(new[] { 2490, 2553 });
		theatre.Fronts.Add(front);
		theatre.Fronts.Add(new CFront { IsHot = true });
		theatre.Provinces.Add(2613);
		strategy.Theatres.Add(theatre);
		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		writer.Begin("GER");

		CSaveAiStrategy.Write(writer, state, germany);
		writer.End();

		// Every block's scalars after initialized, the personality among them; military_access after the other lists; an
		// entry the scaling left at nought is written; a front facing nobody has no local_enemy; no key provinces, no key.
		const string Nothing = "\t\tconsolidate=no\r\n\t\tstatic=no\r\n\t\tpersonality=balanced\r\n\t\tmax_subunits=0\r\n" +
			"\t\tland_perc=0.000\r\n\t\tair_perc=0.000\r\n\t\tnaval_perc=0.000\r\n\t\tarmor_bias=0.000\r\n";
		Assert.Equal(
			"GER=\r\n{\r\n\tai_hard_strategy=\r\n\t{\r\n\t\tinitialized=no\r\n" + Nothing + "\t}\r\n" +
			"\tai_event_strategy=\r\n\t{\r\n\t\tinitialized=no\r\n" + Nothing + "\t}\r\n" +
			"\tai=\r\n\t{\r\n\t\tinitialized=yes\r\n\t\tconsolidate=no\r\n\t\tstatic=no\r\n\t\tpersonality=military\r\n" +
			"\t\tmax_subunits=920\r\n\t\tland_perc=0.797\r\n\t\tair_perc=0.050\r\n\t\tnaval_perc=0.153\r\n\t\tarmor_bias=0.330\r\n" +
			"\t\tdefend_prov=\r\n\t\t{\r\n\t\t\tid=2613\r\n\t\t\tvalue=220\r\n\t\t}\r\n" +
			"\t\tthreat=\r\n\t\t{\r\n\t\t\tid=\"FRA\"\r\n\t\t\tvalue=0\r\n\t\t}\r\n" +
			"\t\tmilitary_access=\r\n\t\t{\r\n\t\t\tid=\"SOV\"\r\n\t\t\tvalue=3035\r\n\t\t}\r\n" +
			"\t\tarea_theatre=\r\n\t\t{\r\n\t\t\tid=\r\n\t\t\t{\r\n\t\t\t\tid=5\r\n\t\t\t\ttype=45\r\n\t\t\t}\r\n" +
			"\t\t\tfront=\r\n\t\t\t{\r\n\t\t\t\tprovinces=\r\n\t\t\t\t{\r\n2490 2553 \t\t\t\t}\r\n\t\t\t\tlocal_enemy=\"FRA\"\r\n" +
			"\t\t\t\tfront=no\r\n\t\t\t\tsea=no\r\n\t\t\t}\r\n" +
			"\t\t\tfront=\r\n\t\t\t{\r\n\t\t\t\tprovinces=\r\n\t\t\t\t{\r\n\t\t\t\t}\r\n\t\t\t\tfront=yes\r\n\t\t\t\tsea=no\r\n\t\t\t}\r\n" +
			"\t\t\tprovinces=\r\n\t\t\t{\r\n2613 \t\t\t}\r\n" +
			"\t\t\tunit=\r\n\t\t\t{\r\n\t\t\t\tid=0\r\n\t\t\t\ttype=0\r\n\t\t\t}\r\n\t\t\tcountry=\"GER\"\r\n\t\t\thot=no\r\n\t\t}\r\n\t}\r\n}\r\n",
			writer.Text);
	}

	[Fact]
	public void A_saves_strategy_is_read_as_it_stands()
	{
		CGameState state = World();
		CCountry germany = state.Country(new CCountryTag("GER"))!;
		var report = new CSaveReport();
		ClausewitzNode node = ClausewitzParser.Parse(
			"GER=\r\n{\r\n\tai=\r\n\t{\r\n\t\tinitialized=yes\r\n\t\tconsolidate=no\r\n\t\tpersonality=diplomacy\r\n\t\tmax_subunits=665\r\n" +
			"\t\tthreat=\r\n\t\t{\r\n\t\t\tid=\"FRA\"\r\n\t\t\tvalue=58\r\n\t\t}\r\n\t\tthreat=\r\n\t\t{\r\n\t\t\tid=\"YUG\"\r\n\t\t\tvalue=0\r\n\t\t}\r\n" +
			"\t\tthreat=\r\n\t\t{\r\n\t\t\tid=\"FRA\"\r\n\t\t\tvalue=0\r\n\t\t}\r\n" +
			"\t\tmilitary_access=\r\n\t\t{\r\n\t\t\tid=\"FRA\"\r\n\t\t\tvalue=345\r\n\t\t}\r\n\t}\r\n}\r\n").Get("GER")!;

		CSaveAiStrategy.Read(state, germany, node, new CSaveIds(), report);

		// The keys nothing holds pass without a word; a tag of no country is reported; an entry at nought is kept.
		Assert.True(germany.Strategy.IsMade);
		Assert.Equal(EAiPersonality.Diplomacy, germany.Strategy.Personality);
		Assert.Equal(new[] { (new CCountryTag("FRA"), 58), (new CCountryTag("FRA"), 0) }, germany.Strategy.Threat.Entries.ToArray());
		Assert.Single(report.UnreadKeys);
	}
}
