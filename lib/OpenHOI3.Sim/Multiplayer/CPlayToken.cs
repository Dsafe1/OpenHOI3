namespace OpenHOI3.Sim.Multiplayer;

/// <summary>
/// What a machine names its files by as it asks to be let in (<c>docs/multiplayer.md</c>, section 15): its play checksum
/// and, after a slash, its base - a figure of the engine's version, the rules and the original's file checksum, which
/// two machines may share while their play checksums differ. A token with no slash has no base.
/// </summary>
public static class CPlayToken
{
	public const char Separator = '/';

	public static string Of(string play, string @base) => @base.Length == 0 ? play : play + Separator + @base;

	/// <summary>The play checksum of a token.</summary>
	public static string Play(string token)
	{
		int at = token.IndexOf(Separator);
		return at < 0 ? token : token[..at];
	}

	/// <summary>The base of a token; empty where it has none.</summary>
	public static string Base(string token)
	{
		int at = token.IndexOf(Separator);
		return at < 0 ? "" : token[(at + 1)..];
	}
}
