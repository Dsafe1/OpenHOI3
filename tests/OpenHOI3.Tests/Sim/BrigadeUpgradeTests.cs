using System.Linq;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>A brigade changed into another type, against <c>docs/mechanics/upgrades.md</c>.</summary>
public class BrigadeUpgradeTests
{
	private static CSubUnitType Type(string key, int index) => new(key, EUnitBranch.Land) { DatabaseIndex = index };

	[Fact]
	public void TheFilesOrderComesFirstAndThenWhatThoseMayBecome()
	{
		CSubUnitType a = Type("a", 1), b = Type("b", 2), c = Type("c", 3), d = Type("d", 4);
		a.UpgradeTargets.Add(b);
		b.UpgradeTargets.Add(c);
		b.UpgradeTargets.Add(d);
		CUnitUpgradeReader.Close(new[] { a, b, c, d });
		Assert.Equal(new[] { b, c, d }, a.UpgradeTargets);
	}

	[Fact]
	public void ACycleLeavesATypeOnItsOwnList()
	{
		CSubUnitType a = Type("a", 1), b = Type("b", 2);
		a.UpgradeTargets.Add(b);
		b.UpgradeTargets.Add(a);
		CUnitUpgradeReader.Close(new[] { a, b });
		Assert.Contains(a, a.UpgradeTargets);
	}

	[Fact]
	public void TheLineThatBringsTheNewBrigadeNamesItsOwnerItsBuilder()
	{
		var country = new CCountry { Tag = new CCountryTag("GER"), Name = "Germany", DefinitionFile = "", Capital = 1 };
		CSubUnitType to = Priced("to", "4", "100"), from = Priced("from", "2", "50");
		var state = new CGameState(new CDate(1936, 1, 1),
			new[] { new OpenHOI3.Sim.Provinces.CProvince(0, "", isSea: false), new OpenHOI3.Sim.Provinces.CProvince(1, "Berlin", isSea: false) },
			new[] { country })
		{
			SubUnitTypes = new System.Collections.Generic.Dictionary<string, CSubUnitType> { ["to"] = to, ["from"] = from },
		};
		var unit = new CUnit(EUnitKind.Division, country.Tag) { Id = 1, Location = 1 };
		var brigade = new CSubUnit(from) { Id = 2, Unit = unit, Name = "1st" };
		unit.SubUnits.Add(brigade);
		country.Units.Add(unit);

		new CUpgradeRegimentCommand(country.Tag, brigade.Id, "to").Execute(state);

		Assert.Equal(country.Tag, country.Constructions.Single().Builder);
	}

	private static CConstruction Line(CCountry country, CSubUnitType to, CSubUnitType from, long strength, string cost, string time, string practical)
	{
		var line = new CConstruction(country.Tag);
		line.Brigades.Add(new CBrigadeOrder(to)
		{
			Upgrade = new CBrigadeUpgradeOrder(from, strength, CFixedPoint.Parse(cost), CFixedPoint.Parse(time), CFixedPoint.Parse(practical)),
		});
		return line;
	}

	private static CSubUnitType Priced(string key, string cost, string days)
	{
		CSubUnitType type = Type(key, 1);
		type.Stats.Values.Set(CSubUnitStats.BuildCostIc, CFixedPoint.Parse(cost));
		type.Stats.Values.Set(CSubUnitStats.BuildTime, CFixedPoint.Parse(days));
		return type;
	}

	[Fact]
	public void TheLineCostsTheNewBrigadeLessAShareOfTheOldByItsStrength()
	{
		var country = new CCountry { Tag = new CCountryTag("GER"), Name = "Germany", DefinitionFile = "" };
		CSubUnitType to = Priced("to", "4", "100"), from = Priced("from", "2", "50");
		CConstruction full = Line(country, to, from, 1000, "0.4", "0.7", "0.9");
		CBuildQueue.Recalculate(country, full);
		CConstruction weak = Line(country, to, from, 500, "0.4", "0.7", "0.9");
		CBuildQueue.Recalculate(country, weak);
		Assert.True(full.Cost < weak.Cost);
		Assert.Equal(CFixedPoint.Parse("3.2"), full.Cost);
		Assert.Equal(CFixedPoint.Parse("65"), full.Duration);
		Assert.Equal(CFixedPoint.Parse("0.72"), full.PracticalFactor);
	}

	[Fact]
	public void AnOldBrigadeWorthMoreThanTheNewCostsNothing()
	{
		var country = new CCountry { Tag = new CCountryTag("GER"), Name = "Germany", DefinitionFile = "" };
		CConstruction line = Line(country, Priced("to", "1", "10"), Priced("from", "9", "50"), 1000, "0.4", "0.7", "0.9");
		CBuildQueue.Recalculate(country, line);
		Assert.Equal(CFixedPoint.Zero, line.Cost);
		Assert.Equal(CFixedPoint.Zero, line.Duration);
		Assert.Equal(CFixedPoint.Zero, line.PracticalFactor);
	}
}
