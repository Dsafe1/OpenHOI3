namespace OpenHOI3.Sim;

/// <summary>
/// One change to a game. Nothing alters a <see cref="CGameState"/> except a command
/// being executed, and commands are executed in the order they were posted - a player's
/// click, the AI's decision and the passing of an hour alike. See
/// <c>docs/mechanics/tick.md</c>, section 4.
///
/// <b>A local input may choose which commands are posted, never what a command does
/// when it runs.</b> In a game between several players every machine executes the same
/// commands, so one that reads anything belonging to a single machine - a pressed key,
/// what is selected, which country that player is - does something different on each.
/// A command carries its own targets, and reads nothing but itself and the game state.
/// </summary>
public abstract class CCommand
{
	/// <summary>Its place in the order of everything posted to the game; -1 until posted.</summary>
	public long Serial { get; internal set; } = -1;

	/// <summary>Which drain of the queue it was posted during; -1 until posted.</summary>
	public int Turn { get; internal set; } = -1;

	/// <summary>
	/// Asked just before execution. A command that no longer makes sense - its target
	/// is gone, the game has moved on - answers false and is dropped.
	/// </summary>
	public virtual bool MayExecute(CGameState state)
	{
		return true;
	}

	public abstract void Execute(CGameState state);
}

/// <summary>
/// A country setting how its IC is divided between the six things it is spent on. The
/// six are given together, since moving one moves the others.
/// </summary>
public sealed class CSetProductionSharesCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly CFixed15[] _shares;

	public CSetProductionSharesCommand(CCountryTag country, System.ReadOnlySpan<CFixed15> shares)
	{
		_country = country;
		_shares = shares.ToArray();
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is not null && CProduction.AreValidShares(_shares);
	}

	public override void Execute(CGameState state)
	{
		_shares.CopyTo(state.Country(_country)!.ProductionShares, 0);
	}
}

/// <summary>
/// A country setting how its leadership is divided between officers, diplomacy,
/// espionage and research. The four are given together, since moving one moves the others.
/// </summary>
public sealed class CSetLeadershipSharesCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly CFixed15[] _shares;

	public CSetLeadershipSharesCommand(CCountryTag country, System.ReadOnlySpan<CFixed15> shares)
	{
		_country = country;
		_shares = shares.ToArray();
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is not null && CLeadership.AreValidShares(_shares);
	}

	public override void Execute(CGameState state)
	{
		_shares.CopyTo(state.Country(_country)!.LeadershipShares, 0);
	}
}

/// <summary>
/// A country setting what it researches, and in what order: the whole queue at once. It
/// is refused if any technology in it does not exist, is there twice, or may not be
/// researched by that country now.
/// </summary>
public sealed class CSetResearchQueueCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly string[] _technologies;

	public CSetResearchQueueCommand(CCountryTag country, System.Collections.Generic.IEnumerable<string> technologies)
	{
		_country = country;
		_technologies = System.Linq.Enumerable.ToArray(technologies);
	}

	public override bool MayExecute(CGameState state)
	{
		if (state.Country(_country) is not { } country)
		{
			return false;
		}
		var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
		foreach (string name in _technologies)
		{
			if (!seen.Add(name) || !state.Technologies.TryGetValue(name, out CTechnology? technology)
				|| !CResearch.MayResearchNext(state, country, technology))
			{
				return false;
			}
		}
		return true;
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_country)!;
		country.ResearchQueue.Clear();
		foreach (string name in _technologies)
		{
			country.ResearchQueue.Add(state.Technologies[name].Name);
		}
	}
}

/// <summary>
/// The passing of one hour. Time is a command like any other, so that the hour turns
/// at the same point in the stream of commands on every machine.
/// </summary>
public sealed class CHourlyTickCommand : CCommand
{
	public override void Execute(CGameState state)
	{
		CTick.RunHour(state);
	}
}
