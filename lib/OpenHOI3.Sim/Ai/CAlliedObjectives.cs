using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// The provinces countries have asked their allies to take: kept by the asked country, a province and who asked, and
/// mirrored by the asker, a province and whom, each in the order asked. The province view's allied-objectives panel
/// writes them; the original's AI takes them as a nudge, and nothing reads them here. See
/// <c>docs/mechanics/provinceview.md</c>, section 6.
/// </summary>
public sealed class CAlliedObjectives
{
	private readonly Dictionary<CCountryTag, List<(int Province, CCountryTag Requester)>> _asked = new();
	private readonly Dictionary<CCountryTag, List<(int Province, CCountryTag Country)>> _asking = new();

	/// <summary>The country is asked to take the province, or the asking taken back.</summary>
	public void Set(CCountryTag country, CCountryTag requester, int province, bool active)
	{
		List<(int, CCountryTag)> asked = List(_asked, country);
		List<(int, CCountryTag)> asking = List(_asking, requester);
		asked.Remove((province, requester));
		asking.Remove((province, country));
		if (active)
		{
			asked.Add((province, requester));
			asking.Add((province, country));
		}
	}

	/// <summary>Whom the requester has asked to take the province, in the order asked.</summary>
	public List<CCountryTag> AskedBy(CCountryTag requester, int province)
	{
		var countries = new List<CCountryTag>();
		if (_asking.TryGetValue(requester, out List<(int Province, CCountryTag Country)>? asking))
		{
			foreach ((int at, CCountryTag country) in asking)
			{
				if (at == province)
				{
					countries.Add(country);
				}
			}
		}
		return countries;
	}

	/// <summary>The provinces the country has been asked to take, and by whom.</summary>
	public IReadOnlyList<(int Province, CCountryTag Requester)> AskedOf(CCountryTag country)
	{
		return _asked.TryGetValue(country, out List<(int Province, CCountryTag Requester)>? asked)
			? asked
			: System.Array.Empty<(int, CCountryTag)>();
	}

	/// <summary>Every objective the country asked or was asked, either way - what leaving a faction does.</summary>
	public void Clear(CCountryTag country)
	{
		if (_asked.Remove(country, out List<(int Province, CCountryTag Requester)>? asked))
		{
			foreach ((int province, CCountryTag requester) in asked)
			{
				List(_asking, requester).Remove((province, country));
			}
		}
		if (_asking.Remove(country, out List<(int Province, CCountryTag Country)>? asking))
		{
			foreach ((int province, CCountryTag other) in asking)
			{
				List(_asked, other).Remove((province, country));
			}
		}
	}

	private static List<(int, CCountryTag)> List(Dictionary<CCountryTag, List<(int, CCountryTag)>> lists, CCountryTag country)
	{
		if (!lists.TryGetValue(country, out List<(int, CCountryTag)>? list))
		{
			lists[country] = list = new List<(int, CCountryTag)>();
		}
		return list;
	}
}
