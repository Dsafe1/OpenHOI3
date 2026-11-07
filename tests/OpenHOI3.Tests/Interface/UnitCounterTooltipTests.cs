using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The tooltip over a unit's counter, against <c>docs/mechanics/tooltips.md</c>, section 7.1: Germany's two divisions and
/// Poland's one stand in Posen, which Germany knows at the level each test sets.
/// </summary>
public class UnitCounterTooltipTests
{
	private const int Posen = 1;
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private static CSubUnitType Type(string key, int index, int priority)
	{
		var type = new CSubUnitType(key, EUnitBranch.Land) { DatabaseIndex = index };
		type.Stats.Values.Set("priority", CFixedPoint.FromInt(priority));
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(100));
		return type;
	}

	private static readonly CSubUnitType Infantry = Type("infantry_brigade", 2, 7);
	private static readonly CSubUnitType Artillery = Type("artillery_brigade", 1, 3);

	private static string Localise(string key) => key == "NO_LEADER" ? "(no leader)" : key;

	private static CGameState State(out CUnit german, out CUnit polish)
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), new CProvince(Posen, "Posen", isSea: false) }, new[] { germany, poland });
		german = Unit(germany, 1, Infantry, Infantry, Artillery);
		german.Leader = new CLeader(1) { Name = "Guderian", Rank = 2, Skill = 3 };
		CUnit second = Unit(germany, 2, Infantry);
		second.Leader = new CLeader(2) { Name = "Rommel", Rank = 2, Skill = 5 };
		second.SubUnits[0].Strength = CFixedPoint.FromInt(95);
		polish = Unit(poland, 3, Infantry);
		return state;
	}

	private static CUnit Unit(CCountry country, int id, params CSubUnitType[] types)
	{
		var unit = new CUnit(EUnitKind.Division, country.Tag) { Id = id, Location = Posen };
		foreach (CSubUnitType type in types)
		{
			unit.SubUnits.Add(new CSubUnit(type) { Unit = unit, Strength = CFixedPoint.FromInt(100) });
		}
		country.Units.Add(unit);
		return unit;
	}

	[Fact]
	public void ThePlayersOwnCounterTellsEveryUnitThereByTypeWithItsBars()
	{
		CGameState state = State(out CUnit german, out _);

		string text = CUnitCounterTooltip.Build(Localise, state, german, Ger);

		// Every country's units, its flags in order; the leader of equal rank and more skill; the types in database order.
		Assert.Equal("@GER @POL Rommel\n1 £artillery_brigade {10\n4 £infantry_brigade {10{10{09{10\n", text);
	}

	[Fact]
	public void AnEnemyKnownAtThreeToFiveNamesOnlyItsTypesOfPrioritySixAndUp()
	{
		CGameState state = State(out _, out CUnit polish);
		state.Province(Posen).Intel[state.Country(Ger)!.Index] = 4;

		Assert.Equal("@GER @POL ?\n? £unknown\n? £infantry_brigade\n", CUnitCounterTooltip.Build(Localise, state, polish, Ger));

		state.Province(Posen).Intel[state.Country(Ger)!.Index] = 6;
		Assert.Equal("@GER @POL Rommel\n1 £artillery_brigade\n4 £infantry_brigade\n",
			CUnitCounterTooltip.Build(Localise, state, polish, Ger));

		state.Province(Posen).Intel[state.Country(Ger)!.Index] = 2;
		Assert.Equal("@GER @POL ?\n? £unknown", CUnitCounterTooltip.Build(Localise, state, polish, Ger));
	}

	[Fact]
	public void TheLeaderReplacesOnlyWithRankAsHighAndMoreSkillAndNoneIsTheNullLeader()
	{
		var units = new[]
		{
			new CUnit(EUnitKind.Division, Ger) { Leader = new CLeader(3) { Name = "A", Rank = 3, Skill = 2 } },
			new CUnit(EUnitKind.Division, Ger) { Leader = new CLeader(4) { Name = "B", Rank = 4, Skill = 1 } },
			new CUnit(EUnitKind.Division, Ger) { Leader = new CLeader(5) { Name = "C", Rank = 2, Skill = 9 } },
		};
		// A higher rank with less skill does not replace, nor a lower rank with more.
		Assert.Equal("A", CUnitCounterTooltip.BestLeader(units)!.Name);
		Assert.Null(CUnitCounterTooltip.BestLeader(new[] { new CUnit(EUnitKind.Division, Ger) }));
	}

	[Fact]
	public void ABarIsTheTenthsOfItsStrengthCutAndFullFromNinetyNine()
	{
		CGameState state = State(out CUnit german, out _);
		CSubUnit brigade = german.SubUnits[0];

		brigade.Strength = CFixedPoint.Parse("98.9");
		Assert.Equal("{09", CUnitCounterTooltip.Bar(state, brigade));
		brigade.Strength = CFixedPoint.FromInt(99);
		Assert.Equal("{10", CUnitCounterTooltip.Bar(state, brigade));
		brigade.Strength = CFixedPoint.Parse("9.9");
		Assert.Equal("{00", CUnitCounterTooltip.Bar(state, brigade));
	}
}
