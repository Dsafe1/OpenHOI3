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
	public void TheAisCommandLendsAUnitWithNoOfferAndNoRecall()
	{
		(CGameState state, CCountry england, CCountry france, CUnit corps, CUnit division) = World();
		var command = new CSendExpeditionCommand(Eng, corps.Id, Fra);
		Assert.True(command.MayExecute(state));
		command.Execute(state);
		Assert.Equal(Fra, division.Owner);
		Assert.Equal(Eng, corps.Lender);
		Assert.Null(corps.RecallHour);
		Assert.Contains(corps, france.Units);
		Assert.Contains(corps, england.LentUnits);
	}

	[Fact]
	public void TheAisCommandSendsALentUnitHomeWithNobodyTold()
	{
		(CGameState state, CCountry england, CCountry france, CUnit corps, CUnit division) = World();
		int told = 0;
		state.PactMessaged += _ => told++;
		new CSendExpeditionCommand(Eng, corps.Id, Fra).Execute(state);
		new CDetachUnitCommand(Fra, division.Id).Execute(state);
		new CSendExpeditionCommand(Fra, corps.Id, Eng).Execute(state);
		Assert.Equal(Eng, corps.Owner);
		Assert.Null(corps.Lender);
		Assert.Contains(corps, england.Units);
		Assert.DoesNotContain(corps, england.LentUnits);
		Assert.Equal(0, told);
	}

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
		// Still on loan: only detached, as the original's children are.
		Assert.Equal(Eng, division.Lender);
		Assert.Contains(division, england.LentUnits);
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
	public void TheLenderKeepsALentUnitOnItsListAndTheHostDoesNotCountIt()
	{
		(CGameState state, CCountry england, CCountry france, CUnit corps, CUnit division) = World();
		CExpeditions.HandOver(state, corps, england, france);
		Assert.Contains(corps, england.LentUnits);
		Assert.Contains(division, england.LentUnits);
		Assert.Equal(Eng, corps.FiguresOwner);
		Assert.DoesNotContain(CUnits.InWorkingOrder(france, _ => true), unit => unit == corps);
		Assert.Contains(CUnits.InWorkingOrder(england, _ => true), unit => unit == corps);
		CExpeditions.Return(state, corps);
		Assert.DoesNotContain(corps, england.LentUnits);
		Assert.Equal(Eng, corps.FiguresOwner);
	}

	[Fact]
	public void AUnitSentHomeTellsBothSidesAboutWhereItStandsAndCarriesItForItsGoto()
	{
		(CGameState state, CCountry england, CCountry france, CUnit corps, _) = World();
		CExpeditions.HandOver(state, corps, england, france);
		var told = new List<CPactMessage>();
		state.PactMessaged += told.Add;
		CExpeditions.Return(state, corps);
		CPactMessage message = Assert.Single(told);
		Assert.Equal(("EXPRETURNEDWE", "EXPRETURNED", "", Fra, Eng), (message.We, message.They, message.Other, message.Actor, message.Recipient));
		Assert.Equal((1, 7), (message.Province, message.Unit));
		Assert.Equal(new[] { ("NAME", "I Corps") }, message.Values);
		Assert.Equal(OpenHOI3.Interface.Messages.EMessageGoto.Unit, OpenHOI3.Interface.Messages.CMessageGotos.Of("EXPRETURNED"));
		Assert.Equal(OpenHOI3.Interface.Messages.EMessageGoto.Unit, OpenHOI3.Interface.Messages.CMessageGotos.Of("EXPRETURNEDWE"));
	}

	[Fact]
	public void ALentUnitIsNotMerged()
	{
		(CGameState state, CCountry england, _, CUnit corps, _) = World();
		var other = new CUnit(EUnitKind.Corps, Eng) { Id = 9, Location = 1 };
		corps.Lender = Fra;
		Assert.False(CUnitMerge.MayMerge(state, england, new[] { corps, other }));
	}

	[Fact]
	public void TheAisCommandSendsALentUnitHomeWithEverythingUnderIt()
	{
		(CGameState state, CCountry england, CCountry france, CUnit corps, CUnit division) = World();
		new CSendExpeditionCommand(Eng, corps.Id, Fra).Execute(state);
		new CSendExpeditionCommand(Fra, corps.Id, Eng).Execute(state);
		Assert.Equal((Eng, Eng), (corps.Owner, division.Owner));
		Assert.Null(corps.Lender);
		Assert.Null(division.Lender);
		Assert.Same(corps, division.Parent);
		Assert.Contains(corps, england.Units);
		Assert.DoesNotContain(corps, france.Units);
		Assert.Empty(england.LentUnits);
	}

	[Fact]
	public void TheAisCommandLendsAUnitsChildrenWithItAndNoRecall()
	{
		(CGameState state, CCountry england, _, CUnit corps, CUnit division) = World();
		division.RecallHour = 5;
		new CSendExpeditionCommand(Eng, corps.Id, Fra).Execute(state);
		Assert.Equal(Eng, division.Lender);
		Assert.Null(division.RecallHour);
		Assert.Same(corps, division.Parent);
		Assert.Equal(new[] { corps, division }, england.LentUnits);
	}

	[Fact]
	public void AUnitOnLoanTakenOutOfTheGameLeavesItsLendersList()
	{
		(CGameState state, CCountry england, CCountry france, CUnit corps, CUnit division) = World();
		CExpeditions.HandOver(state, corps, england, france);
		OpenHOI3.Sim.Combat.CCombatRetreat.RemoveFromGame(state, division);
		Assert.Equal(new[] { corps }, england.LentUnits);
	}

	[Fact]
	public void AGovernmentGoingIntoExileLetsGoWhatItsRemovedFleetCarries()
	{
		CGameState state = WarTests.World();
		var ger = new CCountryTag("GER");
		CCountry france = state.Country(Fra)!;
		CCountry britain = state.Country(Eng)!;
		var war = new OpenHOI3.Sim.Wars.CWar("test");
		state.Wars.Add(war);
		OpenHOI3.Sim.Wars.CWarSides.Join(state, war, ger, attacking: true);
		OpenHOI3.Sim.Wars.CWarSides.Join(state, war, Fra, attacking: false);
		state.Province(4).Controller = ger;
		// A French fleet in a port Germany holds, carrying a French division, a division Britain lent France and one
		// that is Britain's own.
		var fleet = new CUnit(EUnitKind.Navy, Fra) { Id = 20, Name = "Flotte", Location = 4 };
		var own = new CUnit(EUnitKind.Division, Fra) { Id = 21, Name = "1re", Location = 4 };
		var lent = new CUnit(EUnitKind.Division, Eng) { Id = 22, Name = "1st", Location = 4 };
		var foreign = new CUnit(EUnitKind.Division, Eng) { Id = 23, Name = "2nd", Location = 4 };
		france.Units.Add(fleet);
		france.Units.Add(own);
		britain.Units.Add(lent);
		britain.Units.Add(foreign);
		CExpeditions.HandOver(state, lent, britain, france);
		foreach (CUnit passenger in new[] { own, lent, foreign })
		{
			OpenHOI3.Sim.Transport.CTransport.Board(fleet, passenger);
		}

		OpenHOI3.Sim.Wars.CExile.Enter(state, france);
		Assert.True(fleet.IsRemoved);
		// The exile's own passenger goes in its own turn; the lent one goes home; Britain's own is left where it stood.
		Assert.True(own.IsRemoved);
		Assert.False(lent.IsRemoved);
		Assert.Null(lent.Transport);
		Assert.Null(lent.Lender);
		Assert.Equal(5, lent.Location);
		Assert.False(foreign.IsRemoved);
		Assert.Null(foreign.Transport);
		Assert.Equal(4, foreign.Location);
	}

	[Fact]
	public void AGovernmentGoingIntoExileSendsHomeWhatWasLentToItOnGroundNoFriendHolds()
	{
		CGameState state = WarTests.World();
		var ger = new CCountryTag("GER");
		CCountry france = state.Country(Fra)!;
		CCountry britain = state.Country(Eng)!;
		var war = new OpenHOI3.Sim.Wars.CWar("test");
		state.Wars.Add(war);
		OpenHOI3.Sim.Wars.CWarSides.Join(state, war, ger, attacking: true);
		OpenHOI3.Sim.Wars.CWarSides.Join(state, war, Fra, attacking: false);
		state.Province(4).Controller = ger;
		var own = new CUnit(EUnitKind.Division, Fra) { Id = 20, Name = "1re", Location = 4 };
		var inParis = new CUnit(EUnitKind.Corps, Eng) { Id = 21, Name = "BEF", Location = 4 };
		var under = new CUnit(EUnitKind.Division, Eng) { Id = 22, Name = "1st", Location = 4, Parent = inParis };
		inParis.Children.Add(under);
		var nobodys = new CUnit(EUnitKind.Division, Eng) { Id = 23, Name = "2nd", Location = 0 };
		france.Units.Add(own);
		britain.Units.Add(inParis);
		britain.Units.Add(nobodys);
		CExpeditions.HandOver(state, inParis, britain, france);
		CExpeditions.HandOver(state, nobodys, britain, france);
		var told = new List<CPactMessage>();
		state.PactMessaged += told.Add;

		OpenHOI3.Sim.Wars.CExile.Enter(state, france);
		Assert.True(own.IsRemoved);
		// Every level lent goes home on its own and is shattered at its lender's capital, each side told where it stood.
		foreach (CUnit unit in new[] { inParis, under })
		{
			Assert.False(unit.IsRemoved);
			Assert.Equal(Eng, unit.Owner);
			Assert.Null(unit.Lender);
			Assert.Null(unit.Parent);
			Assert.Equal(5, unit.Location);
			Assert.Contains(unit, britain.Units);
		}
		Assert.Equal(new[] { ("EXPRETURNEDWE", "BEF", 4), ("EXPRETURNEDWE", "1st", 4) },
			told.Select(message => (message.We, message.Values[0].Value, message.Province)));
		Assert.All(told, message => Assert.Equal((Fra, Eng), (message.Actor, message.Recipient)));
		// Ground nobody holds is not asked about.
		Assert.Equal((Fra, (CCountryTag?)Eng), (nobodys.Owner, nobodys.Lender));
		Assert.Equal(new[] { nobodys }, britain.LentUnits);
	}

	[Fact]
	public void A_country_with_no_land_hands_its_fleets_to_its_leader_and_each_land_unit_to_whoever_of_its_faction_holds_the_ground()
	{
		var usa = new CCountryTag("USA");
		var hol = new CCountryTag("HOL");
		CProvince Land(int id, CCountryTag owner) => new(id, $"p{id}", isSea: false) { Owner = owner, Controller = owner };
		var state = new CGameState(new CDate(1940, 6, 1),
			new[] { new CProvince(0, "", isSea: false), Land(1, Eng), Land(2, hol), Land(3, usa), new CProvince(4, "sea", isSea: true) },
			new[]
			{
				new CCountry { Tag = Eng, Name = "England", DefinitionFile = "", Faction = "allies" },
				new CCountry { Tag = Fra, Name = "France", DefinitionFile = "", Faction = "allies" },
				new CCountry { Tag = hol, Name = "Holland", DefinitionFile = "", Faction = "allies" },
				new CCountry { Tag = usa, Name = "America", DefinitionFile = "", Faction = "" },
			});
		CCountry france = state.Country(Fra)!;
		CUnit Unit(int id, EUnitKind kind, int at)
		{
			var unit = new CUnit(kind, Fra) { Id = id, Name = $"u{id}", Location = at, ListOrder = id };
			france.Units.Add(unit);
			return unit;
		}
		Unit(1, EUnitKind.Theatre, 1);
		Unit(2, EUnitKind.Division, 1);
		Unit(3, EUnitKind.Division, 2);
		Unit(4, EUnitKind.Division, 3);
		Unit(5, EUnitKind.Navy, 4);

		CLandlessUnitsRun(state);
		Assert.Equal(4, state.Outbox.Count);
		foreach (CCommand command in state.Outbox.ToList())
		{
			command.Execute(state);
		}
		Assert.Equal(Fra, france.Units.Single().Owner);
		Assert.Equal(Eng, CUnitFinder.Find(state, Eng, 2)!.Owner);
		Assert.Equal(hol, CUnitFinder.Find(state, hol, 3)!.Owner);
		// On a factionless country's ground, and at sea: the leader's.
		Assert.Equal(Eng, CUnitFinder.Find(state, Eng, 4)!.Owner);
		Assert.Equal(Eng, CUnitFinder.Find(state, Eng, 5)!.Owner);
		Assert.Equal(Fra, CUnitFinder.Find(state, Eng, 5)!.Lender);

		// A country in exile, or with land, keeps its units; one in no faction has nobody to give them to.
		state.Outbox.Clear();
		Unit(6, EUnitKind.Division, 1);
		france.InExile = true;
		CLandlessUnitsRun(state);
		Assert.Empty(state.Outbox);
		france.InExile = false;
		france.Faction = "";
		CLandlessUnitsRun(state);
		Assert.Empty(state.Outbox);
	}

	private static void CLandlessUnitsRun(CGameState state) => OpenHOI3.Sim.Agents.CLandlessUnits.RunHourly(state);
}
