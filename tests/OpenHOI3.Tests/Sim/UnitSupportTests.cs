using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The three switches of a unit's panel - come first, be reinforced, be upgraded - as
/// commands, against <c>docs/mechanics/unitpanel.md</c>, section 7.
/// </summary>
public class UnitSupportTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private static CGameState State(out CUnit unit)
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		unit = new CUnit(EUnitKind.Division, Ger) { Id = 7, Location = 1 };
		germany.Units.Add(unit);
		return new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Berlin", isSea: false) },
			new[] { germany, poland });
	}

	[Fact]
	public void AUnitStartsReinforcedAndUpgradedAndNotBeforeTheOthers()
	{
		State(out CUnit unit);

		Assert.True(unit.MayReinforce);
		Assert.True(unit.MayUpgrade);
		Assert.False(unit.IsPrioritised);
	}

	[Fact]
	public void EachOfTheThreeIsSwitchedOnItsOwnAndBackAgain()
	{
		CGameState state = State(out CUnit unit);
		var simulation = new CSimulation(state);

		simulation.Post(new CSetUnitSupportCommand(Ger, unit.Id, EUnitSupport.Priority, true));
		simulation.Pump();
		Assert.True(unit.IsPrioritised);
		Assert.True(unit.MayReinforce);
		Assert.True(unit.MayUpgrade);

		simulation.Post(new CSetUnitSupportCommand(Ger, unit.Id, EUnitSupport.Reinforcement, false));
		simulation.Pump();
		Assert.False(unit.MayReinforce);
		Assert.True(unit.MayUpgrade);

		simulation.Post(new CSetUnitSupportCommand(Ger, unit.Id, EUnitSupport.Upgrade, false));
		simulation.Post(new CSetUnitSupportCommand(Ger, unit.Id, EUnitSupport.Reinforcement, true));
		simulation.Post(new CSetUnitSupportCommand(Ger, unit.Id, EUnitSupport.Priority, false));
		simulation.Pump();
		Assert.False(unit.MayUpgrade);
		Assert.True(unit.MayReinforce);
		Assert.False(unit.IsPrioritised);
	}

	[Fact]
	public void ACountrySwitchesOnlyItsOwnUnits()
	{
		CGameState state = State(out CUnit unit);

		Assert.True(new CSetUnitSupportCommand(Ger, unit.Id, EUnitSupport.Upgrade, false).MayExecute(state));
		Assert.False(new CSetUnitSupportCommand(Pol, unit.Id, EUnitSupport.Upgrade, false).MayExecute(state));
		Assert.False(new CSetUnitSupportCommand(Ger, 99, EUnitSupport.Upgrade, false).MayExecute(state));
		Assert.False(new CSetUnitSupportCommand(new CCountryTag("XXX"), unit.Id, EUnitSupport.Upgrade, false).MayExecute(state));
	}
}
