using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Countries;

/// <summary>
/// A country's war record and its <c>lost_IC</c>, counted afresh in the original's daily rebuild of who stands where -
/// at the head of the day, before any country's own, and as a game is entered. See
/// <c>docs/mechanics/triggers.md</c>, section 5.1.
/// </summary>
public static class CWarRecord
{
	/// <summary>Every country's, in turn.</summary>
	public static void CountDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			Count(state, country);
		}
	}

	/// <summary>
	/// One country's: each of its units at every level, passengers and carried air groups among them. An air unit under
	/// an order that bombs the ground, goes for ships or goes for land units adds one to <see cref="CCountry.TotalWeBomb"/>,
	/// whether or not it is bombing now; a fleet in a battle one to <see cref="CCountry.TotalSeaBattles"/>; and each
	/// battle a land unit is in, counted once for the country however many of its units fight it, one to
	/// <see cref="CCountry.TotalOffensives"/> where the battle's province is held by an enemy - at war with it, or either
	/// the rebels - and one to <see cref="CCountry.TotalDefensives"/> otherwise. Then <see cref="CountLostIc"/>.
	/// </summary>
	public static void Count(CGameState state, CCountry country)
	{
		int offensives = 0;
		int defensives = 0;
		int seaBattles = 0;
		int weBomb = 0;
		var counted = new HashSet<CCombat>();
		foreach (CUnit unit in country.AllUnits())
		{
			if (unit.Kind == EUnitKind.Air && Bombs(unit.Order))
			{
				weBomb++;
				continue;
			}
			if (unit.Kind == EUnitKind.Navy)
			{
				if (unit.Combats.Count > 0)
				{
					seaBattles++;
				}
				continue;
			}
			if (unit.Kind == EUnitKind.Air)
			{
				continue;
			}
			foreach (CCombat combat in unit.Combats)
			{
				if (!counted.Add(combat))
				{
					continue;
				}
				CCountryTag? holder = state.Province(combat.Province).Controller;
				if (holder is { } held && CCountryLists.IsEnemy(state, country, held))
				{
					offensives++;
				}
				else
				{
					defensives++;
				}
			}
		}
		country.TotalOffensives = offensives;
		country.TotalDefensives = defensives;
		country.TotalSeaBattles = seaBattles;
		country.TotalWeBomb = weBomb;
		CountLostIc(state, country);
	}

	/// <summary>
	/// The three questions the original's order answers for this count - whether it bombs the ground, goes for ships or
	/// goes for land units - asked together: the eight bombing missions and carrier protection, and not the raid on
	/// convoys from the air.
	/// </summary>
	public static bool Bombs(EUnitOrder order)
	{
		return CBombingOrders.BombsGround(order) || CBombingOrders.GoesForShips(order) || CBombingOrders.GoesForLandUnits(order);
	}

	/// <summary>
	/// <c>lost_IC</c>'s figure. Each province the country owns weighs its local IC modifier times one and the country's
	/// technology IC bonus, cut to thousandths; the weights of all of them are the whole, the weights of those it also holds
	/// the part. Where the whole is above nought the figure is the part over the whole in thousandths, cut; else the part
	/// itself. So it is the share <b>held</b>, not lost, and weighed by the local modifier and not by the IC - the base
	/// game's local modifiers being penalties, the whole is nought or less and the figure the penalties on the provinces it
	/// holds, never above nought (the original's; <c>docs/enhanced.md</c>, *Script*).
	/// </summary>
	public static void CountLostIc(CGameState state, CCountry country)
	{
		long factor = CFixedPoint.Scale + country.Technology[CIndustrialCapacity.TechnologyIc].Raw;
		long whole = 0;
		long held = 0;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner != country.Tag)
			{
				continue;
			}
			long weight = province.Modifier[CIndustrialCapacity.ProvinceLocalIc].Raw * factor / CFixedPoint.Scale;
			whole += weight;
			if (province.Controller == country.Tag)
			{
				held += weight;
			}
		}
		country.LostIc = new CFixedPoint(whole > 0 ? held * CFixedPoint.Scale / whole : held);
	}
}
