using System;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What a war and a war goal say: a war's name written out, and the declare-war dialog's description of the
/// goal being set up. See <c>docs/mechanics/war.md</c>, sections 3 and 7.
/// </summary>
public static class CWarText
{
	/// <summary>
	/// A war's name: the history's as it is, or a declaration's written out - ORDER empty for the first, else
	/// the order, its suffix and a space; FIRST and SECOND the adjectives.
	/// </summary>
	public static string Name(Func<string, string> localise, CWar war)
	{
		return war.MadeName?.Text(localise) ?? war.Name;
	}

	/// <summary>
	/// The original's <c>CWarGoal::BuildSetupText</c>: <c>&lt;type&gt;_setup</c> with ACTOR and RECIPIENT in yellow;
	/// where it asks for THIRD and none is set, <c>WARDIALOG_SELECT_COUNTRY</c> alone, and for STATE,
	/// <c>WARDIALOG_SELECT_STATE</c>; nothing for a null type.
	/// </summary>
	public static string Setup(Func<string, string> localise, CGameState state, CWarGoal? goal)
	{
		if (goal is null || !goal.Type.IsReal)
		{
			return "";
		}
		string text = localise(goal.Type.Name + "_setup");
		if (text.Contains("$THIRD$", StringComparison.Ordinal) && goal.Country is null)
		{
			return localise("WARDIALOG_SELECT_COUNTRY");
		}
		if (text.Contains("$STATE$", StringComparison.Ordinal) && goal.Region is null)
		{
			return localise("WARDIALOG_SELECT_STATE");
		}
		string Yellow(string name) => "§Y" + name + "§W";
		return "§W" + CTextTemplate.Fill(text,
			("ACTOR", Yellow(state.Country(goal.Actor)?.Name ?? goal.Actor.Value)),
			("RECIPIENT", Yellow(state.Country(goal.Recipient)?.Name ?? goal.Recipient.Value)),
			("THIRD", goal.Country is { } third ? state.Country(third)?.Name ?? third.Value : ""),
			("STATE", goal.Region is { } region ? localise(region.Name) : ""));
	}
}
