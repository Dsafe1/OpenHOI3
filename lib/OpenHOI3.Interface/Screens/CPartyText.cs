using System;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// A party's name for a country, as the original writes an ideology's: the text of <c>&lt;ideology&gt;_&lt;TAG&gt;</c>
/// where the localisation has one - <c>national_socialist_GER</c> is the NSDAP - else the ideology's own. See
/// <c>docs/mechanics/politics.md</c>.
/// </summary>
public static class CPartyText
{
	public static string Name(Func<string, string> localise, string ideology, string tag)
	{
		string specific = ideology + "_" + tag;
		string text = localise(specific);
		return text != specific ? text : localise(ideology);
	}

	/// <summary>The name for the country, and the ideology's own after it in brackets where the two differ.</summary>
	public static string Qualified(Func<string, string> localise, string ideology, string tag)
	{
		string name = Name(localise, ideology, tag);
		string own = localise(ideology);
		return name != own ? name + " (" + own + ")" : name;
	}
}
