using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim;

/// <summary>
/// One value for each kind of modifier, nearly all of them zero - so only the ones
/// that are not are held.
///
/// A law is a modifier, and so is a minister's type, a building's worth and a country's
/// or a province's running total. Totals are never adjusted as things happen: they are
/// cleared and summed again from whatever currently applies. See
/// <c>docs/mechanics/modifiers.md</c>.
/// </summary>
public sealed class CModifier
{
	private readonly Dictionary<string, CFixedPoint> _values = new(StringComparer.OrdinalIgnoreCase);

	public CModifier(string name = "")
	{
		Name = name;
	}

	/// <summary>What it is called in the files, when it came from one.</summary>
	public string Name { get; }

	/// <summary>The kinds this holds a value for, and the values.</summary>
	public IReadOnlyDictionary<string, CFixedPoint> Values => _values;

	/// <summary>The value for a kind of modifier, which is zero for any kind not held.</summary>
	public CFixedPoint this[string kind] =>
		_values.TryGetValue(kind, out CFixedPoint value) ? value : CFixedPoint.Zero;

	public void Set(string kind, CFixedPoint value)
	{
		_values[kind] = value;
	}

	public void Add(string kind, CFixedPoint value)
	{
		_values[kind] = this[kind] + value;
	}

	/// <summary>Adds every value of another modifier to this one.</summary>
	public void Add(CModifier other)
	{
		foreach ((string kind, CFixedPoint value) in other._values)
		{
			Add(kind, value);
		}
	}

	public void Clear()
	{
		_values.Clear();
	}

	public override string ToString()
	{
		return Name.Length == 0 ? $"{_values.Count} values" : Name;
	}
}

/// <summary>One law, which is a modifier that belongs to a group a country picks one from.</summary>
public sealed class CLaw
{
	public CLaw(string group, CModifier modifier)
	{
		Group = group;
		Modifier = modifier;
	}

	public string Group { get; }

	public string Name => Modifier.Name;

	public CModifier Modifier { get; }

	public override string ToString()
	{
		return $"{Group} = {Name}";
	}
}

/// <summary>
/// A person who can sit in a government. What he is worth depends on the seat: he is
/// declared with a minister type for each position he could hold.
/// </summary>
public sealed class CMinister
{
	private readonly Dictionary<string, CModifier> _typeByPosition = new(StringComparer.OrdinalIgnoreCase);

	public CMinister(int id, string name)
	{
		Id = id;
		Name = name;
	}

	/// <summary>The number the country's files know him by. Unique within the country only.</summary>
	public int Id { get; }

	public string Name { get; }

	public void SetType(string position, CModifier type)
	{
		_typeByPosition[position] = type;
	}

	/// <summary>The minister type he is in a position, or null if he was not declared for it.</summary>
	public CModifier? TypeIn(string position)
	{
		return _typeByPosition.TryGetValue(position, out CModifier? type) ? type : null;
	}

	public override string ToString()
	{
		return $"{Id} ({Name})";
	}
}
