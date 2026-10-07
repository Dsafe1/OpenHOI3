using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim;

/// <summary>
/// The numbers a game is tuned with, as <c>common/defines.lua</c> gives them: a table of
/// sections, each a table of named values. A value is asked for by section and name, and
/// one the file does not have reads as zero.
/// </summary>
public sealed class CDefines
{
	private readonly Dictionary<(string Section, string Name), CFixedPoint> _values = new();

	public int Count => _values.Count;

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
