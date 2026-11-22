using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The card of a whole unit, against <c>docs/mechanics/brigadecard.md</c>, section 7: each figure made from the
/// brigades' as the game makes it, and the rows of what the unit cost.
/// </summary>
public class UnitCardTextTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static string Localise(string key) => key;

	private static CSubUnit Brigade(CUnit unit, string type, string strength, params (string Statistic, string Value)[] figures)
	{
		var subUnitType = new CSubUnitType(type, EUnitBranch.Land);
		foreach ((string statistic, string value) in figures)
		{
			subUnitType.Stats.Values.Set(statistic, CFixedPoint.Parse(value));
		}
		var subUnit = new CSubUnit(subUnitType) { Id = unit.SubUnits.Count + 10, Unit = unit, Name = type, Strength = CFixedPoint.Parse(strength) };
		subUnit.Stats.CopyFrom(subUnitType.Stats);
		unit.SubUnits.Add(subUnit);
		return subUnit;
	}

	private static (CGameState State, CCountry Country, CUnit Unit) Division()
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Stettin", isSea: false) },
			new[] { germany });
		var unit = new CUnit(EUnitKind.Division, Ger) { Id = 1, Location = 1, Name = "12. Infanterie-Division" };
		germany.Units.Add(unit);
		Brigade(unit, "hq_brigade", "3.2", (CSubUnitType.MaxStrength, "4"), (CSubUnitType.DefaultOrganisation, "80"), ("default_morale", "0.7"),
			("radio_strength", "1.5"), ("maximum_speed", "9"), ("softness", "0.5"), ("soft_attack", "1"), ("toughness", "2.5"),
			("defensiveness", "2.5"), ("ap_attack", "1"), ("armor_value", "0"), (CSubUnitType.CombatWidth, "0"),
			(CSubUnitType.Officers, "20"), (CSubUnitStats.BuildCostIc, "1.5"), (CSubUnitStats.BuildCostManpower, "0.4"), (CSubUnitStats.BuildTime, "60"));
		Brigade(unit, "infantry_brigade", "72", (CSubUnitType.MaxStrength, "90"), (CSubUnitType.DefaultOrganisation, "60"), ("default_morale", "0.5"),
			("radio_strength", "0.5"), ("maximum_speed", "4"), ("softness", "1"), ("soft_attack", "12.25"), ("toughness", "20"),
			("defensiveness", "30"), ("ap_attack", "3"), ("armor_value", "2"), (CSubUnitType.CombatWidth, "3"),
			(CSubUnitType.Officers, "300"), (CSubUnitStats.BuildCostIc, "6"), (CSubUnitStats.BuildCostManpower, "9"), (CSubUnitStats.BuildTime, "120"));
		return (state, germany, unit);
	}

	private static string Value(List<(string Label, string Value)> rows, string label) => rows.First(row => row.Label == label).Value;

	[Fact]
	public void AUnitsFiguresAreMadeFromItsBrigadesAsTheGameMakesThem()
	{
		(CGameState state, CCountry germany, CUnit unit) = Division();
		List<(string Label, string Value)> rows = CUnitCardText.Rows(Localise, state, germany, unit);

		Assert.Equal(("UNIT_STATS", ""), rows[0]);
		// Men added up; organisation and morale averaged; the first brigade's radio; the slowest brigade's speed.
		Assert.Equal("7,520", Value(rows, "BV_STRENGTH"));
		Assert.Equal("9,400", Value(rows, "MAX_STRENGTH"));
		Assert.Equal("70.0", Value(rows, "DEFAULT_ORG"));
		Assert.Equal("60.0%", Value(rows, "DEFAULT_MORALE"));
		Assert.Equal("1.50", Value(rows, "RADIO_STRENGTH"));
		Assert.Equal("4.00KPH", Value(rows, "MAXIMUM_SPEED"));
		// Attacks and defences added up, toughness twice as a battle counts it; softness averaged; the best piercing
		// and armour; the widths together.
		Assert.Equal("13.25", Value(rows, "SOFT_ATTACK"));
		Assert.Equal("32.50", Value(rows, "DEFENSIVENESS"));
		Assert.Equal("45.00", Value(rows, "TOUGHNESS"));
		Assert.Equal("75%", Value(rows, "SOFTNESS"));
		Assert.Equal("3.00", Value(rows, "AP_ATTACK"));
		Assert.Equal("2.00", Value(rows, "ARMOR_VALUE"));
		Assert.Equal("3.00", Value(rows, "COMBAT_WIDTH"));
	}

	[Fact]
	public void TheCardSaysWhatTheUnitCostWhichNoBrigadesCardSays()
	{
		(CGameState state, CCountry germany, CUnit unit) = Division();
		List<(string Label, string Value)> rows = CUnitCardText.Rows(Localise, state, germany, unit);

		int cost = rows.FindIndex(row => row.Label == CUnitCardText.Cost);
		Assert.Equal(("", ""), rows[cost - 1]);
		Assert.Equal("320", Value(rows, CUnitCardText.Officers));
		Assert.Equal("9.40", Value(rows, CUnitCardText.Manpower));
		Assert.Equal("7.50", Value(rows, CUnitCardText.BuildCost));
		// Built side by side, it takes as long as its longest brigade.
		Assert.Equal("120", Value(rows, CUnitCardText.BuildTime));
	}

	[Fact]
	public void ACircumstanceIsTheBrigadesOverHowManyThereAre()
	{
		(CGameState state, CCountry germany, CUnit unit) = Division();
		var night = new CModifier();
		night.Set("attack", CFixedPoint.Parse("-0.5"));
		unit.SubUnits[1].Stats.Adjusters["night"] = night;

		List<(string Label, string Value)> rows = CUnitCardText.Rows(Localise, state, germany, unit);
		Assert.Equal("-25.00%", Value(rows, "NIGHT_ADJUSTER UA_ATTACK"));
		Assert.DoesNotContain(rows, row => row.Label == "NIGHT_ADJUSTER UA_DEFENCE");
	}

	[Fact]
	public void TheBrigadeShownIsTheUnitsFirstAndAUnitWithNoneHasNoCard()
	{
		(_, _, CUnit unit) = Division();
		Assert.Same(unit.SubUnits[0], CUnitCardText.Shown(unit));
		Assert.True(CUnitCardText.HasCard(unit));

		var corps = new CUnit(EUnitKind.Corps, Ger) { Id = 2, Name = "II. A.K." };
		Assert.Null(CUnitCardText.Shown(corps));
		Assert.False(CUnitCardText.HasCard(corps));
	}
}
