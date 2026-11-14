using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A blockaded port and a country's share of its home ports cut off, against <c>docs/mechanics/modifiers.md</c>,
/// section 3, on <see cref="ScriptWorld"/> at war: Berlin is Germany's one port, on sea 5, given a naval base here.
/// </summary>
public class BlockadeTests
{
	private const int Berlin = 1;
	private const int Sea = 5;

	private static CGameState World()
	{
		var blockaded = new CModifier(CProvinceModifiers.Blockaded);
		blockaded.Set("local_ic", CFixedPoint.Parse("-0.5"));
		CGameState state = ScriptWorld.State(atWar: true, statics: new[] { blockaded });
		CProvince berlin = state.Province(Berlin);
		berlin.SetBuildingMaxLevel(ScriptWorld.NavalBase, CFixedPoint.One);
		berlin.SetBuildingLevel(ScriptWorld.NavalBase, CFixedPoint.One);
		return state;
	}

	private static CUnit Fleet(CGameState state, CCountryTag owner, int ships = 1)
	{
		var fleet = new CUnit(EUnitKind.Navy, owner) { Location = Sea };
		var type = new CSubUnitType("destroyer", EUnitBranch.Naval);
		for (int index = 0; index < ships; index++)
		{
			fleet.SubUnits.Add(new CSubUnit(type) { Unit = fleet });
		}
		state.Country(owner)!.Units.Add(fleet);
		state.ForgetPositions();
		return fleet;
	}

	[Fact]
	public void An_enemy_ship_in_the_ports_sea_blockades_it_and_cuts_off_the_owners_share()
	{
		CGameState state = World();
		CProvince berlin = state.Province(Berlin);
		Fleet(state, ScriptWorld.Pol);

		CBlockade.UpdateProvince(state, berlin);

		Assert.True(berlin.Blockaded);
		Assert.Equal(CFixedPoint.One, state.Country(ScriptWorld.Ger)!.Blockade);   // its one home port
	}

	[Fact]
	public void A_friend_an_empty_fleet_one_fighting_or_falling_back_blockades_nothing()
	{
		CGameState state = World();
		CProvince berlin = state.Province(Berlin);
		Fleet(state, ScriptWorld.Ita);                      // Germany's ally
		Fleet(state, ScriptWorld.Pol, ships: 0);
		CUnit retreating = Fleet(state, ScriptWorld.Pol);
		retreating.IsRetreating = true;

		CBlockade.UpdateProvince(state, berlin);

		Assert.False(berlin.Blockaded);
		Assert.Equal(CFixedPoint.Zero, state.Country(ScriptWorld.Ger)!.Blockade);
	}

	[Fact]
	public void A_port_is_worked_out_only_as_its_controller_is_set()
	{
		CGameState state = World();
		CProvince berlin = state.Province(Berlin);
		Fleet(state, ScriptWorld.Pol);
		Assert.False(berlin.Blockaded);                     // the fleet's coming tells it nothing

		// Taken and given back, both times with the enemy's ships outside.
		CProvinceHands.SetController(state, berlin, ScriptWorld.Pol);
		Assert.False(berlin.Blockaded);                     // no Polish enemy at sea
		CProvinceHands.SetController(state, berlin, ScriptWorld.Ger);
		Assert.True(berlin.Blockaded);
	}

	[Fact]
	public void A_port_with_no_naval_base_is_never_blockaded_and_leaves_the_share_alone()
	{
		CGameState state = World();
		CProvince berlin = state.Province(Berlin);
		berlin.SetBuildingMaxLevel(ScriptWorld.NavalBase, CFixedPoint.Zero);
		berlin.Blockaded = true;
		state.Country(ScriptWorld.Ger)!.Blockade = CFixedPoint.Parse("0.3");
		Fleet(state, ScriptWorld.Pol);

		CBlockade.UpdateProvince(state, berlin);

		Assert.False(berlin.Blockaded);
		Assert.Equal(CFixedPoint.Parse("0.3"), state.Country(ScriptWorld.Ger)!.Blockade);
	}

	[Fact]
	public void A_home_port_held_by_somebody_else_counts_as_cut_off()
	{
		CGameState state = World();
		state.Province(Berlin).Controller = ScriptWorld.Pol;

		CBlockade.UpdateLevel(state, state.Country(ScriptWorld.Ger)!);

		Assert.Equal(CFixedPoint.One, state.Country(ScriptWorld.Ger)!.Blockade);
		Assert.Equal(CFixedPoint.Zero, state.Country(ScriptWorld.Pol)!.Blockade);   // no port at all
	}

	[Fact]
	public void A_blockaded_port_has_the_static_modifier_in_its_rebuild()
	{
		CGameState state = World();
		CProvince berlin = state.Province(Berlin);
		CProvinceModifiers.RebuildOne(state, berlin);
		Assert.Equal(CFixedPoint.Zero, berlin.Modifier["local_ic"]);

		berlin.Blockaded = true;
		CProvinceModifiers.RebuildOne(state, berlin);

		Assert.Equal(CFixedPoint.Parse("-0.5"), berlin.Modifier["local_ic"]);
		Assert.Contains(berlin.ModifierSources, source => source.Modifier.Name == CProvinceModifiers.Blockaded);
	}
}
