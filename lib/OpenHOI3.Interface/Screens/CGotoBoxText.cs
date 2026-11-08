using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Interface.Screens;

/// <summary>What the go-to box, <c>goto_box</c>, offers for what has been typed and where it goes. See <c>docs/mechanics/convoywindows.md</c>, section 8.</summary>
public static class CGotoBoxText
{
	public const string WindowName = "goto_box";
	public const string RowName = "save_game_entry";

	private const string Unnamed = "PROV";
	private const int LeastLength = 1;

	/// <summary>The leading whole number of a text, nought where it has none: C's <c>atoi</c>.</summary>
	public static int Number(string text)
	{
		int at = 0;
		while (at < text.Length && char.IsWhiteSpace(text[at]))
		{
			at++;
		}
		bool negative = at < text.Length && text[at] == '-';
		if (at < text.Length && (text[at] == '-' || text[at] == '+'))
		{
			at++;
		}
		long value = 0;
		while (at < text.Length && char.IsAsciiDigit(text[at]) && value < int.MaxValue)
		{
			value = value * 10 + (text[at] - '0');
			at++;
		}
		return (int)(negative ? -value : value);
	}

	private static bool IsNamed(CProvince province) =>
		province.Name.Length < Unnamed.Length + 1 || !province.Name.StartsWith(Unnamed, StringComparison.Ordinal);

	/// <summary>What the list holds for a text of more than one character, and whether Go may be pressed: the province of the number typed, then every named province whose name begins with the text.</summary>
	public static (List<CProvince> Rows, bool MayGo) Filter(CGameState state, string text)
	{
		var rows = new List<CProvince>();
		if (text.Length <= LeastLength)
		{
			return (rows, false);
		}
		string typed = text.ToLowerInvariant();
		bool mayGo = false;
		int number = Number(typed);
		if (number > 0 && number < state.Provinces.Count)
		{
			rows.Add(state.Province(number));
			mayGo = true;
		}
		foreach (CProvince province in state.Provinces.Skip(1).Where(IsNamed))
		{
			string name = province.Name.ToLowerInvariant();
			if (name.StartsWith(typed, StringComparison.Ordinal))
			{
				rows.Add(province);
				mayGo |= name == typed;
			}
		}
		return (rows, mayGo);
	}

	/// <summary>Where Go goes, nought for nowhere: the number typed, whatever province that is; otherwise the first by number whose name begins with the text, or equals it.</summary>
	public static int Target(CGameState state, string text, bool exact)
	{
		string typed = text.ToLowerInvariant();
		if (Number(typed) is var number && number != 0)
		{
			return number > 0 && number < state.Provinces.Count ? number : 0;
		}
		foreach (CProvince province in state.Provinces.Skip(1))
		{
			string name = province.Name.ToLowerInvariant();
			if (exact ? name == typed : name.StartsWith(typed, StringComparison.Ordinal))
			{
				return province.Id;
			}
		}
		return 0;
	}
}
