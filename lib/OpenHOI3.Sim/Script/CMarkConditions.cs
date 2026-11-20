using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The conditions on what script has left - flags, variables and the modifiers an event gave:
/// <c>docs/mechanics/effects.md</c>, sections 3 and 6.
/// </summary>
public static class CMarkConditions
{
	public static bool TryAnswer(CGameState state, CTrigger trigger, CEventScope scope, out bool answer)
	{
		CCountry? country = state.Country(scope.Country);
		CProvince here = CConditions.Here(state, scope);
		string name = trigger.Argument.Text;
		switch (trigger.Keyword)
		{
			case "has_country_flag":
				answer = country?.Flags.IsSet(name) ?? false;
				return true;
			case "has_global_flag":
				answer = state.Flags.IsSet(name);
				return true;
			case "has_province_flag":
				answer = here.Flags.IsSet(name);
				return true;
			case "check_variable":
				answer = country is not null && CheckVariable(state, country, trigger);
				return true;
			case "has_country_modifier":
				// Its time is not looked at.
				answer = country is not null && state.FindStaticModifier(name) is { } forCountry
					&& CMarkEffects.Holds(country.EventModifiers, forCountry);
				return true;
			case "has_province_modifier":
				answer = state.FindStaticModifier(name) is { } forProvince && CMarkEffects.Holds(here.EventModifiers, forProvince);
				return true;
			default:
				answer = false;
				return false;
		}
	}

	/// <summary>The variable is at least the value; one that is not there is made, at nought, by asking.</summary>
	private static bool CheckVariable(CGameState state, CCountry country, CTrigger trigger)
	{
		string which = "";
		CFixedPoint wanted = CFixedPoint.Zero;
		foreach (CScriptField field in trigger.Fields)
		{
			if (field.Key == "which")
			{
				which = field.Argument.Text;
			}
			else if (field.Key == "value")
			{
				wanted = field.Argument.Amount;
			}
		}
		if (!country.Variables.TryGetValue(which, out CFixedPoint held))
		{
			held = CFixedPoint.Zero;
			// Made by a command's asking alone: a screen's - a decision's tooltip, the music's pick - keeps nothing
			// (CGameState.LooksOnly).
			if (!state.LooksOnly)
			{
				country.Variables[which] = held;
			}
		}
		return held >= wanted;
	}
}
