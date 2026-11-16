namespace OpenHOI3.Lua.BiceLib;

/// <summary>
/// One function of <c>OpenHOI3.BiceLib</c>: its path under the table - <c>Leaders.activateOobLeaderRanks</c> - what it comes
/// to here, how much there is to build where it is not, whether it is, and what a call answers where it does nothing or is
/// built - true, false, or nothing (<c>docs/mechanics/bicelib.md</c>).
/// </summary>
public sealed record CBiceLibFunction(string Path, EBiceLibVerdict Verdict, string Size, bool Built, bool? Answer = null)
{
	/// <summary>Whether a call is reported as not built, once, through the Lua API's report.</summary>
	public bool Unbuilt => !Built;

	/// <summary>The name a report and the survey give it: <c>BiceLib.Leaders.activateOobLeaderRanks</c>.</summary>
	public string Name => "BiceLib." + Path;
}
