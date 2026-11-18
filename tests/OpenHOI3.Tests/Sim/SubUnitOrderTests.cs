using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A unit's sub units kept in order of their types' <c>priority</c>, highest first and equals in the order they came, as
/// the original puts every one in (<c>docs/mechanics/units.md</c>, section 3). BlackICE's numbers: its standard division
/// headquarters 1, its infantry 83.
/// </summary>
public class SubUnitOrderTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static CSubUnitType Type(string key, string priority)
	{
		var type = new CSubUnitType(key, EUnitBranch.Land);
		type.Stats.Values.Set(CSubUnitType.Priority, CFixedPoint.Parse(priority));
		return type;
	}

	private static CSubUnit Brigade(CSubUnitType type, string name)
	{
		return new CSubUnit(type) { Name = name };
	}

	[Fact]
	public void AHeadquartersOfLowPriorityGoesBehindTheInfantryThoughItCameFirst()
	{
		CSubUnitType headquarters = Type("division_hq_standard", "1");
		CSubUnitType infantry = Type("infantry_brigade", "83");
		var division = new CUnit(EUnitKind.Division, Ger);

		division.AddSubUnit(Brigade(headquarters, "Stab"));
		division.AddSubUnit(Brigade(infantry, "IR 1"));
		division.AddSubUnit(Brigade(infantry, "IR 2"));

		Assert.Equal(new[] { "IR 1", "IR 2", "Stab" }, division.SubUnits.Select(brigade => brigade.Name));
	}

	[Fact]
	public void EqualsKeepTheOrderTheyCameAndAHigherOneGoesBeforeAllOfThem()
	{
		CSubUnitType infantry = Type("infantry_brigade", "7");
		CSubUnitType gurkha = Type("gurkha_brigade", "9");
		CSubUnitType artillery = Type("artillery_brigade", "3");
		var division = new CUnit(EUnitKind.Division, Ger);

		division.AddSubUnit(Brigade(infantry, "9th"));
		division.AddSubUnit(Brigade(artillery, "Guns"));
		division.AddSubUnit(Brigade(infantry, "10th"));
		division.AddSubUnit(Brigade(gurkha, "3rd Gurkha"));
		division.AddSubUnit(Brigade(gurkha, "4th Gurkha"));

		Assert.Equal(new[] { "3rd Gurkha", "4th Gurkha", "9th", "10th", "Guns" },
			division.SubUnits.Select(brigade => brigade.Name));
	}

	[Fact]
	public void APriorityIsAWholeNumberCutTowardsNought()
	{
		CSubUnitType whole = Type("whole", "2");
		CSubUnitType fraction = Type("fraction", "2.9");
		CSubUnitType negative = Type("negative", "-1.5");
		var unit = new CUnit(EUnitKind.Division, Ger);

		unit.AddSubUnit(Brigade(whole, "whole"));
		unit.AddSubUnit(Brigade(fraction, "fraction"));
		unit.AddSubUnit(Brigade(negative, "negative"));

		Assert.Equal(2, unit.SubUnits[1].Priority);
		Assert.Equal(-1, unit.SubUnits[2].Priority);
		// 2.9 reads as 2, the same as the first, and so goes in after it.
		Assert.Equal(new[] { "whole", "fraction", "negative" }, unit.SubUnits.Select(brigade => brigade.Name));
	}

	[Fact]
	public void ATypeWithNoPriorityIsNoughtAndGoesLast()
	{
		var plain = new CSubUnitType("plain", EUnitBranch.Naval);
		CSubUnitType carrier = Type("carrier", "10");
		var fleet = new CUnit(EUnitKind.Navy, Ger);

		fleet.AddSubUnit(Brigade(plain, "Plain"));
		fleet.AddSubUnit(Brigade(carrier, "Ark"));

		Assert.Equal(new[] { "Ark", "Plain" }, fleet.SubUnits.Select(ship => ship.Name));
	}
}
