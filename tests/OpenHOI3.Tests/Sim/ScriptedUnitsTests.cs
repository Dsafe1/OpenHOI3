using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What an effect does to units: <c>split_troops</c> handing units and manpower over, and a unit type's name giving a
/// brigade - against <c>docs/mechanics/effects.md</c>, section 9, on <see cref="ScriptWorld"/>'s strip of ground:
/// Berlin (1), the border (2) and Posen (3) Germany's, province 4 Poland's.
/// </summary>
public class ScriptedUnitsTests
{
	private static readonly CCountryTag Ger = ScriptWorld.Ger;
	private static readonly CCountryTag Pol = ScriptWorld.Pol;

	private static readonly CSubUnitType Infantry = Type("infantry_brigade");
	private static readonly CSubUnitType Headquarters = Type("hq_brigade");

	private static CSubUnitType Type(string key)
	{
		var type = new CSubUnitType(key, EUnitBranch.Land);
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(10));
		type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.FromInt(40));
		type.Stats.Values.Set(CSubUnitType.Officers, CFixedPoint.FromInt(100));
		return type;
	}

	private static CGameState State()
	{
		return ScriptWorld.State(types: new Dictionary<string, CSubUnitType>(System.StringComparer.OrdinalIgnoreCase)
		{
			[Infantry.Key] = Infantry,
			[Headquarters.Key] = Headquarters,
		});
	}

	private static CUnit Division(CGameState state, CCountry owner, int province, int brigades, CSubUnitType? type = null)
	{
		var unit = new CUnit(EUnitKind.Division, owner.Tag) { Id = ++state.LastUnitId, Location = province, Name = "D" + state.LastUnitId };
		for (int i = 0; i < brigades; i++)
		{
			var brigade = new CSubUnit(type ?? Infantry) { Id = ++state.LastUnitId, Unit = unit };
			brigade.Stats.CopyFrom((type ?? Infantry).Stats);
			unit.SubUnits.Add(brigade);
		}
		owner.Units.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	[Fact]
	public void A_unit_type_as_an_effect_joins_the_emptiest_unit_with_room_at_half_strength()
	{
		CGameState state = State();
		CCountry germany = state.Country(Ger)!;
		CUnit full = Division(state, germany, 1, 4);
		CUnit three = Division(state, germany, 1, 3);
		CUnit one = Division(state, germany, 1, 1);

		CEffects.Run(state, ScriptWorld.Effects("infantry_brigade = 1"), CEventScope.Start(Ger));

		// Of those with fewer than four, the emptiest; it comes at half its strength and half its organisation.
		Assert.Equal(2, one.SubUnits.Count);
		Assert.Equal(3, three.SubUnits.Count);
		Assert.Equal(4, full.SubUnits.Count);
		CSubUnit given = one.SubUnits[1];
		Assert.Equal(CFixedPoint.FromInt(5), given.Strength);
		// Every technology at nought, by an account of the original.
		Assert.Empty(given.TechnologyLevels);
		Assert.Equal(given.Organisation * 2, COrganisation.Ceiling(state, germany, given));
	}

	[Fact]
	public void A_headquarters_or_a_unit_with_no_room_gets_a_unit_of_its_own_and_current_gives_to_the_holder()
	{
		CGameState state = State();
		CCountry germany = state.Country(Ger)!;
		Division(state, germany, 2, 4);
		int before = germany.Units.Count;

		CEffects.Run(state, ScriptWorld.Effects("hq_brigade = 2 infantry_brigade = 2"), CEventScope.Start(Ger));

		// The headquarters always a unit of its own; the brigade finds no room in a full one nor beside a headquarters.
		Assert.Equal(before + 2, germany.Units.Count);
		Assert.All(germany.Units.Skip(before), unit => Assert.Single(unit.SubUnits));

		// current: the scope's own province, given to whoever holds it.
		state.Province(4).Controller = Pol;
		CEffects.Run(state, ScriptWorld.Effects("infantry_brigade = current"), CEventScope.Start(Ger, 4));
		Assert.Contains(state.Country(Pol)!.Units, unit => unit.Location == 4);
	}

	[Fact]
	public void Split_troops_hands_units_and_manpower_from_this_to_the_scope_up_to_its_share_of_brigades()
	{
		CGameState state = State();
		CCountry germany = state.Country(Ger)!;
		CCountry poland = state.Country(Pol)!;
		germany.Officers = CFixedPoint.FromInt(1000);
		var units = Enumerable.Range(0, 10).Select(_ => Division(state, germany, 1, 2)).ToList();
		CFixedPoint manpower = germany.Manpower;

		// THIS is Germany, the scope Poland: a share of one hands every unit the budget allows - all of them.
		CEffects.Run(state, ScriptWorld.Effects("POL = { split_troops = 1 }"), CEventScope.Start(Ger));

		Assert.Equal(10, poland.Units.Count);
		Assert.Empty(germany.Units);
		Assert.All(units, unit => Assert.Equal(Pol, unit.Owner));
		Assert.Equal(manpower, poland.Manpower);
		Assert.Equal(CFixedPoint.Zero, germany.Manpower);
		// Each brigade's officers go with it at Germany's ratio: twenty brigades of a hundred - more than Germany had, so it
		// is left with none.
		Assert.Equal(CFixedPoint.FromInt(2000), poland.Officers);
		Assert.Equal(CFixedPoint.Zero, germany.Officers);
		// Each stepped out of Berlin into the border; each but the last left somebody behind and marches straight back.
		Assert.All(units, unit => Assert.Equal(2, unit.Location));
		var marches = state.Outbox.OfType<OpenHOI3.Sim.Commands.CMoveUnitCommand>().ToList();
		Assert.Equal(9, marches.Count);
		Assert.Equal(9, state.Outbox.Count);
	}

	[Fact]
	public void Split_troops_leaves_a_unit_in_a_battle_or_aboard_and_steps_a_land_unit_aside()
	{
		CGameState state = State();
		CCountry germany = state.Country(Ger)!;
		CCountry poland = state.Country(Pol)!;
		CUnit aboard = Division(state, germany, 1, 1);
		aboard.Transport = new CUnit(EUnitKind.Navy, Ger) { Id = ++state.LastUnitId };
		CUnit handed = Division(state, germany, 3, 1);

		CEffects.Run(state, ScriptWorld.Effects("POL = { split_troops = 1 }"), CEventScope.Start(Ger));

		Assert.Equal(Ger, aboard.Owner);
		Assert.Equal(Pol, handed.Owner);
		// It steps into the first province beside it that either side holds with nobody else's unit in it: the border.
		Assert.Equal(2, handed.Location);
		Assert.Contains(handed, poland.Units);
		// Nobody was left in Posen, so it stays where it stepped.
		Assert.Empty(state.Outbox);
	}
}
