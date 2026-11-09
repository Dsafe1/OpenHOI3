using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>Units lent and taken back, against <c>docs/mechanics/expeditions.md</c>.</summary>
public class ExpeditionTests
{
	private static readonly CCountryTag Eng = new("ENG");
	private static readonly CCountryTag Fra = new("FRA");

	private static (CGameState State, CCountry England, CCountry France, CUnit Corps, CUnit Division) World()
	{
		var england = new CCountry { Tag = Eng, Name = "England", DefinitionFile = "", Faction = "allies", DiplomaticInfluence = CFixedPoint.FromInt(5) };
		var france = new CCountry { Tag = Fra, Name = "France", DefinitionFile = "", Faction = "allies" };
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false), new CProvince(1, "London", isSea: false) }, new[] { england, france });
		state.Defines.Set(COfferRules.Section, CExpeditions.CostDefine, CFixedPoint.One);
		state.Defines.Set(COfferRules.Section, CExpeditions.ReturnDefine, CFixedPoint.FromInt(30));
		state.Defines.Set(COfferRules.Section, CExpeditions.ReclaimDefine, CFixedPoint.FromInt(30));
		var corps = new CUnit(EUnitKind.Corps, Eng) { Id = 7, Name = "I Corps", Location = 1 };
		var division = new CUnit(EUnitKind.Division, Eng) { Id = 8, Name = "1st", Location = 1, Parent = corps };
		corps.Children.Add(division);
		england.Units.Add(corps);
		return (state, england, france, corps, division);
	}

	private static COffer Offer(CGameState state, int unit, EOfferStage stage = EOfferStage.Proposed, bool value = true, bool recall = false) =>
		new(EOfferKind.Expedition, Eng, Fra, stage, value, state.TotalHours) { Unit = unit, Recall = recall };

	[Fact]
	public void AnAcceptedOfferMovesTheUnitAndAllUnderItToTheHost()
	{
		(CGameState state, CCountry england, CCountry france, CUnit corps, CUnit division) = World();
		CExpeditions.Apply(state, Offer(state, corps.Id, EOfferStage.Accepted), england, france);
		Assert.Equal(Fra, corps.Owner);
		Assert.Equal(Fra, division.Owner);
		Assert.Equal(Eng, corps.Lender);
		Assert.Equal(Eng, division.Lender);
		Assert.Null(corps.RecallHour);
		Assert.Contains(corps, france.Units);
		Assert.DoesNotContain(corps, england.Units);
	}

	[Fact]
	public void TheInfluenceIsPaidAtTheOfferAndNotAgain()
	{
		(CGameState state, CCountry england, CCountry france, CUnit corps, _) = World();
		CExpeditions.Apply(state, Offer(state, corps.Id), england, france);
		Assert.Equal(CFixedPoint.FromInt(4), england.DiplomaticInfluence);
		CExpeditions.Apply(state, Offer(state, corps.Id, EOfferStage.Declined), england, france);
		Assert.Equal(CFixedPoint.FromInt(4), england.DiplomaticInfluence);
		Assert.Equal(Eng, corps.Owner);
	}

	[Fact]
	public void AWithdrawalOnlySetsTheDateAndTheDayAfterItTheUnitGoesHomeAlone()
	{
		(CGameState state, CCountry england, CCountry france, CUnit corps, CUnit division) = World();
		CExpeditions.HandOver(state, corps, england, france);
		CExpeditions.Apply(state, Offer(state, corps.Id, value: false, recall: true), england, france);
		Assert.Equal(state.TotalHours + 30 * CGameState.HoursPerDay, corps.RecallHour);
		Assert.Equal(Fra, corps.Owner);
		CExpeditions.RunDaily(state);
		Assert.Equal(Fra, corps.Owner);
		state.SetClock(corps.RecallHour!.Value + 1);
		CExpeditions.RunDaily(state);
		Assert.Equal(Eng, corps.Owner);
		Assert.Null(corps.Lender);
		Assert.Equal(Fra, division.Owner);
		Assert.Null(division.Parent);
		Assert.Contains(division, france.Units);
	}

	[Fact]
	public void SeveralRecalledComeHomeOneADay()
	{
		(CGameState state, CCountry england, CCountry france, CUnit corps, CUnit division) = World();
		corps.Children.Remove(division);
		division.Parent = null;
		england.Units.Add(division);
		CExpeditions.HandOver(state, corps, england, france);
		CExpeditions.HandOver(state, division, england, france);
		corps.RecallHour = 0;
		division.RecallHour = 0;
		state.SetClock(state.TotalHours + 1);
		CExpeditions.RunDaily(state);
		Assert.Equal(1, new[] { corps, division }.Count(unit => unit.Owner == Eng));
		CExpeditions.RunDaily(state);
		Assert.Equal(2, new[] { corps, division }.Count(unit => unit.Owner == Eng));
	}

	[Fact]
	public void AWithdrawalSendsNoDiplomat()
	{
		(CGameState state, CCountry england, CCountry france, CUnit corps, _) = World();
		CExpeditions.HandOver(state, corps, england, france);
		COffer withdraw = Offer(state, corps.Id, value: false, recall: true);
		Assert.True(COfferRules.IsValid(state, withdraw));
		new COfferCommand(withdraw).Execute(state);
		Assert.False(COfferRules.DiplomatEnRoute(state, england, france));
	}

	[Fact]
	public void TheListKeepsToWhatMayGoAndATreeOpensDownward()
	{
		(CGameState state, CCountry england, _, CUnit corps, CUnit division) = World();
		COffer send = Offer(state, 0);
		Assert.Equal(new[] { (corps, 0) }, CExpeditionText.Rows(state, england, send, EExpeditionKind.Army, new HashSet<int>()));
		Assert.Equal(new[] { (corps, 0), (division, 1) }, CExpeditionText.Rows(state, england, send, EExpeditionKind.Army, new HashSet<int> { corps.Id }));
		Assert.Empty(CExpeditionText.Rows(state, england, send, EExpeditionKind.Navy, new HashSet<int>()));
		corps.Lender = Fra;
		Assert.Empty(CExpeditionText.Rows(state, england, send, EExpeditionKind.Army, new HashSet<int>()));
	}

	[Fact]
	public void TheDescriptionCountsTheUnitAndAllUnderIt()
	{
		(_, _, _, CUnit corps, _) = World();
		Assert.Equal(2, CExpeditionText.CountUnder(corps));
	}

	[Fact]
	public void ALentUnitIsNotSplitOrGivenAnotherLeader()
	{
		(CGameState state, _, _, CUnit corps, _) = World();
		corps.Lender = Fra;
		Assert.False(CUnitSplit.MayPress(corps));
		Assert.False(OpenHOI3.Interface.Screens.CUnitPanelText.MayChangeLeader(state, corps, Eng));
	}

	[Fact]
	public void ALentUnitIsNotMerged()
	{
		(CGameState state, CCountry england, _, CUnit corps, _) = World();
		var other = new CUnit(EUnitKind.Corps, Eng) { Id = 9, Location = 1 };
		corps.Lender = Fra;
		Assert.False(CUnitMerge.MayMerge(state, england, new[] { corps, other }));
	}
}
