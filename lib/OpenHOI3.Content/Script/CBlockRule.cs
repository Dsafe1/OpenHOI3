using System;
using System.Collections.Generic;

namespace OpenHOI3.Content.Script;

/// <summary>
/// What a block keyword's block takes: keys of its own, and whether every other key is a
/// condition or an effect of its language, a <c>limit</c>, a <c>random_list</c>'s weights or a
/// unit type.
/// </summary>
public sealed class CBlockRule
{
	/// <summary>A block of nothing but conditions or effects.</summary>
	public static readonly CBlockRule Children = new() { TakesChildren = true };

	/// <summary>An iterating effect's: <c>limit</c> and effects.</summary>
	public static readonly CBlockRule Limited = new() { TakesChildren = true, TakesLimit = true };

	public Dictionary<string, CArgumentRule> Keys { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>Every key not its own is a condition or an effect of the block's language.</summary>
	public bool TakesChildren { get; init; }

	/// <summary><c>limit = { conditions }</c>, as often as it is written, all ANDed.</summary>
	public bool TakesLimit { get; init; }

	/// <summary>Every key is a whole number, a weight, with a block of effects: <c>random_list</c>.</summary>
	public bool TakesWeights { get; init; }

	/// <summary>A unit type as a key, with a name: <c>add_division</c>.</summary>
	public bool TakesUnitTypes { get; init; }

	public CBlockRule With(string key, CArgumentRule rule)
	{
		Keys[key] = rule;
		return this;
	}
}
