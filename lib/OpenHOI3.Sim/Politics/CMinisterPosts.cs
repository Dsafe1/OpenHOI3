using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Politics;

/// <summary>
/// The ministers a country may appoint and the filling of its cabinet posts from them. See
/// <c>docs/mechanics/politics.md</c>, section 7.
/// </summary>
public static class CMinisterPosts
{
	/// <summary>
	/// A new game's pool: every minister the country declares whose start date has come, in its file's order.
	/// Ours, following the start save: the original's bytes also ask that his death date is still to come.
	/// </summary>
	public static void SetUpPool(CGameState state, CCountry country)
	{
		country.PossibleMinisters.Clear();
		foreach (CMinister minister in country.Ministers.Values)
		{
			if (minister.StartDate <= state.Date)
			{
				country.PossibleMinisters.Add(minister);
			}
		}
	}

	/// <summary>
	/// Each day of a game under way, a minister whose start date is today joins the pool. Not while a game is being
	/// made: the original's step waits for the game's screen.
	/// </summary>
	public static void JoinDaily(CGameState state)
	{
		if (state.IsBeingMade)
		{
			return;
		}
		foreach (CCountry country in state.Countries)
		{
			foreach (CMinister minister in country.Ministers.Values)
			{
				if (minister.StartDate == state.Date && !country.PossibleMinisters.Contains(minister))
				{
					country.PossibleMinisters.Add(minister);
				}
			}
		}
	}

	/// <summary>
	/// Fills the posts: each one empty, or every one with <paramref name="refillAll"/>, and with
	/// <paramref name="electedOnly"/> only those the government's election changes. In the pool's order: the first
	/// of the ruling party who may take it; else the last of the ruling party's group who may; else the first of
	/// anyone who may. Where nobody may, the post keeps its holder.
	/// </summary>
	public static void Fill(CGameState state, CCountry country, bool refillAll, bool electedOnly)
	{
		CIdeology? ruling = state.Ideology(country.RulingIdeology);
		CGovernment? government = state.GovernmentOf(country);
		foreach (CGovernmentPosition position in state.GovernmentPositions)
		{
			if (!refillAll && country.Government.ContainsKey(position.Name))
			{
				continue;
			}
			if (electedOnly && (government is null || !government.Elected.Contains(position.Name)))
			{
				continue;
			}
			if (Pick(state, country, position.Name, ruling) is { } minister)
			{
				country.Government[position.Name] = minister;
			}
		}
	}

	/// <summary>
	/// The AI's weekly look at the empty posts, in their file's order: each is given the minister <see cref="Fill"/>
	/// would choose - save that the head of state's is not asked whether his block names the post - by a
	/// <c>CChangeMinisterCommand</c> to the outbox, nothing asked of a second post, his dates or whether the post may be
	/// changed (<c>politics.md</c>, section 12).
	/// </summary>
	public static void FillVacant(CGameState state, CCountry country)
	{
		CIdeology? ruling = state.Ideology(country.RulingIdeology);
		for (int i = 0; i < state.GovernmentPositions.Count; i++)
		{
			string position = state.GovernmentPositions[i].Name;
			if (!country.Government.ContainsKey(position)
				&& Pick(state, country, position, ruling, anyPost: i == 0) is { } minister)
			{
				state.Outbox.Add(new Commands.CChangeMinisterCommand(country.Tag, minister.Id, position));
			}
		}
	}

	private static CMinister? Pick(CGameState state, CCountry country, string position, CIdeology? ruling, bool anyPost = false)
	{
		CMinister? ofGroup = null;
		CMinister? anyone = null;
		foreach (CMinister minister in country.PossibleMinisters)
		{
			if (!anyPost && !minister.MayTake(position))
			{
				continue;
			}
			CIdeology? his = state.Ideology(minister.Ideology);
			if (ruling is not null && his == ruling)
			{
				return minister;
			}
			if (ruling is not null && his is not null && his.Group == ruling.Group)
			{
				ofGroup = minister;
			}
			anyone ??= minister;
		}
		return ofGroup ?? anyone;
	}
}
