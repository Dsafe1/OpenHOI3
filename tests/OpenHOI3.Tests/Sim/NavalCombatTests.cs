using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A battle between fleets against <c>docs/mechanics/navalcombat.md</c>, on a small sea:
///
///     1 Kiel --- 3 Bight --- 4 Deep --- 2 Gdynia (Poland's)
///                               \
///                                5 Far
///
/// Kiel's port opens onto the Bight and Gdynia's onto the Deep.
/// </summary>
public class NavalCombatTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Kiel = 1;
	private const int Gdynia = 2;
	private const int Bight = 3;
	private const int Deep = 4;
	private const int Far = 5;

	private static readonly CBuilding NavalBase = new(0, "naval_base", 10, "", CFixedPoint.Zero);

	private static CProvinceEdge Edge(int to)
	{
		return new CProvinceEdge(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.FromInt(10));
	}

	private static CProvince Port(int id, string name, CCountryTag owner, int x, int portSea, int seaIndex, params CProvinceEdge[] edges)
	{
		var province = new CProvince(id, name, isSea: false)
		{
			Owner = owner, Controller = owner, CentreX = x, CentreY = 10, PortSea = portSea, SeaIndex = seaIndex, Edges = edges,
		};
		province.SetBuildingLevel(NavalBase, CFixedPoint.FromInt(2));
		return province;
	}

	private static CProvince Sea(int id, string name, int x, int seaIndex, params CProvinceEdge[] edges)
	{
		return new CProvince(id, name, isSea: true) { CentreX = x, CentreY = 20, SeaIndex = seaIndex, Edges = edges };
	}

	private static CGameState State(out CCountry germany, out CCountry poland, bool atWar = true)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		var defines = new CDefines();
		void Military(string name, string value) => defines.Set(CManpower.MilitarySection, name, CFixedPoint.Parse(value));
		Military(CMovementSpeed.NavalSpeedModifier, "0.5");
		Military(CNavalBattle.HullLimit, "16");
		Military(CNavalBattle.HullPenalty, "0.04");
		Military(CNavalBattle.MostPenalty, "0.8");
		Military(CNavalFire.OrganisationDice, "1");
		Military(CNavalFire.StrengthDice, "10");
		Military(CNavalFire.CriticalChance, "10");
		Military(CNavalFire.CriticalDamage, "10");
		Military(CNavalFire.StrengthDamage, "0.12");
		Military(CNavalFire.OrganisationDamage, "1");
		Military(CNavalFire.AvoidChance, "48");
		Military(CNavalModifiers.SurpriseChance, "30");
		Military(CNavalModifiers.SurpriseBonus, "5");
		Military(CNavalModifiers.SurpriseRounds, "3");
		Military(CNavalModifiers.PrideBonus, "10");
		Military(CCombatModifiers.NightPenalty, "-0.5");
		var wars = new List<CWar>();
		if (atWar)
		{
			var war = new CWar("war");
			war.Attackers.Add(Ger);
			war.Defenders.Add(Pol);
			wars.Add(war);
		}
		return new CGameState(new CDate(1936, 6, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				Port(Kiel, "Kiel", Ger, 10, Bight, 0, Edge(Bight)),
				Port(Gdynia, "Gdynia", Pol, 70, Deep, 1, Edge(Deep)),
				Sea(Bight, "Bight", 30, 2, Edge(Kiel), Edge(Deep)),
				Sea(Deep, "Deep", 50, 3, Edge(Bight), Edge(Gdynia), Edge(Far)),
				Sea(Far, "Far", 50, 4, Edge(Deep)),
			},
			new[] { germany, poland }, buildings: new[] { NavalBase }, defines: defines, wars: wars)
		{
			MapWidth = 1000,
			MapHeight = 1000,
		};
	}

	private static CSubUnitType Type(string key, string attack = "10", string hull = "1", string distance = "0.2",
		string priority = "4", bool capital = false, bool submarine = false, bool transport = false, string carrier = "0")
	{
		var type = new CSubUnitType(key, EUnitBranch.Naval);
		void Set(string name, string value) => type.Stats.Values.Set(name, CFixedPoint.Parse(value));
		Set(CMovementSpeed.MaximumSpeed, "20");
		Set(CUnitOrders.Range, "1000");
		Set(CSubUnitType.MaxStrength, "100");
		Set(CSubUnitType.DefaultOrganisation, "30");
		Set(CNavalFire.SeaAttack, attack);
		Set(CNavalTargets.SubAttack, "2");
		Set(CNavalBattle.Hull, hull);
		Set(CNavalBattle.FiringDistance, distance);
		Set(CNavalTargets.Priority, priority);
		Set(CNavalSpotting.SurfaceDetection, "100");
		Set(CNavalSpotting.SubDetection, "100");
		Set(CNavalSpotting.Visibility, "1000");
		Set(CShipCensus.CarrierSize, carrier);
		type.Flags[CShipCensus.CapitalFlag] = capital;
		type.Flags[CShipCensus.SubmarineFlag] = submarine;
		type.Flags[CShipCensus.TransportFlag] = transport;
		return type;
	}

	private static readonly CSubUnitType Destroyer = Type("destroyer");
	private static readonly CSubUnitType Battleship = Type("battleship", attack: "15", hull: "1.5", distance: "0.32", priority: "9", capital: true);
	private static readonly CSubUnitType Submarine = Type("submarine", attack: "3", hull: "0.5", distance: "0.15", priority: "3", submarine: true);
	private static readonly CSubUnitType Transport = Type("transport_ship", attack: "0", priority: "1", transport: true);
	private static readonly CSubUnitType Carrier = Type("carrier", attack: "1", distance: "1", priority: "10", capital: true, carrier: "2");

	private static CUnit Fleet(CGameState state, CCountry country, int province, params CSubUnitType[] ships)
	{
		var fleet = new CUnit(EUnitKind.Navy, country.Tag)
		{
			Id = ++state.LastUnitId, Location = province, Base = country.Tag == Ger ? Kiel : Gdynia, Name = country.Tag + " fleet",
		};
		foreach (CSubUnitType type in ships)
		{
			var ship = new CSubUnit(type)
			{
				Id = ++state.LastUnitId, Unit = fleet, Strength = CFixedPoint.FromInt(100),
				Organisation = CFixedPoint.FromInt(30), Experience = CFixedPoint.Zero, Name = type.Key,
			};
			ship.Stats.CopyFrom(type.Stats);
			fleet.SubUnits.Add(ship);
		}
		country.Units.Add(fleet);
		state.ForgetPositions();
		return fleet;
	}

	/// <summary>Two fleets in the Bight with the battle between them already made.</summary>
	private static CCombat Battle(CGameState state, CUnit attacker, CUnit defender)
	{
		var combat = new CCombat(attacker.Location, ECombatKind.Naval);
		state.Combats.Add(combat);
		CNavalBattle.Join(state, combat, combat.Attacker, attacker);
		CNavalBattle.Join(state, combat, combat.Defender, defender);
		return combat;
	}

	// --- what starts one ---

	[Fact]
	public void AShipIsTheFirstSortItAnswersTo()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Bight, Battleship, Carrier, Submarine, Transport, Destroyer, Destroyer);

		// A carrier that is a capital ship is counted with the capital ships.
		Assert.Equal(new CShipCensus(Capitals: 2, Transports: 1, Submarines: 1, Carriers: 0, Screens: 2), CShipCensus.Of(fleet));
	}

	[Fact]
	public void AnIdleFleetOfACountryNobodyPlaysStartsNoBattle()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		Fleet(state, germany, Bight, Destroyer);
		Fleet(state, poland, Bight, Destroyer);

		CTick.RunHour(state);
		Assert.Empty(state.Combats);

		// Played by a person, the same fleet looks every hour - and these two cannot miss.
		state.Played.Add(Ger);
		CTick.RunHour(state);
		CCombat combat = Assert.Single(state.Combats);
		Assert.Equal(ECombatKind.Naval, combat.Kind);
		Assert.Equal(Ger, combat.Attacker.Units[0].Owner);
		Assert.Equal(Pol, combat.Defender.Units[0].Owner);
	}

	[Fact]
	public void FleetsAtPeaceOrInPortDoNotFight()
	{
		CGameState peace = State(out CCountry germany, out CCountry poland, atWar: false);
		peace.Played.Add(Ger);
		Fleet(peace, germany, Bight, Destroyer);
		Fleet(peace, poland, Bight, Destroyer);
		CTick.RunHour(peace);
		Assert.Empty(peace.Combats);

		CGameState state = State(out germany, out poland);
		state.Played.Add(Ger);
		CUnit ours = Fleet(state, germany, Kiel, Destroyer);
		CUnit theirs = Fleet(state, poland, Bight, Destroyer);
		CTick.RunHour(state);
		Assert.Empty(state.Combats);
		Assert.False(CNavalBattle.MayEngage(state, ours, ours));
		Assert.True(CNavalBattle.MayEngage(state, ours, theirs));
	}

	[Fact]
	public void SubmarinesDoNotAttackAFleetWithScreensEnough()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		state.Played.Add(Ger);
		CUnit wolves = Fleet(state, germany, Bight, Submarine, Submarine, Submarine);
		CUnit screened = Fleet(state, poland, Bight, Destroyer, Destroyer);
		CUnit bare = Fleet(state, poland, Deep, Destroyer);

		// Three submarines: more than one screen is too many.
		Assert.False(CNavalSpotting.ShouldStart(state, wolves, screened));
		Assert.True(CNavalSpotting.ShouldStart(state, wolves, bare));
	}

	[Fact]
	public void AFleetStillWaitingAfterABattleStartsNone()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		state.Played.Add(Ger);
		CUnit ours = Fleet(state, germany, Bight, Destroyer);
		CUnit theirs = Fleet(state, poland, Bight, Destroyer);
		ours.EngageWait = 3;

		Assert.False(CNavalSpotting.ShouldStart(state, ours, theirs));
		CTick.RunHour(state);
		CTick.RunHour(state);
		Assert.Empty(state.Combats);
		Assert.Equal(1, ours.EngageWait);
		CTick.RunHour(state);
		CTick.RunHour(state);
		Assert.Single(state.Combats);
	}

	// --- where the ships stand ---

	[Fact]
	public void ShipsAreDrawnUpByTheirReach()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit ours = Fleet(state, germany, Bight, Battleship, Destroyer, Destroyer, Destroyer, Destroyer);
		CUnit theirs = Fleet(state, poland, Bight, Destroyer);
		Battle(state, ours, theirs);

		// A battleship reaches 32: it stands at 16 or up to 0.3 x 16 x 5 = 24 further out.
		long line = ours.SubUnits[0].Distance.Raw;
		Assert.InRange(line, 16000, 16000 + 23000);
		// A destroyer reaches 20: before the line or behind it by 10 to 24, and never nearer than 1.
		foreach (CSubUnit destroyer in ours.SubUnits.Skip(1))
		{
			long off = destroyer.Distance.Raw - line;
			Assert.True(destroyer.Distance.Raw == 1000 || (System.Math.Abs(off) >= 10000 && System.Math.Abs(off) <= 24000),
				$"a destroyer at {destroyer.Distance} with the line at {new CFixedPoint(line)}");
		}
		Assert.Equal(32000, CNavalBattle.Reach(ours.SubUnits[0]));
	}

	[Fact]
	public void AFleetThatJoinsLateStartsAHundredFurtherOut()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit ours = Fleet(state, germany, Bight, Battleship);
		CUnit theirs = Fleet(state, poland, Bight, Destroyer);
		CCombat combat = Battle(state, ours, theirs);
		combat.Hour = 0;
		long line = ours.SubUnits[0].Distance.Raw;

		CUnit late = Fleet(state, germany, Bight, Battleship);
		Assert.True(CCombats.Offer(state, combat, late));
		Assert.Contains(late, combat.Attacker.Units);
		Assert.InRange(late.SubUnits[0].Distance.Raw, 100000 + 16000, 100000 + 16000 + 23000);
		Assert.Equal(line, ours.SubUnits[0].Distance.Raw);
	}

	[Fact]
	public void PositioningIsARollOnFourTenthsLessWhatACrowdCosts()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit few = Fleet(state, germany, Bight, Battleship, Destroyer);
		// Fourteen battleships are 21 hull: five over the limit, at 0.04 each.
		CUnit many = Fleet(state, poland, Bight, Enumerable.Repeat(Battleship, 14).ToArray());
		CCombat combat = Battle(state, few, many);

		Assert.Equal(0, CNavalBattle.StackingPenalty(state, combat.Attacker));
		Assert.Equal(200, CNavalBattle.StackingPenalty(state, combat.Defender));

		CNavalBattle.Begin(state, combat);
		Assert.InRange(combat.Attacker.Positioning.Raw, 400, 590);
		Assert.Equal(combat.Attacker.PositioningRoll.Raw + 400, combat.Attacker.Positioning.Raw);
		Assert.Equal(combat.Defender.PositioningRoll.Raw + 200, combat.Defender.Positioning.Raw);
		Assert.Equal(0, combat.Attacker.PositioningRoll.Raw % 10);
	}

	// --- who a ship shoots at ---

	[Fact]
	public void AShipReachesAnotherWhenTheirDistancesTogetherAreWithinItsReach()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit ours = Fleet(state, germany, Bight, Destroyer);
		CUnit theirs = Fleet(state, poland, Bight, Destroyer, Destroyer, Submarine);
		CCombat combat = Battle(state, ours, theirs);
		CSubUnit ship = ours.SubUnits[0];
		ship.Distance = CFixedPoint.FromInt(8);
		theirs.SubUnits[0].Distance = CFixedPoint.FromInt(12);       // 20 together: just in reach
		theirs.SubUnits[1].Distance = CFixedPoint.Parse("12.001");   // a hair too far
		theirs.SubUnits[2].Distance = CFixedPoint.FromInt(1);
		theirs.SubUnits[2].TargetId = ours.SubUnits[0].Id;           // a submarine that is shooting is seen

		CNavalTargets.Collect(state, combat.Defender, combat.Attacker, ship);
		Assert.Equal(new[] { theirs.SubUnits[0], theirs.SubUnits[2] }, combat.Attacker.Targets);

		// A ship already sunk on this hour's notes is not shot at again.
		theirs.SubUnits[0].PendingStrengthDamage = 100001;
		CNavalTargets.Collect(state, combat.Defender, combat.Attacker, ship);
		Assert.Equal(new[] { theirs.SubUnits[2] }, combat.Attacker.Targets);
	}

	[Fact]
	public void TheBestTargetIsTheMostValuableAndOfTwoAlikeTheMoreDamaged()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit ours = Fleet(state, germany, Bight, Destroyer);
		CUnit theirs = Fleet(state, poland, Bight, Destroyer, Battleship, Battleship);
		CCombat combat = Battle(state, ours, theirs);
		combat.Attacker.Targets.AddRange(theirs.SubUnits);

		// Of two battleships alike, the first.
		Assert.Same(theirs.SubUnits[1], CNavalTargets.Best(state, combat.Attacker));
		theirs.SubUnits[2].Strength = CFixedPoint.FromInt(60);
		Assert.Same(theirs.SubUnits[2], CNavalTargets.Best(state, combat.Attacker));
		// A destroyer has to be badly hurt to be worth more than a whole battleship:
		// 4 x (0.25 + 0.01 x 90) is 4.6 against 9 x 0.65.
		theirs.SubUnits[0].Strength = CFixedPoint.FromInt(10);
		Assert.Same(theirs.SubUnits[2], CNavalTargets.Best(state, combat.Attacker));
	}

	// --- the hour ---

	[Fact]
	public void TheSubmarinesSurpriseIsRolledOnceAndHoldsThreeRounds()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		state.Defines.Set(CManpower.MilitarySection, CNavalModifiers.SurpriseChance, CFixedPoint.FromInt(100));
		CUnit wolves = Fleet(state, germany, Bight, Submarine, Destroyer);
		CUnit theirs = Fleet(state, poland, Bight, Submarine);
		CCombat combat = Battle(state, wolves, theirs);

		for (int hour = 0; hour < 3; hour++)
		{
			combat.Hour = hour;
			CNavalModifiers.Apply(state, combat, combat.Attacker);
			CNavalModifiers.Apply(state, combat, combat.Defender);
			Assert.Equal(CFixedPoint.FromInt(6), wolves.SubUnits[0].CombatAttack);
			Assert.Equal(CFixedPoint.One, wolves.SubUnits[1].CombatAttack);
			// Only the attacker's submarines are ever a surprise.
			Assert.Equal(CFixedPoint.One, theirs.SubUnits[0].CombatAttack);
		}
		combat.Hour = 3;
		CNavalModifiers.Apply(state, combat, combat.Attacker);
		Assert.Equal(CFixedPoint.One, wolves.SubUnits[0].CombatAttack);
	}

	[Fact]
	public void EnteringABattleSavedBeforeItsFirstRoundRollsTheSurpriseAndOneSavedLaterDoesNot()
	{
		// Entering a game asks every battle's modifiers again, the attacker first (savegame.md, section 20).
		CGameState state = State(out CCountry germany, out CCountry poland);
		state.Defines.Set(CManpower.MilitarySection, CNavalModifiers.SurpriseChance, CFixedPoint.FromInt(100));
		CUnit wolves = Fleet(state, germany, Bight, Submarine, Destroyer);
		CUnit theirs = Fleet(state, poland, Bight, Submarine);
		CCombat combat = Battle(state, wolves, theirs);

		// Saved before its first round: one draw for the attacker's fleet with a submarine, none for the defender's.
		combat.Hour = CCombat.NotStarted;
		long draws = state.Random.Draws;
		CCombats.ApplyModifiers(state, combat);
		Assert.Equal(draws + 1, state.Random.Draws);
		Assert.Equal(CFixedPoint.FromInt(6), wolves.SubUnits[0].CombatAttack);
		Assert.True(combat.Attacker.HadSurprise);

		// Saved after its rounds began: nothing is drawn, and a surprise the save does not keep is gone.
		combat.Hour = 1;
		combat.Attacker.HadSurprise = false;
		draws = state.Random.Draws;
		CCombats.ApplyModifiers(state, combat);
		Assert.Equal(draws, state.Random.Draws);
		Assert.Equal(CFixedPoint.One, wolves.SubUnits[0].CombatAttack);
		Assert.False(combat.Attacker.HadSurprise);
	}

	[Fact]
	public void ACapitalShipWithTooFewScreensDefendsWorseAndAnExperiencedShipDoesBoth()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit ours = Fleet(state, germany, Bight, Battleship, Battleship, Destroyer);
		CUnit theirs = Fleet(state, poland, Bight, Destroyer);
		CCombat combat = Battle(state, ours, theirs);
		combat.Hour = 5;
		ours.SubUnits[2].Experience = CFixedPoint.FromInt(20);

		CNavalModifiers.Apply(state, combat, combat.Attacker);
		// One screen to two capital ships: (1 - 0.5) / 3 off their defence, and nothing off their attack.
		CSubUnit battleship = ours.SubUnits[0];
		bool night = OpenHOI3.Sim.Weather.CDaylight.IsNight(state, state.Province(Bight));
		Assert.Contains(battleship.CombatModifiers, m => m.Kind == ECombatModifier.PoorScreens && m.Defence.Raw == -166 && m.Attack.Raw == 0);
		Assert.Equal(night ? 500 : 1000, battleship.CombatAttack.Raw);
		// Twenty experience is a fifth more of both.
		Assert.Contains(ours.SubUnits[2].CombatModifiers, m => m.Kind == ECombatModifier.Experience && m.Attack.Raw == 200 && m.Defence.Raw == 200);
	}

	[Fact]
	public void AShipWithNoTargetRunsInAndOneBreakingAwayDrawsOff()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit ours = Fleet(state, germany, Bight, Transport);
		CUnit theirs = Fleet(state, poland, Bight, Transport);
		CCombat combat = Battle(state, ours, theirs);
		ours.SubUnits[0].Distance = CFixedPoint.FromInt(50);
		theirs.SubUnits[0].Distance = CFixedPoint.FromInt(50);
		theirs.Disengage = 1;

		// No sea attack, so no target: in by its whole speed of 20.
		CNavalFire.Attack(state, combat, combat.Attacker);
		Assert.Equal(CFixedPoint.FromInt(30), ours.SubUnits[0].Distance);
		// A fleet that is breaking away opens by half its speed over its hull, and counts an hour more.
		CNavalFire.Attack(state, combat, combat.Defender);
		Assert.Equal(CFixedPoint.FromInt(60), theirs.SubUnits[0].Distance);
		Assert.Equal(2, theirs.Disengage);

		// Never nearer than 1.
		CNavalFire.Attack(state, combat, combat.Attacker);
		CNavalFire.Attack(state, combat, combat.Attacker);
		Assert.Equal(CFixedPoint.One, ours.SubUnits[0].Distance);
	}

	[Fact]
	public void AShipInAFleetWithACarrierHoldsOff()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit ours = Fleet(state, germany, Bight, Carrier, Transport);
		CUnit theirs = Fleet(state, poland, Bight, Transport);
		CCombat combat = Battle(state, ours, theirs);
		ours.SubUnits[1].Distance = CFixedPoint.FromInt(50);

		CNavalFire.Attack(state, combat, combat.Attacker);
		Assert.Equal(CFixedPoint.FromInt(50), ours.SubUnits[1].Distance);
	}

	[Fact]
	public void AShipsNotesComeOffWholeAndOneLeftUnderATenthIsSunk()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit ours = Fleet(state, germany, Bight, Destroyer, Destroyer);
		CUnit theirs = Fleet(state, poland, Bight, Destroyer);
		CCombat combat = Battle(state, ours, theirs);
		ours.SubUnits[0].PendingStrengthDamage = 2500;
		ours.SubUnits[0].PendingOrganisationDamage = 99000;
		CSubUnit doomed = ours.SubUnits[1];
		doomed.PendingStrengthDamage = 99950;

		CNavalLosses.Settle(state, combat.Attacker);
		Assert.Equal(CFixedPoint.Parse("97.5"), ours.SubUnits[0].Strength);
		Assert.Equal(CFixedPoint.Zero, ours.SubUnits[0].Organisation);
		Assert.Equal(0, ours.SubUnits[0].PendingStrengthDamage);
		Assert.Equal(CFixedPoint.Parse("0.05"), doomed.Strength);

		CNavalLosses.RemoveSunk(state, combat.Attacker);
		Assert.Equal(new[] { doomed }, combat.Attacker.Sunk);
		Assert.Single(ours.SubUnits);
		Assert.False(ours.IsRemoved);

		// The last ship takes the fleet with it.
		ours.SubUnits[0].Strength = CFixedPoint.Zero;
		CNavalLosses.RemoveSunk(state, combat.Attacker);
		Assert.True(ours.IsRemoved);
		Assert.Empty(combat.Attacker.Units);
	}

	[Fact]
	public void AShipCountsOnItsSidesLossesOnlyWhereItsNotesPassItsStrength()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit ours = Fleet(state, germany, Bight, Destroyer, Destroyer, Destroyer);
		CUnit theirs = Fleet(state, poland, Bight, Destroyer);
		CCombat combat = Battle(state, ours, theirs);
		ours.SubUnits[0].PendingStrengthDamage = 99950;       // sunk with a little left, and not counted
		ours.SubUnits[1].PendingStrengthDamage = 100000;      // to nothing exactly, not counted
		ours.SubUnits[2].PendingStrengthDamage = 100001;      // past it: one

		CNavalLosses.Settle(state, combat.Attacker);
		CNavalLosses.Settle(state, combat.Defender);

		Assert.Equal(CFixedPoint.One, combat.Attacker.Losses);
		Assert.Equal(CFixedPoint.Zero, combat.Defender.Losses);
	}

	// --- the end ---

	[Fact]
	public void ASideWhoseShipsAverageUnderOneOrganisationHasLost()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit ours = Fleet(state, germany, Bight, Destroyer, Destroyer);
		CUnit theirs = Fleet(state, poland, Bight, Destroyer);
		CCombat combat = Battle(state, ours, theirs);

		Assert.False(CNavalBattle.HasLost(combat.Attacker));
		ours.SubUnits[0].Organisation = CFixedPoint.Parse("1.999");
		ours.SubUnits[1].Organisation = CFixedPoint.Zero;
		Assert.True(CNavalBattle.HasLost(combat.Attacker));
		ours.SubUnits[0].Organisation = CFixedPoint.FromInt(2);
		Assert.False(CNavalBattle.HasLost(combat.Attacker));
	}

	[Fact]
	public void ABeatenFleetRunsForThePortItLiesOff()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit inBight = Fleet(state, germany, Bight, Destroyer);
		CUnit inDeep = Fleet(state, germany, Deep, Destroyer);
		CUnit inFar = Fleet(state, germany, Far, Destroyer);

		Assert.Equal(Kiel, CNavalRetreat.FindRetreatProvince(state, inBight));
		// Off an enemy's port: the water with a harbour of its own beside it.
		Assert.Equal(Bight, CNavalRetreat.FindRetreatProvince(state, inDeep));
		Assert.Equal(Deep, CNavalRetreat.FindRetreatProvince(state, inFar));

		CUnit theirs = Fleet(state, poland, Bight, Destroyer);
		CCombat combat = Battle(state, inBight, theirs);
		CNavalRetreat.Retreat(state, inBight, combat.Attacker);
		Assert.True(inBight.IsRetreating);
		Assert.Equal(new[] { Kiel }, inBight.Path);
		Assert.Empty(inBight.Combats);
	}

	[Fact]
	public void AFleetOrderedOutOfABattleCountsFourHoursBeforeItGoes()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit ours = Fleet(state, germany, Bight, Transport);
		CUnit theirs = Fleet(state, poland, Bight, Transport);
		CCombat combat = Battle(state, ours, theirs);
		combat.Duration = 10;

		var order = new CMoveUnitCommand(Ger, ours.Id, Deep);
		Assert.True(order.MayExecute(state));
		order.Execute(state);
		Assert.False(ours.IsMoving);
		Assert.Equal(Deep, ours.DisengageTo);
		Assert.Equal(1, ours.Disengage);

		for (int hour = 0; hour < 3; hour++)
		{
			CNavalFire.Attack(state, combat, combat.Attacker);
			Assert.False(ours.IsMoving);
			Assert.Contains(combat, ours.Combats);
		}
		CNavalFire.Attack(state, combat, combat.Attacker);
		Assert.Equal(new[] { Deep }, ours.Path);
		Assert.Empty(ours.Combats);
		// The other side still has somebody: it is falling back until it gets there.
		Assert.True(ours.IsRetreating);
		Assert.InRange(ours.EngageWait, 8, 15);
		// Its going decided the battle against its side.
		Assert.True(combat.IsConcluded);
		Assert.True(combat.AttackerLost);
	}

	[Fact]
	public void TheRetreatButtonSendsAFleetWhereABeatenOneWouldGo()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit ours = Fleet(state, germany, Bight, Transport);
		CUnit theirs = Fleet(state, poland, Bight, Transport);
		CCombat combat = Battle(state, ours, theirs);
		combat.Duration = 10;

		// The province is kept and the count started, as a move order there would.
		var retreat = new CBreakAwayCommand(Ger, ours.Id);
		Assert.True(retreat.MayExecute(state));
		retreat.Execute(state);
		Assert.Equal(Kiel, ours.DisengageTo);
		Assert.Equal(1, ours.Disengage);
		Assert.False(ours.IsMoving);

		// Once the count has run it sets off there.
		for (int hour = 0; hour < 4; hour++)
		{
			CNavalFire.Attack(state, combat, combat.Attacker);
		}
		Assert.Equal(new[] { Kiel }, ours.Path);
		Assert.Empty(ours.Combats);
	}

	[Fact]
	public void AFleetThatHasCountedLongEnoughIsGivenItsWayAtOnce()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit ours = Fleet(state, germany, Bight, Transport);
		CUnit theirs = Fleet(state, poland, Bight, Transport);
		CCombat combat = Battle(state, ours, theirs);
		combat.Duration = 10;
		// The battle's own roll has had it counting, with nowhere named.
		ours.Disengage = CNavalDisengage.CountToLeave;

		new CBreakAwayCommand(Ger, ours.Id).Execute(state);
		Assert.Equal(new[] { Kiel }, ours.Path);
		Assert.Empty(ours.Combats);
	}

	[Fact]
	public void NoFleetLeavesABattleAtSeaInItsFirstFourRounds()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit ours = Fleet(state, germany, Bight, Transport);
		CUnit theirs = Fleet(state, poland, Bight, Transport);
		CCombat combat = Battle(state, ours, theirs);
		ours.Disengage = 5;
		ours.DisengageTo = Deep;

		combat.Duration = 3;
		Assert.False(CNavalDisengage.MayLeave(ours));
		CNavalDisengage.CountHour(state, combat, combat.Attacker);
		Assert.Contains(combat, ours.Combats);

		combat.Duration = 4;
		Assert.True(CNavalDisengage.MayLeave(ours));
		CNavalDisengage.CountHour(state, combat, combat.Attacker);
		Assert.Empty(ours.Combats);
	}

	[Fact]
	public void TheRollToBreakOffSetsEveryFleetCountingAndEndsTheBattleAtFive()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit ours = Fleet(state, germany, Bight, Transport);
		CUnit theirs = Fleet(state, poland, Bight, Transport);
		CCombat combat = Battle(state, ours, theirs);

		// Not before the third round, whatever the roll.
		combat.Duration = 2;
		Assert.False(CNavalDisengage.BreakOff(state, combat));
		Assert.Equal(0, ours.Disengage);

		// A battle old enough passes every roll: 990 rounds and 11 make a thousand and one.
		combat.Duration = 990;
		Assert.False(CNavalDisengage.BreakOff(state, combat));
		Assert.Equal(1, ours.Disengage);
		Assert.Equal(1, theirs.Disengage);
		ours.Disengage = 4;
		Assert.True(CNavalDisengage.BreakOff(state, combat));
		Assert.Equal(5, ours.Disengage);
		// The first fleet to have had enough ends it: the defender's is not counted on.
		Assert.Equal(1, theirs.Disengage);
	}

	[Fact]
	public void ABattleIsFoughtToAnEndAndTheFleetsLetOutOfItWait()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		state.Played.Add(Ger);
		CUnit ours = Fleet(state, germany, Bight, Battleship, Destroyer, Destroyer);
		CUnit theirs = Fleet(state, poland, Bight, Destroyer, Destroyer);

		int hours = 0;
		bool fought = false;
		for (; hours < 24 * 20; hours++)
		{
			CTick.RunHour(state);
			fought |= state.Combats.Count > 0;
			if (fought && state.Combats.Count == 0)
			{
				break;
			}
		}
		Assert.True(fought);
		Assert.Empty(state.Combats);
		Assert.True(hours >= 4, "a battle is not decided before its fourth round");
		// Somebody was hit.
		Assert.True(ours.SubUnits.Concat(theirs.SubUnits).Any(ship => ship.Strength < CFixedPoint.FromInt(100))
			|| ours.IsRemoved || theirs.IsRemoved);
		// Whoever was let out waits; whoever fell back, or is gone, does not.
		foreach (CUnit fleet in new[] { ours, theirs })
		{
			Assert.Empty(fleet.Combats);
			Assert.True(fleet.IsRemoved || fleet.IsRetreating || fleet.IsMoving || fleet.EngageWait > 0 || fleet.Location != Bight,
				$"{fleet.Name} was let out of the battle with no wait");
		}
	}

	// --- leaders ---

	private static CLeader Admiral(int skill, int rank = 1, params (ETraitEffect Kind, string Value)[] effects)
	{
		var leader = new CLeader(100 + skill * 10 + rank) { Skill = skill, Rank = rank, Name = "admiral" };
		var trait = new CTrait("a_trait");
		foreach ((ETraitEffect kind, string value) in effects)
		{
			trait.Effects.Add(new CTraitEffect(kind, CFixedPoint.Parse(value), ""));
		}
		leader.Traits.Add(trait);
		return leader;
	}

	[Fact]
	public void AnAdmiralOfSkillFiveDrawsHisShipsUpAtHalfTheirReachOrOneFurther()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit ours = Fleet(state, germany, Bight, Battleship, Battleship);
		ours.Leader = Admiral(5);
		Battle(state, ours, Fleet(state, poland, Bight, Destroyer));

		// Nothing is left of the spread but its floor of two: a roll of nought or one.
		foreach (CSubUnit battleship in ours.SubUnits)
		{
			Assert.Contains(battleship.Distance.Raw, new long[] { 16000, 17000 });
		}
	}

	[Fact]
	public void ASidesLeaderAddsATenthAPointToItsPositioningAndToWhatItCreepsUpTo()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit ours = Fleet(state, germany, Bight, Battleship, Destroyer);
		ours.Leader = Admiral(3, 2);
		CUnit theirs = Fleet(state, poland, Bight, Destroyer);
		theirs.Leader = Admiral(6, 0);
		CCombat combat = Battle(state, ours, theirs);
		CNavalBattle.Begin(state, combat);

		Assert.Equal(combat.Attacker.PositioningRoll.Raw + 700, combat.Attacker.Positioning.Raw);
		Assert.Equal(700, CNavalBattle.Base(combat.Attacker));
		// A leader of no rank is never a side's leader.
		Assert.Equal(400, CNavalBattle.Base(combat.Defender));
	}

	[Fact]
	public void AnAdmiralWithTheRankForHisFleetAddsHisSkillAndHisTraitsComeIntoTheHour()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		state.Defines.Set(CManpower.MilitarySection, "NAVAL_RANK_1", CFixedPoint.FromInt(2));
		state.Defines.Set(CManpower.MilitarySection, CCombatModifiers.LeaderImpact, CFixedPoint.Parse("0.05"));
		CUnit small = Fleet(state, germany, Bight, Destroyer, Destroyer);
		small.Leader = Admiral(2, 1, (ETraitEffect.ExperienceBonus, "1"));
		CUnit large = Fleet(state, germany, Bight, Destroyer, Destroyer, Destroyer);
		large.Leader = Admiral(4, 1);
		CCombat combat = Battle(state, small, Fleet(state, poland, Bight, Destroyer));
		CNavalBattle.Join(state, combat, combat.Attacker, large);
		small.SubUnits[0].Experience = CFixedPoint.FromInt(20);
		combat.Hour = 5;
		CNavalModifiers.Apply(state, combat, combat.Attacker);

		// Two ships are within his rank's command: 2 x 0.05 on both halves of every ship.
		Assert.All(small.SubUnits, ship => Assert.Contains(ship.CombatModifiers,
			m => m.Kind == ECombatModifier.Leader && m.Attack.Raw == 100 && m.Defence.Raw == 100));
		// Three are not, and the admiral of the larger fleet adds nothing.
		Assert.All(large.SubUnits, ship => Assert.DoesNotContain(ship.CombatModifiers, m => m.Kind == ECombatModifier.Leader));
		// Twenty experience is a fifth, and the trait doubles it.
		Assert.Contains(small.SubUnits[0].CombatModifiers, m => m.Kind == ECombatModifier.Experience && m.Attack.Raw == 400);
	}

	[Fact]
	public void ADisengageTimerTraitShortensTheCountAFleetLeavesAt()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit quick = Fleet(state, germany, Bight, Destroyer);
		quick.Leader = Admiral(1, 1, (ETraitEffect.DisengageTimer, "-0.2"));
		CUnit plain = Fleet(state, germany, Bight, Destroyer);
		quick.Disengage = 4;
		plain.Disengage = 4;

		// Five times 0.8 is four.
		Assert.True(CNavalDisengage.Wants(state, quick));
		Assert.False(CNavalDisengage.Wants(state, plain));
	}

	[Fact]
	public void AShipsTargetingIsTenAPointOfItsAdmiralsSkillAndItsCountrysTargetingChoice()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		germany.Technology.Set(CNavalTargets.TargetingChoice, CFixedPoint.Parse("0.05"));
		CUnit ours = Fleet(state, germany, Bight, Destroyer);
		ours.Leader = Admiral(3, 0);
		CUnit theirs = Fleet(state, poland, Bight, Destroyer);

		// His rank does not matter here: 30 and 50, a chance of eight in a hundred.
		Assert.Equal(80, CNavalTargets.TargetingSkill(state, ours.SubUnits[0]));
		Assert.Equal(0, CNavalTargets.TargetingSkill(state, theirs.SubUnits[0]));
	}
}
