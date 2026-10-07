using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim;

/// <summary>
/// One game: the date, every province and every country.
///
/// A game is an object, not the process. Nothing here is static, so the client can
/// build one, throw it away and build another - which is what "new game", "load" and
/// "exit to menu" each come down to.
/// </summary>
public sealed class CGameState
{
	private readonly Dictionary<string, CCountry> _countries =
		new(StringComparer.OrdinalIgnoreCase);

	/// <param name="provinces">Indexed by id, so entry 0 is the "no province" entry.</param>
	public CGameState(CDate date, IReadOnlyList<CProvince> provinces, IEnumerable<CCountry> countries)
	{
		Date = date;
		Provinces = provinces;
		foreach (CCountry country in countries)
		{
			_countries.TryAdd(country.Tag.Value, country);
		}
	}

	public CDate Date { get; }

	public IReadOnlyList<CProvince> Provinces { get; }

	public IReadOnlyCollection<CCountry> Countries => _countries.Values;

	/// <summary>The province with that id, or the "no province" entry when it is out of range.</summary>
	public CProvince Province(int id)
	{
		return id > 0 && id < Provinces.Count ? Provinces[id] : Provinces[0];
	}

	public CCountry? Country(CCountryTag? tag)
	{
		return tag is { } known && _countries.TryGetValue(known.Value, out CCountry? country)
			? country
			: null;
	}
}
