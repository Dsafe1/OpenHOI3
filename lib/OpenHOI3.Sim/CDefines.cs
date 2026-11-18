using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim;

/// <summary>
/// The numbers a game is tuned with, as <c>common/defines.lua</c> gives them: a table of
/// sections, each a table of named values. A value is asked for by section and name, and
/// one the file does not have reads as zero. Every value is also kept as it is written,
/// for the few that name something - the top level's <c>land_combat</c> names a category,
/// <c>base_militia</c> a brigade type - with an empty section for the top level.
/// </summary>
public sealed class CDefines
{
	private readonly Dictionary<(string Section, string Name), CFixedPoint> _values = new();
	private readonly Dictionary<(string Section, string Name), string> _texts = new();

	public int Count => _values.Count;

	/// <summary>A value as it is written, quotes removed; empty where the file does not have it.</summary>
	public string Text(string section, string name)
	{
		return _texts.TryGetValue((section, name), out string? text) ? text : "";
	}

	public void SetText(string section, string name, string text)
	{
		_texts[(section, name)] = text;
	}

	public CFixedPoint this[string section, string name] =>
		_values.TryGetValue((section, name), out CFixedPoint value) ? value : CFixedPoint.Zero;

	public bool Has(string section, string name)
	{
		return _values.ContainsKey((section, name));
	}

	public void Set(string section, string name, CFixedPoint value)
	{
		_values[(section, name)] = value;
	}
}
