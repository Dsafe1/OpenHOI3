using System;
using OpenHOI3.Content.Messages;
using OpenHOI3.Interface.Events;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Interface.Messages;

/// <summary>
/// The message an answered event and a taken decision raise, as the player on this machine is told
/// it: its type by whether it is the player's own, and its values - the player's government's posts,
/// the country, the event's or the decision's words. See <c>docs/mechanics/messages.md</c>, sections 2
/// and 3.
/// </summary>
public static class CMessages
{
	public static CMessage EventAnswered(Func<string, string> localise, CMessageTypes types, CGameState state,
		CCountryTag? player, CEvent answered, CEventScope scope, int option)
	{
		string name;
		// A province's event has no country of its own, so its owner is told "OTHER".
		if (scope.Country is { } own && own == player)
		{
			name = "EVENTHAPPENUS";
		}
		else if (answered.Options.Count <= 1)
		{
			name = answered.Major ? "MAJOREVENTHAPPENOTHER" : "EVENTHAPPENOTHER";
		}
		else
		{
			name = answered.Major ? "MAJOREVENTHAPPENOTHEROPTION" : "EVENTHAPPENOTHEROPTION";
		}
		CMessage message = Made(localise, types, state, name, scope);
		Government(localise, state, player, message);
		message.Variables.Add(("COUNTRY", CountryName(state, scope)));
		message.Variables.Add(("EVENT", CEventText.Localise(localise, state, answered.Title, scope)));
		message.Variables.Add(("EVENTDESC", CEventText.Localise(localise, state, answered.Description, scope)));
		string chosen = option >= 0 && option < answered.Options.Count ? answered.Options[option].Name : "";
		message.Variables.Add(("OPTION", CEventText.Localise(localise, state, chosen, scope)));
		return message;
	}

	public static CMessage DecisionTaken(Func<string, string> localise, CMessageTypes types, CGameState state,
		CCountryTag? player, CDecision taken, CEventScope scope)
	{
		bool province = scope.Province != 0;
		string name = scope.Country is { } own && own == player
			? province ? "PROVDECISIONUS" : "DECISIONUS"
			: province ? "PROVDECISIONOTHER"
			: taken.Major ? "MAJORDECISIONOTHER" : "DECISIONOTHER";
		CMessage message = Made(localise, types, state, name, scope);
		Government(localise, state, player, message);
		message.Variables.Add(("COUNTRY", CountryName(state, scope)));
		message.Variables.Add(("EVENT", CEventText.Localise(localise, state, taken.Name + "_title", scope)));
		message.Variables.Add(("DESC", CEventText.Localise(localise, state, taken.Name + "_desc", scope)));
		if (province)
		{
			message.Variables.Add(("PROVINCENAME", state.Province(scope.Province).Name));
		}
		return message;
	}

	private static CMessage Made(Func<string, string> localise, CMessageTypes types, CGameState state, string name,
		CEventScope scope)
	{
		return new CMessage(types.Find(name), CMessageText.Date(localise, state.Date, state.Hour))
		{
			FirstCountry = scope.Country ?? state.Province(scope.Province).Owner,
			Province = scope.Province,
		};
	}

	/// <summary>Each post of the player's government, by its key: "$TITLE$ $NAME$".</summary>
	private static void Government(Func<string, string> localise, CGameState state, CCountryTag? player, CMessage message)
	{
		if (state.Country(player) is not { } country)
		{
			return;
		}
		foreach ((string post, CMinister minister) in country.Government)
		{
			string entry = localise("MINISTER_MESSAGE_ENTRY").Replace("$TITLE$", localise(post)).Replace("$NAME$", minister.Name);
			message.Variables.Add((post, entry));
		}
	}

	/// <summary>The scope's country, or for a province's scope its owner, or the province.</summary>
	private static string CountryName(CGameState state, CEventScope scope)
	{
		if (state.Country(scope.Country) is { } country)
		{
			return country.Name;
		}
		Sim.Provinces.CProvince here = state.Province(scope.Province);
		return state.Country(here.Owner)?.Name ?? here.Name;
	}
}
