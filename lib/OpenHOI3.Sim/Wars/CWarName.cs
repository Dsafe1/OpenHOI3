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

	public override string ToString() => $"{Key}({Order}, {First}, {Second})";
}
