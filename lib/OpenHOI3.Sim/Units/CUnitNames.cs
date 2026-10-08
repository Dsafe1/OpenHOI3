using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// What a newly ordered unit is called. A country's file lists names by unit type; one
/// that is not in use anywhere in the game is taken from where the country last left
/// off, and when there is none a number is counted up until the name is free. See
/// <c>docs/mechanics/builders.md</c>, section 8.
/// </summary>
public static class CUnitNames
{
	private const string Number = "$NUM$";
	private const string Order = "$ORDER$";
	private const string Type = "$TYPE$";

	/// <summary>
	/// A name for a division, from the list its country keeps for the type of its first
	/// brigade. The name is not taken until it is handed to <see cref="Register"/>.
	/// </summary>
	public static string ForDivision(CGameState state, CCountry country, CSubUnitType first)
	{
		return FromList(state, country, first)
			?? Numbered(state, state.NameTexts.Division, first);
	}

	/// <summary>
	/// A name for a brigade, a ship or a wing. A ship or a wing takes one of its type's
	/// own list first; a land brigade never does, its type's list being names for
	/// divisions.
	/// </summary>
	public static string ForSubUnit(CGameState state, CCountry country, CSubUnitType type)
	{
		if (type.Branch == EUnitBranch.Land)
		{
			return Numbered(state, state.NameTexts.Brigade, type);
		}
		return FromList(state, country, type) ?? Numbered(state, state.NameTexts.SubUnit, type);
	}

	/// <summary>Marks a name as in use, for every country.</summary>
	public static void Register(CGameState state, string name)
	{
		if (name.Length != 0)
		{
			state.UnitNamesInUse.Add(name);
		}
	}

	/// <summary>
	/// The ending a number takes, as the original picks it: by its last digit, with 11,
	/// 12 and 13 - and only those - sent to the first. So 111 comes out as a first.
	/// </summary>
	public static string Ordinal(CUnitNameTexts texts, int number)
	{
		int digit = number % 10;
		if (number is >= 11 and <= 13)
		{
			digit = 0;
		}
		return digit is >= 1 and <= 3 && digit < texts.Ordinals.Length
			? texts.Ordinals[digit]
			: texts.Ordinals.Length > 0 ? texts.Ordinals[0] : "";
	}

	/// <summary>
	/// Once round the country's list for a type from where it left off, passing over
	/// what is in use. Where it stops is remembered, on the name it took.
	/// </summary>
	private static string? FromList(CGameState state, CCountry country, CSubUnitType type)
	{
		if (!country.UnitNames.TryGetValue(type.Key, out List<string>? names) || names.Count == 0)
		{
			return null;
		}
		int at = country.UnitNameCursors.GetValueOrDefault(type.Key);
		if (at < 0 || at >= names.Count)
		{
			at = 0;
		}
		for (int tries = names.Count; tries > 0; tries--)
		{
			string name = names[at];
			country.UnitNameCursors[type.Key] = at;
			at = at + 1 >= names.Count ? 0 : at + 1;
			if (!state.UnitNamesInUse.Contains(name))
			{
				return name;
			}
		}
		return null;
	}

	/// <summary>The pattern with 1, 2, 3 ... until the name is free.</summary>
	private static string Numbered(CGameState state, string pattern, CSubUnitType type)
	{
		// A pattern with no number in it would never come out any different.
		if (!pattern.Contains(Number, StringComparison.Ordinal))
		{
			return pattern;
		}
		string shortName = state.NameTexts.ShortName(type);
		for (int number = 1; ; number++)
		{
			string name = pattern
				.Replace(Number, number.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
				.Replace(Order, Ordinal(state.NameTexts, number), StringComparison.Ordinal)
				.Replace(Type, shortName, StringComparison.Ordinal);
			if (!state.UnitNamesInUse.Contains(name))
			{
				return name;
			}
		}
	}
}
