using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What a change of hands does with the units based in the province, and a country owning nothing losing its units in its
/// hour, against <c>docs/mechanics/war.md</c>, section 11, on <see cref="ScriptWorld"/> at war: Poland takes Berlin, Germany's
/// port, with Germany's fleets based there.
/// </summary>
public class CapturedBasesTests
{
	private const int Berlin = 1;
	private const int Posen = 3;
	private const int Sea = 5;

	private static CGameState World(bool secondBase)
	{
		CGameState state = ScriptWorld.State(atWar: true);
		state.Province(Berlin).SetBuildingMaxLevel(ScriptWorld.NavalBase, CFixedPoint.One);
		if (secondBase)
		{
			state.Province(Posen).SetBuildingMaxLevel(ScriptWorld.NavalBase, CFixedPoint.One);
		}
		return state;
	}

	private static CUnit Fleet(CGameState state, CCountryTag owner, int at)
	{
		var fleet = new CUnit(EUnitKind.Navy, owner) { Id = ++state.LastUnitId, Location = at, Base = Berlin };
		fleet.SubUnits.Add(new CSubUnit(new CSubUnitType("destroyer", EUnitBranch.Naval)) { Unit = fleet });
		state.Country(owner)!.Units.Add(fleet);
		state.ForgetPositions();
		return fleet;
	}

	[Fact]
	public void An_enemys_fleets_are_given_the_old_holders_nearest_other_base_and_one_in_port_is_sent_there()
	{
		CGameState state = World(secondBase: true);
		CUnit atSea = Fleet(state, ScriptWorld.Ger, Sea);
		CUnit inPort = Fleet(state, ScriptWorld.Ger, Berlin);
		CUnit ally = Fleet(state, ScriptWorld.Ita, Sea);       // Italy is at war with nobody

		CProvinceHands.SetController(state, state.Province(Berlin), ScriptWorld.Pol, consequences: true);

		Assert.Equal(Posen, atSea.Base);
		Assert.Equal(EUnitOrder.None, atSea.Order);
		Assert.Equal(Posen, inPort.Base);
		Assert.Equal(EUnitOrder.Rebase, inPort.Order);
		Assert.Equal(Posen, inPort.OrderProvince);
		Assert.Equal(Berlin, ally.Base);
	}

	[Fact]
	public void With_no_other_base_a_fleet_loses_its_base_and_goes_unless_it_is_moving()
	{
		CGameState state = World(secondBase: false);
		CUnit idle = Fleet(state, ScriptWorld.Ger, Sea);
		CUnit moving = Fleet(state, ScriptWorld.Ger, Sea);
		moving.Path.Add(Berlin);

		CProvinceHands.SetController(state, state.Province(Berlin), ScriptWorld.Pol, consequences: true);

		Assert.Equal(0, idle.Base);
		Assert.Equal(0, moving.Base);
		Assert.Contains(idle.Id, state.PendingUnitRemovals);
		Assert.DoesNotContain(moving.Id, state.PendingUnitRemovals);
	}

	[Fact]
	public void Without_consequences_nothing_based_there_is_touched()
	{
		CGameState state = World(secondBase: true);
		CUnit fleet = Fleet(state, ScriptWorld.Ger, Sea);

		CProvinceHands.SetController(state, state.Province(Berlin), ScriptWorld.Pol);

		Assert.Equal(Berlin, fleet.Base);
	}

	[Fact]
	public void A_country_owning_nothing_loses_its_units_in_its_hour_but_an_exile_keeps_them()
	{
		CGameState state = World(secondBase: false);
		var unit = new CUnit(EUnitKind.Army, ScriptWorld.Slo) { Id = ++state.LastUnitId, Location = Posen };
		state.Country(ScriptWorld.Slo)!.Units.Add(unit);
		state.ForgetPositions();

		state.Country(ScriptWorld.Slo)!.InExile = true;
		CMovement.RunHourly(state);
		Assert.False(unit.IsRemoved);

		state.Country(ScriptWorld.Slo)!.InExile = false;
		CMovement.RunHourly(state);
		Assert.True(unit.IsRemoved);
	}
}
