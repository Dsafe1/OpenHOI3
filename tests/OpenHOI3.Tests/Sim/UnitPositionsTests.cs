using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The table of who stands where, kept between hours: a unit placed or made is in it at once, the hour's check gives back
/// what making it again would, and a change made without telling it is found by the check.
/// </summary>
public class UnitPositionsTests
{
	private static readonly CCountryTag Ger = new("GER");

	private const int Berlin = 1;
	private const int Hamburg = 2;

	private static readonly CSubUnitType Infantry = Type();

	private static CSubUnitType Type()
	{
		var type = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(100));
		type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.FromInt(30));
		type.Stats.Values.Set(CSubUnitType.CombatWidth, CFixedPoint.One);
		return type;
	}

	private static CGameState State(out CCountry germany)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		return new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(Berlin, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
				new CProvince(Hamburg, "Hamburg", isSea: false) { Owner = Ger, Controller = Ger },
			},
			new[] { germany });
	}

	private static CUnit Unit(CGameState state, CCountry country, EUnitKind kind, int brigades, int location = Berlin)
	{
		var unit = new CUnit(kind, country.Tag) { Id = ++state.LastUnitId, Location = location, Name = $"unit {state.LastUnitId}" };
		for (int i = 0; i < brigades; i++)
		{
			unit.AddSubUnit(new CSubUnit(Infantry) { Id = ++state.LastUnitId, Unit = unit, Name = "infantry" });
		}
		country.Units.Add(unit);
		return unit;
	}

	/// <summary>A unit finished and waiting to be placed.</summary>
	private static CUnit Waiting(CGameState state, CCountry country, int brigades)
	{
		CUnit unit = Unit(state, country, EUnitKind.Division, brigades, location: 0);
		country.Units.Remove(unit);
		country.Deployments.Add(unit);
		return unit;
	}

	[Fact]
	public void AUnitPlacedStandsInItsProvinceAtOnceAndLast()
	{
		CGameState state = State(out CCountry germany);
		CUnit there = Unit(state, germany, EUnitKind.Division, 2);
		Assert.Equal(new[] { there }, state.UnitsIn(Berlin));
		CUnit placed = Waiting(state, germany, 2);

		CDeployment.Place(state, germany, placed, Berlin);

		Assert.Equal(new[] { there, placed }, state.UnitsIn(Berlin));
	}

	[Fact]
	public void AUnitJoiningOneInTheFieldStandsThereAndALoneBrigadeGoingIntoItDoesNot()
	{
		CGameState state = State(out CCountry germany);
		CUnit corps = Unit(state, germany, EUnitKind.Corps, 1);
		CUnit division = Unit(state, germany, EUnitKind.Division, 2, location: Hamburg);
		Assert.Equal(new[] { corps }, state.UnitsIn(Berlin));
		CUnit joining = Waiting(state, germany, 2);
		CUnit brigade = Waiting(state, germany, 1);

		CDeployment.Join(state, germany, joining, corps);
		CDeployment.Join(state, germany, brigade, division);

		Assert.Equal(new[] { corps, joining }, state.UnitsIn(Berlin));
		Assert.Equal(new[] { division }, state.UnitsIn(Hamburg));
		Assert.Equal(3, division.SubUnits.Count);
	}

	[Fact]
	public void UnitsMadeInOneProvinceComeInTheOrderTheUnitsAreWalkedAsMakingTheTableAgainWouldGive()
	{
		CGameState state = State(out CCountry germany);
		CUnit corps = Unit(state, germany, EUnitKind.Corps, 1);
		CUnit other = Unit(state, germany, EUnitKind.Corps, 1);
		Assert.Equal(new[] { corps, other }, state.UnitsIn(Berlin));

		// Made the second under the first corps and the first under the second, so the walk meets them the other way round.
		CUnit first = Unit(state, germany, EUnitKind.Division, 1);
		germany.Units.Remove(first);
		first.Parent = other;
		other.Children.Add(first);
		state.AddToPositions(first);
		CUnit second = Unit(state, germany, EUnitKind.Division, 1);
		germany.Units.Remove(second);
		second.Parent = corps;
		corps.Children.Add(second);
		state.AddToPositions(second);

		Assert.Equal(new[] { corps, other, second, first }, state.UnitsIn(Berlin));
	}

	[Fact]
	public void TheHoursCheckKeepsTheOrderAndFindsAUnitPutSomewhereWithoutTellingTheTable()
	{
		CGameState state = State(out CCountry germany);
		CUnit a = Unit(state, germany, EUnitKind.Division, 1);
		CUnit b = Unit(state, germany, EUnitKind.Division, 1);
		CUnit c = Unit(state, germany, EUnitKind.Division, 1);
		state.Positions.Move(a, Berlin, Hamburg);
		state.Positions.Move(a, Hamburg, Berlin);
		Assert.Equal(new[] { b, c, a }, state.UnitsIn(Berlin));

		state.RecheckPositions();
		Assert.Equal(new[] { b, c, a }, state.UnitsIn(Berlin));

		// Moved behind the table's back, as nothing in the game should: the next hour's check finds it.
		b.Location = Hamburg;
		Assert.Equal(new[] { b, c, a }, state.UnitsIn(Berlin));
		state.RecheckPositions();
		Assert.Equal(new[] { c, a }, state.UnitsIn(Berlin));
		Assert.Equal(new[] { b }, state.UnitsIn(Hamburg));
	}
}
