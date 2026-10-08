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

	private readonly List<CCountry> _countriesInOrder = new();

	/// <param name="provinces">Indexed by id, so entry 0 is the "no province" entry.</param>
	private readonly Dictionary<string, CModifier> _staticModifiers =
		new(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, CTechnology> _technologies =
		new(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, CModifier> _strategicResources =
		new(StringComparer.OrdinalIgnoreCase);

	public const int HoursPerDay = 24;

	/// <summary>
	/// The one clock: hours since midnight on 1 January of year 0. The date and the
	/// hour of the day are both read off it. See <c>docs/mechanics/tick.md</c>, section 1.
	/// </summary>
	private long _hours;

	public CGameState(CDate date, IReadOnlyList<CProvince> provinces, IEnumerable<CCountry> countries,
		IReadOnlyList<CBuilding>? buildings = null, IEnumerable<CTechnology>? technologies = null,
		IEnumerable<CModifier>? staticModifiers = null, CDefines? defines = null, IEnumerable<CWar>? wars = null,
		IEnumerable<CModifier>? strategicResources = null,
		IEnumerable<CTechnologyCategory>? technologyCategories = null)
	{
		TechnologyCategories = new List<CTechnologyCategory>(technologyCategories ?? Array.Empty<CTechnologyCategory>());
		Defines = defines ?? new CDefines();
		Wars = new List<CWar>(wars ?? Array.Empty<CWar>());
		foreach (CModifier resource in strategicResources ?? Array.Empty<CModifier>())
		{
			_strategicResources.TryAdd(resource.Name, resource);
		}
		foreach (CModifier modifier in staticModifiers ?? Array.Empty<CModifier>())
		{
			_staticModifiers.TryAdd(modifier.Name, modifier);
		}
		foreach (CTechnology technology in technologies ?? Array.Empty<CTechnology>())
		{
			_technologies.TryAdd(technology.Name, technology);
		}
		_hours = (long)date.DayNumber * HoursPerDay;
		Provinces = provinces;
		Buildings = buildings ?? Array.Empty<CBuilding>();
		foreach (CCountry country in countries)
		{
			if (_countries.TryAdd(country.Tag.Value, country))
			{
				_countriesInOrder.Add(country);
			}
		}
	}

	public long TotalHours => _hours;

	public CDate Date => CDate.FromDayNumber((int)(_hours / HoursPerDay));

	/// <summary>The hour of the day, 0 to 23.</summary>
	public int Hour => (int)(_hours % HoursPerDay);

	/// <summary>Moves the clock on by one hour. Only the hourly tick calls this.</summary>
	public void AdvanceOneHour()
	{
		_hours++;
	}

	public IReadOnlyList<CProvince> Provinces { get; }

	/// <summary>The kinds of building there are, in the order the file declares them.</summary>
	public IReadOnlyList<CBuilding> Buildings { get; }

	/// <summary>Every country, in the order they were declared. Some rules depend on that order.</summary>
	public IReadOnlyList<CCountry> Countries => _countriesInOrder;

	public IReadOnlyDictionary<string, CTechnology> Technologies => _technologies;

	/// <summary>The theories and practicals there are, in the order the file lists them.</summary>
	public IReadOnlyList<CTechnologyCategory> TechnologyCategories { get; }

	/// <summary>The numbers out of <c>common/defines.lua</c>.</summary>
	public CDefines Defines { get; }

	/// <summary>The wars being fought now.</summary>
	public List<CWar> Wars { get; }

	public bool IsAtWar(CCountryTag tag)
	{
		foreach (CWar war in Wars)
		{
			if (war.Involves(tag))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// A country's stockpile, which is the pool of its capital province and not something
	/// the country holds: two countries with one capital share it. Null for a country
	/// with no capital. See <c>docs/mechanics/stockpile.md</c>, section 1.
	/// </summary>
	public CGoodsPool? Stockpile(CCountry country)
	{
		CProvince capital = Province(country.Capital);
		return capital.Id == 0 ? null : capital.Pool;
	}

	/// <summary>
	/// The value a static modifier holds for one kind, or zero when the modifier or the
	/// kind is not there. The engine knows these modifiers by name.
	/// </summary>
	public CFixedPoint StaticModifier(string name, string kind)
	{
		return _staticModifiers.TryGetValue(name, out CModifier? modifier) ? modifier[kind] : CFixedPoint.Zero;
	}

	/// <summary>A static modifier by name, whole, or null when there is none of that name.</summary>
	public CModifier? FindStaticModifier(string name)
	{
		return _staticModifiers.TryGetValue(name, out CModifier? modifier) ? modifier : null;
	}

	/// <summary>What a strategic resource is worth to whoever has it, or null when none has that name.</summary>
	public CModifier? FindStrategicResource(string name)
	{
		return _strategicResources.TryGetValue(name, out CModifier? resource) ? resource : null;
	}

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
