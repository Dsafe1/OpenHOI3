using System.Collections.Generic;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// One effect out of an effect block - a keyword with its value, a name the original looks up,
/// or a block of effects under a scope. See <c>docs/mechanics/script.md</c>, section 6. What
/// each does is not built yet.
/// </summary>
public sealed class CEffect
{
	public CEffect(EEffectKind kind, string key, CScriptArgument argument)
	{
		Kind = kind;
		Key = key;
		Argument = argument;
	}

	public EEffectKind Kind { get; }

	/// <summary>The key as written.</summary>
	public string Key { get; }

	/// <summary>The keyword, in lower case, where <see cref="Kind"/> is a keyword; otherwise empty.</summary>
	public string Keyword => Kind == EEffectKind.Keyword ? Key.ToLowerInvariant() : "";

	/// <summary>The value, where the key takes one; for a scope, what it names; for a weight, the weight.</summary>
	public CScriptArgument Argument { get; }

	/// <summary>
	/// The conditions an iterating effect or a region picks its provinces or countries by: every
	/// <c>limit</c> of the block, ANDed. Null where there is none.
	/// </summary>
	public CTrigger? Limit { get; set; }

	/// <summary>The effects of a block of them, in order.</summary>
	public List<CEffect> Children { get; } = new();

	/// <summary>A block keyword's own keys: <c>relation</c>'s <c>who</c> and <c>value</c>.</summary>
	public List<CScriptField> Fields { get; } = new();

	public override string ToString()
	{
		return Children.Count > 0 || Fields.Count > 0 ? $"{Key} = {{ {Children.Count + Fields.Count} }}" : $"{Key} = {Argument}";
	}
}
