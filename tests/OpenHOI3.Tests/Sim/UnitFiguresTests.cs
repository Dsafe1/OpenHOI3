using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The two numbers on a unit's counter, against <c>docs/mechanics/mapunits.md</c>,
/// section 4.
/// </summary>
public class UnitFiguresTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static CSubUnitType Type(string key, EUnitBranch branch, params (string Statistic, string Value)[] figures)
	{
		var type = new CSubUnitType(key, branch);
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(100));
		type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.FromInt(30));
		foreach ((string statistic, string value) in figures)
		{
			type.Stats.Values.Set(statistic, CFixedPoint.Parse(value));
		}
		return type;
	}

	private static CUnit Unit(EUnitKind kind, params CSubUnitType[] types)
	{
		var unit = new CUnit(kind, Ger);
		foreach (CSubUnitType type in types)
		{
			unit.SubUnits.Add(new CSubUnit(type) { Unit = unit });
		}
		return unit;
	}

	[Fact]
	public void ADivisionsAttackIsItsSoftAndHardAttackOverSixAndItsDefenceCountsToughnessTwice()
	{
		// The base game's infantry of 1936, near enough.
		CSubUnitType infantry = Type("infantry_brigade", EUnitBranch.Land,
			("soft_attack", "1.67"), ("hard_attack", "0.33"), ("defensiveness", "4.33"), ("toughness", "3.33"),
			("combat_width", "1"));
		CUnit division = Unit(EUnitKind.Division, infantry, infantry, infantry);

		Assert.Equal(1, CUnitFigures.Attack(division));        // 6.00 over 6
		Assert.Equal(2, CUnitFigures.Defence(division));       // 12.99 and twice 9.99, over 16: 2.06
	}

	[Fact]
	public void AFigureIsCutThenRoundedWithAHalfGoingToTheEvenNumberAndIsNeverOverNine()
	{
		CSubUnitType Soft(string attack) => Type("x", EUnitBranch.Land, ("soft_attack", attack), ("combat_width", "1"));

		Assert.Equal(0, CUnitFigures.Attack(Unit(EUnitKind.Division, Soft("2.999"))));    // 0.499 cut
		Assert.Equal(0, CUnitFigures.Attack(Unit(EUnitKind.Division, Soft("3"))));        // 0.5, to the even
		Assert.Equal(1, CUnitFigures.Attack(Unit(EUnitKind.Division, Soft("3.006"))));    // 0.501
		Assert.Equal(2, CUnitFigures.Attack(Unit(EUnitKind.Division, Soft("9"))));        // 1.5, to the even
		Assert.Equal(2, CUnitFigures.Attack(Unit(EUnitKind.Division, Soft("15"))));       // 2.5, to the even
		Assert.Equal(9, CUnitFigures.Attack(Unit(EUnitKind.Division, Soft("600"))));
		Assert.Equal(0, CUnitFigures.Attack(Unit(EUnitKind.Division)));
	}

	[Fact]
	public void AFleetShowsItsSeaFiguresOverTwentyFiveAndAnAirUnitItsAirFiguresOverTen()
	{
		CSubUnitType battleship = Type("battleship", EUnitBranch.Naval, ("sea_attack", "60"), ("sea_defence", "40"));
		CSubUnitType fighter = Type("interceptor", EUnitBranch.Air, ("air_attack", "12"), ("air_defence", "26"));

		CUnit fleet = Unit(EUnitKind.Navy, battleship, battleship);
		Assert.Equal(5, CUnitFigures.Attack(fleet));           // 120 over 25: 4.8
		Assert.Equal(3, CUnitFigures.Defence(fleet));          // 80 over 25: 3.2

		CUnit wing = Unit(EUnitKind.Air, fighter);
		Assert.Equal(1, CUnitFigures.Attack(wing));
		Assert.Equal(3, CUnitFigures.Defence(wing));           // 2.6
	}

	[Fact]
	public void ASupportBrigadeAllButDestroyedAddsNothingToItsDivision()
	{
		CSubUnitType infantry = Type("infantry_brigade", EUnitBranch.Land, ("soft_attack", "6"), ("combat_width", "1"));
		CSubUnitType engineers = Type("engineer_brigade", EUnitBranch.Land, ("soft_attack", "6"));
		CUnit division = Unit(EUnitKind.Division, infantry, engineers);
		Assert.Equal(2, CUnitFigures.Attack(division));

		division.SubUnits[1].Organisation = CFixedPoint.Parse("0.5");
		Assert.Equal(1, CUnitFigures.Attack(division));

		// A brigade that holds frontage counts however little is left of it.
		division.SubUnits[0].Organisation = CFixedPoint.Zero;
		Assert.Equal(1, CUnitFigures.Attack(division));
	}
}
