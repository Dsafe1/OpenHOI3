using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Occupation;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// When a province's modifier is made again, against <c>docs/mechanics/modifiers.md</c>, section 3, on
/// <see cref="ScriptWorld"/> at war, with one policy that takes 95% off IC. A value set on a province's modifier by
/// hand stands until the province is made again, which is how these tell a rebuild from none.
/// </summary>
public class ProvinceModifierScheduleTests
{
	private static readonly CFixedPoint Cut = CFixedPoint.Parse("-0.95");

	private static CGameState World()
	{
		CGameState state = ScriptWorld.State(atWar: true);
		List<COccupationPolicy> policies = COccupationPolicies.WithNull();
		var collaboration = new CModifier("collaboration_government");
		collaboration.Set("local_ic", Cut);
		policies.Add(new COccupationPolicy(collaboration, 1));
		state.OccupationPolicies = policies;
		state.Defines.Set(CNationalism.Section, CNationalism.Years, CFixedPoint.FromInt(20));
		CProvinceModifiers.Rebuild(state);
		return state;
	}

	private static void Mark(CProvince province) => province.Modifier.Set("mark", CFixedPoint.One);

	private static bool Marked(CProvince province) => province.Modifier["mark"] == CFixedPoint.One;

	[Fact]
	public void A_battles_capture_makes_the_province_again_at_once_and_a_plain_change_of_hands_does_not()
	{
		CGameState state = World();
		CProvince warsaw = state.Province(4);

		Assert.True(CProvinceControl.Take(state, warsaw, ScriptWorld.Ger));
		Assert.Equal(Cut, warsaw.Modifier["local_ic"]);

		// The day's return or an effect: the controller written, the modifier left as it was.
		Mark(warsaw);
		CProvinceHands.SetController(state, warsaw, ScriptWorld.Pol, consequences: true);
		Assert.True(Marked(warsaw));
		Assert.Equal(Cut, warsaw.Modifier["local_ic"]);

		// Until the month, or anything else that makes it again.
		CProvinceModifiers.Rebuild(state);
		Assert.Equal(CFixedPoint.Zero, warsaw.Modifier["local_ic"]);
	}

	[Fact]
	public void A_change_of_owner_makes_the_province_again_and_drops_what_events_gave_it()
	{
		CGameState state = World();
		CProvince warsaw = state.Province(4);
		warsaw.EventModifiers.Add(CTimedModifier.Given(state, new CModifier("given"), 30));
		Mark(warsaw);

		CProvinceHands.ChangeOwner(state, warsaw, ScriptWorld.Ger);

		Assert.False(Marked(warsaw));
		Assert.Empty(warsaw.EventModifiers);
	}

	[Fact]
	public void A_moved_building_waits_for_the_hours_look_which_makes_only_that_province_again()
	{
		CGameState state = World();
		CProvince border = state.Province(2);
		CProvince berlin = state.Province(1);
		Mark(border);
		Mark(berlin);

		border.SetBuildingCurrentLevel(ScriptWorld.Industry, CFixedPoint.Parse("2.5"));
		Assert.True(border.BuildingMoved);
		Assert.True(Marked(border));

		CProvinceModifiers.RebuildMoved(state);
		Assert.False(Marked(border));
		Assert.False(border.BuildingMoved);
		Assert.True(Marked(berlin));

		// A level set to what it is moves nothing.
		Mark(border);
		border.SetBuildingCurrentLevel(ScriptWorld.Industry, CFixedPoint.Parse("2.5"));
		CProvinceModifiers.RebuildMoved(state);
		Assert.True(Marked(border));
	}

	[Fact]
	public void The_days_repair_makes_the_province_again_at_once_only_across_a_whole_level()
	{
		CGameState state = World();
		CProvince border = state.Province(2);
		border.SetBuildingCurrentLevel(ScriptWorld.Industry, CFixedPoint.Parse("2.5"));
		CProvinceModifiers.RebuildMoved(state);

		// A tenth and a tenth of a level a day, with no roads: 2.61, the level not crossed.
		state.Defines.Set(CProvinceRepair.EconomySection, CProvinceRepair.RepairSpeed, CFixedPoint.One);
		Mark(border);
		CProvinceRepair.RunDaily(state);
		Assert.True(Marked(border));
		Assert.True(border.BuildingMoved);

		// Five times as fast: 3.0 and over, which is a whole level crossed.
		state.Defines.Set(CProvinceRepair.EconomySection, CProvinceRepair.RepairSpeed, CFixedPoint.FromInt(5));
		CProvinceRepair.RunDaily(state);
		Assert.False(Marked(border));
		Assert.False(border.BuildingMoved);
	}

	[Fact]
	public void The_occupation_command_makes_again_only_the_setters_provinces_of_the_other()
	{
		CGameState state = World();
		CProvince warsaw = state.Province(4);
		CProvince rome = state.Province(7);
		warsaw.Controller = ScriptWorld.Ger;
		rome.Controller = ScriptWorld.Ger;
		Mark(warsaw);
		Mark(rome);

		new CChangeOccupationPolicyCommand(ScriptWorld.Ger, ScriptWorld.Pol, "collaboration_government").Execute(state);

		Assert.Equal(Cut, warsaw.Modifier["local_ic"]);
		Assert.False(Marked(warsaw));
		Assert.True(Marked(rome));
	}

	[Fact]
	public void A_given_modifier_outlives_its_end_until_the_province_is_made_again()
	{
		CGameState state = World();
		CProvince warsaw = state.Province(4);
		var given = new CModifier("given");
		given.Set("local_ic", CFixedPoint.Parse("0.1"));
		warsaw.EventModifiers.Add(new CTimedModifier(given, state.TotalHours - 1));
		CProvinceModifiers.RebuildOne(state, warsaw);
		Assert.Empty(warsaw.EventModifiers);

		warsaw.EventModifiers.Add(CTimedModifier.Given(state, given, 1));
		CProvinceModifiers.RebuildOne(state, warsaw);
		warsaw.EventModifiers[0] = new CTimedModifier(given, state.TotalHours - 1);
		Assert.Single(warsaw.EventModifiers);
		Assert.Equal(CFixedPoint.Parse("0.1"), warsaw.Modifier["local_ic"]);
	}
}
