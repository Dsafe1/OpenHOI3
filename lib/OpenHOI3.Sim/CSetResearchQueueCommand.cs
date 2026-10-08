namespace OpenHOI3.Sim;

/// <summary>
/// A country setting what it researches, and in what order: the whole queue at once. It
/// is refused if any technology in it does not exist, is there twice, or may not be
/// researched by that country now.
/// </summary>
public sealed class CSetResearchQueueCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly string[] _technologies;

	public CSetResearchQueueCommand(CCountryTag country, System.Collections.Generic.IEnumerable<string> technologies)
	{
		_country = country;
		_technologies = System.Linq.Enumerable.ToArray(technologies);
	}

	public override bool MayExecute(CGameState state)
	{
		if (state.Country(_country) is not { } country)
		{
			return false;
		}
		var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
		foreach (string name in _technologies)
		{
			if (!seen.Add(name) || !state.Technologies.TryGetValue(name, out CTechnology? technology)
				|| !CResearch.MayResearchNext(state, country, technology))
			{
				return false;
			}
		}
		return true;
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_country)!;
		country.ResearchQueue.Clear();
		foreach (string name in _technologies)
		{
			country.ResearchQueue.Add(state.Technologies[name].Name);
		}
	}
}
