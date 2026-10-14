using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// A <c>CChangeLawCommand</c> a script has made and not yet posted: its two flags are the original's, both set only
/// while a game is under way, and <c>SetEnablePostMessage</c> sets the second. It becomes the simulation's command when
/// it is posted. See <c>docs/mechanics/politics.md</c>, section 12.
/// </summary>
public sealed class CLuaLawCommand
{
	public CLuaLawCommand(CCountryTag country, CLaw law, CLawGroup group, bool underWay)
	{
		Country = country;
		Law = law;
		Group = group;
		PayCost = underWay;
		PostMessage = underWay;
	}

	public CCountryTag Country { get; }

	public CLaw Law { get; }

	public CLawGroup Group { get; }

	public bool PayCost { get; }

	public bool PostMessage { get; set; }

	public Sim.Commands.CChangeLawCommand ToCommand() => new(Country, Law.Name, Group.Name, PayCost, PostMessage);
}
