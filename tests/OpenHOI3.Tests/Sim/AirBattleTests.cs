using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Air superiority, the intercept and the battle in the air against
/// <c>docs/mechanics/aircombat.md</c>, on a strip of ground:
///
///     1 Berlin --- 2 Border --- 3 Field --- 4 Warsaw --- 5 East
///     (Germany's, an airfield)     |        (Poland's, an airfield)
///                               6 Lake
///
/// Germany holds the first two and Poland the rest; the lake is sea. Each is forty from
/// the next as a range measures it, and a wing covers one province an hour.
/// </summary>
public class AirBattleTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Berlin = 1;
	private const int Border = 2;
	private const int Field = 3;
	private const int Warsaw = 4;
	private const int East = 5;
	private const int Lake = 6;

	private static readonly CBuilding NavalBase = new(0, "naval_base", 10, "", CFixedPoint.Zero);
	private static readonly CBuilding AirBase = new(1, "air_base", 10, "", CFixedPoint.Zero);

	private static CProvinceEdge Edge(int to)
	{
		return new CProvinceEdge(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.FromInt(10));
	}

	private static CProvince Land(int id, string name, CCountryTag owner, int x, bool airfield, params CProvinceEdge[] edges)
	{
		var province = new CProvince(id, name, isSea: false)
		{
			Owner = owner, Controller = owner, CentreX = x, CentreY = 10, Edges = edges,
		};
		if (airfield)
		{
			province.SetBuildingLevel(AirBase, CFixedPoint.FromInt(4));
		}
		return province;
	}

	private static CSubUnitType Type(string key, string attack, string range = "1000", string priority = "5",
		bool bomber = false, bool cag = false)
	{
		var type = new CSubUnitType(key, EUnitBranch.Air);
		void Set(string name, string value) => type.Stats.Values.Set(name, CFixedPoint.Parse(value));
		Set(CMovementSpeed.MaximumSpeed, "20");
		Set(CUnitOrders.Range, range);
		Set(CSubUnitType.MaxStrength, "100");
		Set(CSubUnitType.DefaultOrganisation, "30");
		Set(CAirMissions.AirAttack, attack);
		Set(CAirFire.Priority, priority);
		type.Flags[CAirModifiers.IsBomber] = bomber;
		type.Flags[CAirFire.IsCag] = cag;
		return type;
	}

	private static readonly CSubUnitType Fighter = Type("interceptor", "4");
	private static readonly CSubUnitType Bomber = Type("tactical_bomber", "1", priority: "3", bomber: true);
	private static readonly CSubUnitType Transport = Type("transport_plane", "0", priority: "1", bomber: true);
	private static readonly CSubUnitType ShortLegged = Type("short", "4", range: "90");
	private static readonly CSubUnitType CarrierGroup = Type("cag", "2", cag: true);

	private static CGameState State(out CCountry germany, out CCountry poland, bool atWar = true)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		var defines = new CDefines();
		void Military(string name, string value) => defines.Set(CManpower.MilitarySection, name, CFixedPoint.Parse(value));
		Military(CMovementSpeed.AirSpeedModifier, "0.5");
		Military("AGGRESSIVE_STRENGTH_LIMIT", "0.25");
		Military("DEFENSIVE_STRENGTH_LIMIT", "0.5");
		Military("PASSIVE_STRENGTH_LIMIT", "0.75");
		Military("AGGRESSIVE_ORGANISATION_LIMIT", "0.2");
		Military("DEFENSIVE_ORGANISATION_LIMIT", "0.3");
		Military("PASSIVE_ORGANISATION_LIMIT", "0.5");
		Military(CAirFire.AvoidChance, "48");
		Military(CAirFire.OrganisationDie, "9");
		Military(CAirFire.StrengthDie, "4");
		Military(CAirFire.CriticalMultiplier, "10");
		Military(CAirFire.CriticalChance, "10");
		Military(CAirFire.OrganisationDamage, "12.5");
		Military(CAirFire.StrengthDamage, "10");
		Military(CAirFire.CagOrganisationDamage, "0.75");
		Military(CAirFire.CagStrengthDamage, "0.75");
		Military(CAirModifiers.StackingPenalty, "-0.1");
		Military(CAirModifiers.MostStackingPenalty, "-0.8");
		Military(CAirModifiers.BaseProximityBonus, "0.1");
		Military(CAirModifiers.InterceptBonus, "0.2");
		Military(CAirModifiers.SuperiorityBonus, "0.2");
		Military(CCombatModifiers.NightPenalty, "-0.5");
		var wars = new List<CWar>();
		if (atWar)
		{
			var war = new CWar("war");
			war.Attackers.Add(Ger);
			war.Defenders.Add(Pol);
			wars.Add(war);
		}
		var lake = new CProvince(Lake, "Lake", isSea: true) { CentreX = 30, CentreY = 20, Edges = new[] { Edge(Field) } };
		return new CGameState(new CDate(1936, 6, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				Land(Berlin, "Berlin", Ger, 10, true, Edge(Border)),
				Land(Border, "Border", Ger, 20, false, Edge(Berlin), Edge(Field)),
				Land(Field, "Field", Pol, 30, false, Edge(Border), Edge(Warsaw), Edge(Lake)),
				Land(Warsaw, "Warsaw", Pol, 40, true, Edge(Field), Edge(East)),
				Land(East, "East", Pol, 50, false, Edge(Warsaw)),
				lake,
			},
			new[] { germany, poland }, buildings: new[] { NavalBase, AirBase }, defines: defines, wars: wars)
		{
			MapWidth = 1000,
			MapHeight = 1000,
		};
	}

	private static CUnit Air(CGameState state, CCountry country, int province, params CSubUnitType[] wings)
	{
		var unit = new CUnit(EUnitKind.Air, country.Tag)
		{
			Id = ++state.LastUnitId, Location = province, Base = country.Tag == Ger ? Berlin : Warsaw,
			Name = country.Tag + " wing " + state.LastUnitId,
		};
		foreach (CSubUnitType type in wings)
		{
			var wing = new CSubUnit(type)
			{
				Id = ++state.LastUnitId, Unit = unit, Strength = CFixedPoint.FromInt(100),
				Organisation = CFixedPoint.FromInt(30), Experience = CFixedPoint.Zero, Name = type.Key,
			};
			wing.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(wing);
		}
		country.Units.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	private static void Give(CGameState state, CCountry country, CUnit unit, EUnitOrder order, int province,
		int stance = CMission.Aggressive, params int[] provinces)
	{
		CUnitOrders.Give(state, country, unit, order, province, stance,
			provinces: provinces.Length == 0 ? null : provinces);
	}

	// --- giving one, and its area ---

	[Fact]
	public void AMissionMayBeGivenToAnAirUnitAndKeepsTheProvincesWithinItsRange()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fighters = Air(state, germany, Berlin, Fighter);
		CUnit shortLegged = Air(state, germany, Berlin, ShortLegged);

		Assert.True(CUnitOrders.MayGive(state, germany, fighters, EUnitOrder.AirSuperiority, state.Province(Warsaw)));
		Assert.True(new CSetUnitOrderCommand(Ger, fighters.Id, EUnitOrder.AirIntercept, Berlin).MayExecute(state));

		// Each province is forty from the next: a range of ninety reaches the second from Berlin.
		Give(state, germany, shortLegged, EUnitOrder.AirSuperiority, Border, CMission.Aggressive, Border, Field, Warsaw, Border);
		Assert.Equal(new[] { Border, Field }, shortLegged.Mission.Provinces);

		// With none said, the order's own province.
		Give(state, germany, fighters, EUnitOrder.AirSuperiority, Warsaw);
		Assert.Equal(new[] { Warsaw }, fighters.Mission.Provinces);
	}

	[Fact]
	public void ACircleIsEverythingJoinedToItsMiddleWithinItsRangeAndTheUnits()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fighters = Air(state, germany, Berlin, Fighter);
		CUnit shortLegged = Air(state, germany, Berlin, ShortLegged);

		// Forty around Field: its neighbours, the lake among them (ten down and none across).
		List<int> area = CAirMissionArea.Circle(state, germany, fighters, EUnitOrder.AirSuperiority, Field, CFixedPoint.FromInt(40));
		Assert.Equal(new[] { Field, Border, Warsaw, Lake }, area);

		// The same for a unit that reaches no further than Field: what is beyond its range is out,
		// and the walk does not go on from it.
		area = CAirMissionArea.Circle(state, germany, shortLegged, EUnitOrder.AirSuperiority, Field, CFixedPoint.FromInt(200));
		Assert.Equal(new[] { Field, Border, Berlin, Lake }, area);
	}

	// --- the frame ---

	[Fact]
	public void AUnitIsFitToFlyByItsStancesShareOfStrengthAndOrganisation()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fighters = Air(state, germany, Berlin, Fighter, Fighter);

		// Under no air order nothing is fit.
		Assert.False(CAirMissions.IsFitToFly(state, fighters));

		Give(state, germany, fighters, EUnitOrder.AirSuperiority, Berlin, CMission.Defensive);
		Assert.True(CAirMissions.IsFitToFly(state, fighters));

		// Defensive: half its strength, three tenths of its organisation - reached is enough.
		fighters.SubUnits[0].Strength = CFixedPoint.Zero;
		Assert.True(CAirMissions.IsFitToFly(state, fighters));
		fighters.SubUnits[1].Strength = CFixedPoint.Parse("99.9");
		Assert.False(CAirMissions.IsFitToFly(state, fighters));
		fighters.Mission.Stance = CMission.Aggressive;
		Assert.True(CAirMissions.IsFitToFly(state, fighters));

		fighters.SubUnits[0].Organisation = CFixedPoint.FromInt(6);
		fighters.SubUnits[1].Organisation = CFixedPoint.FromInt(6);
		Assert.True(CAirMissions.IsFitToFly(state, fighters));            // a fifth of thirty
		fighters.SubUnits[1].Organisation = CFixedPoint.Parse("5.9");
		Assert.False(CAirMissions.IsFitToFly(state, fighters));

		// And not before its start hour.
		fighters.SubUnits[1].Organisation = CFixedPoint.FromInt(30);
		fighters.Mission.StartHour = state.TotalHours + 1;
		Assert.False(CAirMissions.IsFitToFly(state, fighters));
	}

	[Fact]
	public void AMissionGivenAwayFromItsBaseFliesHomeFirstAndThenOut()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fighters = Air(state, germany, Border, Fighter);

		Give(state, germany, fighters, EUnitOrder.AirSuperiority, Field);

		Assert.True(fighters.Mission.ReturnToBase);
		Assert.Equal(new[] { Berlin }, fighters.Path);

		// Home, it is no longer returning and its order goes on; the hour after it flies.
		CTick.RunHour(state);
		Assert.Equal(Berlin, fighters.Location);
		CTick.RunHour(state);
		Assert.False(fighters.Mission.ReturnToBase);
		Assert.Equal(EUnitOrder.AirSuperiority, fighters.Order);
		CTick.RunHour(state);
		Assert.Equal(Field, fighters.Mission.Destination);
		Assert.True(fighters.IsMoving || fighters.Location == Field);
	}

	[Fact]
	public void AMissionIsOverOnceItsUnitIsHomePastItsEndHour()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fighters = Air(state, germany, Berlin, Fighter);
		Give(state, germany, fighters, EUnitOrder.AirSuperiority, Border);
		fighters.Mission.EndHour = state.TotalHours + 2;

		// It flies out; past its end it is told to return, and at home the order is done.
		for (int hour = 0; hour < 8 && fighters.Order != EUnitOrder.None; hour++)
		{
			CTick.RunHour(state);
		}

		Assert.Equal(EUnitOrder.None, fighters.Order);
		Assert.Equal(Berlin, fighters.Location);
	}

	[Fact]
	public void AUnitBeyondItsRangeTurnsForHomeAndLosesItsOrder()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit shortLegged = Air(state, germany, Berlin, ShortLegged);
		Give(state, germany, shortLegged, EUnitOrder.AirSuperiority, Border);
		// Put beyond its ninety by something outside the mission.
		state.Positions.Move(shortLegged, shortLegged.Location, East);
		shortLegged.Location = East;
		CMovement.Cancel(shortLegged);

		Assert.False(CAirMissions.IsStillGoing(state, germany, shortLegged));
		Assert.True(shortLegged.Mission.ReturnToBase);
		Assert.Equal(Berlin, shortLegged.Path[^1]);
	}

	[Fact]
	public void AMissionByDayTurnsForHomeAtNight()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fighters = Air(state, germany, Berlin, Fighter);
		Give(state, germany, fighters, EUnitOrder.AirSuperiority, East);
		fighters.Mission.Time = CMission.ByDay;

		// The game's hour nought is noon here; by its eighth it is night.
		Assert.True(CAirMissions.DayNightGate(state, germany, fighters));
		for (int hour = 0; hour < 8; hour++)
		{
			state.AdvanceOneHour();
		}
		state.Positions.Move(fighters, fighters.Location, Field);
		fighters.Location = Field;
		fighters.Mission.ReturnToBase = false;

		Assert.False(CAirMissions.DayNightGate(state, germany, fighters));
		Assert.True(fighters.Mission.ReturnToBase);
		Assert.Equal(Berlin, fighters.Path[^1]);

		// And one by night flies.
		fighters.Mission.Time = CMission.ByNight;
		Assert.True(CAirMissions.DayNightGate(state, germany, fighters));
	}

	// --- what a country knows ---

	[Fact]
	public void ACountryKnowsWhatItOrAFriendHoldsWhereItsUnitsStandAndTheLandBesideThem()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit fighters = Air(state, germany, Field, Fighter);

		// Nothing is known until the day's rebuild (intelligence.md, section 3).
		Assert.Equal(0, CIntelligence.Level(state, state.Province(Berlin), Ger));
		CProvinceIntel.Rebuild(state);
		Assert.Equal(9, CIntelligence.Level(state, state.Province(Berlin), Ger));
		Assert.Equal(9, CIntelligence.Level(state, state.Province(Field), Ger));
		// Beside its unit, its decryption equal to Poland's encryption: 2 + 1.
		Assert.Equal(3, CIntelligence.Level(state, state.Province(Warsaw), Ger));
		Assert.Equal(0, CIntelligence.Level(state, state.Province(East), Ger));

		// A fellow member of its faction's ground is its own to it.
		germany.Faction = "axis";
		poland.Faction = "axis";
		CProvinceIntel.Rebuild(state);
		Assert.Equal(9, CIntelligence.Level(state, state.Province(East), Ger));
		Assert.NotNull(fighters);
	}

	// --- picking a target ---

	[Fact]
	public void ATargetIsNeverOneAtItsOwnBaseNorOneThreeWingsAreAfterAlready()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit ours = Air(state, germany, Berlin, Fighter, Fighter);
		CUnit others = Air(state, germany, Berlin, Fighter, Fighter);
		CUnit atHome = Air(state, poland, Warsaw, Fighter);
		CUnit flying = Air(state, poland, Field, Fighter, Fighter);
		flying.SubUnits[0].Strength = CFixedPoint.FromInt(60);

		Assert.Null(CAirTargets.Score(state, ours, atHome, needsWings: false));
		Assert.Equal(160000, CAirTargets.Score(state, ours, flying, needsWings: false));

		// Two wings after it and two of our own are four: enough are going already.
		Give(state, germany, others, EUnitOrder.AirSuperiority, Field);
		others.Mission.Target = flying;
		Assert.Null(CAirTargets.Score(state, ours, flying, needsWings: false));

		// One wing of ours makes three, which may still go.
		ours.SubUnits.RemoveAt(1);
		Assert.Equal(160000, CAirTargets.Score(state, ours, flying, needsWings: false));

		// And the intercept will not have one with no wings.
		flying.SubUnits.Clear();
		Assert.Null(CAirTargets.Score(state, ours, flying, needsWings: true));
	}

	[Fact]
	public void AirSuperiorityGoesRoundItsProvincesWhenItHasNothingToChase()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fighters = Air(state, germany, Berlin, Fighter);

		Give(state, germany, fighters, EUnitOrder.AirSuperiority, Border, CMission.Aggressive, Border, Field);

		Assert.Equal(Border, fighters.Mission.Destination);
		var seen = new List<int> { fighters.Location };
		for (int hour = 0; hour < 8; hour++)
		{
			CTick.RunHour(state);
			if (seen[^1] != fighters.Location)
			{
				seen.Add(fighters.Location);
			}
		}
		Assert.Equal(new[] { Berlin, Border, Field, Border, Field }, seen.Take(5));
		Assert.Equal(EUnitOrder.AirSuperiority, fighters.Order);
	}

	[Fact]
	public void AirSuperiorityFliesAtTheStrongestItKnowsOfAndToWhereThatOneIsGoing()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit fighters = Air(state, germany, Berlin, Fighter);
		CUnit weak = Air(state, poland, Border, Bomber);
		weak.SubUnits[0].Strength = CFixedPoint.FromInt(50);
		CUnit strong = Air(state, poland, Border, Bomber);
		CMovement.SetPath(strong, new[] { Berlin }, clear: true);

		// It is Germany's own ground, so it is known; both are in the air, away from their base.
		CProvinceIntel.Rebuild(state);
		Give(state, germany, fighters, EUnitOrder.AirSuperiority, Border, CMission.Aggressive, Border, Berlin);

		Assert.Same(strong, fighters.Mission.Target);
		// The target is coming to Berlin, where the fighters are: nothing to fly, and it is
		// where they are going.
		Assert.Equal(Berlin, fighters.Mission.Destination);

		// One it does not know of is not there to be picked: Poland's ground with nobody of ours on it or beside it.
		CUnit far = Air(state, poland, East, Bomber);
		CUnit second = Air(state, germany, Berlin, Fighter);
		Give(state, germany, second, EUnitOrder.AirSuperiority, East, CMission.Aggressive, East);
		Assert.Null(second.Mission.Target);
		Assert.NotNull(far);
	}

	[Fact]
	public void AnInterceptWatchesTwoStepsAroundItsProvincesAndMeetsWhatIsOnItsWayIn()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit fighters = Air(state, germany, Berlin, Fighter);
		CUnit raid = Air(state, poland, Border, Bomber);
		CMovement.SetPath(raid, new[] { Berlin }, clear: true);
		CProvinceIntel.Rebuild(state);

		Give(state, germany, fighters, EUnitOrder.AirIntercept, Berlin);

		// Berlin, the Border beside it and Field beyond that.
		Assert.Equal(new[] { Berlin, Border, Field }, fighters.Mission.Watched);
		// The raid is in a province watched and known, with Berlin on its path: met there.
		Assert.Same(raid, fighters.Mission.Target);
		Assert.Equal(Berlin, fighters.Mission.Meeting);
		Assert.False(fighters.IsMoving);
	}

	[Fact]
	public void AnInterceptFliesToItsTargetAndIsToldToReturnWhenItHasCaughtIt()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit fighters = Air(state, germany, Berlin, Fighter);
		CUnit raid = Air(state, poland, Border, Transport);

		// The raid sits over the Border, which is in the mission: no meeting province, straight at it.
		CProvinceIntel.Rebuild(state);
		Give(state, germany, fighters, EUnitOrder.AirIntercept, Berlin, CMission.Aggressive, Berlin, Border);
		Assert.Same(raid, fighters.Mission.Target);
		Assert.Equal(0, fighters.Mission.Meeting);
		Assert.Equal(new[] { Border }, fighters.Path);

		CTick.RunHour(state);
		Assert.Equal(Border, fighters.Location);
		Assert.True(fighters.IsFighting);

		// In its battle it does nothing; the battle over, it has caught its target and goes home.
		for (int hour = 0; hour < 5; hour++)
		{
			CTick.RunHour(state);
		}
		Assert.False(fighters.IsFighting);
		Assert.True(fighters.Mission.ReturnToBase || fighters.Location == Berlin);
	}

	// --- what may fight what ---

	[Fact]
	public void InTheAirAnyTwoEnemiesFightAndAtABaseOnlyAFighterAgainstAUnitFitToFly()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit fighters = Air(state, germany, Field, Fighter);
		CUnit bombers = Air(state, germany, Field, Bomber);
		CUnit raid = Air(state, poland, Field, Bomber);
		CUnit atHome = Air(state, poland, Warsaw, Fighter);
		CUnit overWarsaw = Air(state, germany, Warsaw, Fighter);

		// Over Field nobody is at a base: orders do not come into it.
		Assert.True(CAirBattle.MayEngage(state, fighters, raid));
		Assert.True(CAirBattle.MayEngage(state, bombers, raid));
		Assert.True(CAirBattle.MayEngage(state, raid, bombers));
		Assert.False(CAirBattle.MayEngage(state, fighters, bombers));     // its own country's

		// Over Warsaw the Poles sit at their base with no order: unfit, and not to be fought.
		Assert.False(CAirBattle.MayEngage(state, overWarsaw, atHome));
		Give(state, poland, atHome, EUnitOrder.AirIntercept, Warsaw);
		// Fit now - but only a unit under a fighting order may fight one at its base.
		Assert.False(CAirBattle.MayEngage(state, overWarsaw, atHome));
		Give(state, germany, overWarsaw, EUnitOrder.AirSuperiority, Warsaw);
		overWarsaw.Mission.ReturnToBase = false;
		Assert.True(CAirBattle.MayEngage(state, overWarsaw, atHome));
		Assert.True(CAirBattle.MayEngage(state, atHome, overWarsaw));

		// Worn below its stance's share it is safe again.
		atHome.SubUnits[0].Organisation = CFixedPoint.One;
		Assert.False(CAirBattle.MayEngage(state, overWarsaw, atHome));
		Assert.False(CAirBattle.MayEngage(state, atHome, overWarsaw));
	}

	[Fact]
	public void OnlyAUnitUnderAFightingOrderStartsABattleAndTheRestAreOfferedASide()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit bombers = Air(state, germany, Field, Bomber);
		CUnit raid = Air(state, poland, Field, Bomber);

		CCombats.CheckForCombat(state, bombers, Field);
		Assert.Empty(state.Combats);

		CUnit fighters = Air(state, germany, Field, Fighter);
		Give(state, germany, fighters, EUnitOrder.AirSuperiority, Field);
		fighters.Mission.ReturnToBase = false;
		CMovement.Cancel(fighters);
		CCombats.CheckForCombat(state, fighters, Field);

		CCombat combat = Assert.Single(state.Combats);
		Assert.Equal(ECombatKind.Air, combat.Kind);
		Assert.Equal(new[] { fighters, bombers }, combat.Attacker.Units);
		Assert.Equal(new[] { raid }, combat.Defender.Units);
	}

	// --- the battle ---

	[Fact]
	public void ABattleLastsFourHoursAndItsUnitsHaveAnHoursGrace()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit fighters = Air(state, germany, Berlin, Fighter);
		CUnit raid = Air(state, poland, Berlin, Bomber);
		raid.Base = 0;                                               // or, with no order, it would fly home between battles
		Give(state, germany, fighters, EUnitOrder.AirSuperiority, Berlin);

		CTick.RunHour(state);                                        // it looks at its province, and fights the hour's round
		CCombat combat = Assert.Single(state.Combats);
		Assert.Equal(1, combat.Duration);
		CTick.RunHour(state);
		CTick.RunHour(state);
		Assert.Single(state.Combats);
		CTick.RunHour(state);                                        // the fourth round, and the end
		Assert.Empty(state.Combats);
		Assert.False(combat.IsConcluded);                            // nobody was worn to nothing
		Assert.Equal(1, fighters.NoBattleHours);

		CTick.RunHour(state);                                        // the hour of grace: no battle
		Assert.Empty(state.Combats);
		Assert.Equal(0, fighters.NoBattleHours);
		CTick.RunHour(state);                                        // and then it may be fought again
		Assert.Single(state.Combats);
		Assert.NotNull(raid);
	}

	[Fact]
	public void AWingFiresItsAttacksWorthOfShotsAndAHitCostsADieOfEach()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		state.Defines.Set(CManpower.MilitarySection, CAirFire.AvoidChance, CFixedPoint.Zero);
		state.Defines.Set(CManpower.MilitarySection, CAirFire.CriticalChance, CFixedPoint.Zero);
		CUnit fighters = Air(state, germany, Field, Fighter);
		CUnit raid = Air(state, poland, Field, Transport);
		var combat = new CCombat(Field, ECombatKind.Air);
		CAirBattle.Join(combat.Attacker, fighters);
		CAirBattle.Join(combat.Defender, raid);
		state.SeedHour();

		CAirModifiers.Apply(state, combat, combat.Attacker);
		CAirModifiers.Apply(state, combat, combat.Defender);
		CAirFire.Attack(state, combat, combat.Attacker);
		CAirFire.Attack(state, combat, combat.Defender);

		// Four shots, none avoided: each a die of four times ten of strength and a die of nine
		// times twelve and a half of organisation, noted and not yet taken off.
		CSubUnit hit = raid.SubUnits[0];
		Assert.InRange(hit.PendingStrengthDamage, 4 * 10000, 4 * 40000);
		Assert.InRange(hit.PendingOrganisationDamage, 4 * 12500, 4 * 112500);
		Assert.Equal(0, hit.PendingStrengthDamage % 10000);
		Assert.Equal(hit.Id, fighters.SubUnits[0].TargetId);
		// A wing with no air attack fires nothing.
		Assert.Equal(0, fighters.SubUnits[0].PendingStrengthDamage);
	}

	[Fact]
	public void TheBestTargetIsByPriorityAndTheStrengthItHasLostAndTheTargetingOfItsKind()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit raid = Air(state, poland, Field, Bomber, Fighter, Transport);
		List<CSubUnit> targets = raid.SubUnits;

		// Priority five against three: the fighter.
		Assert.Same(targets[1], CAirFire.Best(targets, germany));

		// A bomber that has lost enough is worth more: 3 x (250 + 800) against 5 x 250.
		targets[0].Strength = CFixedPoint.FromInt(20);
		Assert.Same(targets[0], CAirFire.Best(targets, germany));

		// And whole again, a country good at bombers still prefers it: 3 x 250 x 1.7 = 1275.
		targets[0].Strength = CFixedPoint.FromInt(100);
		germany.Technology.Set(CAirFire.BomberTargeting, CFixedPoint.Parse("0.7"));
		Assert.Same(targets[0], CAirFire.Best(targets, germany));
		Assert.Equal(700, CAirFire.TargetingSkill(targets[0], germany));
	}

	[Fact]
	public void WhatTheHourMakesOfAWingGoesOnItsAttack()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit fighters = Air(state, germany, Border, Fighter, Fighter);
		CUnit raid = Air(state, poland, Border, Bomber);
		Give(state, germany, fighters, EUnitOrder.AirIntercept, Berlin, CMission.Aggressive, Berlin, Border);
		var combat = new CCombat(Border, ECombatKind.Air);
		CAirBattle.Join(combat.Attacker, fighters);
		CAirBattle.Join(combat.Defender, raid);

		CAirModifiers.Apply(state, combat, combat.Attacker);
		CAirModifiers.Apply(state, combat, combat.Defender);

		// Beside its base, on an intercept, two wings on its side: 1.1 x 1.2 x 0.9.
		CSubUnit wing = fighters.SubUnits[0];
		Assert.Equal(CFixedPoint.Parse("1.188"), wing.CombatAttack);
		Assert.Equal(new[] { ECombatModifier.BaseProximity, ECombatModifier.Intercept, ECombatModifier.Stacking },
			wing.CombatModifiers.Select(modifier => modifier.Kind));
		// The intercept's bonus is on the attack alone.
		Assert.Equal(CFixedPoint.Parse("0.99"), wing.CombatDefence);
		// A bomber far from home with nobody beside it: nothing.
		Assert.Equal(CFixedPoint.One, raid.SubUnits[0].CombatAttack);
	}

	[Fact]
	public void AnAirMarshalWithTheRankForHisWingsAddsHisSkillAndTenAPointToTheirTargeting()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		state.Defines.Set(CManpower.MilitarySection, "AIR_RANK_1", CFixedPoint.FromInt(1));
		state.Defines.Set(CManpower.MilitarySection, CCombatModifiers.LeaderImpact, CFixedPoint.Parse("0.05"));
		CUnit fighters = Air(state, germany, Field, Fighter);
		fighters.Leader = new CLeader(1) { Skill = 3, Rank = 1, Name = "marshal" };
		CUnit raid = Air(state, poland, Field, Bomber, Bomber);
		raid.Leader = new CLeader(2) { Skill = 4, Rank = 1, Name = "marshal" };
		var combat = new CCombat(Field, ECombatKind.Air);
		CAirBattle.Join(combat.Attacker, fighters);
		CAirBattle.Join(combat.Defender, raid);

		CAirModifiers.Apply(state, combat, combat.Attacker);
		CAirModifiers.Apply(state, combat, combat.Defender);

		Assert.Contains(fighters.SubUnits[0].CombatModifiers,
			m => m.Kind == ECombatModifier.Leader && m.Attack.Raw == 150 && m.Defence.Raw == 150);
		// His rank commands one wing and the raid has two: no bonus there - but his skill still picks targets.
		Assert.DoesNotContain(raid.SubUnits[0].CombatModifiers, m => m.Kind == ECombatModifier.Leader);
		Assert.Equal(40, CAirFire.TargetingSkill(raid.SubUnits[0], poland));
	}

	[Fact]
	public void ASideWornToNothingLosesAtTheFourthHourAndFallsBackOutToSeaWhereThereIsAny()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit fighters = Air(state, germany, Field, Fighter);
		CUnit raid = Air(state, poland, Field, Bomber);
		raid.SubUnits[0].Organisation = CFixedPoint.Parse("0.5");
		var combat = new CCombat(Field, ECombatKind.Air);
		state.Combats.Add(combat);
		CAirBattle.Join(combat.Attacker, fighters);
		CAirBattle.Join(combat.Defender, raid);
		combat.Duration = 3;
		combat.Hour = 3;
		state.SeedHour();

		Assert.True(CAirBattle.RunHour(state, combat));

		Assert.True(combat.IsConcluded);
		Assert.False(combat.AttackerLost);
		// Field has the lake beside it, and that is where a beaten air unit goes.
		Assert.True(raid.IsRetreating);
		Assert.Equal(new[] { Lake }, raid.Path);
		Assert.Empty(raid.Combats);

		// With no sea beside it, its base.
		Assert.Equal(Warsaw, CAirRetreat.FindRetreatProvince(state, Air(state, poland, East, Bomber)));
	}

	[Fact]
	public void AnAirUnitAwayFromItsBaseWithNoOrganisationIsSentHome()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fighters = Air(state, germany, Berlin, Fighter);
		Give(state, germany, fighters, EUnitOrder.AirSuperiority, Field);
		state.Positions.Move(fighters, fighters.Location, Field);
		fighters.Location = Field;
		CMovement.Cancel(fighters);
		fighters.SubUnits[0].Organisation = CFixedPoint.Zero;

		CAirMissions.SendHomeEmpty(state, germany, fighters);

		Assert.Equal(EUnitOrder.None, fighters.Order);
		Assert.Equal(Berlin, fighters.Path[^1]);

		// And with next to no strength it is gone.
		fighters.SubUnits[0].Strength = CFixedPoint.Zero;
		CAirMissions.SendHomeEmpty(state, germany, fighters);
		Assert.True(fighters.IsRemoved);
	}
}
