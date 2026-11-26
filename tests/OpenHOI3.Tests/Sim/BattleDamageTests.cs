using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What an enhanced game keeps of a unit's damage in its land battle, against <c>docs/mechanics/combatview.md</c>,
/// section 12: what is really taken off a unit is its taken and is shared among those who fired at it.
/// </summary>
public class BattleDamageTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private static CGameState State(bool enhanced = true)
	{
		var state = new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), new CProvince(1, "p1", isSea: false) { Owner = Pol, Controller = Pol } },
			new[]
			{
				new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" },
				new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" },
			});
		if (enhanced)
		{
			state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(System.Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
		}
		return state;
	}

	private static CUnit Unit(CGameState state, CCountryTag owner, int id, int brigades)
	{
		var unit = new CUnit(EUnitKind.Division, owner) { Id = id, Location = 1, Name = $"unit {id}" };
		for (int index = 0; index < brigades; index++)
		{
			var type = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
			type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(10));
			var brigade = new CSubUnit(type)
			{
				Unit = unit, Id = id * 10 + index, Strength = CFixedPoint.FromInt(10), Organisation = CFixedPoint.FromInt(30),
			};
			brigade.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(brigade);
		}
		state.Country(owner)!.Units.Add(unit);
		return unit;
	}

	[Fact]
	public void WhatIsTakenOffAUnitIsSharedAmongThoseWhoFiredByWhatEachAsked()
	{
		CGameState state = State();
		var combat = new CCombat(1);
		CUnit first = Unit(state, Ger, 1, 1), second = Unit(state, Ger, 2, 1), target = Unit(state, Pol, 3, 2);

		// The first asked three quarters of the strength and a quarter of the organisation.
		CBattleDamages.Note(state, combat, first, target, 3000, 1000);
		CBattleDamages.Note(state, combat, second, target, 1000, 3000);
		// Settled: 0.040 strength and 0.080 organisation over its two brigades really went.
		CBattleDamages.Settled(state, target, 40, 80);

		CBattleDamage taken = target.BattleDamage!;
		Assert.Equal(40, taken.StrengthTaken);
		Assert.Equal(80, taken.OrganisationTaken);
		Assert.Empty(taken.Asked);
		// Dealt is a thousand times finer; the organisation as the unit's, the sum over its two brigades.
		Assert.Equal(30 * CBattleDamage.Fine, first.BattleDamage!.StrengthDealt);
		Assert.Equal(10 * CBattleDamage.Fine, second.BattleDamage!.StrengthDealt);
		Assert.Equal(10 * CBattleDamage.Fine, first.BattleDamage!.OrganisationDealt);
		Assert.Equal(30 * CBattleDamage.Fine, second.BattleDamage!.OrganisationDealt);
		Assert.Equal(first.BattleDamage!.StrengthDealt, first.BattleDamage!.HourStrengthDealt);
	}

	[Fact]
	public void TheHoursFiguresStartAfreshAtALaterHourAndTheBattlesGoOn()
	{
		var record = new CBattleDamage();
		record.AddTaken(5, 10, 20);
		record.AddTaken(5, 1, 2);
		Assert.Equal(11, record.HourStrengthTaken);
		record.AddDealt(6, 1000, 0);
		Assert.Equal(6, record.Hour);
		Assert.Equal(0, record.HourStrengthTaken);
		Assert.Equal(1000, record.HourStrengthDealt);
		Assert.Equal(11, record.StrengthTaken);
		Assert.Equal(22, record.OrganisationTaken);
	}

	[Fact]
	public void AShotOfAnotherBattleStartsAUnitsFiguresAfreshAndLeavingABattleDropsThem()
	{
		CGameState state = State();
		var combat = new CCombat(1);
		var later = new CCombat(1);
		CUnit attacker = Unit(state, Ger, 1, 1), target = Unit(state, Pol, 3, 1);
		CBattleDamages.Note(state, combat, attacker, target, 1000, 1000);
		CBattleDamages.Settled(state, target, 10, 10);
		Assert.Equal(10, target.BattleDamage!.StrengthTaken);

		CBattleDamages.Note(state, later, attacker, target, 1000, 1000);
		Assert.Same(later, target.BattleDamage!.Combat);
		Assert.Equal(0, target.BattleDamage!.StrengthTaken);
		Assert.Equal(0, attacker.BattleDamage!.StrengthDealt);

		CBattleDamages.Forget(target, combat);
		Assert.NotNull(target.BattleDamage);
		CBattleDamages.Forget(target, later);
		Assert.Null(target.BattleDamage);
	}

	[Fact]
	public void AFaithfulGameKeepsNothingAndAUnitNoShotReachedIsNotCounted()
	{
		CGameState faithful = State(enhanced: false);
		var combat = new CCombat(1);
		CUnit attacker = Unit(faithful, Ger, 1, 1), target = Unit(faithful, Pol, 3, 1);
		CBattleDamages.Note(faithful, combat, attacker, target, 1000, 1000);
		CBattleDamages.Settled(faithful, target, 10, 10);
		Assert.Null(attacker.BattleDamage);
		Assert.Null(target.BattleDamage);
	}

	[Fact]
	public void TheHoursSettlingCountsWhatItReallyTakesOffAUnit()
	{
		CGameState state = State();
		var combat = new CCombat(1);
		CUnit attacker = Unit(state, Ger, 1, 1), target = Unit(state, Pol, 3, 2);
		CBattleDamages.Note(state, combat, attacker, target, 4000, 6000);
		CCombatDamage.Take(state, target, 4000, 6000);

		CCombatDamage.SettleUnit(state, target);

		// A hundredth of what the hits came to: 0.040 strength over its brigades; the organisation is spread evenly, so
		// each of the two loses 0.030 - which is what the unit, their average, loses.
		Assert.Equal(40, target.BattleDamage!.StrengthTaken);
		Assert.Equal(60, target.BattleDamage!.OrganisationTaken);
		Assert.Equal(40 * CBattleDamage.Fine, attacker.BattleDamage!.StrengthDealt);
		Assert.Equal(30 * CBattleDamage.Fine, attacker.BattleDamage!.OrganisationDealt);
	}

	[Fact]
	public void AnEnhancedSaveHoldsTheDamageOfUnitsInALandBattleAndAFaithfulOneNone()
	{
		CGameState state = State();
		var combat = new CCombat(1);
		CUnit attacker = Unit(state, Ger, 1, 1), target = Unit(state, Pol, 3, 2), idle = Unit(state, Pol, 4, 1);
		combat.Attacker.Units.Add(attacker);
		combat.Defender.Units.Add(target);
		attacker.Combats.Add(combat);
		target.Combats.Add(combat);
		state.Combats.Add(combat);
		CBattleDamages.Note(state, combat, attacker, target, 1000, 1000);
		CBattleDamages.Settled(state, target, 40, 80);

		var writer = new CSaveTextWriter();
		CSaveBattleDamage.Write(writer, state);
		Assert.Contains(CSaveBattleDamage.Key, writer.Text);

		var units = new Dictionary<int, CUnit> { [1] = attacker, [3] = target, [4] = idle };
		long dealt = attacker.BattleDamage!.StrengthDealt;
		attacker.BattleDamage = null;
		target.BattleDamage = null;
		CSaveBattleDamage.Read(ClausewitzParser.Parse(writer.Text).Get(CSaveBattleDamage.Key)!, units);
		Assert.Equal(dealt, attacker.BattleDamage!.StrengthDealt);
		Assert.Same(combat, attacker.BattleDamage!.Combat);
		Assert.Equal(40, target.BattleDamage!.StrengthTaken);
		Assert.Equal(80, target.BattleDamage!.HourOrganisationTaken);
		Assert.Equal(state.TotalHours, target.BattleDamage!.Hour);
		// A unit in no battle, or one that is gone, is passed over.
		CSaveBattleDamage.Read(ClausewitzParser.Parse("x = { 4 0 1 1 1 1 1 1 1 1 9 0 1 1 1 1 1 1 1 1 }").Get("x")!, units);
		Assert.Null(idle.BattleDamage);

		CGameState faithful = State(enhanced: false);
		faithful.Combats.Add(combat);
		var none = new CSaveTextWriter();
		CSaveBattleDamage.Write(none, faithful);
		Assert.Equal("", none.Text);
	}
}
