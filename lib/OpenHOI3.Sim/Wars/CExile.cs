using System.Collections.Generic;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// A government in exile, the original's <c>SetGovernmentInExile</c> both ways and an exile's day. See
/// <c>docs/mechanics/war.md</c>, section 14.
/// </summary>
public static class CExile
{
	/// <summary>The share of manpower and officers an exile keeps, in thousandths: a tenth.</summary>
	private const long Kept = 100;

	/// <summary>The share of its land the holder of an exile's capital must hold to make it a puppet, in thousandths.</summary>
	private const long SwapShare = 500;

	/// <summary>
	/// The country goes into exile, in the original's order - nothing for one in exile already: its own money is
	/// nought and it is an exile, its stockpile its own pool from now on; manpower and officers cut to a tenth; its
	/// building queue deleted; every unit standing on ground a country not its friend holds taken out of the game -
	/// at sea, none - but one lent to it, which goes home and is shattered there, its two sides told
	/// (<c>expeditions.md</c>, section 5); where it led its faction, the member of greatest IC, not in exile, put at
	/// the front; where another leads its faction, its government sits in the leader's seat; and where it then does
	/// not own its seat, every trade route it has ends. Nothing else is told.
	/// </summary>
	public static void Enter(CGameState state, CCountry country)
	{
		if (country.InExile)
		{
			return;
		}
		country.PoolInExile[EGoods.Money] = CFixedPoint.Zero;
		country.InExile = true;
		country.Manpower = new CFixedPoint(country.Manpower.Raw * Kept / 1000);
		country.Officers = new CFixedPoint(country.Officers.Raw * Kept / 1000);
		country.Constructions.Clear();
		var lent = new List<CUnit>();
		foreach (CUnit unit in country.UnitsInOrder())
		{
			if (state.Province(unit.Location).Controller is { } holder && !IsFriend(state, holder, country))
			{
				if (unit.Lender is null)
				{
					CCombatRetreat.RemoveFromGame(state, unit);
				}
				else
				{
					lent.Add(unit);
				}
			}
		}
		// What was lent to it and stands there goes home, both sides told, and is shattered at its lender's.
		foreach (CUnit unit in lent)
		{
			CExpeditions.Return(state, unit);
			if (state.Country(unit.Owner) is { } lender && lender != country)
			{
				CCombatRetreat.Shatter(state, lender, unit);
			}
		}
		if (COfferRules.RealFaction(country))
		{
			List<CCountry> members = CCountryLists.FactionMembers(state, country.Faction);
			if (members.Count >= 2 && members[0] == country)
			{
				CCountry? best = null;
				long most = 0;
				foreach (CCountry member in members)
				{
					if (member != country && !member.InExile && member.MaxIc * 1000L > most)
					{
						best = member;
						most = member.MaxIc * 1000L;
					}
				}
				if (best is not null)
				{
					CFactionMembership.MoveToFront(state, best);
				}
			}
			if (state.Country(COfferRules.Leader(state, country.Faction)) is { } leader && leader != country)
			{
				country.ActingCapital = leader.ActingCapital;
			}
		}
		if (state.Province(country.ActingCapital).Owner != country.Tag)
		{
			foreach (CCountry other in state.Countries)
			{
				Trade.CTradeRoutes.EndPair(state, country.Tag, other.Tag);
			}
		}
	}

	/// <summary>
	/// The country's exile ends - nothing for one not in exile: its government goes back to its capital. Nothing that
	/// was cut comes back.
	/// </summary>
	public static void Leave(CGameState state, CCountry country)
	{
		if (!country.InExile)
		{
			return;
		}
		country.ActingCapital = country.Capital;
		country.InExile = false;
	}

	/// <summary>
	/// <c>form_government_in_exile</c>'s handing over: each province the exile holds on the continent of the host's
	/// capital, or whose area borders ground the host holds, goes to the host where the host is at war with its owner
	/// or either is the rebels, and otherwise back to its owner.
	/// </summary>
	public static void HandLand(CGameState state, CCountry exile, CCountry host)
	{
		Supply.CSupplyNetwork network = Supply.CSupplyNetworks.Refresh(state);
		string continent = state.Province(host.Capital).Continent;
		var handed = new List<CProvince>();
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller == exile.Tag && ((continent.Length > 0 && province.Continent == continent)
				|| (network.AreaOf(province.Id) is { } area && area.Neighbours.Exists(next => next.Controller == host.Tag))))
			{
				handed.Add(province);
			}
		}
		foreach (CProvince province in handed)
		{
			if (province.Owner is not { } owner)
			{
				continue;
			}
			bool toHost = CCountryLists.AtWar(state, host.Tag, owner) || host.Tag.Value == "REB" || owner.Value == "REB";
			CProvinceHands.SetController(state, province, toHost ? host.Tag : owner);
		}
	}

	/// <summary>
	/// The original's <c>CCountry::IsFriend</c> as the exile asks it: the holder is the country itself, its overlord or
	/// its puppet, a puppet of the same overlord, of its real faction, its ally, fighting beside it and not against it,
	/// or one giving it military access.
	/// </summary>
	public static bool IsFriend(CGameState state, CCountryTag holder, CCountry country)
	{
		if (holder == country.Tag || holder == country.Overlord || state.Country(holder) is not { } other)
		{
			return true;
		}
		return other.Overlord == country.Tag
			|| (other.Overlord is { } master && master == country.Overlord)
			|| COfferRules.OneRealFaction(other, country)
			|| CCountryLists.HasAlliance(state, holder, country.Tag)
			|| (CCountryLists.FightingTogether(state, holder, country.Tag) && !CCountryLists.AtWar(state, holder, country.Tag))
			|| CGameState.Status(other, country).HasMilitaryAccess;
	}

	/// <summary>
	/// Early in an exile's day: where a country not its enemy and of another faction - with no truce between them -
	/// holds its capital and half of what it owns, it is made that country's puppet and comes home, everybody told.
	/// </summary>
	public static void SwapDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			if (country.InExile)
			{
				SwapForPuppet(state, country);
			}
		}
	}

	/// <summary>
	/// After an exile's production and before its shares: its supplies and fuel go to whoever holds its seat, into
	/// that country's ledger and the seat's pool.
	/// </summary>
	public static void HandOverDaily(CGameState state)
	{
		// The day's ledger made anew (ours: where the original's daily reset of its pools falls against this was not
		// compared).
		foreach (CCountry country in state.Countries)
		{
			country.ReceivedFromExiles.Clear();
		}
		foreach (CCountry country in state.Countries)
		{
			if (country.InExile)
			{
				HandOverSupplies(state, country);
			}
		}
	}

	/// <summary>Late in an exile's day, after the surrender: one leading its faction leaves it, and its exile ends.</summary>
	public static void ReturnDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			if (country.InExile && COfferRules.RealFaction(country) && COfferRules.Leader(state, country.Faction) == country.Tag)
			{
				CFactionMembership.Leave(state, country);
				Leave(state, country);
			}
		}
	}

	private static void HandOverSupplies(CGameState state, CCountry exile)
	{
		CProvince seat = state.Province(exile.ActingCapital);
		if (state.Country(seat.Controller) is not { } host)
		{
			return;
		}
		foreach (EGoods goods in new[] { EGoods.Supplies, EGoods.Fuel })
		{
			host.ReceivedFromExiles[goods] += exile.PoolInExile[goods];
			seat.Pool[goods] += exile.PoolInExile[goods];
			exile.PoolInExile[goods] = CFixedPoint.Zero;
		}
	}

	/// <summary>The original's <c>CCountry_ReplaceExileWithPuppet</c>.</summary>
	private static void SwapForPuppet(CGameState state, CCountry exile)
	{
		if (state.Country(state.Province(exile.Capital).Controller) is not { } holder || holder == exile
			|| CCountryLists.AtWar(state, holder.Tag, exile.Tag) || holder.Tag.Value == "REB" || holder.Faction == exile.Faction
			|| CTruces.Has(state, exile, holder))
		{
			return;
		}
		int owned = 0;
		int held = 0;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner == exile.Tag)
			{
				owned++;
				if (province.Controller == holder.Tag)
				{
					held++;
				}
			}
		}
		long share = owned == 0 ? 0 : 1000L * held / owned;
		if (share < SwapShare)
		{
			return;
		}
		CPuppets.Make(state, holder, exile);
		Leave(state, exile);
		state.RaisePactMessage(new CPactMessage("GIESWAP", "GIESWAPFORUS", "GIESWAPOTHER", holder.Tag, exile.Tag)
		{
			Faction = holder.Faction,
			Values = new[] { ("BOSS", holder.Name), ("VICTIM", exile.Name), ("CAPITAL", state.Province(exile.Capital).Name),
				("GOVERNMENT", exile.GovernmentType) },
		});
	}
}
