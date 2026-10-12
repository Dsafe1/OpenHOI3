using System.Collections.Generic;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// One of a block keyword's own keys - <c>who</c> and <c>value</c> in a <c>relation</c>, a
/// war goal's <c>casus_belli</c> - with its value, or with the keys of its own block where it
/// takes one, as a war's <c>attacker_goal</c> does.
/// </summary>
public sealed class CScriptField
{
	public CScriptField(string key, CScriptArgument argument)
	{
		Key = key;
		Argument = argument;
	}

	/// <summary>The key, in lower case.</summary>
	public string Key { get; }

	public CScriptArgument Argument { get; }

	/// <summary>The keys of its block, where it is written with one.</summary>
	public List<CScriptField> Fields { get; } = new();
}
