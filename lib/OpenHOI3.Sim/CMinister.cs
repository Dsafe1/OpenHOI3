using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim;

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
