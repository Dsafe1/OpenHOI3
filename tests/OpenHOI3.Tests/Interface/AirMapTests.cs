using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Map;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The air map mode, against <c>docs/mechanics/airmap.md</c>: its colours, the battle icons
/// and the large pictures, the rings of range and what it says of a province. Germany and
/// Italy are at war with Poland; England is at peace with all three.
///
///     London                 Berlin --40-- Breslau --60-- Warszawa ------100------ Lodz
///                                  (the sea below Berlin)
///
/// Berlin is 100 pixels from Warszawa - 400 as a range measures it - and 200 from Lodz.
/// </summary>
public class AirMapTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Ita = new("ITA");
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Eng = new("ENG");

	private const int Berlin = 1;
	private const int Breslau = 2;
	private const int Warszawa = 3;
	private const int Lodz = 4;
	private const int London = 5;
	private const int Sea = 6;

	private static readonly CBuilding AirBase = new(0, "air_base", 10, "", CFixedPoint.Zero) { IsOnMap = true };
	private static readonly CBuilding AntiAir = new(1, "anti_air", 10, "", CFixedPoint.Zero) { IsOnMap = true };

	private static readonly CBuildingPosition[][] Placed =
	{
		Array.Empty<CBuildingPosition>(),
		new[] { new CBuildingPosition("air_base", 102.5f, 98f) },
		Array.Empty<CBuildingPosition>(),
		Array.Empty<CBuildingPosition>(),
		Array.Empty<CBuildingPosition>(),
		Array.Empty<CBuildingPosition>(),
		Array.Empty<CBuildingPosition>(),
	};

	private static readonly CMapPosition[] Own =
		{ default, new(100, 100), new(140, 100), new(200, 100), new(300, 100), new(0, 300), new(120, 150) };

	private static CProvince Land(int id, CCountryTag owner, int x, int y)
	{
		return new CProvince(id, "P" + id, isSea: false) { Owner = owner, Controller = owner, CentreX = x, CentreY = y };
	}

	private static CGameState State(out CCountry germany, out CCountry poland)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		var italy = new CCountry { Tag = Ita, Name = "Italy", DefinitionFile = "" };
		var england = new CCountry { Tag = Eng, Name = "England", DefinitionFile = "" };
		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Attackers.Add(Ita);
		war.Defenders.Add(Pol);
		CProvince berlin = Land(Berlin, Ger, 100, 100);
		berlin.SetBuildingLevel(AirBase, CFixedPoint.FromInt(10));
		berlin.SetBuildingLevel(AntiAir, CFixedPoint.FromInt(2));
		CProvince breslau = Land(Breslau, Ger, 140, 100);
		breslau.SetBuildingLevel(AirBase, CFixedPoint.FromInt(2));
		CProvince warszawa = Land(Warszawa, Pol, 200, 100);
		warszawa.SetBuildingLevel(AirBase, CFixedPoint.FromInt(6));
		return new CGameState(new CDate(1936, 6, 1),
			new[]
			{
				new CProvince(0, "", isSea: false), berlin, breslau, warszawa, Land(Lodz, Pol, 300, 100),
				Land(London, Eng, 0, 300), new CProvince(Sea, "Sea", isSea: true) { CentreX = 120, CentreY = 150 },
			},
			new[] { germany, poland, italy, england }, buildings: new[] { AirBase, AntiAir }, wars: new[] { war })
		{
			MapWidth = 1000,
			MapHeight = 1000,
		};
	}

	private static CUnit Fighters(CGameState state, CCountry country, int home, string range = "500",
		int strength = 100, EUnitOrder order = EUnitOrder.None, params int[] provinces)
	{
		var type = new CSubUnitType("interceptor", EUnitBranch.Air);
		type.Stats.Values.Set(CUnitOrders.Range, CFixedPoint.Parse(range));
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(100));
		var unit = new CUnit(EUnitKind.Air, country.Tag)
		{
			Id = ++state.LastUnitId, Location = home, Base = home, Name = "wing", Order = order,
		};
		var wing = new CSubUnit(type) { Id = ++state.LastUnitId, Unit = unit, Strength = CFixedPoint.FromInt(strength), Name = "w" };
		wing.Stats.CopyFrom(type.Stats);
		unit.SubUnits.Add(wing);
		unit.Mission.Provinces.AddRange(provinces);
		country.Units.Add(unit);
		return unit;
	}

	private static void Finished(CGameState state, ECombatKind kind, int province, CCountryTag? attacker, CCountryTag? defender)
	{
		var combat = new CCombat(province, kind);
		if (attacker is { } first)
		{
			combat.Attacker.AddCountry(first);
		}
		if (defender is { } second)
		{
			combat.Defender.AddCountry(second);
		}
		state.CombatHistory.Record(combat, state.TotalHours);
	}

	// --- the colours ---

	[Fact]
	public void AnInterceptColoursWhatItReachesGreenAndNothingBeyond()
	{
		CGameState state = State(out CCountry germany, out _);
		Fighters(state, germany, Berlin, order: EUnitOrder.AirIntercept, provinces: new[] { Warszawa, Lodz });

		var colours = CAirMapColours.Build(state, Ger);

		Assert.Equal((CAirMapColours.Green, CAirMapColours.Green), colours[Warszawa]);
		Assert.False(colours.ContainsKey(Lodz));
	}

	[Fact]
	public void AirSuperiorityStripesRedAndOverAnInterceptLeavesItsGroundGreen()
	{
		CGameState state = State(out CCountry germany, out _);
		Fighters(state, germany, Berlin, order: EUnitOrder.AirIntercept, provinces: new[] { Warszawa });
		Fighters(state, germany, Berlin, order: EUnitOrder.AirSuperiority, provinces: new[] { Warszawa, Sea });

		var colours = CAirMapColours.Build(state, Ger);

		Assert.Equal((CAirMapColours.Green, CAirMapColours.Red), colours[Warszawa]);
		Assert.Equal((CAirMapColours.Red, CAirMapColours.Red), colours[Sea]);
	}

	[Fact]
	public void ABombingMissionAndAnotherCountrysFightersColourNothing()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		Fighters(state, germany, Berlin, order: EUnitOrder.StrategicBombing, provinces: new[] { Warszawa });
		Fighters(state, poland, Warszawa, order: EUnitOrder.AirSuperiority, provinces: new[] { Berlin });

		Assert.Empty(CAirMapColours.Build(state, Ger));
	}

	[Fact]
	public void TheGreyIsTheOriginalsHalfAndTheGreenAndRedItsBytes()
	{
		Assert.Equal(new CColor(128 / 255f, 128 / 255f, 128 / 255f, 128 / 255f), CAirMapColours.Grey);
		Assert.Equal(new CColor(88 / 255f, 1f, 88 / 255f, 128 / 255f), CAirMapColours.Green);
		Assert.Equal(new CColor(1f, 88 / 255f, 88 / 255f, 128 / 255f), CAirMapColours.Red);
	}

	// --- the battle icons ---

	[Fact]
	public void ABombingOfOurGroundIsOursAndOneOfTheirsIsTheirs()
	{
		CGameState state = State(out _, out _);
		Finished(state, ECombatKind.GroundBombing, Berlin, Pol, Ger);
		Finished(state, ECombatKind.GroundBombing, Warszawa, Ger, Pol);
		Finished(state, ECombatKind.LandBombing, Warszawa, Ita, null);

		CAirMapCount[] counts = CAirMapIcons.Count(state, Ger);

		Assert.Equal(new CAirMapCount(1, 0, 0), counts[Berlin]);
		Assert.Equal(new CAirMapCount(0, 2, 0), counts[Warszawa]);
	}

	[Fact]
	public void AnAirBattleCountsWhoeverWonAndALandBattleNotAtAll()
	{
		CGameState state = State(out _, out _);
		Finished(state, ECombatKind.Air, Warszawa, Ger, null);
		Finished(state, ECombatKind.Air, Warszawa, null, Pol);
		Finished(state, ECombatKind.Land, Warszawa, Ger, null);

		Assert.Equal(new CAirMapCount(0, 0, 2), CAirMapIcons.Count(state, Ger)[Warszawa]);
	}

	[Fact]
	public void ABattleBetweenStrangersOverAStrangersGroundIsLeftOut()
	{
		CGameState state = State(out _, out _);
		Finished(state, ECombatKind.Air, London, Eng, null);

		Assert.Equal(default, CAirMapIcons.Count(state, Ger)[London]);
	}

	[Fact]
	public void AHundredAndTwentyEightOfAKindReadBelowOne()
	{
		CGameState state = State(out _, out _);
		for (int i = 0; i < 128; i++)
		{
			Finished(state, ECombatKind.GroundBombing, Berlin, Pol, Ger);
		}

		Assert.Equal(-128, CAirMapIcons.Count(state, Ger)[Berlin].BombedUs);

		// An enhanced game's fix fresh_screens stops the count where the byte still reads above nought.
		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(System.Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
		Assert.Equal(127, CAirMapIcons.Count(state, Ger)[Berlin].BombedUs);
		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(new[] { OpenHOI3.Sim.Rules.EFix.FreshScreens }));
		Assert.Equal(-128, CAirMapIcons.Count(state, Ger)[Berlin].BombedUs);
	}

	[Fact]
	public void AnEnhancedGamesColoursAreMadeAgainWhenAFighterUnitChangesItsBase()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fighters = Fighters(state, germany, Berlin, order: EUnitOrder.AirIntercept, provinces: new[] { Warszawa });
		long before = CAirMapColours.Figure(state, Ger);
		fighters.Base = Breslau;

		// The original's colours are made only when such an order starts or goes: a new base changes nothing.
		Assert.Equal(before, CAirMapColours.Figure(state, Ger));
		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(System.Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
		long enhanced = CAirMapColours.Figure(state, Ger);
		fighters.Base = Berlin;
		Assert.NotEqual(enhanced, CAirMapColours.Figure(state, Ger));
	}

	[Fact]
	public void ALargePictureIsSizedByTheLevelBuiltWhereThePlayerKnowsTheProvince()
	{
		CGameState state = State(out _, out _);
		OpenHOI3.Sim.Intelligence.CProvinceIntel.Rebuild(state);

		Assert.Equal(10, CAirMapIcons.Level(state, state.Province(Berlin), CMovementRules.AirBase, Ger));
		Assert.Equal(2, CAirMapIcons.Level(state, state.Province(Berlin), CAirMapIcons.AntiAir, Ger));
		Assert.Equal(1, CAirMapIcons.Level(state, state.Province(Warszawa), CMovementRules.AirBase, Ger));
		Assert.Equal(0, CAirMapIcons.Level(state, state.Province(Lodz), CMovementRules.AirBase, Ger));
		Assert.Equal(1f, CAirMapIcons.Size(1));
		Assert.Equal(2.8f, CAirMapIcons.Size(10), 5);
	}

	// --- the rings ---

	[Fact]
	public void UnitsWithinATenthOfAKilometreShareARingWithTheRangeOfTheLast()
	{
		CGameState state = State(out CCountry germany, out _);
		Fighters(state, germany, Berlin, range: "1200");
		Fighters(state, germany, Berlin, range: "1000.05", strength: 50);
		Fighters(state, germany, Berlin, range: "1000");

		List<CAirBaseCircle> rings = CAirBaseCircles.Build(state, Placed, Own, Ger, null);

		Assert.Equal(2, rings.Count);
		Assert.Equal(new CAirBaseCircle(Berlin, 102.5f, 98f, 250.0125f, 0.75f, 1f), rings[0]);
		Assert.Equal(new CAirBaseCircle(Berlin, 102.5f, 98f, 300f, 1f, 1f), rings[1]);
	}

	[Fact]
	public void WithAnAirUnitSelectedEveryOtherGroupsRingIsDimmed()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit near = Fighters(state, germany, Berlin, range: "500");
		Fighters(state, germany, Berlin, range: "900");

		List<CAirBaseCircle> rings = CAirBaseCircles.Build(state, Placed, Own, Ger, near);

		Assert.Equal(new[] { 1f, CAirBaseCircles.Dimmed }, rings.Select(ring => ring.Dim));
		var land = new CUnit(EUnitKind.Division, Ger);
		Assert.All(CAirBaseCircles.Build(state, Placed, Own, Ger, land), ring => Assert.Equal(1f, ring.Dim));
	}

	[Fact]
	public void OnlyThePlayersBasesWithUnitsAreRinged()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		Fighters(state, poland, Warszawa);

		Assert.Empty(CAirBaseCircles.Build(state, Placed, Own, Ger, null));
		Fighters(state, germany, Breslau);
		Assert.Equal(Breslau, Assert.Single(CAirBaseCircles.Build(state, Placed, Own, Ger, null)).Province);
	}

	[Fact]
	public void ARingGoesFromRedThroughYellowToGreenAndIsSeenThrough()
	{
		CColor Of(float share, float dim) => CAirBaseCircles.ColourOf(new CAirBaseCircle(1, 0f, 0f, 1f, share, dim));

		Assert.Equal(new CColor(1f, 0f, 0f, 0.3f), Of(0f, 1f));
		Assert.Equal(new CColor(1f, 1f, 0f, 0.3f), Of(0.5f, 1f));
		Assert.Equal(new CColor(0f, 1f, 0f, 0.3f * 0.3f), Of(1f, 0.3f));
	}

	// --- what it says of a province ---

	[Fact]
	public void TheProvinceSaysItsBombingsFirstAndThenItsAirBattles()
	{
		CGameState state = State(out _, out _);
		Finished(state, ECombatKind.Air, Warszawa, Ger, null);
		Finished(state, ECombatKind.GroundBombing, Warszawa, Ger, Pol);
		Finished(state, ECombatKind.NavalBombing, Warszawa, Ita, Pol);
		Finished(state, ECombatKind.Air, London, Eng, null);

		Assert.Equal(new[] { "AIR_TOOLTIP_BOMBING 2", "AIR_TOOLTIP_DOGFIGHT 1" },
			CAirMapText.Lines(key => key + " $NUM$", state, state.Province(Warszawa), Ger));
		Assert.Empty(CAirMapText.Lines(key => key + " $NUM$", state, state.Province(London), Ger));
	}

	[Fact]
	public void TheProvinceCountsWhatTheIconsCount()
	{
		CGameState state = State(out _, out _);
		CCountryTag?[] sides = { null, Ger, Ita, Pol, Eng };
		foreach (CCountryTag? attacker in sides)
		{
			foreach (CCountryTag? defender in sides)
			{
				Finished(state, ECombatKind.Air, London, attacker, defender);
			}
		}

		foreach (CFinishedCombat entry in state.CombatHistory.Entries)
		{
			bool icon = CAirMapIcons.IsOurs(state, entry, Ger) || CAirMapIcons.IsTheirs(state, entry, Ger);
			Assert.Equal(icon, CAirMapText.Concerns(state, entry, Ger));
		}
	}
}
