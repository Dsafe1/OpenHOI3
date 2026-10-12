namespace OpenHOI3.Lua.Api;

/// <summary>
/// A government or a faction handed to a script, one object for each name: the original compares two
/// with <c>==</c> by their save tokens, which is the same as asking whether they are one object.
/// The empty name stands for its null government or null faction, which every country with none
/// shares. See <c>docs/mechanics/lua.md</c>, section 4.
/// </summary>
public sealed class CLuaNamed
{
	public CLuaNamed(string name)
	{
		Name = name;
	}

	public string Name { get; }
}
