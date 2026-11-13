using System.Collections.Generic;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the original shows of a land battle, against <c>docs/mechanics/combatview.md</c>.
/// Italy attacks Imi out of Dolo; the texts are the base game's English.
/// </summary>
public class CombatViewTextTests
{
	private static readonly CCountryTag Ita = new("ITA");
	private static readonly CCountryTag Eth = new("ETH");

	private const int Dolo = 1;
	private const int Imi = 2;

	private static readonly Dictionary<string, string> Texts = new()
	{
		["BATTLE_OF"] = "Battle of",
		["COMMITED_LABEL"] = "§Y$COUNT$§W units, §Y$WIDTH$§W width.",
		["RESERVES_LABEL"] = "§Y$COUNT$§W units in reserve.",
		["NO_COMMANDER"] = "(No Commander)",
		["tactic_assault"] = "Assault",
	};

	private static string Localise(string key) => Texts.GetValueOrDefault(key, key);

	private static CGameState State(out CCountry italy, out CCountry ethiopia)
	{
		italy = new CCountry { Tag = Ita, Name = "Italy", DefinitionFile = "", Capital = Dolo };
		ethiopia = new CCountry { Tag = Eth, Name = "Ethiopia", DefinitionFile = "", Capital = Imi };
		var defines = new CDefines();
		defines.Set(CManpower.MilitarySection, CFrontLine.BaseWidth, CFixedPoint.Parse("10"));
		defines.Set(CManpower.MilitarySection, CFrontLine.AdditionalWidth, CFixedPoint.Parse("5"));
		return new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(Dolo, "Dolo", isSea: false) { Owner = Ita, Controller = Ita },
				new CProvince(Imi, "Imi", isSea: false) { Owner = Eth, Controller = Eth, Terrain = new CTerrain("desert", false, default) },
			},
			new[] { italy, ethiopia }, defines: defines);
	}

	private static CUnit Division(CGameState state, CCountry country, int province, int brigades = 3,
		string soft = "2", string hard = "1", string toughness = "3", string defensiveness = "4",
		string organisation = "30", string strength = "30", string width = "1")
	{
		var unit = new CUnit(EUnitKind.Division, country.Tag) { Id = ++state.LastUnitId, Location = province, Name = "Division" };
		for (int i = 0; i < brigades; i++)
		{
			var type = new CSubUnitType("militia_brigade", EUnitBranch.Land);
			type.Stats.Values.Set(CUnitTotals.SoftAttack, CFixedPoint.Parse(soft));
			type.Stats.Values.Set(CUnitTotals.HardAttack, CFixedPoint.Parse(hard));
			type.Stats.Values.Set(CSubUnitStats.Toughness, CFixedPoint.Parse(toughness));
			type.Stats.Values.Set(CUnitTotals.Defensiveness, CFixedPoint.Parse(defensiveness));
			type.Stats.Values.Set(CSubUnitType.CombatWidth, CFixedPoint.Parse(width));
			type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.Parse("30"));
			type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.Parse("30"));
			var subUnit = new CSubUnit(type)
			{
				Id = ++state.LastUnitId, Unit = unit,
				Organisation = CFixedPoint.Parse(organisation), Strength = CFixedPoint.Parse(strength),
			};
			subUnit.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(subUnit);
		}
		country.Units.Add(unit);
		return unit;
	}

	private static CCombat Battle(CGameState state, CUnit attacker, CUnit defender)
	{
		var combat = new CCombat(Imi);
		state.Combats.Add(combat);
		combat.Attacker.Add(attacker);
		combat.Defender.Add(defender);
		CCombats.Begin(state, combat);
		return combat;
	}

	[Fact]
	public void A_battle_is_named_after_its_province()
	{
		CGameState state = State(out CCountry italy, out CCountry ethiopia);
		CCombat combat = Battle(state, Division(state, italy, Dolo), Division(state, ethiopia, Imi));

		Assert.Equal("Battle of Imi", CCombatViewText.Name(Localise, state, combat));
	}

	[Fact]
	public void A_side_is_worth_its_brigades_organisation_by_strength_by_what_the_hour_makes_of_it()
	{
		CGameState state = State(out CCountry italy, out CCountry ethiopia);
		CUnit attacker = Division(state, italy, Dolo, brigades: 1);
		CCombat combat = Battle(state, attacker, Division(state, ethiopia, Imi, brigades: 1));

		// 30 * 30 = 900; attacks and toughness come to 6, a tenth of which is under one and does nothing.
		Assert.Equal(900_000, CCombatViewText.Worth(combat.Attacker));

		// Both of the hour's products count, the defence's too for a unit that attacks.
		attacker.CombatAttack = CFixedPoint.Parse("0.4");
		attacker.CombatDefence = CFixedPoint.Parse("0.5");
		Assert.Equal(180_000, CCombatViewText.Worth(combat.Attacker));
	}

	[Fact]
	public void Arms_count_only_past_ten_and_a_defender_s_are_its_defensiveness()
	{
		CGameState state = State(out CCountry italy, out CCountry ethiopia);
		CUnit attacker = Division(state, italy, Dolo, brigades: 1, soft: "10", hard: "5", toughness: "5", defensiveness: "45");
		CUnit defender = Division(state, ethiopia, Imi, brigades: 1, soft: "10", hard: "5", toughness: "5", defensiveness: "45");
		CCombat combat = Battle(state, attacker, defender);

		Assert.Equal(1_800_000, CCombatViewText.Worth(combat.Attacker));   // 900 * (10 + 5 + 5) / 10
		Assert.Equal(5_400_000, CCombatViewText.Worth(combat.Defender));   // 900 * (10 + 5 + 45) / 10
		Assert.Equal(250, CCombatViewText.Share(combat));
		Assert.Equal(25, CCombatViewText.Progress(250));
	}

	[Fact]
	public void The_share_is_the_bar_and_the_marker_s_number_cut_to_a_whole()
	{
		Assert.Equal(6, CCombatViewText.Progress(64));
		Assert.Equal("6", CCombatViewText.MarkerText(64));
		Assert.Equal("66", CCombatViewText.MarkerText(669));
		Assert.Equal("0", CCombatViewText.MarkerText(9));
	}

	[Fact]
	public void A_side_with_nothing_left_still_counts_for_one()
	{
		CGameState state = State(out CCountry italy, out CCountry ethiopia);
		CUnit attacker = Division(state, italy, Dolo, organisation: "0");
		CCombat combat = Battle(state, attacker, Division(state, ethiopia, Imi));

		Assert.Equal(0, CCombatViewText.Worth(combat.Attacker));
		Assert.Equal(0, CCombatViewText.Share(combat));
	}

	[Theory]
	[InlineData(661, false, CCombatViewText.MarkerGood)]
	[InlineData(660, false, CCombatViewText.MarkerEven)]
	[InlineData(331, false, CCombatViewText.MarkerEven)]
	[InlineData(330, false, CCombatViewText.MarkerBad)]
	[InlineData(64, false, CCombatViewText.MarkerBad)]
	[InlineData(661, true, CCombatViewText.MarkerBad)]
	[InlineData(500, true, CCombatViewText.MarkerEven)]
	[InlineData(330, true, CCombatViewText.MarkerGood)]
	public void The_marker_says_how_it_goes_for_the_one_looking(long share, bool defends, int frame)
	{
		Assert.Equal(frame, CCombatViewText.MarkerFrame(share, defends));
	}

	[Fact]
	public void A_country_takes_part_in_a_battle_on_one_side_of_it()
	{
		CGameState state = State(out CCountry italy, out CCountry ethiopia);
		CCombat combat = Battle(state, Division(state, italy, Dolo), Division(state, ethiopia, Imi));

		Assert.True(CCombatViewText.TakesPart(combat, Ita));
		Assert.False(CCombatViewText.Defends(combat, Ita));
		Assert.True(CCombatViewText.Defends(combat, Eth));
		Assert.False(CCombatViewText.TakesPart(combat, new CCountryTag("GER")));
	}

	[Fact]
	public void The_labels_count_the_front_its_width_and_the_reserves()
	{
		CGameState state = State(out CCountry italy, out CCountry ethiopia);
		CCombat combat = Battle(state, Division(state, italy, Dolo), Division(state, ethiopia, Imi));
		combat.Attacker.Reserves.Add(Division(state, italy, Dolo));

		Assert.Equal("§Y1§W units, §Y3§W width.", CCombatViewText.CommittedLabel(Localise, state, combat.Attacker));
		Assert.Equal("§Y1§W units in reserve.", CCombatViewText.ReservesLabel(Localise, combat.Attacker));
		Assert.Equal("§Y0§W units in reserve.", CCombatViewText.ReservesLabel(Localise, combat.Defender));
		Assert.Equal("10", CCombatViewText.Width(state, combat));
	}

	[Fact]
	public void A_side_with_no_leader_says_so_and_flies_its_first_country_s_flag()
	{
		CGameState state = State(out CCountry italy, out CCountry ethiopia);
		CCombat combat = Battle(state, Division(state, italy, Dolo), Division(state, ethiopia, Imi));

		Assert.Equal("(No Commander)", CCombatViewText.LeaderName(Localise, combat.Attacker));
		Assert.Equal(Ita, CCombatViewText.Flag(state, combat.Attacker));

		// With no country still fighting the province's holder's flag stands in, and the
		// name is left as it was.
		combat.Defender.Standing.Clear();
		Assert.Equal(Eth, CCombatViewText.Flag(state, combat.Defender));
		Assert.Null(CCombatViewText.LeaderName(Localise, combat.Defender));
		Assert.Null(CCombatViewText.LeaderPortrait(combat.Defender));
	}

	[Fact]
	public void A_side_shows_its_highest_ranked_leader_and_the_more_skilled_has_the_initiative()
	{
		CGameState state = State(out CCountry italy, out CCountry ethiopia);
		CUnit first = Division(state, italy, Dolo);
		CUnit second = Division(state, italy, Dolo);
		CUnit defender = Division(state, ethiopia, Imi);
		first.Leader = new CLeader(1) { Name = "Graziani", Rank = 1, Skill = 3, Picture = "graziani" };
		second.Leader = new CLeader(2) { Name = "Badoglio", Rank = 2, Skill = 1, Picture = "badoglio" };
		second.Leader.Unit = second;
		CCombat combat = Battle(state, first, defender);
		combat.Attacker.Add(second);

		Assert.Equal("Badoglio", CCombatViewText.LeaderName(Localise, combat.Attacker));
		Assert.Equal("gfx/pictures/portraits/badoglio.tga", CCombatViewText.LeaderPortrait(combat.Attacker));
		Assert.Equal(Ita, CCombatViewText.Flag(state, combat.Attacker));
		Assert.True(CCombatViewText.HasInitiative(combat.Attacker));     // 1 against nobody's 0
		Assert.False(CCombatViewText.HasInitiative(combat.Defender));

		defender.Leader = new CLeader(3) { Name = "Ras Kassa", Rank = 1, Skill = 1 };
		Assert.False(CCombatViewText.HasInitiative(combat.Attacker));    // equal: neither
		Assert.False(CCombatViewText.HasInitiative(combat.Defender));
	}

	[Fact]
	public void A_tactic_is_shown_by_its_name_and_its_picture()
	{
		CGameState state = State(out CCountry italy, out CCountry ethiopia);
		CCombat combat = Battle(state, Division(state, italy, Dolo), Division(state, ethiopia, Imi));

		Assert.Equal("", CCombatViewText.TacticName(Localise, combat.Attacker));
		Assert.Equal("", CCombatViewText.TacticPicture(combat.Attacker));

		combat.Attacker.Tactic = new CCombatTactic("tactic_assault", 1) { Picture = "assault" };
		Assert.Equal("Assault", CCombatViewText.TacticName(Localise, combat.Attacker));
		Assert.Equal("gfx/pictures/tactics/assault.dds", CCombatViewText.TacticPicture(combat.Attacker));
	}

	[Fact]
	public void The_arrow_between_the_tactics_says_whose_counters_whose()
	{
		CGameState state = State(out CCountry italy, out CCountry ethiopia);
		CCombat combat = Battle(state, Division(state, italy, Dolo), Division(state, ethiopia, Imi));
		var assault = new CCombatTactic("tactic_assault", 1) { CounteredByNumber = 2 };
		var counter = new CCombatTactic("tactic_counterattack", 2);
		var delay = new CCombatTactic("tactic_delay", 3) { CounteredByNumber = 1 };

		Assert.Equal(CCombatViewText.NobodyCountered, CCombatViewText.IndicatorFrame(combat));

		combat.Attacker.Tactic = assault;
		combat.Defender.Tactic = counter;
		Assert.Equal(CCombatViewText.AttackerCountered, CCombatViewText.IndicatorFrame(combat));

		combat.Defender.Tactic = delay;
		Assert.Equal(CCombatViewText.DefenderCountered, CCombatViewText.IndicatorFrame(combat));
	}

	[Fact]
	public void The_modifiers_in_play_on_either_side_stand_once_each_in_their_own_order()
	{
		CGameState state = State(out CCountry italy, out CCountry ethiopia);
		CUnit attacker = Division(state, italy, Dolo);
		CUnit defender = Division(state, ethiopia, Imi);
		CCombat combat = Battle(state, attacker, defender);
		attacker.CombatModifiers.Add(new CCombatModifier(ECombatModifier.Weather, default, default));
		attacker.CombatModifiers.Add(new CCombatModifier(ECombatModifier.River, default, default));
		attacker.CombatModifiers.Add(new CCombatModifier(ECombatModifier.Experience, default, default));
		defender.CombatModifiers.Add(new CCombatModifier(ECombatModifier.DugIn, default, default));
		defender.CombatModifiers.Add(new CCombatModifier(ECombatModifier.Experience, default, default));

		Assert.Equal(new[] { "CMOD_BM_EXPERIENCE", "CMOD_BM_DUGIN_MODIFIER", "CMOD_BM_RIVER_PENALTY", "CMOD_BM_WEATHER" },
			CCombatViewText.Modifiers(combat));
	}

	[Fact]
	public void The_picture_is_the_province_s_own_or_its_terrain_s()
	{
		CGameState state = State(out CCountry italy, out CCountry ethiopia);
		CCombat combat = Battle(state, Division(state, italy, Dolo), Division(state, ethiopia, Imi));

		Assert.Equal("gfx/interface/prov_pictures/provimg_2.dds", CCombatViewText.ProvincePicture(combat));
		Assert.Equal("GFX_terrainimg_desert", CCombatViewText.TerrainPicture(state, combat));
	}

	[Fact]
	public void A_unit_known_under_intel_level_3_shows_the_unknown_counter()
	{
		CGameState state = State(out CCountry italy, out CCountry ethiopia);
		CUnit unit = Division(state, italy, Dolo, organisation: "15", strength: "24");
		CProvince dolo = state.Province(Dolo);

		// Its own country knows it whole; Ethiopia knows Dolo at nought, then at 2, then at 3.
		Assert.Equal("GFX_counter_militia_brigade", CCombatViewText.UnitSprite(state, unit, italy.Tag));
		Assert.Equal("GFX_counter_unknown", CCombatViewText.UnitSprite(state, unit, ethiopia.Tag));
		dolo.Intel[ethiopia.Index] = 2;
		Assert.Equal("unknown", CCombatViewText.CounterKey(state, unit, ethiopia.Tag));
		dolo.Intel[ethiopia.Index] = 3;
		Assert.Equal("GFX_counter_militia_brigade", CCombatViewText.UnitSprite(state, unit, ethiopia.Tag));
		// A unit with no brigade is the unknown counter, known or not.
		Assert.Equal("GFX_counter_unknown", CCombatViewText.UnitSprite(state, new CUnit(EUnitKind.Division, Ita), null));
	}

	[Fact]
	public void A_unit_in_a_list_shows_its_kind_its_width_and_its_two_bars()
	{
		CGameState state = State(out CCountry italy, out CCountry ethiopia);
		CUnit unit = Division(state, italy, Dolo, organisation: "15", strength: "24");

		Assert.Equal("GFX_counter_militia_brigade", CCombatViewText.UnitSprite(state, unit, null));
		Assert.Equal(2, CCombatViewText.UnitWidthFrame(unit));   // a width of 3 is under 4
		Assert.Equal(80, CCombatViewText.StrengthBar(unit));
		Assert.Equal(50, CCombatViewText.OrganisationBar(state, unit));
		Assert.Equal("Dolo", CCombatViewText.Location(state, unit));
	}

	// --- a battle at sea ---

	private static CUnit Fleet(CGameState state, CCountry country, params string[] types)
	{
		var fleet = new CUnit(EUnitKind.Navy, country.Tag) { Id = ++state.LastUnitId, Location = Dolo, Name = "Fleet" };
		foreach (string key in types)
		{
			var type = new CSubUnitType(key, EUnitBranch.Naval);
			type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.Parse("100"));
			type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.Parse("40"));
			var ship = new CSubUnit(type)
			{
				Id = ++state.LastUnitId, Unit = fleet, Name = key,
				Organisation = CFixedPoint.Parse("30"), Strength = CFixedPoint.Parse("75"),
			};
			ship.Stats.CopyFrom(type.Stats);
			fleet.SubUnits.Add(ship);
		}
		country.Units.Add(fleet);
		return fleet;
	}

	[Fact]
	public void A_side_at_sea_says_how_many_ships_it_has_and_how_well_it_is_placed()
	{
		CGameState state = State(out CCountry italy, out CCountry ethiopia);
		var combat = new CCombat(Dolo, ECombatKind.Naval);
		combat.Attacker.Add(Fleet(state, italy, "battleship", "destroyer"));
		combat.Attacker.Add(Fleet(state, italy, "destroyer"));
		combat.Defender.Add(Fleet(state, ethiopia, "submarine"));
		combat.Attacker.Positioning = CFixedPoint.Parse("0.436");
		string Words(string key) => key == CCombatViewText.ShipsKey ? "ships" : key;

		Assert.Equal("§Y3§W ships", CCombatViewText.ShipsLabel(Words, combat.Attacker));
		Assert.Equal("§Y1§W ships", CCombatViewText.ShipsLabel(Words, combat.Defender));
		Assert.Equal("43.6%", CCombatViewText.Positioning(combat.Attacker));
		Assert.Equal("0.0%", CCombatViewText.Positioning(combat.Defender));
	}

	[Fact]
	public void A_ships_worth_is_its_own_and_the_share_is_three_ships_against_one()
	{
		CGameState state = State(out CCountry italy, out CCountry ethiopia);
		var combat = new CCombat(Dolo, ECombatKind.Naval);
		CUnit ours = Fleet(state, italy, "battleship", "destroyer", "destroyer");
		CUnit theirs = Fleet(state, ethiopia, "destroyer");
		combat.Attacker.Add(ours);
		combat.Defender.Add(theirs);

		// 30 organisation by 75 strength a ship, whatever its guns.
		Assert.Equal(3 * 2250000, CCombatViewText.Worth(combat.Attacker));
		Assert.Equal(750, CCombatViewText.Share(combat));
		// What the hour makes of a ship counts, attack and defence both.
		theirs.SubUnits[0].CombatAttack = CFixedPoint.Parse("1.5");
		theirs.SubUnits[0].CombatDefence = CFixedPoint.Parse("2");
		Assert.Equal(500, CCombatViewText.Share(combat));

		theirs.SubUnits[0].CombatModifiers.Add(new CCombatModifier(ECombatModifier.Surprise, CFixedPoint.One, CFixedPoint.Zero));
		ours.SubUnits[0].CombatModifiers.Add(new CCombatModifier(ECombatModifier.PoorScreens, CFixedPoint.Zero, CFixedPoint.One));
		ours.SubUnits[1].CombatModifiers.Add(new CCombatModifier(ECombatModifier.MissionEfficiency, CFixedPoint.One, CFixedPoint.One));
		Assert.Equal(new[] { "CMOD_BM_MISSION_EFFICIENCY", "CMOD_BM_POOR_SCREEN_PENALTY", "CMOD_BM_SURPRISE_BONUS" },
			CCombatViewText.Modifiers(combat));
	}

	[Fact]
	public void A_ship_in_a_list_stands_along_its_row_by_its_distance()
	{
		CGameState state = State(out CCountry italy, out CCountry ethiopia);
		var combat = new CCombat(Dolo, ECombatKind.Naval);
		CUnit ours = Fleet(state, italy, "battleship");
		CUnit theirs = Fleet(state, ethiopia, "destroyer");
		combat.Attacker.Add(ours);
		combat.Defender.Add(theirs);
		CSubUnit battleship = ours.SubUnits[0];
		CSubUnit destroyer = theirs.SubUnits[0];

		Assert.Equal("GFX_navalcombat_l_battleship", CCombatViewText.ShipPicture(battleship, isAttacker: true));
		Assert.Equal("GFX_navalcombat_r_destroyer", CCombatViewText.ShipPicture(destroyer, isAttacker: false));
		Assert.Equal(75, CCombatViewText.ShipStrengthBar(state, battleship));
		Assert.Equal(75, CCombatViewText.ShipOrganisationBar(battleship));

		// A row of 170 and a picture of 60 leave 110 to travel, which forty of distance covers.
		battleship.Distance = CFixedPoint.Zero;
		Assert.Equal(129, CCombatViewText.ShipPlace(battleship, true, 170, 60));
		Assert.Equal(-19, CCombatViewText.ShipPlace(battleship, false, 170, 60));
		battleship.Distance = CFixedPoint.FromInt(20);
		Assert.Equal(74, CCombatViewText.ShipPlace(battleship, true, 170, 60));
		Assert.Equal(36, CCombatViewText.ShipPlace(battleship, false, 170, 60));
		battleship.Distance = CFixedPoint.FromInt(132);
		Assert.Equal(19, CCombatViewText.ShipPlace(battleship, true, 170, 60));
		Assert.Equal(91, CCombatViewText.ShipPlace(battleship, false, 170, 60));

		// The two sides' pictures have their frames the other way round.
		Assert.Equal(1, CCombatViewText.ShipFrame(combat, battleship, isAttacker: true));
		Assert.Equal(2, CCombatViewText.ShipFrame(combat, destroyer, isAttacker: false));
		battleship.TargetId = destroyer.Id;
		destroyer.TargetId = 999999;                               // a ship that is gone
		Assert.Equal(2, CCombatViewText.ShipFrame(combat, battleship, isAttacker: true));
		Assert.Equal(2, CCombatViewText.ShipFrame(combat, destroyer, isAttacker: false));
		destroyer.TargetId = battleship.Id;
		Assert.Equal(1, CCombatViewText.ShipFrame(combat, destroyer, isAttacker: false));
	}
}
