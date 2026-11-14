namespace OpenHOI3.Sim.Countries;

/// <summary>
/// The yearly sample of <c>nation_size_statistics</c>: as a year begins, for the null country and then every country in
/// the countries' order, the provinces it owns and the year, added to its line - a line made at the end where it has
/// none. Nothing reads it but the save, as in the original, whose graphs are never drawn. See
/// <c>docs/mechanics/statistics.md</c>, section 5.
/// </summary>
public static class CNationSize
{
	/// <summary>The name the null country's line is under.</summary>
	public const string NullCountry = "---";

	public static void SampleYearly(CGameState state)
	{
		int year = state.Date.Year;
		Add(state, NullCountry, 0, year);
		foreach (CCountry country in state.Countries)
		{
			Add(state, country.Tag.Value, CCountryLists.OwnedCount(state, country.Tag), year);
		}
	}

	private static void Add(CGameState state, string name, long value, int year)
	{
		CStatisticsSeries? series = state.NationSizeStatistics.Find(each => each.Name == name);
		if (series is null)
		{
			series = new CStatisticsSeries(name);
			state.NationSizeStatistics.Add(series);
		}
		series.Years.Add(year);
		series.Values.Add(value);
	}
}
