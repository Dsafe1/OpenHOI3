using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>What standing down gives back, against <c>docs/mechanics/politics.md</c>, section 11.</summary>
public class DemobilisationTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static CSubUnitType Infantry()
	{
		var type = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(30));
		type.Stats.Values.Set(CSubUnitStats.BuildCostManpower, CFixedPoint.Parse("3.33"));
		return type;
	}

	private static (CGameState State, CCountry Germany) World()
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1 };
		germany.Modifier.Set(CUnits.ReservesPenaltySize, CFixedPoint.Parse("-0.5"));
		germany.Manpower = CFixedPoint.FromInt(10);
		var defines = new CDefines();
		defines.Set("country", CDemobilisation.Factor, CFixedPoint.Parse("0.5"));
		var state = new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger } },
			new[] { germany }, defines: defines);
		return (state, germany);
	}

	private static CSubUnit Brigade(CCountry country, bool reserve, string strength)
	{
		var unit = new CUnit(EUnitKind.Division, country.Tag) { Id = country.Units.Count + 1, Location = 1 };
		country.Units.Add(unit);
		CSubUnitType type = Infantry();
		var brigade = new CSubUnit(type) { Unit = unit, IsReserve = reserve, Strength = CFixedPoint.Parse(strength) };
		brigade.Highest = brigade.Strength;
		unit.SubUnits.Add(brigade);
		return brigade;
	}

	[Fact]
	public void AReserveAboveItsPeacetimeShareIsCutToItAndAShareOfItsMenComeBack()
	{
		(CGameState state, CCountry germany) = World();
		CSubUnit reserve = Brigade(germany, reserve: true, "30");

		CDemobilisation.GiveBack(state, germany);

		// 15 over a most of 15: half its max strength, times 3.33 and the factor's half - 0.8325, truncated.
		Assert.Equal(CFixedPoint.FromInt(15), reserve.Strength);
		Assert.Equal(CFixedPoint.FromInt(15), reserve.Highest);
		Assert.Equal(CFixedPoint.Parse("10.832"), germany.Manpower);
	}

	[Fact]
	public void ABrigadeAtOrBelowItsMostIsLeftAlone()
	{
		(CGameState state, CCountry germany) = World();
		CSubUnit line = Brigade(germany, reserve: false, "30");
		CSubUnit reserve = Brigade(germany, reserve: true, "12");
		reserve.Highest = CFixedPoint.FromInt(30);

		CDemobilisation.GiveBack(state, germany);

		Assert.Equal(CFixedPoint.FromInt(30), line.Strength);
		Assert.Equal(CFixedPoint.FromInt(12), reserve.Strength);
		Assert.Equal(CFixedPoint.FromInt(30), reserve.Highest);
		Assert.Equal(CFixedPoint.FromInt(10), germany.Manpower);
	}

	[Fact]
	public void AMobilisedCountrysReservesKeepTheirMen()
	{
		(CGameState state, CCountry germany) = World();
		germany.Mobilised = true;
		CSubUnit reserve = Brigade(germany, reserve: true, "30");

		CDemobilisation.GiveBack(state, germany);

		Assert.Equal(CFixedPoint.FromInt(30), reserve.Strength);
		Assert.Equal(CFixedPoint.FromInt(10), germany.Manpower);
	}
}
