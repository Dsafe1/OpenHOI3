using System.Collections.Generic;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CCountry</c>: what a country is, holds and thinks of others, as the original's methods answer
/// it. See <c>docs/mechanics/lua.md</c>, sections 4 to 4.2.
/// </summary>
public static class CCountryClass
{
	private const string Name = "CCountry";

	/// <summary>Over this much base IC a country is a major whatever its file says.</summary>
	private const int MajorIc = 100;


	public static CLuaClass Define(CGameApi api)
	{
		CLuaClass luaClass = new CLuaClass(Name)
			.Method("GetCountryTag", call => call.Return(api.Tag(Of(call).Tag)))
			.Method("GetRelation", call =>
			{
				return call.Return(api.Status(Of(call), call, 2) is { } status ? new CLuaObject(api.DiplomacyStatus, status) : null);
			})
			.Method("IsAtWar", call => call.Return(api.State.IsAtWar(Of(call).Tag)))
			.Method("Exists", call => call.Return(CCountryFacts.Of(api.State).OwnedCount(Of(call).Tag) > 0))
			.Method("GetGovernment", call => call.Return(api.GovernmentNamed(Of(call).GovernmentType)))
			.Method("GetFaction", call => call.Return(api.FactionNamed(Of(call).Faction)))
			.Method("HasFaction", call => call.Return(Of(call).Faction.Length > 0))
			.Method("GetRulingIdeology", call =>
			{
				// A country with no ruling party has the null one, never nil (politics.md, section 12).
				string ideology = Of(call).RulingIdeology;
				return call.Return(api.IdeologyNamed(ideology.Length > 0 ? ideology : CIdeologyClass.NullIdeology));
			})
			.Method("GetActingCapitalLocation", call => call.Return(new CLuaObject(api.Province, Capital(api, Of(call)))))
			.Method("GetPool", call => call.Return(new CLuaObject(api.GoodsPool, api.State.Stockpile(Of(call)) ?? api.State.Province(0).Pool)))
			.Method("GetDailyBalance", call =>
				call.Return(api.Fixed(new CFixedPoint(CCountryGoods.Balance(api, Of(call), CGoodsPoolClass.Goods(call, 2))))))
			.Method("GetDailyExpense", call =>
				call.Return(api.Fixed(new CFixedPoint(CCountryGoods.Expense(api, Of(call), CGoodsPoolClass.Goods(call, 2))))))
			.Method("AICalculateExpense", call =>
				call.Return(CCountryGoods.AiExpense(api, Of(call), CGoodsPoolClass.Goods(call, 2))))
			.Method("GetDiplomaticDistance", call =>
			{
				CCountry country = Of(call);
				CCountry? other = api.CountryOf(call, 2);
				CProvince there = other is null ? api.State.Province(0) : Capital(api, other);
				long distance = CMapDistance.Between(Capital(api, country), there, api.State.MapWidth);
				return call.Return(api.Fixed(CFixedPoint.FromInt(distance)));
			})
			.Method("GetNeutrality", call => call.Return(api.Fixed(Of(call).Neutrality)))
			.Method("GetEffectiveNeutrality", call => call.Return(api.Fixed(Of(call).EffectiveNeutrality)))
			.Method("GetSurrenderLevel", call => call.Return(api.Fixed(SurrenderLevel(api, Of(call)))))
			.Method("GetMaxIC", call => call.Return(Of(call).BaseIc))
			.Method("GetTotalIC", call => call.Return(Of(call).TotalIc))
			.Method("GetNumberOfOwnedProvinces", call => call.Return(CCountryFacts.Of(api.State).OwnedCount(Of(call).Tag)))
			.Method("GetNumberOfControlledProvinces", call => call.Return(ControlledCount(api, Of(call))))
			.Method("GetOverlord", call => call.Return(api.Tag(Of(call).Overlord)))
			.Method("IsPuppet", call => call.Return(Of(call).Overlord is not null))
			.Method("GetStrategy", call => call.Return(new CLuaObject(api.Strategy, Of(call))))
			.Method("GetFlags", call => call.Return(new CLuaObject(api.Flags, Of(call).Flags)))
			// What the foreign minister's script asks (foreignminister.md, section 3).
			.Method("GetAllies", call => call.ReturnIterator(Tags(api, CCountryLists.Allies(api.State, Of(call)))))
			.Method("GetVassals", call => call.ReturnIterator(Tags(api, Vassals(api, Of(call)))))
			.Method("GetControllerNeighbours", call => call.ReturnIterator(Tags(api, CCountryLists.ControllerNeighbours(api.State, Of(call)))))
			.Method("IsNeighbour", call => call.Return(CCountryFacts.Of(api.State).AreNeighbours(Of(call).Tag,
				call.Object<CCountryTag>(2, "CCountryTag"))))
			.Method("HasNeighborInFaction", call =>
			{
				CCountry country = Of(call);
				string faction = call.Object<CLuaNamed>(2, "CFaction").Name;
				if (faction.Length == 0 || CCountryLists.FactionMembers(api.State, faction).Count == 0)
				{
					return call.Return(false);
				}
				return call.Return(CCountryLists.Neighbours(api.State, country).Exists(other => other.Faction == faction));
			})
			.Method("HasActiveLendLeaseFrom", call => call.Return(api.CountryOf(call, 2) is { } giver
				&& Sim.Aid.CLendLease.Gives(giver, Of(call))))
			.Method("IsGivingLendLeaseToTarget", call => call.Return(api.CountryOf(call, 2) is { } receiver
				&& Sim.Aid.CLendLease.Gives(Of(call), receiver)))
			// The ceiling for war (diplomacy.md), which the politics script asks of a war prepared (foreignminister.md, section 4).
			.Method("GetMaxNeutralityForWarWith", call =>
			{
				CCountry country = Of(call);
				return call.Return(api.Fixed(api.CountryOf(call, 2) is { } other
					? Sim.Diplomacy.CNeutrality.WarCeiling(api.State, country, other)
					: CFixedPoint.Zero));
			})
			// Trade (docs/mechanics/trade.md, section 9).
			.Method("AIGetTradeRoutes", call => call.ReturnIterator(TradeRoutes(api, Of(call))))
			.Method("GetTradedAwaySansAlliedSupply", call => call.Return(new CLuaObject(api.GoodsPool, Of(call).TradedAwaySansAlliedSupply)))
			.Method("GetTradedForSansAlliedSupply", call => call.Return(new CLuaObject(api.GoodsPool, Of(call).TradedForSansAlliedSupply)))
			.Method("HasDiplomatEnroute", call =>
			{
				CCountry country = Of(call);
				return call.Return(api.CountryOf(call, 2) is { } other
					&& Sim.Agreements.COfferRules.DiplomatEnRoute(api.State, country, other));
			})
			.Method("NeedConvoyToTradeWith", call =>
			{
				CCountry country = Of(call);
				return call.Return(api.CountryOf(call, 2) is { } other
					&& Sim.Trade.CTradeRules.NeedsConvoy(api.State, Sim.Supply.CSupplyNetworks.Refresh(api.State), country, other));
			})
			.Method("GetTransports", call => call.Return(Of(call).Transports))
			.Method("GetGlobalModifier", call => call.Return(new CLuaObject(api.Modifier, Of(call))))
			.Method("GetTechnologyStatus", call => call.Return(new CLuaObject(api.TechnologyStatus, Of(call))))
			.Method("GetCurrentAtWarWith", call => call.ReturnIterator(Tags(api, CCountryLists.Enemies(api.State, Of(call)))))
			.Method("GetNeighbours", call => call.ReturnIterator(Tags(api, CCountryLists.Neighbours(api.State, Of(call)))))
			.Method("GetDiplomacy", call =>
			{
				var statuses = new List<object?>();
				foreach (Sim.Diplomacy.CDiplomacyStatus status in Of(call).Diplomacy)
				{
					statuses.Add(new CLuaObject(api.DiplomacyStatus, status));
				}
				return call.ReturnIterator(statuses);
			})
			.Method("IsEnemy", call => call.Return(IsEnemy(api, Of(call), call.Object<CCountryTag>(2, "CCountryTag"))))
			.Method("IsFriend", call => call.Return(IsFriend(api, Of(call), call.Object<CCountryTag>(2, "CCountryTag"), call.Boolean(3))))
			.Method("IsGovernmentInExile", call => call.Return(Of(call).InExile))
			.Method("IsLandSafeToJoin", call =>
			{
				Of(call);
				string faction = call.Object<CLuaNamed>(2, "CFaction").Name;
				if (faction.Length == 0)
				{
					return call.Return(false);
				}
				// The original looks for a reason it is not safe and answers false whether it finds one or
				// not: only a leader at peace makes it true.
				CCountryTag leader = CFactionClass.Leader(api, faction);
				return call.Return(!api.State.IsAtWar(leader));
			})
			.Method("IsMajor", call =>
			{
				CCountry country = Of(call);
				return call.Return(country.IsMajor || country.BaseIc > MajorIc);
			})
			.Method("IsNonExileNeighbour", call =>
			{
				CCountry country = Of(call);
				CCountryTag tag = call.Object<CCountryTag>(2, "CCountryTag");
				// A neighbour by what each holds, and not in exile.
				return call.Return(api.State.Country(tag) is { } other && CCountryFacts.Of(api.State).AreControllerNeighbours(country.Tag, tag)
					&& !other.InExile);
			})
			// Espionage (docs/mechanics/espionage.md, section 15).
			.Method("GetCapitalLocation", call => call.Return(new CLuaObject(api.Province, api.State.Province(Of(call).Capital))))
			.Method("GetSpyPresence", call =>
			{
				CCountry country = Of(call);
				return call.Return(api.CountryOf(call, 2) is { } other
					? new CLuaObject(api.SpyPresence, CGameState.Presence(country, other))
					: null);
			})
			.Method("GetSpyingOnUs", call => call.ReturnIterator(Tags(api, SpyingOn(api, Of(call)))))
			.Method("CalculateIsAllied", call =>
			{
				CCountry country = Of(call);
				return call.Return(api.State.Country(call.Object<CCountryTag>(2, "CCountryTag")) is { } other
					&& IsAllied(api, country, other));
			})
			.Method("GetNationalUnity", call => call.Return(api.Fixed(Of(call).NationalUnity)))
			.Method("AccessIdeologyPopularity", call => call.Return(new CLuaObject(api.IdeologyData, Of(call).Popularity)))
			// Politics (docs/mechanics/politics.md, section 12).
			.Method("GetLaw", call => call.Return(new CLuaObject(api.Law,
				CLawClass.LawOf(Of(call), call.Object<CLawGroup>(2, "CLawGroup")))))
			.Method("GetMinister", call => call.Return(new CLuaObject(api.Minister,
				CMinisterClass.HolderOf(Of(call), CMinisterClass.Position(call, 2)))))
			.Method("GetPossibleMinisters", call =>
			{
				var ministers = new List<object?>();
				foreach (CMinister minister in Of(call).PossibleMinisters)
				{
					ministers.Add(new CLuaObject(api.Minister, minister));
				}
				return call.ReturnIterator(ministers);
			})
			.Method("IsMobilized", call => call.Return(Of(call).Mobilised))
			.Method("GetManpower", call => call.Return(api.Fixed(Of(call).Manpower)))
			.Method("MayLiberateCountries", call => call.Return(CCountryLists.PossibleLiberations(api.State, Of(call)).Count != 0))
			.Method("GetPossibleLiberations", call => call.ReturnIterator(Tags(api, CCountryLists.PossibleLiberations(api.State, Of(call)))))
			.Method("CanCreatePuppet", call => call.Return(CCountryLists.PossibleVassals(api.State, Of(call)).Count != 0))
			// The original registers the list of possible vassals under this name, which ITA.lua's HandlePuppets calls.
			.Method("GetPossiblePuppets", call => call.ReturnIterator(Tags(api, CCountryLists.PossibleVassals(api.State, Of(call)))));
		// What the tech and production scripts reach (docs/mechanics/ministers.md, section 7), and what BlackICE's reach besides.
		return CCountryHoldingsMethods.Add(CCountryEconomyMethods.Add(luaClass, api), api);
	}

	private static CCountry Of(CLuaCall call) => call.Object<CCountry>(1, Name);

	/// <summary>
	/// A country's trade routes, running or not: the other countries in their order, and each pair's
	/// routes in the order they were made.
	/// </summary>
	private static List<object?> TradeRoutes(CGameApi api, CCountry country)
	{
		var routes = new List<object?>();
		foreach (CCountry other in api.State.Countries)
		{
			foreach (Sim.Trade.CTradeRoute route in api.State.TradeRoutes)
			{
				if (other != country && route.Joins(country.Tag, other.Tag))
				{
					routes.Add(new CLuaObject(api.TradeRoute, route));
				}
			}
		}
		return routes;
	}

	/// <summary>The country's capital, which is its acting capital where nobody is in exile.</summary>
	/// <summary>Where its government sits, the acting capital (FINDINGS-capitalreaders.md).</summary>
	private static CProvince Capital(CGameApi api, CCountry country) => api.State.Province(country.ActingCapital);

	private static int ControlledCount(CGameApi api, CCountry country)
	{
		int count = 0;
		foreach (CProvince province in api.State.Provinces)
		{
			if (province.Controller == country.Tag)
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>The countries this one is overlord of, in the countries' order.</summary>
	private static List<CCountry> Vassals(CGameApi api, CCountry country)
	{
		var vassals = new List<CCountry>();
		foreach (CCountry other in api.State.Countries)
		{
			if (other.Overlord == country.Tag)
			{
				vassals.Add(other);
			}
		}
		return vassals;
	}

	private static List<object?> Tags(CGameApi api, List<CCountry> countries)
	{
		var tags = new List<object?>(countries.Count);
		foreach (CCountry country in countries)
		{
			tags.Add(api.Tag(country.Tag));
		}
		return tags;
	}

	/// <summary>The surrender progress, 0 to 1000 thousandths, as the simulation has it (<c>war.md</c>, section 12).</summary>
	private static CFixedPoint SurrenderLevel(CGameApi api, CCountry country)
	{
		return new CFixedPoint(Sim.Wars.CSurrender.Progress(api.State, country));
	}

	/// <summary>
	/// Every other country, not REB, owning a province, in order, whose spies in this one are above nought - a fraction
	/// of a spy counting. Ours: worked out when asked, where the original rebuilds the list once a day.
	/// </summary>
	private static List<CCountry> SpyingOn(CGameApi api, CCountry country)
	{
		var spying = new List<CCountry>();
		CCountryFacts facts = CCountryFacts.Of(api.State);
		foreach (CCountry other in api.State.Countries)
		{
			if (other != country && other.Tag.Value != "REB" && facts.OwnedCount(other.Tag) > 0
				&& CGameState.Presence(other, country).Level > 0)
			{
				spying.Add(other);
			}
		}
		return spying;
	}

	/// <summary>Allied: an alliance between the two, or both members of one real faction.</summary>
	public static bool IsAllied(CGameApi api, CCountry country, CCountry other)
	{
		return other != country && (CCountryLists.HasAlliance(api.State, country.Tag, other.Tag)
			|| (country.Faction.Length > 0 && country.Faction == other.Faction));
	}

	/// <summary>The original's <c>IsEnemy</c>: <see cref="CCountryLists.IsEnemy"/>.</summary>
	public static bool IsEnemy(CGameApi api, CCountry country, CCountryTag other) => CCountryLists.IsEnemy(api.State, country, other);

	/// <summary>The original's <c>IsFriend</c>: <see cref="CCountryLists.IsFriend"/>.</summary>
	public static bool IsFriend(CGameApi api, CCountry country, CCountryTag tag, bool withAccess) =>
		CCountryLists.IsFriend(api.State, country, tag, withAccess);
}
