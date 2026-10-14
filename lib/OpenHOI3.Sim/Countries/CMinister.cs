using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim.Countries;

/// <summary>
/// A person who can sit in a government. What he is worth depends on the seat: he is
/// declared with a minister type for each position he could hold.
/// </summary>
public sealed class CMinister
{
	private readonly Dictionary<string, CMinisterType> _typeByPosition = new(StringComparer.OrdinalIgnoreCase);

	public CMinister(int id, string name)
	{
		Id = id;
		Name = name;
	}

	/// <summary>The number the country's files know him by. Unique within the country only.</summary>
	public int Id { get; }

	public string Name { get; }

	/// <summary>His party, by the ideology's name.</summary>
	public string Ideology { get; set; } = "";

	/// <summary>His portrait, a picture's name under <c>gfx/pictures/portraits</c>.</summary>
	public string Picture { get; set; } = "";

	public CFixedPoint Loyalty { get; set; }

	/// <summary>When he may first be appointed; <see cref="CDate.Zero"/> where his block gives none.</summary>
	public CDate StartDate { get; set; } = CDate.Zero;

	/// <summary>His death date, which nothing acts on but <c>minister_alive</c>; <see cref="CDate.Zero"/> for none.</summary>
	public CDate DeathDate { get; set; } = CDate.Zero;

	/// <summary>Whether his block names the post: whether he may take it (<c>docs/mechanics/politics.md</c>, section 7).</summary>
	public bool MayTake(string position)
	{
		return _typeByPosition.ContainsKey(position);
	}

	public void SetType(string position, CMinisterType type)
	{
		_typeByPosition[position] = type;
	}

	/// <summary>The minister type he is in a position, or null if he was not declared for it.</summary>
	public CMinisterType? TypeIn(string position)
	{
		return _typeByPosition.TryGetValue(position, out CMinisterType? type) ? type : null;
	}

	public override string ToString()
	{
		return $"{Id} ({Name})";
	}
}
