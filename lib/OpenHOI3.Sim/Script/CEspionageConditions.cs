using System.Globalization;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Espionage;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The conditions on spies, each asked of the scope country's presence in THIS's country - its own presence where THIS
/// is the scope itself or names no country: <c>docs/mechanics/espionage.md</c>, section 13.
/// </summary>
public static class CEspionageConditions
{
	public static bool TryAnswer(CGameState state, CTrigger trigger, CEventScope scope, out bool answer)
	{
		switch (trigger.Keyword)
		{
			case "spies":
				answer = Presence(state, scope) is { } spies && spies.Level >= trigger.Argument.Amount.Raw;
				return true;
			case "spy_mission_count":
				answer = Presence(state, scope) is { } presence
					&& presence.Level * presence.Shares[Mission(trigger)] / 1000 >= (CConditions.Field(trigger, "value")?.Amount.Raw ?? 0);
				return true;
			default:
				answer = false;
				return false;
		}
	}

	private static CSpyPresence? Presence(CGameState state, CEventScope scope)
	{
		if (state.Country(scope.Country) is not { } country)
		{
			return null;
		}
		// THIS's country is read as it stands; a THIS that is a province alone falls back to the scope.
		CCountry target = state.Country(scope.Root.Country) ?? country;
		return CGameState.Presence(country, target);
	}

	/// <summary>The mission by its number; a type nothing knows is counter-espionage.</summary>
	private static int Mission(CTrigger trigger)
	{
		string written = CConditions.Field(trigger, "type")?.Text ?? "";
		return int.TryParse(written, NumberStyles.Integer, CultureInfo.InvariantCulture, out int mission)
			&& mission >= 0 && mission < CSpyPresence.MissionCount ? mission : 0;
	}
}
