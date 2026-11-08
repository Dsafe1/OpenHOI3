using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>A unit left with no brigades, against <c>docs/mechanics/reorganise.md</c>, section 5.</summary>
public class EmptyUnitTests
{
	private static readonly CCountryTag Eng = new("ENG");

	private static (CGameState State, CCountry England) World()
	{
		var england = new CCountry { Tag = Eng, Name = "England", DefinitionFile = "" };
		var provinces = new[] { new CProvince(0, "", isSea: false), new CProvince(1, "London", isSea: false) };
		return (new CGameState(new CDate(1936, 1, 1), provinces, new[] { england }), england);
	}

	private static CUnit Unit(CCountry country, int id, int brigades = 0)
	{
		var unit = new CUnit(EUnitKind.Division, Eng) { Id = id, Location = 1 };
		var type = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		for (int i = 0; i < brigades; i++)
		{
			unit.SubUnits.Add(new CSubUnit(type) { Id = id * 10 + i, Unit = unit });
		}
		country.Units.Add(unit);
		return unit;
	}

	[Fact]
	public void AnEmptyUnitNotAllowedSoIsToGo()
	{
		(_, CCountry england) = World();
		Assert.True(CEmptyUnits.IsToGo(Unit(england, 1)));
		Assert.False(CEmptyUnits.IsToGo(Unit(england, 2, brigades: 1)));
		Assert.False(CEmptyUnits.IsToGo(new CUnit(EUnitKind.Division, Eng) { IsAllowedEmpty = true }));
	}

	[Fact]
	public void TheHourDisbandsANobodysEmptyUnit()
	{
		(CGameState state, CCountry england) = World();
		Unit(england, 1);
		Unit(england, 2, brigades: 1);
		CEmptyUnits.RunHourly(state);
		Assert.IsType<CDisbandCommand>(Assert.Single(state.Outbox));
	}

	[Fact]
	public void TheHourLeavesAPlayedCountryToItsPlayer()
	{
		(CGameState state, CCountry england) = World();
		Unit(england, 1);
		state.Played.Add(Eng);
		CEmptyUnits.RunHourly(state);
		Assert.Empty(state.Outbox);
	}

	[Fact]
	public void TheNewUnitButtonsUnitMayStandEmptyUntilTheWindowShuts()
	{
		(CGameState state, CCountry england) = World();
		new CCreateUnitCommand(Eng, 5, EUnitKind.Division, 1, 0, 0, isAllowedEmpty: true).Execute(state);
		CUnit unit = england.Units.Single();
		Assert.False(CEmptyUnits.IsToGo(unit));
		new CClearAllowedEmptyCommand(Eng, 5).Execute(state);
		Assert.True(CEmptyUnits.IsToGo(unit));
	}

	[Fact]
	public void AnAgentsNewUnitMayNotStandEmpty()
	{
		(CGameState state, CCountry england) = World();
		new CCreateUnitCommand(Eng, 5, EUnitKind.Division, 1, 0, 0).Execute(state);
		Assert.True(CEmptyUnits.IsToGo(england.Units.Single()));
	}
}
