using System;
using System.Globalization;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Interface.Script;

/// <summary>
/// What every line a condition or an effect writes of itself draws on: the localisation, the game,
/// the country the text is shown to, and the original's ways of writing a name and a number. See
/// <c>docs/mechanics/scripttext.md</c>, section 2.
/// </summary>
public sealed class CScriptWords
{
	public CScriptWords(Func<string, string> localise, CGameState state, CCountryTag? player)
	{
		Localise = localise;
		State = state;
		Player = player;
	}

	/// <summary>A key's text; a key no table has is the key itself, and one of no length nothing.</summary>
	public Func<string, string> Localise { get; }

	public CGameState State { get; }

	/// <summary>The country the text is shown to, which a scope of effects does not name.</summary>
	public CCountryTag? Player { get; }

	public const string Yellow = "§Y";
	public const string White = "§W";
	public const string Red = "§R";
	public const string Green = "§G";

	/// <summary>A key's text with its places filled.</summary>
	public string T(string key, params (string Key, string Value)[] values)
	{
		return CTextTemplate.Fill(Localise(key), values);
	}

	/// <summary>A name in yellow and back to white.</summary>
	public static string InYellow(string text)
	{
		return Yellow + text + White;
	}

	/// <summary>
	/// A name the original colours by asking for it as a key: no table has it, so it is itself.
	/// </summary>
	public string Nested(string text)
	{
		return Localise(text);
	}

	/// <summary>A country's name, or its tag where no country has it, or nothing for none.</summary>
	public string Country(CCountryTag? tag)
	{
		if (tag is null)
		{
			return "";
		}
		return State.Country(tag)?.Name ?? tag.Value.Value;
	}

	public string Province(int id)
	{
		return State.Province(id).Name;
	}

	/// <summary>A whole number, cut toward nought.</summary>
	public static string Whole(CFixedPoint value)
	{
		return (value.Raw / CFixedPoint.Scale).ToString(CultureInfo.InvariantCulture);
	}

	public static string Whole(long value)
	{
		return value.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>Cut to so many places.</summary>
	public static string Cut(CFixedPoint value, int places)
	{
		return CNumberText.Format(value, places);
	}

	/// <summary>As many places as it needs.</summary>
	public static string Trimmed(CFixedPoint value)
	{
		return CNumberText.Trimmed(value);
	}

	/// <summary>A share as a percentage: a hundred times the value.</summary>
	public static CFixedPoint Percent(CFixedPoint value)
	{
		return new CFixedPoint(value.Raw * 100);
	}
}
