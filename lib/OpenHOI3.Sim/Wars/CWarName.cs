using System;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// The name a declaration gives a war: the key of its text, the adjective of its first attacker - and of
/// its first defender, for the key that asks for both - and its order among wars of the same name, from
/// one. Written out when shown; two names are one where all four agree. See <c>docs/mechanics/war.md</c>,
/// section 7.
/// </summary>
public sealed record CWarName(string Key, string First, string Second, int Order)
{
	/// <summary>A declaration's key, <c>$ORDER$War of $FIRST$ Aggression</c>.</summary>
	public const string Aggression = "AGRESSION_WAR_NAME";

	/// <summary>A revolution's key, <c>$ORDER$$FIRST$-$SECOND$ War</c> (<c>rebels.md</c>, section 12).</summary>
	public const string Normal = "NORMAL_WAR_NAME";

	/// <summary>
	/// The name written out: ORDER empty for the first, else the order, its suffix and a space; FIRST and SECOND the
	/// adjectives. As the war list shows it and a save writes it.
	/// </summary>
	public string Text(Func<string, string> localise)
	{
		string order = Order == 1 ? "" : $"{Order}{localise(Suffix(Order))} ";
		return localise(Key).Replace("$ORDER$", order).Replace("$FIRST$", First).Replace("$SECOND$", Second);
	}

	/// <summary>The suffix key of an order by its last digit, eleven to thirteen taking <c>TH</c>.</summary>
	public static string Suffix(int order)
	{
		if (order % 100 is >= 11 and <= 13)
		{
			return "TH";
		}
		return (order % 10) switch
		{
			1 => "ST",
			2 => "ND",
			3 => "RD",
			_ => "TH",
		};
	}

	public override string ToString() => $"{Key}({Order}, {First}, {Second})";
}
