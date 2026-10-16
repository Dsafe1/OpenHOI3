using System.Collections.Generic;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// One of the original's arrays as a script is handed it - <c>CArrayInt</c>, <c>CArrayFix</c>, <c>CArrayCountryTag</c>:
/// values indexed from nought, which a script may read and set.
/// </summary>
public sealed class CLuaArray
{
	public CLuaArray(IEnumerable<object?> values)
	{
		Values = new List<object?>(values);
	}

	public List<object?> Values { get; }
}
