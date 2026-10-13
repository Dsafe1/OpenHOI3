using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Peace, leaving a war, the day's return of occupied land and the surrender progress, against
/// <c>docs/mechanics/war.md</c>, sections 11 and 12, on <see cref="WarTests.World"/>: Poland at war with France,
/// neither in a faction.
/// </summary>
public class PeaceTests
{
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Fra = new("FRA");
	private static readonly CCountryTag Eng = new("ENG");
	private static readonly CCountryTag Ita = new("ITA");
	private static readonly CCountryTag Lit = new("LIT");

	private const int Warsaw = 2;
	private const int Rome = 3;
	private const int Paris = 4;
	private const int London = 5;

	/// <summary>Poland attacking France, the two alone.</summary>
	private static (CGameState State, CWar War) AtWar()
	{
		CGameState state = WarTests.World();
		state.Alliances.Clear();
		state.Guarantees.Clear();
		C(state, Lit).Overlord = null;
		var war = new CWar("test");
		state.Wars.Add(war);
		CWarSides.Join(state, war, Pol, attacking: true);
		CWarSides.Join(state, war, Fra, attacking: false);
		return (state, war);
	}

	private static CCountry C(CGameState state, CCountryTag tag) => state.Country(tag)!;

	private static COffer Peace(CGameState state, CCountryTag actor, CCountryTag recipient, EOfferStage stage) =>
		new(EOfferKind.Peace, actor, recipient, stage, true, state.TotalHours);

	[Fact]
	public void A_peace_moves_occupied_cores_to_their_occupier_gives_the_rest_back_and_ends_the_war()
	{
		(CGameState state, CWar war) = AtWar();
		CProvince paris = state.Province(Paris);
		paris.Controller = Pol;
		paris.Cores.Add(Pol);
		state.Province(Warsaw).Controller = Fra;
		new COfferCommand(Peace(state, Pol, Fra, EOfferStage.Accepted)).Execute(state);
		Assert.Equal(Pol, paris.Owner);
		Assert.Equal(Pol, paris.Controller);
		// Warsaw had no French core: it goes back to Poland's control at once.
		Assert.Equal(Pol, state.Province(Warsaw).Controller);
		Assert.False(CCountryLists.AtWar(state, Pol, Fra));
		Assert.Empty(war.Attackers);
		Assert.Empty(war.Defenders);
		// Nothing removes the war itself.
		Assert.Contains(war, state.Wars);
	}

	[Fact]
	public void A_province_goes_to_the_first_core_holder_of_the_other_side_once_for_each_of_its_cores()
	{
		(CGameState state, CWar war) = AtWar();
		CWarSides.Join(state, war, Eng, attacking: true);
		CProvince paris = state.Province(Paris);
		// Britain holds it, Poland's core is first, Britain's second.
		paris.Controller = Eng;
		paris.Cores.Add(Pol);
		paris.Cores.Add(Eng);
		List<CPeaceTransfer> lost = CPeace.DefendersLose(state, war, Pol);
		Assert.Equal(2, lost.Count);
		Assert.All(lost, transfer => Assert.Equal(Pol, transfer.Gainer));
		// Held by a country outside the war, it goes nowhere.
		paris.Controller = Ita;
		Assert.Empty(CPeace.DefendersLose(state, war, Pol));
	}

	[Fact]
	public void Against_a_lone_enemy_only_the_lone_country_leaves_and_the_other_side_stays_in_the_war()
	{
		(CGameState state, CWar war) = AtWar();
		CWarSides.Join(state, war, Eng, attacking: false);
		// Britain is not the first of its side and is in no faction: it may not speak for it.
		Assert.False(CPeace.IsSelectable(state, C(state, Pol), C(state, Eng)));
		Assert.True(CPeace.IsSelectable(state, C(state, Pol), C(state, Fra)));
		CPeace.Apply(state, Peace(state, Pol, Fra, EOfferStage.Accepted), C(state, Pol), C(state, Fra));
		Assert.Empty(war.Attackers);
		Assert.Equal(new[] { Fra, Eng }, war.Defenders);
		Assert.False(CCountryLists.AtWar(state, Pol, Eng));
	}

	[Fact]
	public void Leaving_a_war_deletes_the_goals_held_and_the_goals_against()
	{
		(CGameState state, CWar war) = AtWar();
		CWarSides.Join(state, war, Eng, attacking: false);
		CWarGoals.AddToWar(state, new CWarGoal(state.CasusBelliTypes[1], Pol, Fra), war);
		CWarGoals.AddToWar(state, new CWarGoal(state.CasusBelliTypes[1], Pol, Eng), war);
		CWarGoals.AddToWar(state, new CWarGoal(state.CasusBelliTypes[1], Fra, Pol), war);
		CWarSides.Leave(state, war, Fra);
		Assert.Empty(war.GoalsOf(Fra));
		Assert.Single(war.GoalsOf(Pol));
		Assert.Equal(Eng, war.GoalsOf(Pol)[0].Recipient);
	}

	[Fact]
	public void The_players_who_made_it_are_told_of_a_peace_and_of_its_refusal()
	{
		(CGameState state, CWar _) = AtWar();
		state.Played.Add(Pol);
		var told = new List<CPactMessage>();
		state.PactMessaged += told.Add;
		CPeace.Apply(state, Peace(state, Pol, Fra, EOfferStage.Declined), C(state, Pol), C(state, Fra));
		Assert.Equal("PEACEDECLINE", told[0].We);
		Assert.True(CCountryLists.AtWar(state, Pol, Fra));
		CPeace.Apply(state, Peace(state, Pol, Fra, EOfferStage.Accepted), C(state, Pol), C(state, Fra));
		Assert.Equal("PEACEACCEPT", told[1].We);
		Assert.Equal(Pol, told[1].Told);
	}

	[Fact]
	public void Each_day_land_held_by_a_country_its_owner_is_not_at_war_with_goes_back()
	{
		(CGameState state, CWar _) = AtWar();
		state.Province(Paris).Controller = Pol;
		state.Province(London).Controller = Pol;
		COccupiedLand.ReturnDaily(state);
		// At war with France: kept. At peace with Britain: given back.
		Assert.Equal(Pol, state.Province(Paris).Controller);
		Assert.Equal(Eng, state.Province(London).Controller);
	}

	[Fact]
	public void A_capital_taken_with_consequences_loses_a_quarter_of_its_money_and_resources_and_its_production()
	{
		(CGameState state, CWar _) = AtWar();
		CProvince paris = state.Province(Paris);
		paris.Pool[OpenHOI3.Sim.Economy.EGoods.Money] = CFixedPoint.FromInt(100);
		paris.Pool[OpenHOI3.Sim.Economy.EGoods.Supplies] = CFixedPoint.FromInt(100);
		paris.CurrentProducing[OpenHOI3.Sim.Economy.EGoods.Metal] = CFixedPoint.FromInt(5);
		CProvinceHands.SetController(state, paris, Pol, consequences: true);
		Assert.Equal(CFixedPoint.FromInt(75), paris.Pool[OpenHOI3.Sim.Economy.EGoods.Money]);
		Assert.Equal(CFixedPoint.FromInt(25), state.Province(Warsaw).Pool[OpenHOI3.Sim.Economy.EGoods.Money]);
		// Supplies stay where they are.
		Assert.Equal(CFixedPoint.FromInt(100), paris.Pool[OpenHOI3.Sim.Economy.EGoods.Supplies]);
		Assert.Equal(CFixedPoint.Zero, paris.CurrentProducing[OpenHOI3.Sim.Economy.EGoods.Metal]);
		// Peacefully, nothing of it.
		CProvinceHands.SetController(state, paris, Fra, consequences: true, peaceful: true);
		Assert.Equal(CFixedPoint.FromInt(25), state.Province(Warsaw).Pool[OpenHOI3.Sim.Economy.EGoods.Money]);
	}

	[Fact]
	public void The_surrender_progress_is_the_occupied_share_of_owned_cores_against_national_unity()
	{
		(CGameState state, CWar _) = AtWar();
		CCountry france = C(state, Fra);
		france.NationalUnity = CFixedPoint.FromInt(50);
		CProvince paris = state.Province(Paris);
		CProvince rome = state.Province(Rome);
		paris.Cores.Add(Fra);
		paris.Points = 3;
		// France's core it does not own counts for nothing.
		rome.Cores.Add(Fra);
		rome.Points = 10;
		CSurrender.CountVictoryPoints(state);
		Assert.Equal(3000, france.OwnedCoreVictoryPoints);
		Assert.Equal(0, CSurrender.Progress(state, france));
		paris.Controller = Pol;
		CSurrender.CountVictoryPoints(state);
		// All of it occupied, against a half's unity: held at the brink.
		Assert.Equal(CSurrender.Brink, CSurrender.Progress(state, france));
	}
}
