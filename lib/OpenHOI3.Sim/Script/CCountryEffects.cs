using System;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The effects on the scope's country: its figures, its goods, its capital, what it knows, its
/// leaders, an alliance and a war: <c>docs/mechanics/effects.md</c>, section 4. Where the scope
/// has no country, the original writes its placeholder and nothing here changes.
/// </summary>
public static class CCountryEffects
{
	/// <summary>The modifier that scales a change of national unity.</summary>
	public const string NationalUnityEffect = "national_unity_effect";

	public static bool TryRun(CGameState state, CEffect effect, CEventScope scope)
	{
		CCountry? country = state.Country(scope.Country);
		CFixedPoint value = Value(effect);
		switch (effect.Keyword)
		{
			case "dissent":
				if (country is not null)
				{
					country.Dissent = Floored(country.Dissent + value);
				}
				return true;
			case "national_unity":
				if (country is not null && value != CFixedPoint.Zero)
				{
					long scale = country.Modifier[NationalUnityEffect].Raw + CFixedPoint.Scale;
					long unity = country.NationalUnity.Raw + value.Raw * scale / CFixedPoint.Scale;
					country.NationalUnity = new CFixedPoint(Math.Clamp(unity, CFixedPoint.Scale, 100 * CFixedPoint.Scale));
				}
				return true;
			case "manpower":
				if (country is not null)
				{
					country.Manpower = Floored(country.Manpower + value);
				}
				return true;
			case "officer_pool":
				if (country is not null && value != CFixedPoint.Zero)
				{
					country.Officers = Floored(country.Officers + value);
				}
				return true;
			case "capital":
				if (country is not null)
				{
					// Both capitals at once, and no test of whose the province is.
					int id = (int)CConditions.WholeOf(value);
					// Both capitals, as the original's effect sets them.
					country.Capital = id >= state.Provinces.Count ? 0 : id;
					country.ActingCapital = country.Capital;
				}
				return true;
			case "money":
			case "supplies":
			case "fuel":
			case "crude_oil":
			case "metal":
			case "energy":
			case "rare_materials":
				AddGoods(state, effect.Keyword, value, scope, country);
				return true;
			case "practical":
				if (country is not null && CEffects.Field(effect, "technology_group") is { } group)
				{
					// The partner a country shares technology with is not built, so its own level is the base.
					CFixedPoint gain = CEffects.Field(effect, "value")?.Amount ?? CFixedPoint.Zero;
					CResearch.SetAbility(state, country, group.Text, country.Ability(group.Text).Raw + gain.Raw);
				}
				return true;
			case "kill_leader":
				if (country is not null)
				{
					KillLeader(state, country, (int)CConditions.WholeOf(value));
				}
				return true;
			case "leave_alliance":
				if (scope.Country is { } us && CEffects.Target(state, effect, scope) is { } ally)
				{
					state.Alliances.RemoveAll(alliance => alliance.Joins(us, ally));
				}
				return true;
			// The pacts an effect makes cost nothing and move no relation or threat, and say nothing.
			case "guarantee":
				if (scope.Country is { } guarantor && CEffects.Target(state, effect, scope) is { } guaranteed
					&& !state.Guarantees.Exists(g => g.Guarantor == guarantor && g.Guaranteed == guaranteed))
				{
					// One the other way is left with nothing pointing at it in the original: it is gone.
					state.Guarantees.RemoveAll(g => g.Joins(guarantor, guaranteed));
					state.Guarantees.Add(new CGuarantee(guarantor, guaranteed, state.TotalHours));
				}
				return true;
			case "end_guarantee":
				if (scope.Country is { } giver && CEffects.Target(state, effect, scope) is { } given)
				{
					state.Guarantees.RemoveAll(g => g.Guarantor == giver && g.Guaranteed == given);
				}
				return true;
			case "non_aggression_pact":
				if (scope.Country is { } first && CEffects.Target(state, effect, scope) is { } second
					&& COfferRules.Nap(state, first, second) is null)
				{
					state.Naps.Add(new CNap(first, second, state.TotalHours));
				}
				return true;
			case "end_non_aggression_pact":
				if (scope.Country is { } one && CEffects.Target(state, effect, scope) is { } other)
				{
					state.Naps.RemoveAll(nap => nap.Joins(one, other));
				}
				return true;
			case "military_access":
			case "end_military_access":
				if (country is not null && state.Country(CEffects.Target(state, effect, scope)) is { } through)
				{
					CGameState.Status(country, through).HasMilitaryAccess = effect.Keyword == "military_access";
				}
				return true;
			case "end_war":
				if (scope.Country is { } side && CEffects.Target(state, effect, scope) is { } leaving)
				{
					EndWar(state, side, leaving);
				}
				return true;
			default:
				return false;
		}
	}

	/// <summary>The effect's own value: what <c>this</c> and <c>from</c> leave as nought.</summary>
	private static CFixedPoint Value(CEffect effect)
	{
		CScriptArgument argument = effect.Argument;
		return argument.IsThis || argument.IsFrom ? CFixedPoint.Zero : argument.Amount;
	}

	private static CFixedPoint Floored(CFixedPoint value)
	{
		return value < CFixedPoint.Zero ? CFixedPoint.Zero : value;
	}

	/// <summary>
	/// Goods to a province's scope - money, supplies and fuel into its pool, the four resources into
	/// what it can yield - or else to the country's stockpile, the pool of its capital.
	/// </summary>
	private static void AddGoods(CGameState state, string key, CFixedPoint value, CEventScope scope, CCountry? country)
	{
		CGoodsPool.TryParseKey(key, out EGoods goods);
		if (scope.Province != 0)
		{
			var here = state.Province(scope.Province);
			bool yields = goods is EGoods.CrudeOil or EGoods.Metal or EGoods.Energy or EGoods.RareMaterials;
			CGoodsPool pool = yields ? here.MaxProducing : here.Pool;
			pool[goods] += value;
			return;
		}
		CGoodsPool stockpile = country is null ? state.Province(0).Pool : state.Stockpile(country) ?? state.Province(0).Pool;
		stockpile[goods] += value;
	}

	/// <summary>Takes one of the country's leaders out of service and off his unit; he stays among its leaders.</summary>
	private static void KillLeader(CGameState state, CCountry country, int id)
	{
		CLeader? leader = country.ActiveLeaders.Find(active => active.Id == id);
		if (leader is null)
		{
			return;
		}
		if (leader.Unit is { } unit)
		{
			CLeaders.SetLeader(state, unit, null, resetTraitHours: false, organisationHit: false);
		}
		country.ActiveLeaders.Remove(leader);
	}

	/// <summary>The country leaving, and every country it is overlord of, leave the war it and the side are in.</summary>
	private static void EndWar(CGameState state, CCountryTag side, CCountryTag leaving)
	{
		CWar? war = state.Wars.Find(w => (w.Attackers.Contains(side) && w.Defenders.Contains(leaving))
			|| (w.Defenders.Contains(side) && w.Attackers.Contains(leaving)));
		if (war is null)
		{
			return;
		}
		CWarSides.Leave(state, war, leaving);
		foreach (CCountry vassal in state.Countries)
		{
			if (vassal.Overlord == leaving)
			{
				CWarSides.Leave(state, war, vassal.Tag);
			}
		}
	}
}
