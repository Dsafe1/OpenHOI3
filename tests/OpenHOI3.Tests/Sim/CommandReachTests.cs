using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// How far a command reaches, against <c>docs/mechanics/leaders.md</c>, section 8: the
/// reach of one headquarters, its compounding up a chain, being out of radio range, a
/// trait's effect coming down the chain, and what the reach does to organisation and to
/// what a unit uses.
/// </summary>
public class CommandReachTests
{
	private static readonly CCountryTag Ger = new("GER");

	/// <summary>A headquarters' brigade, with the radio of 2 the base game's has.</summary>
	private static readonly CSubUnitType Headquarters = Type("hq_brigade", radio: "2");

	private static readonly CSubUnitType Infantry = Type("infantry_brigade", radio: "1");

	private static CSubUnitType Type(string key, string radio)
	{
		var type = new CSubUnitType(key, EUnitBranch.Land);
		type.Stats.Values.Set(CCommandReach.RadioStrength, CFixedPoint.Parse(radio));
		type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.FromInt(30));
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(100));
		return type;
	}

	/// <summary>
	/// Provinces along one row, by how many pixels east of the first they are: a corps'
	/// radius of 100 x 2 is 200 quarters of a pixel, 50 pixels.
	/// </summary>
	private static CGameState State(out CCountry germany)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var defines = new CDefines();
		void Military(string name, string value) => defines.Set(CManpower.MilitarySection, name, CFixedPoint.Parse(value));
		Military("RADIO_CORPS_LEADER_DISTANCE", "100");
		Military("RADIO_ARMY_LEADER_DISTANCE", "200");
		Military("RADIO_ARMYGROUP_LEADER_DISTANCE", "300");
		Military("RADIO_THEATHRE_LEADER_DISTANCE", "1000");
		int[] east = { 0, 0, 50, 75, 101, 100, 200, 400 };
		var provinces = new CProvince[east.Length];
		provinces[0] = new CProvince(0, "", isSea: false);
		for (int id = 1; id < east.Length; id++)
		{
			provinces[id] = new CProvince(id, $"p{id}", isSea: false) { CentreX = 1000 + east[id], CentreY = 500 };
		}
		return new CGameState(new CDate(1936, 1, 1), provinces, new[] { germany }, defines: defines) { MapWidth = 5616 };
	}

	private static CUnit Unit(CGameState state, EUnitKind kind, int location, CUnit? parent = null, CLeader? leader = null)
	{
		var unit = new CUnit(kind, Ger) { Id = ++state.LastUnitId, Location = location, Parent = parent, Leader = leader };
		parent?.Children.Add(unit);
		CSubUnitType type = kind == EUnitKind.Division ? Infantry : Headquarters;
		var subUnit = new CSubUnit(type) { Id = ++state.LastUnitId, Unit = unit, Strength = CFixedPoint.FromInt(100) };
		subUnit.Stats.CopyFrom(type.Stats);
		unit.SubUnits.Add(subUnit);
		return unit;
	}

	private static CLeader Leader(int skill, params CTraitEffect[] effects)
	{
		var trait = new CTrait("a_trait");
		trait.Effects.AddRange(effects);
		var leader = new CLeader(skill + 100) { Skill = skill, Rank = 1 };
		leader.Traits.Add(trait);
		return leader;
	}

	// ---- one headquarters

	[Fact]
	public void ACorpsReachesAllOfItsRadiusThenTheRadiusOverTheDistanceAndNothingPastTwice()
	{
		CGameState state = State(out _);
		CUnit corps = Unit(state, EUnitKind.Corps, 1);

		Assert.Equal(1000, CCommandReach.From(state, Unit(state, EUnitKind.Division, 1, corps), corps));
		Assert.Equal(1000, CCommandReach.From(state, Unit(state, EUnitKind.Division, 2, corps), corps));   // exactly on the radius
		Assert.Equal(667, CCommandReach.From(state, Unit(state, EUnitKind.Division, 3, corps), corps));    // 1000 - 100 x 1000 / 300
		Assert.Equal(500, CCommandReach.From(state, Unit(state, EUnitKind.Division, 5, corps), corps));    // exactly twice the radius
		Assert.Equal(0, CCommandReach.From(state, Unit(state, EUnitKind.Division, 4, corps), corps));
	}

	[Fact]
	public void TheRadiusIsTheRadioOfTheFirstBrigadeTimesTheLevelsDefine()
	{
		CGameState state = State(out _);

		Assert.Equal(200, CCommandReach.Radius(state, Unit(state, EUnitKind.Corps, 1)));
		Assert.Equal(400, CCommandReach.Radius(state, Unit(state, EUnitKind.Army, 1)));
		Assert.Equal(2000, CCommandReach.Radius(state, Unit(state, EUnitKind.Theatre, 1)));
	}

	[Fact]
	public void AUnitThatIsNowhereIsReachedByNothingAndAHeadquartersThatIsNowhereReachesAll()
	{
		CGameState state = State(out _);
		CUnit corps = Unit(state, EUnitKind.Corps, 1);
		CUnit lost = Unit(state, EUnitKind.Corps, 0);

		Assert.Equal(0, CCommandReach.From(state, Unit(state, EUnitKind.Division, 0, corps), corps));
		Assert.Equal(1000, CCommandReach.From(state, Unit(state, EUnitKind.Division, 4, lost), lost));
	}

	// ---- up the chain

	[Fact]
	public void TheReachOfAnArmyIsEveryLinkBetweenMultipliedTogether()
	{
		CGameState state = State(out _);
		CUnit army = Unit(state, EUnitKind.Army, 6);                     // 200 pixels from the corps: 800 against 400, half
		CUnit corps = Unit(state, EUnitKind.Corps, 1, army);
		CUnit division = Unit(state, EUnitKind.Division, 3, corps);       // two thirds of the corps

		Assert.Equal(500, CCommandReach.From(state, corps, army));
		Assert.Equal(333, CCommandReach.Through(state, division, 2));     // 667 x 500 / 1000
		Assert.Equal(667, CCommandReach.Through(state, division, 3));
		Assert.Equal(1000, CCommandReach.Through(state, army, 2));
	}

	[Fact]
	public void AUnitIsOutOfRangeWhereAnyLinkAboveItLetsLessThanAllThrough()
	{
		CGameState state = State(out _);
		CUnit army = Unit(state, EUnitKind.Army, 7);                     // 400 pixels: past twice its radius
		CUnit corps = Unit(state, EUnitKind.Corps, 1, army);
		CUnit near = Unit(state, EUnitKind.Division, 2, corps);
		var alone = Unit(state, EUnitKind.Corps, 1);
		CUnit withAll = Unit(state, EUnitKind.Division, 2, alone);

		Assert.True(CCommandReach.IsOutOfRange(state, near));               // its own corps reaches it; the army does not reach the corps
		Assert.False(CCommandReach.IsOutOfRange(state, withAll));
		Assert.False(CCommandReach.IsOutOfRange(state, alone));
	}

	// ---- a trait down the chain

	[Fact]
	public void ATraitIsHalvedAtEveryStepDownAndScaledByTheReach()
	{
		CGameState state = State(out _);
		var effect = new CTraitEffect(ETraitEffect.SupplyConsumption, CFixedPoint.Parse("-0.2"), "");
		CUnit corps = Unit(state, EUnitKind.Corps, 1, leader: Leader(3, effect));
		CUnit near = Unit(state, EUnitKind.Division, 1, corps);
		CUnit far = Unit(state, EUnitKind.Division, 3, corps);
		CUnit led = Unit(state, EUnitKind.Division, 1, corps, Leader(1, effect));

		Assert.Equal(-200, CCommandReach.Effect(state, corps, ETraitEffect.SupplyConsumption));
		Assert.Equal(-100, CCommandReach.Effect(state, near, ETraitEffect.SupplyConsumption));
		Assert.Equal(-66, CCommandReach.Effect(state, far, ETraitEffect.SupplyConsumption));       // -133 halved, toward nothing
		Assert.Equal(-300, CCommandReach.Effect(state, led, ETraitEffect.SupplyConsumption));
		Assert.Equal(0, CCommandReach.Effect(state, near, ETraitEffect.DefenceModifier));
	}

	// ---- what it does

	[Fact]
	public void AnArmysCommanderAddsHisSkillByHisReachToWhatItsBrigadesMayHave()
	{
		CGameState state = State(out CCountry germany);
		CUnit army = Unit(state, EUnitKind.Army, 1, leader: Leader(2));
		CUnit corps = Unit(state, EUnitKind.Corps, 1, army);
		CUnit near = Unit(state, EUnitKind.Division, 1, corps);
		CUnit far = Unit(state, EUnitKind.Division, 3, corps);
		germany.Units.Add(army);

		Assert.Equal(2000, COrganisation.CommanderBonus(state, near));
		Assert.Equal(1334, COrganisation.CommanderBonus(state, far));     // 667 x 2
		Assert.Equal(CFixedPoint.FromInt(32), COrganisation.Ceiling(state, germany, near.SubUnits[0]));
	}

	[Fact]
	public void NothingIsAddedWithoutAnArmyOrWhileTheGameIsBeingMade()
	{
		CGameState state = State(out CCountry germany);
		CUnit theatre = Unit(state, EUnitKind.Theatre, 1, leader: Leader(4));
		CUnit division = Unit(state, EUnitKind.Division, 1, theatre);
		CUnit army = Unit(state, EUnitKind.Army, 1, leader: Leader(2));
		CUnit led = Unit(state, EUnitKind.Division, 1, army);

		Assert.Equal(0, COrganisation.CommanderBonus(state, division));
		state.IsBeingMade = true;
		Assert.Equal(CFixedPoint.FromInt(30), COrganisation.Ceiling(state, germany, led.SubUnits[0]));
	}

	[Fact]
	public void AnArmyGroupsCommanderTakesATwentiethASkillOffWhatItsUnitsUse()
	{
		CGameState state = State(out CCountry germany);
		CUnit group = Unit(state, EUnitKind.ArmyGroup, 1, leader: Leader(3));
		CUnit army = Unit(state, EUnitKind.Army, 1, group);
		CUnit division = Unit(state, EUnitKind.Division, 1, army);
		CUnit nowhere = Unit(state, EUnitKind.Division, 0, army);

		Assert.Equal(850, CUnitSupply.Potency(state, germany, division));
		Assert.Equal(850, CUnitSupply.Potency(state, germany, group));
		Assert.Equal(1000, CUnitSupply.Potency(state, germany, nowhere));
	}

	[Fact]
	public void ALeadersSupplyTraitIsInWhatHisUnitsUseAndItNeverGoesBelowNothing()
	{
		CGameState state = State(out CCountry germany);
		var thrifty = new CTraitEffect(ETraitEffect.SupplyConsumption, CFixedPoint.Parse("-0.5"), "");
		CUnit corps = Unit(state, EUnitKind.Corps, 1, leader: Leader(1, thrifty));
		CUnit division = Unit(state, EUnitKind.Division, 1, corps, Leader(1, thrifty, thrifty, thrifty));

		Assert.Equal(500, CUnitSupply.Potency(state, germany, corps));
		Assert.Equal(0, CUnitSupply.Potency(state, germany, division));
	}
}
