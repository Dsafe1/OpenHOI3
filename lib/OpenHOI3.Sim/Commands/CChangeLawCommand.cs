using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Politics;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country's law changed: posted by the politics screen, the AI's Lua and the peace-time revert. It checks nothing
/// but that the country, the law and the group are real - whoever posts it asks the rest - and pays the cost and tells
/// the player only where it says to. The original keeps the two flags only while a game is under way: one made as a
/// game is set up pays nothing and is silent. See <c>docs/mechanics/politics.md</c>, section 9.
/// </summary>
public sealed class CChangeLawCommand : CCommand
{
	public CChangeLawCommand(CCountryTag country, string law, string group, bool payCost = true, bool postMessage = true)
	{
		Country = country;
		Law = law;
		Group = group;
		PayCost = payCost;
		PostMessage = postMessage;
	}

	public CCountryTag Country { get; }

	public string Law { get; }

	public string Group { get; }

	public bool PayCost { get; }

	public bool PostMessage { get; }

	public override bool MayExecute(CGameState state) => Find(state) is not null;

	public override void Execute(CGameState state)
	{
		if (Find(state) is ({ } country, { } law))
		{
			CLaws.Change(state, country, law, Group, PayCost, PostMessage);
		}
	}

	private (CCountry Country, CLaw Law)? Find(CGameState state)
	{
		if (state.Country(Country) is not { } country || state.LawGroup(Group) is null)
		{
			return null;
		}
		foreach (CLawGroup group in state.LawGroups)
		{
			if (group.Laws.Find(law => law.Name == Law) is { } law)
			{
				return (country, law);
			}
		}
		return null;
	}
}
