using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.StrategicWarfare;

/// <summary>
/// Where the played country's records are written from: convoy raids, bombings, air and sea battles, each hour of a land battle, brigades lost aboard a transport and units lost whole. Each
/// keeps what the original does, the odd parts with it. See <c>docs/mechanics/strategicwarfare.md</c>, section 3.
/// </summary>
public static class CWarfareTally
{
	private const string Economy = "economy";
	private const string CargoScale = "CARGO_TONS_SUNK_SCALE";
	private const string ResourceToIc = "RESOURCE_TO_IC_COST";
	private const long BrigadeShare = 10;
	private const long BombingStrengthShare = 10;

	private static CCountryTag? Player(CGameState state) => state.Warfare.Player;

	private static void Add(CWarfareRecord? record, int category, int column, long value)
	{
		if (record is not null)
		{
			record.Categories[category].Add(column, new CFixed15(value));
		}
	}

	private static bool Regiments(CSubUnitType type) => type.Branch == EUnitBranch.Land;

	private static bool Ships(CSubUnitType type) => type.Branch == EUnitBranch.Naval;

	/// <summary>A convoy raid that sank something: on the raider's record where the player is the victim, on the victim's where the player is the raider.</summary>
	public static void ConvoyRaid(CGameState state, CConvoy convoy, CCombat? combat, CUnit? raider, long transportsLost)
	{
		if (Player(state) is not { } player || raider is null || state.Country(convoy.Owner) is not { } victim)
		{
			return;
		}
		bool asVictim = victim.Tag == player;
		if (!asVictim && raider.Owner != player)
		{
			return;
		}
		CWarfareRecord? record = state.Warfare.Of(asVictim ? raider.Owner : convoy.Owner);
		int first = asVictim ? 0 : 3;
		long escort = CFixedPoint.Scale * CConvoyLines.Time(state, victim, escorts: true).Raw / CFixedPoint.Scale;
		long size = state.Defines[Economy, CConvoyLines.SizeDefine].Raw;
		long each = size == 0 ? 0 : (CWarfareCosts.Fix(escort * CConvoyLines.Cost(state, victim, escorts: true).Raw / CFixedPoint.Scale) << CFixed15.Places) / CWarfareCosts.Fix(size);
		long icLost = each * transportsLost;
		long share = convoy.Transports > 0 ? (transportsLost << CFixed15.Places) / convoy.Transports : 0;
		if (convoy.LendLeaseFrom is { } giver && convoy.Transports > 0)
		{
			long receipt = victim.LendLeaseReceived.FirstOrDefault(entry => entry.Giver == giver)?.Amount ?? 0;
			icLost += (share * CWarfareCosts.Fix(receipt)) >> CFixed15.Places;
		}
		long cargo = Enum.GetValues<Economy.EGoods>().Sum(goods => convoy.Daily[goods].Raw);
		long tons = (CWarfareCosts.Fix(state.Defines[Economy, CargoScale].Raw * cargo / CFixedPoint.Scale) * share) >> CFixed15.Places;
		Add(record, CWarfareRecord.Naval, first, icLost);
		Add(record, CWarfareRecord.Naval, first + 1, tons);
		if (combat is not null)
		{
			Add(record, CWarfareRecord.Naval, first + 2, CWarfareCosts.SideLoss(state, combat.Attacker, raider.Owner, Ships));
		}
	}

	/// <summary>A land battle's hour, after both sides have fired and before it is settled. What the player's side lost goes whole onto every enemy's record: it counts once for each.</summary>
	public static void LandHour(CGameState state, CCombat combat)
	{
		if (Player(state) is not { } player)
		{
			return;
		}
		CCombatSide? ours = combat.Attacker.Countries.Contains(player) ? combat.Attacker : combat.Defender.Countries.Contains(player) ? combat.Defender : null;
		if (ours is null)
		{
			return;
		}
		CCombatSide theirs = combat.Other(ours);
		(long oursIc, long oursMen) = SideHour(state, ours, player);
		foreach (CCountryTag enemy in theirs.Countries)
		{
			if (state.Warfare.Of(enemy) is not { } record)
			{
				continue;
			}
			(long theirsIc, long theirsMen) = SideHour(state, theirs, enemy);
			Add(record, CWarfareRecord.Land, 0, oursIc);
			Add(record, CWarfareRecord.Land, 1, theirsIc);
			Add(record, CWarfareRecord.Land, 2, oursMen);
			Add(record, CWarfareRecord.Land, 3, theirsMen);
		}
	}

	private static (long Ic, long Men) SideHour(CGameState state, CCombatSide side, CCountryTag country)
	{
		long ic = 0;
		long men = 0;
		foreach (CUnit unit in side.Units.Where(unit => unit.Owner == country))
		{
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				long amount = Math.Min(brigade.PendingStrengthDamage * BrigadeShare / CFixedPoint.Scale, brigade.Strength.Raw);
				(long partIc, long partMen) = CWarfareCosts.LandDamage(state, brigade, amount);
				ic += partIc;
				men += partMen;
			}
		}
		return (ic, men);
	}

	/// <summary>A brigade aboard a transport took its share of what the transport took: from the other side's every country's record where it is the player's, otherwise on its owner's for the player.</summary>
	public static void Passenger(CGameState state, CCombatSide fleetSide, CSubUnit brigade, long amount)
	{
		if (Player(state) is not { } player || brigade.Unit is not { } unit)
		{
			return;
		}
		(long ic, long men) = CWarfareCosts.LandDamage(state, brigade, amount);
		foreach (CCountryTag country in fleetSide.Combat.Other(fleetSide).Countries)
		{
			if (unit.Owner == player)
			{
				CWarfareRecord? record = state.Warfare.Of(country);
				Add(record, CWarfareRecord.Land, 0, ic);
				Add(record, CWarfareRecord.Land, 2, men);
			}
			else if (country == player && CMovementRules.IsEnemy(state, player, unit.Owner))
			{
				CWarfareRecord? record = state.Warfare.Of(unit.Owner);
				Add(record, CWarfareRecord.Land, 1, ic);
				Add(record, CWarfareRecord.Land, 3, men);
			}
		}
	}

	/// <summary>
	/// A day's attrition off a brigade, as the original tallies it: the player's own brigade, where the province it stands in
	/// is held by another than its owner and that owner is the player's enemy, on the owner's record; else a brigade in a
	/// province the player owns, its owner his enemy, on that owner's record - with no look at who holds the province on this
	/// arm. Nothing otherwise (<c>strategicwarfare.md</c>, section 3).
	/// </summary>
	public static void Attrition(CGameState state, Provinces.CProvince province, CSubUnit brigade, long amount)
	{
		if (Player(state) is not { } player || brigade.Unit is not { } unit || province.Owner is not { } owner)
		{
			return;
		}
		if (unit.Owner == player)
		{
			if (province.Controller != owner && CMovementRules.IsEnemy(state, player, owner))
			{
				(long ic, long men) = CWarfareCosts.LandDamage(state, brigade, amount);
				CWarfareRecord? record = state.Warfare.Of(owner);
				Add(record, CWarfareRecord.Land, 0, ic);
				Add(record, CWarfareRecord.Land, 2, men);
			}
		}
		else if (owner == player && CMovementRules.IsEnemy(state, unit.Owner, player))
		{
			(long ic, long men) = CWarfareCosts.LandDamage(state, brigade, amount);
			CWarfareRecord? record = state.Warfare.Of(unit.Owner);
			Add(record, CWarfareRecord.Land, 1, ic);
			Add(record, CWarfareRecord.Land, 3, men);
		}
	}

	/// <summary>A land unit lost whole, every brigade at its full price - on top of what its battle has already counted hour by hour, as in the original.</summary>
	public static void UnitLost(CGameState state, CCountryTag? killer, CUnit unit)
	{
		if (Player(state) is not { } player || killer is not { } by || !CMovementRules.IsLandUnit(unit) || state.Country(unit.Owner) is not { } owner)
		{
			return;
		}
		bool ours = unit.Owner == player;
		if (!ours && (by != player || !CMovementRules.IsEnemy(state, player, unit.Owner)))
		{
			return;
		}
		CWarfareRecord? record = state.Warfare.Of(ours ? by : unit.Owner);
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			(long ic, long men) = CWarfareCosts.Total(owner, brigade.Type);
			Add(record, CWarfareRecord.Land, ours ? 0 : 1, ic);
			Add(record, CWarfareRecord.Land, ours ? 2 : 3, men);
		}
	}

	/// <summary>Whatever kind of battle has just ended.</summary>
	public static void BattleEnded(CGameState state, CCombat combat)
	{
		if (Player(state) is null || combat.IsConvoyBattle)
		{
			return;
		}
		if (CBombing.IsBombing(combat.Kind))
		{
			BombingEnded(state, combat);
		}
		else if (combat.Kind == ECombatKind.Air)
		{
			AirEnded(state, combat);
		}
		else if (combat.Kind == ECombatKind.Naval)
		{
			NavalEnded(state, combat);
		}
	}

	/// <summary>
	/// A bombing over: on the first country bombed that has a record, where the player bombed; and otherwise on the first of the bombers' countries with one, whoever was bombed.
	/// </summary>
	private static void BombingEnded(CGameState state, CCombat combat)
	{
		CCountryTag player = Player(state)!.Value;
		CCombatSide bombers = combat.Attacker;
		CCombatSide bombed = combat.Defender;
		if (bombers.Countries.Count == 0)
		{
			return;
		}
		bool ours = bombers.Countries.Contains(player);
		CCountryTag? enemy = (ours ? bombed.Countries : bombers.Countries).Cast<CCountryTag?>().FirstOrDefault(tag => state.Warfare.Of(tag!.Value) is not null);
		if (enemy is not { } against || state.Warfare.Of(against) is not { } record)
		{
			return;
		}
		int first = ours ? 4 : 0;
		long dealt = bombers.StrengthDealt.Raw * BombingStrengthShare / CFixedPoint.Scale;
		long buildings = 0;
		long levels = 0;
		foreach (Provinces.CBuilding building in state.Buildings)
		{
			long destroyed = bombers.BuildingsDestroyed.GetValueOrDefault(building.Key).Raw;
			levels += destroyed;
			buildings += CWarfareCosts.Fix(building.Cost.Raw * destroyed / CFixedPoint.Scale);
		}
		long goods = Enum.GetValues<Economy.EGoods>().Sum(each => bombers.StockpileDestroyed[each].Raw + bombers.ProductionDestroyed[each].Raw);
		long icLost = buildings + CWarfareCosts.Fix(state.Defines[Economy, ResourceToIc].Raw * goods / CFixedPoint.Scale);
		if (dealt > 0 && bombed.Countries.Count > 0)
		{
			icLost += CWarfareCosts.SideLoss(state, bombed, bombed.Countries[0], Regiments);
		}
		long strengthLost = bombers.Destroyed.Where(entry => state.SubUnitTypes.TryGetValue(entry.Key, out CSubUnitType? type) && CWarfareCosts.IsBomber(type)).Sum(entry => entry.Value);
		Add(record, CWarfareRecord.Air, first, CWarfareCosts.SideLoss(state, bombers, bombers.Countries[0], CWarfareCosts.IsBomber));
		Add(record, CWarfareRecord.Air, first + 1, icLost);
		Add(record, CWarfareRecord.Air, first + 2, CWarfareCosts.Fix(strengthLost));
		Add(record, CWarfareRecord.Air, first + 3, CWarfareCosts.Fix(levels + goods + dealt));
	}

	/// <summary>An air battle over: the player's bombers lost, to every country on the other side's record - in the first column when the player attacked, which the tooltip calls the enemy's bombers.</summary>
	private static void AirEnded(CGameState state, CCombat combat)
	{
		CCountryTag player = Player(state)!.Value;
		foreach ((CCombatSide side, int column) in new[] { (combat.Attacker, 0), (combat.Defender, 4) })
		{
			if (!side.Countries.Contains(player))
			{
				continue;
			}
			long value = CWarfareCosts.SideLoss(state, side, player, CWarfareCosts.IsBomber);
			if (value <= 0)
			{
				continue;
			}
			foreach (CCountryTag enemy in combat.Other(side).Countries)
			{
				Add(state.Warfare.Of(enemy), CWarfareRecord.Air, column, value);
			}
		}
	}

	/// <summary>A fleet battle over, where one of its fleets was raiding convoys: the damage to the raiders goes in the naval columns.</summary>
	private static void NavalEnded(CGameState state, CCombat combat)
	{
		CCountryTag player = Player(state)!.Value;
		CCombatSide? mine = combat.Attacker.Countries.Contains(player) ? combat.Attacker : combat.Defender.Countries.Contains(player) ? combat.Defender : null;
		if (mine is null)
		{
			return;
		}
		CCombatSide theirs = combat.Other(mine);
		if (Raider(mine) is { } ours)
		{
			long value = CWarfareCosts.SideLoss(state, mine, ours.Owner, Ships);
			foreach (CCountryTag enemy in theirs.Countries)
			{
				Add(state.Warfare.Of(enemy), CWarfareRecord.Naval, 5, value);
			}
		}
		if (Raider(theirs) is { } other)
		{
			Add(state.Warfare.Of(other.Owner), CWarfareRecord.Naval, 2, CWarfareCosts.SideLoss(state, theirs, other.Owner, Ships));
		}
	}

	private static CUnit? Raider(CCombatSide side) => side.Units.FirstOrDefault(unit => unit.Order == EUnitOrder.ConvoyRaid);
}
