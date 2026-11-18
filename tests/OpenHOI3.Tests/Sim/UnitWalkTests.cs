using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The walk of a country's units: each before what its ships carry and then the units under it, live as it goes - what
/// is done to a unit as it is met is what the walk then finds below it.
/// </summary>
public class UnitWalkTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CSubUnitType Destroyer = new("destroyer", EUnitBranch.Naval);

	private static CUnit Unit(EUnitKind kind, string name, CUnit? parent = null)
	{
		var unit = new CUnit(kind, Ger) { Name = name, Parent = parent };
		parent?.Children.Add(unit);
		return unit;
	}

	private static string Names(IEnumerable<CUnit> units) => string.Join(" ", units.Select(unit => unit.Name));

	[Fact]
	public void EachUnitComesBeforeWhatItsShipsCarryAndThenTheUnitsUnderIt()
	{
		var country = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		CUnit army = Unit(EUnitKind.Army, "army");
		Unit(EUnitKind.Division, "first", Unit(EUnitKind.Corps, "corps", army));
		Unit(EUnitKind.Division, "second", army);
		CUnit fleet = Unit(EUnitKind.Navy, "fleet");
		var carrier = new CSubUnit(Destroyer) { Unit = fleet };
		fleet.SubUnits.Add(carrier);
		CUnit group = Unit(EUnitKind.Air, "group");
		carrier.Carried.Add(group);
		CUnit underAWing = Unit(EUnitKind.Air, "underAWing", Unit(EUnitKind.Navy, "other"));
		carrier.Carried.Add(underAWing);   // met under its own superior, not through the ship
		Unit(EUnitKind.Division, "underFleet", fleet);
		country.Units.AddRange(new[] { army, fleet });

		Assert.Equal("army corps first second fleet group underFleet", Names(country.AllUnits()));
		Assert.Equal("fleet group underFleet", Names(fleet.WithAllBelow()));
	}

	[Fact]
	public void AUnitGivenToAUnitAsTheWalkMeetsItIsWalkedAndOneTakenOutIsNot()
	{
		var country = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		CUnit corps = Unit(EUnitKind.Corps, "corps");
		CUnit taken = Unit(EUnitKind.Division, "taken", corps);
		country.Units.Add(corps);
		var met = new List<string>();

		foreach (CUnit unit in country.AllUnits())
		{
			met.Add(unit.Name);
			if (unit == corps)
			{
				corps.Children.Remove(taken);
				Unit(EUnitKind.Division, "given", corps);
			}
		}

		Assert.Equal("corps given", string.Join(" ", met));
	}

	[Fact]
	public void WalksInsideWalksAndWalksLeftEarlyEachGoTheirOwnWay()
	{
		var country = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		CUnit corps = Unit(EUnitKind.Corps, "corps");
		Unit(EUnitKind.Division, "a", corps);
		Unit(EUnitKind.Division, "b", corps);
		country.Units.Add(corps);

		Assert.Equal("corps", country.AllUnits().First().Name);
		var pairs = new List<string>();
		foreach (CUnit outer in country.AllUnits())
		{
			foreach (CUnit inner in country.AllUnits())
			{
				pairs.Add(outer.Name + inner.Name);
			}
		}

		Assert.Equal(9, pairs.Count);
		Assert.Equal("corpscorps", pairs[0]);
		Assert.Equal("bb", pairs[8]);
		Assert.Equal("corps a b", Names(country.AllUnits()));
	}

	[Fact]
	public void TheDefaultWalkMeetsNothing()
	{
		Assert.Empty(default(CUnitWalk));
	}
}
