namespace OpenHOI3.Sim;

/// <summary>
/// Rebuilds every country's modifier from what currently applies to it: the base values
/// every country shares, its laws, and the ministers in its government, each of whom
/// counts as the minister type he has for the seat he holds. See
/// <c>docs/mechanics/modifiers.md</c>, sections 4 and 5.
///
/// The total is cleared and summed again every time. Nothing is ever added to it and
/// taken away later, so there is nothing to forget.
/// </summary>
public static class CCountryModifiers
{
	public static void Rebuild(CGameState state)
	{
		CModifier? baseValues = state.FindStaticModifier(CIndustrialCapacity.BaseValues);
		foreach (CCountry country in state.Countries)
		{
			Rebuild(country, baseValues);
			RebuildTechnology(state, country);
		}
	}

	/// <summary>Each technology's effect, times the level the country has of it.</summary>
	public static void RebuildTechnology(CGameState state, CCountry country)
	{
		CModifier total = country.Technology;
		total.Clear();
		foreach ((string name, int level) in country.TechnologyLevels)
		{
			if (level <= 0 || !state.Technologies.TryGetValue(name, out CTechnology? technology))
			{
				continue;
			}
			foreach ((string kind, CFixedPoint value) in technology.EffectPerLevel.Values)
			{
				total.Add(kind, value * level);
			}
		}
	}

	/// <param name="baseValues">
	/// The static modifier every country starts from. It is where a country's demand for
	/// consumer goods begins, before its laws take theirs off.
	/// </param>
	public static void Rebuild(CCountry country, CModifier? baseValues = null)
	{
		CModifier total = country.Modifier;
		total.Clear();
		if (baseValues is not null)
		{
			total.Add(baseValues);
		}

		foreach (CLaw law in country.Laws.Values)
		{
			total.Add(law.Modifier);
		}
		foreach ((string position, CMinister minister) in country.Government)
		{
			if (minister.TypeIn(position) is { } type)
			{
				total.Add(type);
			}
		}
	}
}
