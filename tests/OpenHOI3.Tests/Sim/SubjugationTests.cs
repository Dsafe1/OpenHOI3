using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Annexation and the puppet, against <c>docs/mechanics/war.md</c>, section 13, on <see cref="WarTests.World"/>:
/// Germany leads the axis with Italy; Slovakia is Germany's puppet and Lithuania Poland's; France guarantees Poland,
/// Britain is allied to it.
/// </summary>
public class SubjugationTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Ita = new("ITA");
	private static readonly CCountryTag Fra = new("FRA");
	private static readonly CCountryTag Eng = new("ENG");
	private static readonly CCountryTag Lit = new("LIT");

	private static CCountry C(CGameState state, CCountryTag tag) => state.Country(tag)!;

	[Fact]
	public void An_annexed_countrys_land_goes_to_its_occupier_or_the_annexer_and_everybody_forgets_it()
	{
		CGameState state = WarTests.World();
		var war = new CWar("test");
		state.Wars.Add(war);
		CWarSides.Join(state, war, Ger, attacking: true);
		CWarSides.Join(state, war, Pol, attacking: false);
		CWarSides.Join(state, war, Eng, attacking: false);
		// Poland owns Rome too, which France holds; Poland holds Paris.
		state.Province(3).Owner = Pol;
		state.Province(3).Controller = Fra;
		state.Province(4).Controller = Pol;
		CGameState.Status(C(state, Eng), C(state, Pol)).Threat = CFixedPoint.FromInt(20);
		var told = new List<CPactMessage>();
		state.PactMessaged += told.Add;

		CAnnexation.Annex(state, C(state, Ger), C(state, Pol));
		Assert.Equal("ANNEX", told[0].We);
		Assert.Equal(Ger, state.Province(2).Owner);
		Assert.Equal(Fra, state.Province(3).Owner);
		// What Poland held of France's goes back.
		Assert.Equal(Fra, state.Province(4).Controller);
		Assert.False(war.Involves(Pol));
		Assert.True(CCountryLists.AtWar(state, Ger, Eng));
		Assert.Null(C(state, Lit).Overlord);
		Assert.DoesNotContain(state.Alliances, alliance => alliance.Joins(Pol, Eng));
		Assert.DoesNotContain(state.Guarantees, guarantee => guarantee.Joins(Fra, Pol));
		Assert.Equal(CFixedPoint.Zero, CGameState.Status(C(state, Eng), C(state, Pol)).Threat);
	}

	[Fact]
	public void A_country_occupied_to_its_national_unity_surrenders_and_its_victors_annexing_goal_is_enforced()
	{
		CGameState state = WarTests.World();
		state.Alliances.Clear();
		state.Guarantees.Clear();
		state.CasusBelliTypes[1].PeaceOptions.Add(CWarGoalEnforcement.Annex);
		var war = new CWar("test");
		state.Wars.Add(war);
		CWarSides.Join(state, war, Ger, attacking: true);
		CWarSides.Join(state, war, Pol, attacking: false);
		CProvince warsaw = state.Province(2);
		warsaw.Cores.Add(Pol);
		warsaw.Points = 5;
		C(state, Pol).NationalUnity = CFixedPoint.FromInt(50);
		CSurrender.CountVictoryPoints(state);
		CSurrender.RunDaily(state);
		Assert.Equal(Pol, warsaw.Owner);

		warsaw.Controller = Ger;
		CSurrender.CountVictoryPoints(state);
		Assert.Equal(CSurrender.Brink, CSurrender.Progress(state, C(state, Pol)));
		CSurrender.RunDaily(state);
		// No goal: a conquer goal of the victor's, which annexes here.
		Assert.Equal(Ger, warsaw.Owner);
		Assert.False(war.Involves(Pol));
		Assert.Equal(state.TotalHours, C(state, Pol).LastSurrender);
	}

	[Fact]
	public void A_country_with_a_host_goes_into_exile_and_sits_in_its_leaders_capital()
	{
		CGameState state = WarTests.World();
		state.CasusBelliTypes[1].PeaceOptions.Add(CWarGoalEnforcement.Annex);
		CCountry france = C(state, Fra);
		CCountry britain = C(state, Eng);
		britain.Faction = "allies";
		britain.BaseIc = 200;
		france.Faction = "allies";
		// A puppet whose overlord owns land goes into exile whatever the draw.
		france.Overlord = Eng;
		france.Manpower = CFixedPoint.FromInt(100);
		var war = new CWar("test");
		state.Wars.Add(war);
		CWarSides.Join(state, war, Ger, attacking: true);
		CWarSides.Join(state, war, Fra, attacking: false);

		CCapitulation.Capitulate(state, france, C(state, Ger));
		Assert.True(france.InExile);
		Assert.Equal(5, france.ActingCapital);
		Assert.Equal(CFixedPoint.FromInt(10), france.Manpower);
		Assert.Same(france.PoolInExile, state.Stockpile(france));
		// Germany holds Paris now, which France still owns; France fights on.
		Assert.Equal(Ger, state.Province(4).Controller);
		Assert.Equal(Fra, state.Province(4).Owner);
		Assert.True(war.Involves(Fra));
		Assert.Equal(0, CSurrender.Progress(state, france));
	}

	[Fact]
	public void A_fallen_capital_is_looted_and_a_government_goes_back_when_its_capital_is_held_again()
	{
		CGameState state = WarTests.World();
		CCountry poland = C(state, Pol);
		poland.OriginalCapital = 2;
		// Poland owns and holds Rome as well, with a victory point.
		state.Province(3).Owner = Pol;
		state.Province(3).Controller = Pol;
		state.Province(3).Points = 1;
		state.Province(2).Pool[OpenHOI3.Sim.Economy.EGoods.Money] = CFixedPoint.FromInt(40);

		CProvinceHands.SetController(state, state.Province(2), Ger, consequences: true);
		// A quarter of the money looted; with no province in a supply area - this world has no roads - the seat stays.
		Assert.Equal(CFixedPoint.FromInt(30), state.Province(2).Pool[OpenHOI3.Sim.Economy.EGoods.Money]);
		Assert.Equal(2, poland.ActingCapital);

		// Sitting in Rome, the government goes back to Warsaw the hour Warsaw is held again.
		poland.ActingCapital = 3;
		Assert.Same(state.Province(3).Pool, state.Stockpile(poland));
		state.Province(2).Controller = Pol;
		CActingCapital.RunHourly(state);
		Assert.Equal(2, poland.ActingCapital);
		Assert.Equal(2, poland.Capital);
	}

	[Fact]
	public void With_no_capital_declared_in_play_a_government_holding_its_seat_stays_whatever_the_history_had_first()
	{
		// China at the 1939 bookmark: the history moved its capital from Nanjing to Chongqing, and Nanjing comes back to its
		// control as Japan's puppet. The original capital is the declared one, so the government stays in Chongqing.
		CGameState state = WarTests.World();
		CCountry poland = C(state, Pol);
		state.Province(3).Owner = Pol;
		state.Province(3).Controller = Pol;
		poland.Capital = 3;
		poland.ActingCapital = 3;
		Assert.Equal(0, poland.OriginalCapital);

		CActingCapital.RunHourly(state);
		Assert.Equal(3, poland.ActingCapital);
		Assert.Same(state.Province(3).Pool, state.Stockpile(poland));
	}

	[Fact]
	public void The_first_capital_declared_in_play_is_the_original_capital()
	{
		CGameState state = WarTests.World();
		CCountry poland = C(state, Pol);
		state.Province(3).Owner = Pol;
		state.Province(3).Controller = Pol;
		state.Province(3).Cores.Add(Pol);

		// Warsaw lost for good: the next day's head declares Rome, and that is logged as the original capital; the seat
		// is not moved.
		state.Province(2).Owner = Ger;
		CActingCapital.EnsureCapitals(state);
		Assert.Equal(3, poland.Capital);
		Assert.Equal(3, poland.OriginalCapital);
		Assert.Equal(2, poland.ActingCapital);

		// A later declaration leaves the first in place.
		poland.DeclareCapital(2);
		Assert.Equal(2, poland.Capital);
		Assert.Equal(3, poland.OriginalCapitalOrDeclared);
	}

	[Fact]
	public void A_puppet_is_allied_joins_its_overlords_faction_at_the_back_and_its_leaders_wars()
	{
		CGameState state = WarTests.World();
		var war = new CWar("test");
		state.Wars.Add(war);
		CWarSides.Join(state, war, Ger, attacking: true);
		CWarSides.Join(state, war, Fra, attacking: false);
		state.Province(2).Cores.Add(Ger);
		state.Province(2).Cores.Add(Pol);

		CPuppets.Make(state, C(state, Ita), C(state, Pol));
		Assert.Equal(Ita, C(state, Pol).Overlord);
		// Its own puppet goes free; its alliance and the guarantee it had end.
		Assert.Null(C(state, Lit).Overlord);
		Assert.DoesNotContain(state.Alliances, alliance => alliance.Joins(Pol, Eng));
		Assert.Contains(state.Alliances, alliance => alliance.Joins(Ita, Pol));
		Assert.DoesNotContain(state.Guarantees, guarantee => guarantee.Joins(Fra, Pol));
		// At the back of the axis, and into Germany's war - the leader's, not Italy's own.
		Assert.Equal(new[] { Ger, Ita, Pol }, CCountryLists.FactionMembers(state, "axis").Select(c => c.Tag));
		Assert.Contains(Pol, war.Attackers);
		Assert.Equal(CFixedPoint.FromInt(200), CGameState.Status(C(state, Pol), C(state, Ita)).Relation);
		// Its province with no Italian core stays its own.
		Assert.Equal(Pol, state.Province(2).Owner);
	}
}
