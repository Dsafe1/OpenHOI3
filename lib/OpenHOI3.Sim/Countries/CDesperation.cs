using System;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Countries;

/// <summary>
/// How desperate a country at war is, 0 to 1100 thousandths: the weighted share of its owned cores in or beside its
/// acting capital's area that it does not hold, and a tenth more where it does not hold its own capital. Nought at
/// peace. The original's <c>CCountry::CalcDesperation</c>, which the production minister keeps and the scripts ask. See
/// <c>docs/mechanics/ministers.md</c>, section 4.1.
/// </summary>
public static class CDesperation
{
	private const string FortLevel = "fort_level";

	/// <summary>What the loss of the capital adds.</summary>
	private const long CapitalLost = 100;

	/// <summary>A province away from its owner's capital's area and continent counts a fifth.</summary>
	private const long Away = 5;

	public static long Of(CGameState state, CCountry country)
	{
		if (!state.IsAtWar(country.Tag))
		{
			return 0;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CSupplyArea? home = country.ActingCapital > 0 ? network.AreaOf(country.ActingCapital) : null;
		long total = 0;
		long held = 0;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner != country.Tag || !province.Cores.Contains(country.Tag)
				|| network.AreaOf(province.Id) is not { } area
				|| home is null || (area != home && !home.Neighbours.Contains(area)))
			{
				continue;
			}
			long weight = Weight(state, province);
			total += weight;
			if (province.Controller == country.Tag)
			{
				held += weight;
			}
		}
		total = Math.Max(total, 1);
		long desperation = CFixedPoint.Scale - held * CFixedPoint.Scale * CFixedPoint.Scale / (total * CFixedPoint.Scale);
		if (country.Capital > 0 && state.Province(country.Capital).Controller != country.Tag)
		{
			desperation += CapitalLost;
		}
		return desperation;
	}

	/// <summary>
	/// What a province counts for: its manpower in whole men and one, its forts added where it is in its owner's acting
	/// capital's area or on its continent and a fifth of it otherwise; at least one, and one for a province nobody owns.
	/// </summary>
	public static long Weight(CGameState state, CProvince province)
	{
		if (state.Country(province.Owner) is not { } owner)
		{
			return 1;
		}
		long weight = province.Manpower.Raw / CFixedPoint.Scale + 1;
		bool nearHome = owner.ActingCapital > 0
			&& (CSupplyNetworks.Refresh(state).AreaOf(owner.ActingCapital) is { } home && home.Provinces.Contains(province.Id)
				|| string.Equals(state.Province(owner.ActingCapital).Continent, province.Continent, StringComparison.Ordinal));
		if (nearHome)
		{
			weight += province.Modifier[FortLevel].Raw / CFixedPoint.Scale;
		}
		else
		{
			weight /= Away;
		}
		return Math.Max(weight, 1);
	}
}
