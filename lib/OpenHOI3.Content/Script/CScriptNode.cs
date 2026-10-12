using System.Collections.Generic;

namespace OpenHOI3.Content.Script;

/// <summary>
/// One <c>key = value</c> or <c>key = { ... }</c> of an event or decision file, with whether
/// each side was quoted and the line the key is on.
/// </summary>
public sealed class CScriptNode
{
	public CScriptNode(string key, bool keyQuoted, int line)
	{
		Key = key;
		KeyQuoted = keyQuoted;
		Line = line;
	}

	public string Key { get; }

	public bool KeyQuoted { get; }

	public int Line { get; }

	/// <summary>The value; null for a block.</summary>
	public string? Value { get; init; }

	public bool ValueQuoted { get; init; }

	public List<CScriptNode> Children { get; } = new();

	public bool IsBlock => Value is null;

	/// <summary>Whether the key is a bare word, matched without case - the only kind a keyword can be.</summary>
	public bool KeyIs(string word)
	{
		return !KeyQuoted && Key.Equals(word, System.StringComparison.OrdinalIgnoreCase);
	}

	public override string ToString()
	{
		return IsBlock ? $"{Key} = {{ {Children.Count} }}" : $"{Key} = {Value}";
	}
}
