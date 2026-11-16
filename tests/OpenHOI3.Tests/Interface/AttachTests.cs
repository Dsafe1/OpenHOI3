using System.Linq;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>Attaching, detaching and a new headquarters, against <c>docs/mechanics/attaching.md</c>.</summary>
public class AttachTests
{
	private static readonly CCountryTag Eng = new("ENG");

	private static (CGameState State, CCountry England) World(long manpower = 100)
	{
		var england = new CCountry { Tag = Eng, Name = "England", DefinitionFile = "", Manpower = CFixedPoint.FromInt((int)manpower) };
		var provinces = new[]
		{
			new CProvince(0, "", isSea: false),
			new CProvince(1, "London", isSea: false) { CentreX = 100, CentreY = 100 },
			new CProvince(2, "York", isSea: false) { CentreX = 110, CentreY = 100 },
			new CProvince(3, "Glasgow", isSea: false) { CentreX = 200, CentreY = 100 },
		};
		var hq = new CSubUnitType(CTheatreHeadquarters.HqBrigade, EUnitBranch.Land);
		hq.Stats.Values.Add(CSubUnitStats.BuildCostManpower, CFixedPoint.FromInt(2));
		hq.Stats.Values.Add(CSubUnitType.MaxStrength, CFixedPoint.FromInt(100));
		var state = new CGameState(new CDate(1936, 1, 1), provinces, new[] { england })
		{
			MapWidth = 18000,
			SubUnitTypes = new System.Collections.Generic.Dictionary<string, CSubUnitType> { [CTheatreHeadquarters.HqBrigade] = hq },
		};
		return (state, england);
	}

	private static CUnit Unit(CCountry country, EUnitKind kind, int id, int province = 1, CUnit? parent = null)
	{
		var unit = new CUnit(kind, Eng) { Id = id, Location = province, Name = "Unit " + id, Parent = parent };
		if (parent is null)
		{
			country.Units.Add(unit);
		}
		else
		{
			parent.Children.Add(unit);
		}
		return unit;
	}

	[Fact]
	public void ALandUnitIsOfferedItsLowestRanksFirstAndTheNearestFirst()
	{
		(CGameState state, CCountry england) = World();
		CUnit division = Unit(england, EUnitKind.Division, 1);
		CUnit far = Unit(england, EUnitKind.Corps, 2, province: 3);
		CUnit near = Unit(england, EUnitKind.Corps, 3, province: 2);
		CUnit army = Unit(england, EUnitKind.Army, 4, province: 3);
		Assert.Equal(new[] { near, far, army }, CAttachText.Candidates(state, england, division));
	}

	[Fact]
	public void TheDistanceIsAskedEvenWhereTheRanksSayTheOtherWay()
	{
		(CGameState state, CCountry england) = World();
		CUnit division = Unit(england, EUnitKind.Division, 1);
		CUnit corps = Unit(england, EUnitKind.Corps, 2, province: 2);
		CUnit army = Unit(england, EUnitKind.Army, 3, province: 1);
		Assert.Equal(new[] { army, corps }, CAttachText.Candidates(state, england, division));
	}

	[Fact]
	public void TheRowsLineSaysWhereAndHowFar()
	{
		(CGameState state, CCountry england) = World();
		CUnit division = Unit(england, EUnitKind.Division, 1);
		CUnit corps = Unit(england, EUnitKind.Corps, 2, province: 2);
		Assert.Equal("York (40km)", CAttachText.Location(state, division, corps));
	}

	[Fact]
	public void NothingAboveMeansNoAttaching()
	{
		(_, CCountry england) = World();
		CUnit division = Unit(england, EUnitKind.Division, 1);
		Assert.False(CAttachText.MayAttach(england, division));
		Unit(england, EUnitKind.Corps, 2);
		Assert.True(CAttachText.MayAttach(england, division));
	}

	[Fact]
	public void AHeadquartersMayNotBeMadeUnderACommanderOneLevelUp()
	{
		(CGameState state, CCountry england) = World();
		CUnit corps = Unit(england, EUnitKind.Corps, 1);
		CUnit under = Unit(england, EUnitKind.Division, 2, parent: corps);
		CUnit army = Unit(england, EUnitKind.Army, 3);
		CUnit underArmy = Unit(england, EUnitKind.Division, 4, parent: army);
		Assert.False(CAttachText.MayCreateHigher(state, england, under));
		Assert.True(CAttachText.MayCreateHigher(state, england, underArmy));
		Assert.True(CAttachText.MayCreateHigher(state, england, corps));
	}

	[Fact]
	public void AHeadquartersWantsAWholeBrigadesMen()
	{
		(CGameState state, CCountry england) = World(manpower: 1);
		Assert.False(CAttachText.MayCreateHigher(state, england, Unit(england, EUnitKind.Division, 1)));
	}

	[Fact]
	public void DetachingPutsAUnitAtTheTopWithWhatIsUnderIt()
	{
		(CGameState state, CCountry england) = World();
		CUnit army = Unit(england, EUnitKind.Army, 1);
		CUnit corps = Unit(england, EUnitKind.Corps, 2, parent: army);
		CUnit division = Unit(england, EUnitKind.Division, 3, parent: corps);
		new CDetachUnitCommand(Eng, 2).Execute(state);
		Assert.Null(corps.Parent);
		Assert.Empty(army.Children);
		Assert.Contains(corps, england.Units);
		Assert.Equal(division, Assert.Single(corps.Children));
	}

	[Fact]
	public void SeveralDetachedLeaveOutATheatre()
	{
		(_, CCountry england) = World();
		CUnit theatre = Unit(england, EUnitKind.Theatre, 1);
		CUnit division = Unit(england, EUnitKind.Division, 2);
		Assert.Equal(division, Assert.Single(CAttachText.Detachable(new[] { theatre, division })));
	}

	[Fact]
	public void SeveralGivenAHeadquartersMayBeRefusedByTheCommandersCommander()
	{
		(_, CCountry england) = World();
		CUnit theatre = Unit(england, EUnitKind.Theatre, 1);
		CUnit corps = Unit(england, EUnitKind.Corps, 2, parent: theatre);
		CUnit division = Unit(england, EUnitKind.Division, 3, parent: corps);
		Assert.Equal(2, CAttachText.ForHigher(new[] { division, corps }).Count);
		CUnit deep = Unit(england, EUnitKind.Division, 4, parent: division);
		var odd = new CUnit(EUnitKind.Air, Eng) { Id = 5, Parent = deep };
		Assert.Empty(CAttachText.ForHigher(new[] { odd }));
	}

	[Fact]
	public void ANewHeadquartersTakesEveryUnitItMayAndChargesATwentieth()
	{
		(CGameState state, CCountry england) = World();
		CUnit first = Unit(england, EUnitKind.Division, 1);
		CUnit second = Unit(england, EUnitKind.Division, 2);
		state.LastUnitId = 10;
		var command = new CCreateHigherCommand(Eng, new[] { 1, 2 });
		Assert.True(command.MayExecute(state));
		command.Execute(state);
		CUnit corps = Assert.Single(england.Units);
		Assert.Equal(EUnitKind.Corps, corps.Kind);
		Assert.Equal(new[] { first, second }, corps.Children);
		Assert.Equal(11, corps.Id);
		Assert.Equal(13, state.LastUnitId);
		Assert.Equal(CFixedPoint.FromInt(100).Raw - 100, england.Manpower.Raw);
	}

	[Fact]
	public void SeveralUnitsWantABrigadesMenEach()
	{
		(CGameState state, CCountry england) = World(manpower: 3);
		Unit(england, EUnitKind.Division, 1);
		Unit(england, EUnitKind.Division, 2);
		Assert.False(new CCreateHigherCommand(Eng, new[] { 1, 2 }).MayExecute(state));
		Assert.True(new CCreateHigherCommand(Eng, new[] { 1 }).MayExecute(state));
	}

	[Fact]
	public void ATheatreMayNotGoHigher()
	{
		(CGameState state, CCountry england) = World();
		Unit(england, EUnitKind.Theatre, 1);
		Assert.False(new CCreateHigherCommand(Eng, new[] { 1 }).MayExecute(state));
	}

	[Fact]
	public void AnArmyGroupsHeadquartersIsATheatreWithNothingInIt()
	{
		(CGameState state, CCountry england) = World();
		Unit(england, EUnitKind.ArmyGroup, 1);
		state.NextTheatreId = 7;
		new CCreateHigherCommand(Eng, new[] { 1 }).Execute(state);
		CUnit theatre = Assert.Single(england.Units);
		Assert.Equal(EUnitKind.Theatre, theatre.Kind);
		CTheatre made = Assert.Single(england.Strategy.Theatres);
		Assert.Equal(7, made.Id);
		Assert.Equal(theatre.Id, made.Headquarters);
		Assert.Empty(made.Provinces);
	}
}
