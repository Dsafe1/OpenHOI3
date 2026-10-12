using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Everything a trigger or an effect may name, as the reading of script looks it up: each list
/// by exact name, as the original's are, with a second look without case only to say that a
/// name is there in another case. Countries are looked up by the original's own rule.
/// <c>docs/mechanics/script.md</c>, sections 2 and 7.
/// </summary>
public sealed class CScriptNames
{
	/// <summary>
	/// The original's own words that are spelled like a country's tag, and so never are one where
	/// a tag is asked of a word it does not know: <c>docs/mechanics/script.md</c>, section 7. Only
	/// these two collide with the base game's and BlackICE's tags.
	/// </summary>
	private static readonly HashSet<string> OriginalWords = new(StringComparer.OrdinalIgnoreCase) { "phi", "pan" };

	private readonly Dictionary<EScriptName, HashSet<string>> _exact = new();
	private readonly Dictionary<EScriptName, Dictionary<string, string>> _anyCase = new();
	private readonly Dictionary<string, string> _groupOfLaw = new(StringComparer.Ordinal);
	private readonly List<CCountryTag> _tags = new();

	/// <summary>How many provinces the map has, the nought that is none of them counted.</summary>
	public int ProvinceCount { get; set; }

	public void Add(EScriptName kind, string name)
	{
		if (!_exact.TryGetValue(kind, out HashSet<string>? exact))
		{
			_exact[kind] = exact = new HashSet<string>(StringComparer.Ordinal);
			_anyCase[kind] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		}
		exact.Add(name);
		_anyCase[kind].TryAdd(name, name);
	}

	/// <summary>A law, and the group it belongs to.</summary>
	public void AddLaw(string group, string law)
	{
		Add(EScriptName.LawGroup, group);
		Add(EScriptName.Law, law);
		_groupOfLaw.TryAdd(law, group);
	}

	public void AddCountry(CCountryTag tag)
	{
		_tags.Add(tag);
	}

	public bool Has(EScriptName kind, string name)
	{
		return _exact.TryGetValue(kind, out HashSet<string>? exact) && exact.Contains(name);
	}

	/// <summary>The name as the list has it, where it is there only in another case.</summary>
	public string? InOtherCase(EScriptName kind, string name)
	{
		return _anyCase.TryGetValue(kind, out Dictionary<string, string>? anyCase)
			&& anyCase.TryGetValue(name, out string? found) && found != name ? found : null;
	}

	/// <summary>Whether anything is known of the kind, so that a name missing from it means something.</summary>
	public bool Knows(EScriptName kind)
	{
		return _exact.ContainsKey(kind);
	}

	/// <summary>The group a law belongs to; null for no law.</summary>
	public string? GroupOfLaw(string law)
	{
		return _groupOfLaw.GetValueOrDefault(law);
	}

	public bool IsProvince(int id)
	{
		return id > 0 && id < ProvinceCount;
	}

	/// <summary>
	/// The country a text names, by the original's rule: its first three letters, matched without
	/// case only where their codes add up to the same remainder by 64 as the tag's own. A text of
	/// fewer than three letters names nothing.
	/// </summary>
	public bool TryTag(string text, out CCountryTag tag)
	{
		tag = default;
		if (text.Length < CCountryTag.Length)
		{
			return false;
		}
		string first = text[..CCountryTag.Length];
		int bucket = Bucket(first);
		foreach (CCountryTag candidate in _tags)
		{
			if (string.Equals(candidate.Value, first, StringComparison.OrdinalIgnoreCase) && Bucket(candidate.Value) == bucket)
			{
				tag = candidate;
				return true;
			}
		}
		return false;
	}

	/// <summary>Whether a bare word is one of the original's own, and so never a tag where it asks for one.</summary>
	public static bool IsOriginalWord(string word)
	{
		return OriginalWords.Contains(word);
	}

	private static int Bucket(string three)
	{
		return (three[0] + three[1] + three[2]) % 64;
	}
}
