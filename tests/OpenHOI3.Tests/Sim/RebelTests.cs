using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The roll, which type rises, a rising and its army, the capture, a faction's day and month, independence and the
/// release, against <c>docs/mechanics/rebels.md</c>, on <see cref="RebelWorld"/>.
/// </summary>
public class RebelTests
{
	private static CRebelType TypeOf(CGameState state, string name) => state.RebelTypes.First(type => type.Name == name);

	private static CUnit Garrison(CGameState state, CCountryTag owner, int province)
	{
		var unit = new CUnit(EUnitKind.Division, owner) { Id = ++state.LastUnitId, Location = province };
		var brigade = new CSubUnit(state.SubUnitTypes["infantry_brigade"]) { Id = ++state.LastUnitId, Unit = unit };
		CRebelArmies.SetFull(state, state.Country(owner)!, brigade);
		unit.SubUnits.Add(brigade);
		state.Country(owner)!.Units.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	[Theory]
	[InlineData(0, 0)]
	[InlineData(24, 0)]
	[InlineData(25, 0)]
	[InlineData(26, 1)]
	[InlineData(75, 2)]
	[InlineData(1000, 20)]
	[InlineData(47000, 940)]
	public void The_first_draws_threshold_is_the_risk_over_fifty_rounded_to_even(long risk, long threshold)
	{
		Assert.Equal(threshold, CRevoltRoll.Threshold(risk));
	}

	[Fact]
	public void The_roll_draws_once_for_every_province_held_in_an_area_and_never_while_a_game_is_made()
	{
		CGameState state = RebelWorld.State();
		long before = state.Random.Draws;
		CRevoltRoll.RunDaily(state);
		// Germany holds four, Poland two, Slovakia and the rebels own nothing; with no risk nothing passes.
		Assert.Equal(6, state.Random.Draws - before);
		Assert.Empty(state.RebelFactions);

		state.IsBeingMade = true;
		before = state.Random.Draws;
		CRevoltRoll.RunDaily(state);
		Assert.Equal(0, state.Random.Draws - before);
	}

	[Fact]
	public void A_province_chooses_partisans_where_occupied_and_the_strictly_highest_type_where_not()
	{
		CGameState state = RebelWorld.State();
		Assert.Equal("partisans", CRebelChoice.TypeFor(state, state.Province(4))!.Name);
		// Posen would free Slovakia, so the nationalists outscore the rabble; Berlin frees nobody.
		Assert.Equal("nationalist_rebels", CRebelChoice.TypeFor(state, state.Province(3))!.Name);
		Assert.Equal("disgruntled_rabble", CRebelChoice.TypeFor(state, state.Province(1))!.Name);
		Assert.Equal(RebelWorld.Slo, CRebelChoice.IndependenceOf(state, TypeOf(state, "nationalist_rebels"), state.Province(3)));
		// A neighbour of Slovakia's capital, with no core of it.
		Assert.Equal(RebelWorld.Slo, CRebelChoice.IndependenceOf(state, TypeOf(state, "nationalist_rebels"), state.Province(2)));
		Assert.Null(CRebelChoice.IndependenceOf(state, TypeOf(state, "nationalist_rebels"), state.Province(1)));
	}

	[Fact]
	public void A_rising_makes_a_faction_and_an_army_against_the_occupier_and_zeroes_the_risk()
	{
		CGameState state = RebelWorld.State();
		Garrison(state, RebelWorld.Ger, 4);
		var messages = new List<CPactMessage>();
		state.PactMessaged += messages.Add;
		CProvince warsaw = state.Province(4);
		warsaw.RevoltRisk = CFixedPoint.FromInt(5);

		CRisings.Rise(state, warsaw, TypeOf(state, "partisans"), 7);

		Assert.Equal(CFixedPoint.Zero, warsaw.RevoltRisk);
		CRebelFaction faction = Assert.Single(state.RebelFactions);
		Assert.Equal(RebelWorld.Pol, faction.Country);
		Assert.Equal(new[] { RebelWorld.Ger }, faction.Targets);
		Assert.Equal("Polish Partisans", faction.Name);
		CUnit army = CRebelFactions.FindUnit(state, Assert.Single(faction.Armies))!;
		Assert.Equal(RebelWorld.Reb, army.Owner);
		Assert.Equal("1st Polish partisans", army.Name);
		// Seven is held to four; each brigade at -1 and Germany's efficiency of 2, below Germany's level, whole.
		Assert.Equal(4, army.SubUnits.Count);
		Assert.All(army.SubUnits, brigade =>
		{
			Assert.Equal(CFixedPoint.FromInt(1), brigade.Experience);
			Assert.InRange(brigade.TechnologyLevels[RebelWorld.Smallarms.Name], 0, 2);
			Assert.Equal(CFixedPoint.FromInt(30), brigade.Strength);
		});
		// The German garrison is there, so it fights at once.
		Assert.True(army.IsFighting);
		CPactMessage revolt = Assert.Single(messages, message => message.We == "REVOLT");
		Assert.Equal(RebelWorld.Ger, revolt.Told);
	}

	[Fact]
	public void Partisans_winning_their_owners_occupied_land_hand_it_straight_back()
	{
		CGameState state = RebelWorld.State();
		var messages = new List<CPactMessage>();
		state.PactMessaged += messages.Add;

		CRisings.Rise(state, state.Province(4), TypeOf(state, "partisans"), 3);

		Assert.Equal(RebelWorld.Pol, state.Province(4).Controller);
		CPactMessage lost = Assert.Single(messages, message => message.We == "PROVINCELOST");
		Assert.Equal(RebelWorld.Ger, lost.Told);
		Assert.Contains(("COUNTRY", "POL"), lost.Values);
	}

	[Fact]
	public void A_rebel_army_worn_under_a_tenth_of_a_point_goes_at_the_hours_end()
	{
		CGameState state = RebelWorld.State();
		CRisings.Rise(state, state.Province(1), TypeOf(state, "disgruntled_rabble"), 2);
		CRebelFaction faction = Assert.Single(state.RebelFactions);
		CUnit army = CRebelFactions.FindUnit(state, Assert.Single(faction.Armies))!;
		Assert.False(CSpentRebels.IsSpent(army));

		// The brigades' strength is added up: 0.05 and 0.05 is a tenth, which stays.
		foreach (CSubUnit brigade in army.SubUnits)
		{
			brigade.Strength = new CFixedPoint(50);
		}
		Assert.False(CSpentRebels.IsSpent(army));
		army.SubUnits[0].Strength = new CFixedPoint(49);
		Assert.True(CSpentRebels.IsSpent(army));

		// Queued in the hour, and gone when the queue is emptied.
		CSpentRebels.RunHourly(state);
		Assert.False(army.IsRemoved);
		Assert.Equal(new[] { army.Id }, state.PendingUnitRemovals);
		CPendingUnitRemovals.Drain(state);
		Assert.True(army.IsRemoved);
	}

	[Fact]
	public void Only_the_rebels_own_units_go_for_being_worn()
	{
		CGameState state = RebelWorld.State();
		var division = new CUnit(EUnitKind.Division, RebelWorld.Ger) { Id = 900, Location = 1 };
		state.Country(RebelWorld.Ger)!.Units.Add(division);
		Assert.False(CSpentRebels.IsSpent(division));
		CSpentRebels.RunHourly(state);
		Assert.Empty(state.PendingUnitRemovals);
	}

	[Fact]
	public void Rebels_in_their_owners_own_land_take_it_and_a_second_rising_joins_them()
	{
		CGameState state = RebelWorld.State();
		CRisings.Rise(state, state.Province(1), TypeOf(state, "disgruntled_rabble"), 3);

		CProvince berlin = state.Province(1);
		Assert.Equal(RebelWorld.Reb, berlin.Controller);
		Assert.Equal(RebelWorld.Ger, berlin.Owner);
		CRebelFaction faction = Assert.Single(state.RebelFactions);
		Assert.Equal(new[] { 1 }, faction.Provinces);
		Assert.Equal(state.Date, berlin.RebelsTook);

		// A nation-wide type in the same country's land joins it, and adds the holder to its targets once.
		CRisings.Rise(state, state.Province(2), TypeOf(state, "disgruntled_rabble"), 1);
		Assert.Single(state.RebelFactions);
		Assert.Equal(2, faction.Armies.Count);
		Assert.Equal(new[] { RebelWorld.Ger }, faction.Targets);
	}

	[Fact]
	public void Partisans_of_an_exiles_land_are_the_exiles()
	{
		CGameState state = RebelWorld.State();
		Garrison(state, RebelWorld.Ger, 4);
		state.Country(RebelWorld.Pol)!.InExile = true;

		CRisings.Rise(state, state.Province(4), TypeOf(state, "partisans"), 2);

		CRebelFaction faction = Assert.Single(state.RebelFactions);
		CUnit army = state.Country(RebelWorld.Pol)!.Units.Single(unit => unit.Id == faction.Armies[0]);
		Assert.Equal(RebelWorld.Pol, army.Owner);
		// The faction lets go of an army that is not the rebels' on its next day.
		CRebelFactionDay.Run(state, faction);
		Assert.Empty(faction.Armies);
	}

	[Fact]
	public void The_rebels_are_everybodys_enemy()
	{
		CGameState state = RebelWorld.State();
		Assert.True(CMovementRules.IsEnemy(state, RebelWorld.Reb, RebelWorld.Slo));
		Assert.True(CMovementRules.IsEnemy(state, RebelWorld.Ger, RebelWorld.Reb));
		Assert.False(CMovementRules.IsEnemy(state, RebelWorld.Reb, RebelWorld.Reb));
	}

	[Fact]
	public void A_faction_hands_back_land_whose_owner_is_its_friend_and_dies_with_nothing_left()
	{
		CGameState state = RebelWorld.State();
		var faction = new CRebelFaction(1, TypeOf(state, "partisans"), 5, RebelWorld.Pol);
		faction.Targets.Add(RebelWorld.Ger);
		faction.Provinces.Add(5);
		state.RebelFactions.Add(faction);
		state.Province(5).Controller = RebelWorld.Reb;

		CRebelFactionDay.RunDaily(state);

		Assert.Equal(RebelWorld.Pol, state.Province(5).Controller);
		Assert.Empty(state.RebelFactions);
	}

	[Fact]
	public void An_army_with_nothing_to_do_beside_a_friendly_home_is_disbanded()
	{
		CGameState state = RebelWorld.State();
		var faction = new CRebelFaction(1, TypeOf(state, "partisans"), 5, RebelWorld.Pol);
		faction.Targets.Add(RebelWorld.Ger);
		state.RebelFactions.Add(faction);
		CUnit army = Garrison(state, RebelWorld.Reb, 6);
		faction.Armies.Add(army.Id);

		// Germany holds Warsaw, which the partisans want; elsewhere Poland is their friend.
		Assert.Equal(4, CRebelFactionDay.ChooseTarget(state, faction, army)?.Id);
		state.Province(4).Controller = RebelWorld.Pol;
		Assert.Null(CRebelFactionDay.ChooseTarget(state, faction, army));

		CRebelFactionDay.Run(state, faction);
		Assert.True(army.IsRemoved);
		Assert.Empty(faction.Armies);
	}

	[Fact]
	public void Independence_releases_the_land_hands_over_the_armies_and_makes_a_revolutionary_war()
	{
		CGameState state = RebelWorld.State();
		state.Country(RebelWorld.Slo)!.EveryUnitUnlocked = true;
		var faction = new CRebelFaction(1, TypeOf(state, "nationalist_rebels"), 3, RebelWorld.Ger)
		{
			Independence = RebelWorld.Slo,
		};
		faction.Targets.Add(RebelWorld.Ger);
		faction.Provinces.Add(3);
		state.RebelFactions.Add(faction);
		state.Province(3).Controller = RebelWorld.Reb;
		CUnit army = Garrison(state, RebelWorld.Reb, 3);
		faction.Armies.Add(army.Id);
		var messages = new List<CPactMessage>();
		state.PactMessaged += messages.Add;

		Assert.Single(CRebelFactionMonth.Candidates(state, faction));
		Assert.True(CRebelFactionMonth.GrantIndependence(state, faction));

		CProvince posen = state.Province(3);
		Assert.Equal(RebelWorld.Slo, posen.Owner);
		Assert.Equal(RebelWorld.Slo, posen.Controller);
		Assert.Equal(RebelWorld.Slo, army.Owner);
		Assert.Contains(army, state.Country(RebelWorld.Slo)!.Units);
		// Germany's technology, and an army of three infantry brigades in the province with industry.
		Assert.Equal(3, state.Country(RebelWorld.Slo)!.TechnologyLevels[RebelWorld.Smallarms.Name]);
		Assert.Contains(state.Country(RebelWorld.Slo)!.Units, unit => unit.SubUnits.Count == 3);
		CWar war = state.Wars.Single(each => each.IsRevolutionary);
		Assert.Equal(new[] { RebelWorld.Slo }, war.Attackers);
		Assert.Equal(new[] { RebelWorld.Ger }, war.Defenders);
		Assert.Equal(CWarName.Normal, war.MadeName!.Key);
		Assert.Contains(messages, message => message.We == "INDEPENDANCE");
		Assert.True(faction.WantsRemoval);
	}

	[Fact]
	public void Release_vassal_REB_frees_the_scopes_rebel_faction_from_the_scopes_country_with_no_war()
	{
		CGameState state = RebelWorld.State();
		state.Country(RebelWorld.Slo)!.EveryUnitUnlocked = true;
		var faction = new CRebelFaction(1, TypeOf(state, "nationalist_rebels"), 3, RebelWorld.Ger)
		{
			Independence = RebelWorld.Slo,
		};
		faction.Targets.Add(RebelWorld.Ger);
		faction.Provinces.Add(3);
		state.RebelFactions.Add(faction);
		state.Province(3).Controller = RebelWorld.Reb;
		CUnit army = Garrison(state, RebelWorld.Reb, 3);
		faction.Armies.Add(army.Id);

		// With no rebel faction in the scope it does nothing.
		CEffects.Run(state, ScriptWorld.Effects("release_vassal = REB"), CEventScope.Start(RebelWorld.Ger));
		Assert.Equal(RebelWorld.Ger, state.Province(3).Owner);

		Assert.Single(CRebelFactionMonth.Candidates(state, faction, state.Country(RebelWorld.Ger)!));
		CEffects.Run(state, ScriptWorld.Effects("release_vassal = REB"),
			CEventScope.Start(RebelWorld.Ger).WithRebelFaction(faction));

		Assert.Equal(RebelWorld.Slo, state.Province(3).Owner);
		Assert.Equal(RebelWorld.Slo, army.Owner);
		Assert.DoesNotContain(state.Wars, war => war.IsRevolutionary);
		Assert.True(faction.WantsRemoval);
	}

	[Fact]
	public void The_month_frees_nobody_before_the_land_has_been_held_its_delay()
	{
		CGameState state = RebelWorld.State();
		var faction = new CRebelFaction(1, TypeOf(state, "nationalist_rebels"), 3, RebelWorld.Ger)
		{
			Independence = RebelWorld.Slo,
		};
		faction.Targets.Add(RebelWorld.Ger);
		faction.Provinces.Add(3);
		state.RebelFactions.Add(faction);
		state.Province(3).Controller = RebelWorld.Reb;
		state.Province(3).RebelsTook = state.Date.AddMonths(-23);
		long before = state.Random.Draws;

		CRebelFactionMonth.RunMonthly(state);

		Assert.Equal(0, state.Random.Draws - before);
		Assert.Equal(RebelWorld.Ger, state.Province(3).Owner);
		state.Province(3).RebelsTook = state.Date.AddMonths(-24);
		CRebelFactionMonth.RunMonthly(state);
		Assert.Equal(1, state.Random.Draws - before);
	}

	[Fact]
	public void Release_gives_a_country_its_cores_the_scope_owns()
	{
		CGameState state = RebelWorld.State();
		CEffects.Run(state, ScriptWorld.Effects("release = SLO"), CEventScope.Start(RebelWorld.Ger));

		Assert.Equal(RebelWorld.Slo, state.Province(3).Owner);
		Assert.Equal(RebelWorld.Ger, state.Province(2).Owner);
		// The two were at war a day ago, so a truce stands between them.
		Assert.Equal(state.TotalHours - 24, CGameState.Status(state.Country(RebelWorld.Slo)!, state.Country(RebelWorld.Ger)!).LastWar);
	}

	[Fact]
	public void A_rebel_type_as_an_effect_raises_the_provinces_manpower_times_its_value()
	{
		CGameState state = RebelWorld.State();
		Garrison(state, RebelWorld.Ger, 4);
		CEffects.Run(state, ScriptWorld.Effects("partisans = 1"), CEventScope.Start(RebelWorld.Pol, 4));

		// Manpower 2.5 times 1 is 2.
		CRebelFaction faction = Assert.Single(state.RebelFactions);
		Assert.Equal(2, CRebelFactions.FindUnit(state, faction.Armies[0])!.SubUnits.Count);
	}

	[Fact]
	public void The_rebels_take_land_through_their_siege_and_drop_it_when_they_lose_it()
	{
		CGameState state = RebelWorld.State();
		var faction = new CRebelFaction(1, TypeOf(state, "disgruntled_rabble"), 2, RebelWorld.Ger);
		faction.Targets.Add(RebelWorld.Ger);
		state.RebelFactions.Add(faction);
		CUnit army = Garrison(state, RebelWorld.Reb, 2);
		faction.Armies.Add(army.Id);

		CProvinceHands.SetController(state, state.Province(2), RebelWorld.Reb, consequences: true);
		Assert.Equal(new[] { 2 }, faction.Provinces);

		CProvinceHands.SetController(state, state.Province(2), RebelWorld.Ger, consequences: true);
		Assert.Empty(faction.Provinces);
	}

	[Fact]
	public void A_resilient_and_smart_factions_army_breaks_off_a_battle_whose_odds_are_too_long()
	{
		CGameState state = RebelWorld.State();
		CUnit garrison = Garrison(state, RebelWorld.Ger, 4);
		garrison.SubUnits[0].Stats.Values.Set(CUnitTotals.SoftAttack, CFixedPoint.FromInt(5));
		state.Province(5).Controller = RebelWorld.Ger;   // occupied too: somewhere hostile to fall back to
		CRebelType partisans = TypeOf(state, "partisans");
		CRisings.Rise(state, state.Province(4), partisans, 1);
		CRebelFaction faction = Assert.Single(state.RebelFactions);
		CUnit army = CRebelFactions.FindUnit(state, Assert.Single(faction.Armies))!;
		Assert.True(army.IsFighting);

		// Not resilient: nothing. Resilient and not smart: it fights no friend, so nothing.
		CResilientRetreat.Run(state, faction);
		partisans.Resilient = true;
		CResilientRetreat.Run(state, faction);
		Assert.True(army.IsFighting);

		// Smart: one brigade to one, a garrison with power against partisans with none - far above five to one.
		partisans.Smart = true;
		CResilientRetreat.Run(state, faction);
		Assert.False(army.IsFighting);
		Assert.Single(army.Path);
		partisans.Resilient = false;
		partisans.Smart = false;
	}
}
