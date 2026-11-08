using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Plans;

namespace OpenHOI3.Interface.BattlePlans;

/// <summary>Every country's battle plans, in the order they were made. See <c>docs/mechanics/battleplans.md</c>, section 6.</summary>
public sealed class CBattlePlanBook
{
	private readonly Dictionary<string, List<CBattlePlan>> _plans = new();

	public List<CBattlePlan> Of(string country)
	{
		if (!_plans.TryGetValue(country, out List<CBattlePlan>? plans))
		{
			_plans[country] = plans = new List<CBattlePlan>();
		}
		return plans;
	}

	public void Add(CBattlePlan plan) => Of(plan.Country).Add(plan);

	public void Remove(CBattlePlan plan) => Of(plan.Country).Remove(plan);

	/// <summary>The Current tab: the player's own, then every other country's shared plan where the player is among its allies.</summary>
	public List<CBattlePlan> Shown(CGameState state, CCountry player)
	{
		var shown = new List<CBattlePlan>(Of(player.Tag.Value));
		foreach (CCountry country in state.Countries)
		{
			if (country.Tag == player.Tag || !_plans.TryGetValue(country.Tag.Value, out List<CBattlePlan>? plans))
			{
				continue;
			}
			if (CCountryLists.Allies(state, country).Contains(player))
			{
				shown.AddRange(plans.Where(plan => plan.IsShared));
			}
		}
		return shown;
	}

	/// <summary>Every plan, country by country in the game's order, as a saved game holds them.</summary>
	public IEnumerable<CBattlePlan> All(CGameState state)
	{
		return state.Countries.SelectMany(country => _plans.TryGetValue(country.Tag.Value, out List<CBattlePlan>? plans) ? plans : new List<CBattlePlan>());
	}
}
