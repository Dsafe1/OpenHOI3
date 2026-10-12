using System.Collections.Generic;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// One condition out of a trigger block - a keyword with its value, a name the original looks
/// up, or a block of conditions under a scope. The block a container holds - an event's
/// <c>trigger</c>, a decision's <c>allow</c> - is an <c>and</c>. See
/// <c>docs/mechanics/script.md</c>, section 5. What each answers is not built yet.
/// </summary>
public sealed class CTrigger
{
	public CTrigger(ETriggerKind kind, string key, CScriptArgument argument)
	{
		Kind = kind;
		Key = key;
		Argument = argument;
	}

	/// <summary>An empty <c>and</c>, which holds.</summary>
	public static CTrigger All()
	{
		return new CTrigger(ETriggerKind.Keyword, "and", CScriptArgument.None);
	}

	public ETriggerKind Kind { get; }

	/// <summary>The key as written.</summary>
	public string Key { get; }

	/// <summary>The keyword, in lower case, where <see cref="Kind"/> is a keyword; otherwise empty.</summary>
	public string Keyword => Kind == ETriggerKind.Keyword ? Key.ToLowerInvariant() : "";

	/// <summary>The value, where the key takes one; for a scope, what it names.</summary>
	public CScriptArgument Argument { get; }

	/// <summary>The conditions of a block of them.</summary>
	public List<CTrigger> Children { get; } = new();

	/// <summary>A block keyword's own keys: <c>relation</c>'s <c>who</c> and <c>value</c>.</summary>
	public List<CScriptField> Fields { get; } = new();

	public override string ToString()
	{
		return Children.Count > 0 || Fields.Count > 0 ? $"{Key} = {{ {Children.Count + Fields.Count} }}" : $"{Key} = {Argument}";
	}
}
