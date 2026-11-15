using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Events;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Revolts;

/// <summary>
/// Which rebel type a province chooses, and which country a rising there would free. See
/// <c>docs/mechanics/rebels.md</c>, section 4.
/// </summary>
public static class CRebelChoice
{
	private const long ScoreBase = 1000;
	private const long ScoreCore = 200000;
	private const long ScoreCapital = 300000;

	/// <summary>
	/// The original's <c>ChooseRebelTypeForProvince</c>: where the rebels hold the province, the type of the faction
	/// holding it; otherwise the type scoring strictly highest above nought on its <c>spawn_chance</c>, in a scope of the
	/// province and its owner - or of <paramref name="actor"/> for an underground's rising, the only one that may choose
	/// a triggered-only type. An independence type that would free nobody is passed over.
	/// </summary>
	public static CRebelType? TypeFor(CGameState state, CProvince province, bool allowTriggeredOnly = false,
		CCountryTag? actor = null)
	{
		if (CRebelFactions.IsRebels(province.Controller) && CRebelFactions.Holding(state, province.Id) is { } holding)
		{
			return holding.Type;
		}
		CEventScope scope = CEventScope.Start(allowTriggeredOnly ? actor : province.Owner, province.Id);
		long best = 0;
		CRebelType? chosen = null;
		foreach (CRebelType type in state.RebelTypes)
		{
			if (type.IsTriggeredOnly && !allowTriggeredOnly)
			{
				continue;
			}
			if (type.Independence && IndependenceOf(state, type, province) is null)
			{
				continue;
			}
			long days = CEventChance.Days(state, type.SpawnChance, scope);
			if (days > best)
			{
				best = days;
				chosen = type;
			}
		}
		return chosen;
	}

	/// <summary>
	/// The original's <c>ChooseRebelIndependenceTag</c>: none for a type that frees nobody or a province in no real
	/// area. Otherwise each country with a core on the province, then each country of the game whose original capital
	/// is the province or a neighbour of it - neither the rebels nor owning anything, each once - scores 1000, 200000
	/// more for a core on the province and 300000 more where the province is its original capital; the first strictly
	/// highest is freed.
	/// </summary>
	public static CCountryTag? IndependenceOf(CGameState state, CRebelType type, CProvince province)
	{
		if (!type.Independence || CRebelFactions.AreaOf(state, province.Id) is null)
		{
			return null;
		}
		var listed = new List<CCountry>();
		foreach (CCountryTag core in province.Cores)
		{
			if (state.Country(core) is { } country && Candidate(state, country, listed))
			{
				listed.Add(country);
			}
		}
		foreach (CCountry country in state.Countries)
		{
			int capital = OriginalCapital(country);
			if ((capital == province.Id || IsNeighbour(province, capital)) && Candidate(state, country, listed))
			{
				listed.Add(country);
			}
		}
		CCountryTag? freed = null;
		long best = 0;
		foreach (CCountry country in listed)
		{
			long score = ScoreBase;
			if (province.Cores.Contains(country.Tag))
			{
				score += ScoreCore;
			}
			if (OriginalCapital(country) == province.Id)
			{
				score += ScoreCapital;
			}
			if (score > best)
			{
				best = score;
				freed = country.Tag;
			}
		}
		return freed;
	}

	/// <summary>The original's <c>GetOriginalCapital</c>: the first capital declared in play, else the country's capital.</summary>
	public static int OriginalCapital(CCountry country) => country.OriginalCapitalOrDeclared;

	private static bool Candidate(CGameState state, CCountry country, List<CCountry> listed)
	{
		return !CRebelFactions.IsRebels(country.Tag) && CCountryLists.OwnedCount(state, country.Tag) <= 0
			&& !listed.Contains(country);
	}

	private static bool IsNeighbour(CProvince province, int other)
	{
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (edge.To == other)
			{
				return true;
			}
		}
		return false;
	}
}
