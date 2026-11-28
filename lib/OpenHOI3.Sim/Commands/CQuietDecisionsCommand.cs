using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Events;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country taking decisions out of its "decisions available" alert, or putting them back: the tick beside a decision
/// in the diplomacy screen's list, and with Ctrl the same for every decision the list shows
/// (<c>docs/mechanics/events.md</c>, section 12; <c>alerts.md</c>). An enhanced game's alone - a faithful one refuses
/// it - and the one way the list changes, so that it is the same on every machine, for everyone who plays the country,
/// and in a replay. It changes nothing a decision does: what is offered and what may be taken are as before.
///
/// Refused for a country that is not there, with no name given, and where any name is no decision.
/// </summary>
public sealed class CQuietDecisionsCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly string[] _decisions;
	private readonly bool _quiet;

	/// <param name="quiet">Whether the alert is to pass the decisions over; false puts them back.</param>
	public CQuietDecisionsCommand(CCountryTag country, IEnumerable<string> decisions, bool quiet)
	{
		_country = country;
		_decisions = decisions.ToArray();
		_quiet = quiet;
	}

	public override string ToString() => $"{(_quiet ? "quiet" : "alert for")} {_decisions.Length} decisions of {_country}";

	public override bool MayExecute(CGameState state)
	{
		return state.Rules.Enhanced && state.Country(_country) is not null && _decisions.Length > 0
			&& _decisions.All(name => CDecisions.Find(state, name) is not null);
	}

	public override void Execute(CGameState state)
	{
		if (!MayExecute(state) || state.Country(_country) is not { } country)
		{
			return;
		}
		foreach (string name in _decisions)
		{
			if (_quiet)
			{
				country.QuietDecisions.Add(name);
			}
			else
			{
				country.QuietDecisions.Remove(name);
			}
		}
	}
}
