using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What a country knows, against <c>docs/mechanics/intelligence.md</c>, on <see cref="ScriptWorld"/>'s strip:
/// Germany holds 1 to 3, Poland 4, Italy 7 (Germany's ally in the axis); 6 is nobody's land beside Warsaw, 5 the sea.
/// </summary>
public class IntelligenceTests
{
	private static readonly CCountryTag Ger = ScriptWorld.Ger;
	private static readonly CCountryTag Pol = ScriptWorld.Pol;
	private const int Posen = 3;
	private const int Warsaw = 4;
	private const int Sea = 5;
	private const int Wilds = 6;
	private const int Rome = 7;

	private static CCountry C(CGameState state, CCountryTag tag) => state.Country(tag)!;

	private static int Known(CGameState state, int province, CCountryTag country) =>
		CProvinceIntel.Level(state, state.Province(province), country);

	private static CUnit Put(CGameState state, CCountryTag owner, int province)
	{
		var unit = new CUnit(EUnitKind.Division, owner) { Location = province };
		C(state, owner).Units.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	[Fact]
	public void The_figure_for_another_country_is_its_spies_less_their_counter_intelligence_with_no_floor()
	{
		CGameState state = ScriptWorld.State();
		CGameState.Presence(C(state, Ger), C(state, Pol)).Level = 3000;
		C(state, Pol).Modifier.Add(CCountryIntel.CounterIntelligence, CFixedPoint.Parse("0.25"));

		Assert.Equal(50, CCountryIntel.Factor(C(state, Ger), C(state, Pol)));
		Assert.Equal(-250, CCountryIntel.Factor(C(state, ScriptWorld.Ita), C(state, Pol)));
		Assert.False(CCountryIntel.HasNone(C(state, Ger), C(state, Pol)));
	}

	[Fact]
	public void The_day_gives_9_to_the_holder_and_its_friends_and_3_on_the_land_beside()
	{
		CGameState state = ScriptWorld.State();

		CProvinceIntel.Rebuild(state);

		Assert.Equal(9, Known(state, Posen, Ger));
		Assert.Equal(9, Known(state, Posen, ScriptWorld.Ita));   // of Germany's faction
		Assert.Equal(3, Known(state, Warsaw, Ger));                // beside Posen
		Assert.Equal(3, Known(state, Posen, Pol));
		Assert.Equal(0, Known(state, Wilds, Ger));
		Assert.Equal(0, Known(state, Rome, Pol));
	}

	[Fact]
	public void With_the_fog_of_war_off_the_country_played_knows_every_province_at_9_and_the_AI_as_before()
	{
		CGameState state = ScriptWorld.State();
		state.Played.Add(ScriptWorld.Slo);
		CProvinceIntel.Rebuild(state);
		int pol = Known(state, Posen, Pol);
		Assert.NotEqual(9, Known(state, Posen, ScriptWorld.Slo));

		new OpenHOI3.Sim.Commands.CFogOfWarCommand(false).Execute(state);

		Assert.False(state.FogOfWar);
		CCountry slovakia = C(state, ScriptWorld.Slo);
		Assert.All(state.Provinces.Skip(1), province => Assert.Equal(9, province.Intel[slovakia.Index]));
		Assert.Equal(pol, Known(state, Posen, Pol));                    // a country nobody plays knows what it knew

		new OpenHOI3.Sim.Commands.CFogOfWarCommand(true).Execute(state);
		Assert.NotEqual(9, Known(state, Posen, ScriptWorld.Slo));
	}

	[Fact]
	public void A_foreign_unit_gives_9_on_land_2_at_sea_and_the_land_beside_by_decryption()
	{
		CGameState state = ScriptWorld.State();
		Put(state, Ger, Warsaw);
		Put(state, Pol, Sea);
		C(state, Ger).Technology.Add(CProvinceIntel.Decryption, CFixedPoint.Parse("0.3"));

		CProvinceIntel.Rebuild(state);

		Assert.Equal(9, Known(state, Warsaw, Ger));
		Assert.Equal(2, Known(state, Sea, Pol));
		// A lead of 0.3 over Poland's encryption: 0.3 x 10 is three, and one more - 2 + 4.
		Assert.Equal(6, Known(state, Wilds, Ger));
	}

	[Fact]
	public void The_day_s_step_from_decryption_is_0_1_2_and_then_a_tenth_at_a_time()
	{
		Assert.Equal(0, CProvinceIntel.DayStep(-100));
		Assert.Equal(1, CProvinceIntel.DayStep(0));
		Assert.Equal(2, CProvinceIntel.DayStep(100));
		Assert.Equal(3, CProvinceIntel.DayStep(200));
		Assert.Equal(9, CProvinceIntel.DayStep(5000));
	}

	[Fact]
	public void The_hour_raises_where_a_unit_went_with_the_unscaled_step_beside_it()
	{
		CGameState state = ScriptWorld.State();
		C(state, Ger).Technology.Add(CProvinceIntel.Decryption, CFixedPoint.Parse("0.3"));
		CUnit unit = Put(state, Ger, Posen);
		CProvinceIntel.Rebuild(state);
		Assert.Equal(0, Known(state, Wilds, Ger));

		unit.Location = Warsaw;
		state.ForgetPositions();
		CProvinceIntel.RaiseHourly(state);

		Assert.Equal(9, Known(state, Warsaw, Ger));
		// The hour gives a lead at most 4 where the day gave 6.
		Assert.Equal(4, Known(state, Wilds, Ger));
		CProvinceIntel.Rebuild(state);
		Assert.Equal(6, Known(state, Wilds, Ger));
	}

	[Fact]
	public void An_hour_that_changes_an_alliance_rebuilds_the_whole()
	{
		CGameState state = ScriptWorld.State();
		CProvinceIntel.Rebuild(state);
		Assert.Equal(3, Known(state, Warsaw, Ger));

		state.Alliances.Add(new CAlliance(Ger, Pol));
		CProvinceIntel.RaiseHourly(state);

		Assert.Equal(9, Known(state, Warsaw, Ger));
	}

	[Fact]
	public void Radar_sees_at_its_near_level_where_a_station_on_its_list_reaches()
	{
		CGameState state = ScriptWorld.State();
		state.Defines.Set("military", CRadarIntel.RadarRange, CFixedPoint.Parse("0.18"));
		state.Defines.Set("military", CRadarIntel.BaseRadioStrength, CFixedPoint.FromInt(10));
		state.Province(Posen).Modifier.Add("radar_level", CFixedPoint.One);
		CRadarProvinces.ProvinceMade(state, state.Province(Posen));
		CProvinceIntel.Rebuild(state);
		Assert.Equal(0, Known(state, Wilds, Ger));   // no listening station yet

		C(state, Ger).Technology.Add(CTechnology.ListeningStation, CFixedPoint.One);
		CProvinceIntel.Rebuild(state);

		// Every centre here is at one point; decryption equals the holderless province's encryption: 0 + 2.
		Assert.Equal(2, Known(state, Wilds, Ger));
		Assert.Contains(Posen, C(state, Ger).RadarProvinces);
		Assert.Equal(1000, CRadarIntel.Visibility(state, state.Province(Wilds), 10000));

		// The country's radar efficiency is read by nothing - and with an enhanced game's fix scales the reach.
		C(state, Ger).Technology.Add(CRadarIntel.RadarEfficiency, CFixedPoint.FromInt(-1));
		CProvinceIntel.Rebuild(state);
		Assert.Equal(2, Known(state, Wilds, Ger));
		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(System.Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
		CProvinceIntel.Rebuild(state);
		Assert.Equal(0, Known(state, Wilds, Ger));
		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules());
		C(state, Ger).Technology.Add(CRadarIntel.RadarEfficiency, CFixedPoint.FromInt(1));

		// A station that is not on the list sees nothing, whatever its level.
		C(state, Ger).RadarProvinces.Clear();
		CProvinceIntel.Rebuild(state);
		Assert.Equal(0, Known(state, Wilds, Ger));
	}

	private static void Radar(CGameState state, int province, string level)
	{
		CProvince here = state.Province(province);
		here.Modifier.Set("radar_level", CFixedPoint.Parse(level));
		CRadarProvinces.ProvinceMade(state, here);
	}

	[Fact]
	public void A_province_s_rebuild_adds_its_radar_from_under_a_level_and_takes_it_off_on_a_fall_from_above_nought()
	{
		CGameState state = ScriptWorld.State();

		Radar(state, Posen, "0.5");                      // from nought under a level to above nought: added
		Assert.Equal(new[] { Posen }, C(state, Ger).RadarProvinces);
		Assert.True(state.IntelWholeAsked);
		Radar(state, Posen, "0.7");                      // under a level still: added again, and once only
		Assert.Equal(new[] { Posen }, C(state, Ger).RadarProvinces);
		Radar(state, Posen, "1.5");                      // from under a level: added again
		Radar(state, Posen, "0.5");                      // from above nought to under a level: taken off
		Assert.Empty(C(state, Ger).RadarProvinces);
		Radar(state, Posen, "0.7");                      // and back, from under a level
		Assert.Equal(new[] { Posen }, C(state, Ger).RadarProvinces);
		Assert.Equal(CFixedPoint.Parse("0.7"), state.Province(Posen).RadarLevelApplied);

		// Unchanged, it asks for nothing.
		CProvinceIntel.Rebuild(state);
		Radar(state, Posen, "0.7");
		Assert.False(state.IntelWholeAsked);
	}

	[Fact]
	public void A_province_nobody_owns_tells_no_list()
	{
		CGameState state = ScriptWorld.State();
		Radar(state, Wilds, "2");
		Assert.All(state.Countries, country => Assert.Empty(country.RadarProvinces));
		Assert.Equal(CFixedPoint.Zero, state.Province(Wilds).RadarLevelApplied);
	}

	[Fact]
	public void A_change_of_hands_with_consequences_moves_the_radar_and_a_plain_one_leaves_it_with_the_old_holder()
	{
		CGameState state = ScriptWorld.State();
		Radar(state, Posen, "1");

		CProvinceHands.SetController(state, state.Province(Posen), Pol);
		Assert.Equal(new[] { Posen }, C(state, Ger).RadarProvinces);   // a plain change: the old holder goes on seeing
		Assert.Empty(C(state, Pol).RadarProvinces);

		CProvinceHands.SetController(state, state.Province(Posen), Ger, consequences: true, peaceful: true);
		CProvinceHands.SetController(state, state.Province(Posen), Pol, consequences: true, peaceful: true);
		Assert.Empty(C(state, Ger).RadarProvinces);
		Assert.Equal(new[] { Posen }, C(state, Pol).RadarProvinces);
	}

	[Fact]
	public void A_local_boost_gives_from_8_on_every_unit_in_the_controller_s_provinces_within_reach()
	{
		CGameState state = ScriptWorld.State();
		state.Defines.Set("country", "SPY_MILITARY_INTEL_LOCAL_TIME_BASE", CFixedPoint.FromInt(15));
		CUnit german = Put(state, Ger, Posen);
		CUnit polish = Put(state, Pol, Warsaw);

		CEffects.Run(state, ScriptWorld.Effects("local_intel_boost = 60"), CEventScope.Start(null, Posen, Pol));

		CIntelGrant grant = Assert.Single(german.IntelGrants);
		Assert.Equal(Pol, grant.Country);
		Assert.Equal(8, grant.Level);
		Assert.Equal(15, grant.Days);
		Assert.Empty(polish.IntelGrants);   // Warsaw is not Germany's
		Assert.Contains(Posen, state.IntelRequests);
	}
}
