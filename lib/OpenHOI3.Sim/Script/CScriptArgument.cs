using System;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The value of one <c>key = value</c> in a trigger or an effect, as written and as read.
/// What the reading makes of it depends on the key: <see cref="Amount"/> is set where the key
/// takes a number, <see cref="Tag"/> where it takes a country, and <see cref="Province"/> where
/// it takes a province. See <c>docs/mechanics/script.md</c>, sections 2, 5 and 6.
/// </summary>
public sealed class CScriptArgument
{
	public static readonly CScriptArgument None = new("", false);

	public CScriptArgument(string text, bool quoted)
	{
		Text = text;
		Quoted = quoted;
	}

	/// <summary>The value as written, quotes left off.</summary>
	public string Text { get; }

	/// <summary>Whether it was written in quotes, and so is never one of the language's words.</summary>
	public bool Quoted { get; }

	/// <summary>The number, for a key that takes one: a whole number's decimals are cut.</summary>
	public CFixedPoint Amount { get; init; }

	/// <summary>The country, for a key that takes a tag; <c>---</c> where none is found.</summary>
	public CCountryTag Tag { get; init; }

	/// <summary>The province, for a key that takes a province's number; 0 for none.</summary>
	public int Province { get; init; }

	/// <summary>Whether the value is the language's word, which is matched without case.</summary>
	public bool IsWord(string word)
	{
		return !Quoted && Text.Equals(word, StringComparison.OrdinalIgnoreCase);
	}

	public bool IsYes => IsWord("yes");

	public bool IsThis => IsWord("this");

	public bool IsFrom => IsWord("from");

	public override string ToString()
	{
		return Quoted ? $"\"{Text}\"" : Text;
	}
}
