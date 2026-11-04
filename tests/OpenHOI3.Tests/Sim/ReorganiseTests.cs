using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What may be moved between two units and when two may be reorganised, against <c>docs/mechanics/reorganise.md</c>,
/// sections 1 and 4. Berlin and Potsdam are Germany's; a division holds four brigades.
/// </summary>
public class ReorganiseTests
{
	private static readonly CCountryTag Ger = new("GER");
	private const int Berlin = 1;
	private const int Potsdam = 2;

	private static readonly CSubUnitType Infantry = new("infantry_brigade", EUnitBranch.Land);
	private static readonly CSubUnitType Headquarters = HeadquartersType();
	private static readonly CSubUnitType Destroyer = new("destroyer", EUnitBranch.Naval);

	private static CSubUnitType HeadquartersType()
	{
		var type = new CSubUnitType("hq_brigade", EUnitBranch.Land);
		type.Flags[CUnitAvailability.IsBuildable] = false;
		return type;
	}

	private static CGameState State(out CCountry germany)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(Berlin, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
				new CProvince(Potsdam, "Potsdam", isSea: false) { Owner = Ger, Controller = Ger },
			},
			new[] { germany });
		state.Defines.Set(CManpower.MilitarySection, CUnitAvailability.BrigadesInDivision, CFixedPoint.FromInt(4));
		return state;
	}

	private static CUnit Unit(CGameState state, CCountry country, EUnitKind kind, int province, params CSubUnitType[] brigades)
	{
		var unit = new CUnit(kind, country.Tag) { Id = ++state.LastUnitId, Location = province, Name = $"Unit {state.LastUnitId}" };
		foreach (CSubUnitType type in brigades)
		{
			var brigade = new CSubUnit(type) { Id = ++state.LastUnitId, Unit = unit, Name = type.Key };
			brigade.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(brigade);
		}
		country.Units.Add(unit);
		return unit;
	}

	[Fact]
	public void A_brigade_may_go_to_a_land_unit_in_the_same_province_with_room_and_its_last_brigade_may_go_too()
	{
		CGameState state = State(out CCountry germany);
		CUnit full = Unit(state, germany, EUnitKind.Division, Berlin, Infantry, Infantry, Infantry, Infantry);
		CUnit lone = Unit(state, germany, EUnitKind.Division, Berlin, Infantry);
		CUnit away = Unit(state, germany, EUnitKind.Division, Potsdam, Infantry);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Berlin, Destroyer);

		// A division at its four is full; the lone division's last brigade may go anywhere with room.
		Assert.False(CSubUnitTransfer.MayTake(state, germany, full, lone.SubUnits[0], sameProvince: true));
		Assert.True(CSubUnitTransfer.MayTake(state, germany, lone, full.SubUnits[0], sameProvince: true));
		// Another province only where the test is told so; a ship never into an army.
		Assert.False(CSubUnitTransfer.MayTake(state, germany, away, lone.SubUnits[0], sameProvince: true));
		Assert.True(CSubUnitTransfer.MayTake(state, germany, away, lone.SubUnits[0], sameProvince: false));
		Assert.False(CSubUnitTransfer.MayTake(state, germany, lone, fleet.SubUnits[0], sameProvince: true));

		var command = new CTransferSubUnitCommand(Ger, full.SubUnits[0].Id, lone.Id);
		Assert.True(command.MayExecute(state));
		command.Execute(state);
		Assert.Equal(3, full.SubUnits.Count);
		Assert.Equal(2, lone.SubUnits.Count);
	}

	[Fact]
	public void A_headquarters_first_brigade_and_a_type_not_buildable_stay_where_they_are()
	{
		CGameState state = State(out CCountry germany);
		CUnit corps = Unit(state, germany, EUnitKind.Corps, Berlin, Infantry, Infantry);
		CUnit division = Unit(state, germany, EUnitKind.Division, Berlin, Infantry, Headquarters);

		// The corps' first brigade is its headquarters; its second may go.
		Assert.False(CSubUnitTransfer.MayTake(state, germany, division, corps.SubUnits[0], sameProvince: true));
		Assert.True(CSubUnitTransfer.MayTake(state, germany, division, corps.SubUnits[1], sameProvince: true));
		// A type the files say is not buildable goes nowhere.
		Assert.False(CSubUnitTransfer.MayTake(state, germany, corps, division.SubUnits[1], sameProvince: true));
	}

	[Fact]
	public void Two_units_may_be_reorganised_standing_together_idle_and_not_with_two_brigades_between_them()
	{
		CGameState state = State(out CCountry germany);
		CUnit first = Unit(state, germany, EUnitKind.Division, Berlin, Infantry, Infantry);
		CUnit second = Unit(state, germany, EUnitKind.Division, Berlin, Infantry);
		CUnit third = Unit(state, germany, EUnitKind.Division, Berlin, Infantry);
		CUnit away = Unit(state, germany, EUnitKind.Division, Potsdam, Infantry, Infantry);

		Assert.True(CSubUnitTransfer.MayReorganise(new[] { first, second }));
		Assert.False(CSubUnitTransfer.MayReorganise(new[] { second, third }));          // two brigades between them
		Assert.False(CSubUnitTransfer.MayReorganise(new[] { first, away }));            // apart
		Assert.False(CSubUnitTransfer.MayReorganise(new[] { first, second, third }));   // three
		second.Path.Add(Potsdam);
		Assert.False(CSubUnitTransfer.MayReorganise(new[] { first, second }));          // on its way
	}
}
