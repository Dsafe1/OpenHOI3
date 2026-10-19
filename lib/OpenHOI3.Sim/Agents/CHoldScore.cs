using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// How much an agent wants one province held, in thousandths: its threatening neighbours, what it is worth with its
/// depot, revolt and ships, a landing threatened, and the strategy's wish to hold it. The original's
/// <c>CAIUnit_ScoreHoldProvince</c>. See <c>docs/mechanics/moving.md</c>, section 5.
/// </summary>
public static class CHoldScore
{
	private const long Whole = 1000;

	private const long LeastInfrastructure = 200;

	private const int Threatening = 100;

	/// <summary>A holder surrendered this far is no threat.</summary>
	private const long Beaten = 100;

	private const long LowCountries = 4000;

	public static long Of(CGameState state, CCountry country, CAgent agent, CProvince province,
		(bool AtPeace, bool Full) context)
	{
		if (CSupplyFigures.EffectiveInfrastructure(province) < LeastInfrastructure)
		{
			return 0;
		}
		bool depot = CSupplyNetworks.Refresh(state).DepotOf(province.Id) == province.Id;
		long score = 0;
		long sum = 0;
		int count = 0;
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable)
			{
				continue;
			}
			CProvince beside = state.Province(edge.To);
			if (beside.IsSea || beside.Controller is not { } holder)
			{
				continue;
			}
			if (province.Points <= 0 && !depot && CFrontWorthy.IsWarCandidate(state, country, holder))
			{
				return 0;
			}
			score += LowCountriesTerm(state, country, holder);
			if (CCountryLists.IsFriend(state, country, holder, true) || state.Country(holder) is not { } other)
			{
				continue;
			}
			int threat = other.Strategy.Threat.WeightOf(country.Tag);
			if (context.AtPeace && country.UnitAi.RunsUnits)
			{
				threat += country.Strategy.Antagonize.WeightOf(holder);
			}
			if (threat >= Threatening && CSurrender.Progress(state, other) < Beaten)
			{
				sum += threat;
				count++;
			}
		}
		if (count > 0)
		{
			score += sum * Whole / count / 20 + count * Whole;
		}
		score += CProvinceHoldValue.Of(state, country, agent, province, hold: true, context);
		if (agent.Threatened.Contains(province.Id))
		{
			score += 200 * Whole;
		}
		if (state.Country(province.Controller) is { } keeper)
		{
			score += keeper.Strategy.DefendProvinces.WeightOf(province.Id) * Whole;
		}
		return score;
	}

	/// <summary>France's fear of the Low Countries' neighbour, and Belgium's of Luxembourg's, while Germany stands apart.</summary>
	private static long LowCountriesTerm(CGameState state, CCountry country, CCountryTag holder)
	{
		bool french = country.Tag.Value == "FRA" && holder.Value is "BEL" or "LUX";
		bool belgian = country.Tag.Value == "BEL" && holder.Value == "LUX";
		if (!french && !belgian)
		{
			return 0;
		}
		if (state.Country(new CCountryTag("GER")) is not { } germany || CCountryLists.OwnedCount(state, germany.Tag) == 0
			|| state.Country(holder) is not { } other || germany.Faction == other.Faction
			|| CCountryLists.AtWar(state, germany.Tag, holder))
		{
			return 0;
		}
		return LowCountries * Whole;
	}
}
