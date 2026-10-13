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

	private const string ExileUnbuilt = "governments in exile: none is one";
	private const string VictoryPointsUnbuilt = "victory points: the share of claimed ones lost is 0";

	public static CLuaClass Define(CGameApi api)
	{
		return new CLuaClass(Name)
			.Method("GetCountryTag", call => call.Return(api.Tag(Of(call).Tag)))
			.Method("GetRelation", call =>
			{
				CCountry country = Of(call);
				return call.Return(api.CountryOf(call, 2) is { } other
					? new CLuaObject(api.DiplomacyStatus, CGameState.Status(country, other))
					: null);
			})
			.Method("IsAtWar", call => call.Return(api.State.IsAtWar(Of(call).Tag)))
			.Method("Exists", call => call.Return(CCountryFacts.Of(api.State).OwnedCount(Of(call).Tag) > 0))
			.Method("GetGovernment", call => call.Return(api.GovernmentNamed(Of(call).GovernmentType)))
			.Method("GetFaction", call => call.Return(api.FactionNamed(Of(call).Faction)))
			.Method("HasFaction", call => call.Return(Of(call).Faction.Length > 0))
			.Method("GetRulingIdeology", call =>
			{
				string ideology = Of(call).RulingIdeology;
				return call.Return(ideology.Length > 0 ? api.IdeologyNamed(ideology) : null);
			})
			.Method("GetActingCapitalLocation", call => call.Return(new CLuaObject(api.Province, Capital(api, Of(call)))))
			.Method("GetPool", call => call.Return(new CLuaObject(api.GoodsPool, Capital(api, Of(call)).Pool)))
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
			.Method("IsGovernmentInExile", call =>
			{
				Of(call);
				api.ReportUnbuilt(ExileUnbuilt);
				return call.Return(false);
			})
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
				if (api.State.Country(tag) is not null && CCountryFacts.Of(api.State).AreControllerNeighbours(country.Tag, tag))
				{
					// Nobody is in exile here, which the original asks next.
					api.ReportUnbuilt(ExileUnbuilt);
					return call.Return(true);
				}
				return call.Return(false);
			});
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
	private static CProvince Capital(CGameApi api, CCountry country) => api.State.Province(country.Capital);

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

	private static List<object?> Tags(CGameApi api, List<CCountry> countries)
	{
		var tags = new List<object?>(countries.Count);
		foreach (CCountry country in countries)
		{
			tags.Add(api.Tag(country.Tag));
		}
		return tags;
	}

	/// <summary>
	/// 1000 for a country holding no province; else the share of its claimed victory points it has
	/// lost over its national unity, between 0 and 1000 - and that share is not built.
	/// </summary>
	private static CFixedPoint SurrenderLevel(CGameApi api, CCountry country)
	{
		if (ControlledCount(api, country) == 0)
		{
			return CFixedPoint.One;
		}
		long unity = country.NationalUnity.Raw * 1000 / 100000;
		if (unity == 0)
		{
			return CFixedPoint.One;
		}
		api.ReportUnbuilt(VictoryPointsUnbuilt);
		const long lost = 0;
		return new CFixedPoint(System.Math.Clamp(lost * 1000 / unity, 0, 1000));
	}

	/// <summary>Not itself, and at war with it or either is the rebels.</summary>
	public static bool IsEnemy(CGameApi api, CCountry country, CCountryTag other)
	{
		if (other == country.Tag)
		{
			return false;
		}
		return CCountryLists.AtWar(api.State, country.Tag, other) || other.Value == "REB" || country.Tag.Value == "REB";
	}

	/// <summary>The original's <c>IsFriend</c>, in its order: with <paramref name="withAccess"/>, access through the other counts.</summary>
	public static bool IsFriend(CGameApi api, CCountry country, CCountryTag tag, bool withAccess)
	{
		if (tag == country.Tag)
		{
			return true;
		}
		CCountry? other = api.State.Country(tag);
		if (other?.Overlord == country.Tag || country.Overlord == tag)
		{
			return true;
		}
		if (country.Overlord is { } overlord && other?.Overlord == overlord)
		{
			return true;
		}
		if (country.Faction.Length > 0 && other?.Faction == country.Faction)
		{
			return true;
		}
		if (CCountryLists.HasAlliance(api.State, tag, country.Tag))
		{
			return true;
		}
		if (CCountryLists.FightingTogether(api.State, tag, country.Tag) && !IsEnemy(api, country, tag))
		{
			return true;
		}
		return withAccess && other is not null && CGameState.Status(country, other).HasMilitaryAccess;
	}
}
