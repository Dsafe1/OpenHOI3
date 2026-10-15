using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The effects that leave a mark for script to find again: flags, variables and the modifiers an
/// event gives. See <c>docs/mechanics/effects.md</c>, sections 3 and 6.
/// </summary>
public static class CMarkEffects
{
	public static bool TryRun(CGameState state, CEffect effect, CEventScope scope)
	{
		CCountry? country = state.Country(scope.Country);
		CProvince here = state.Province(scope.Province);
		string name = effect.Argument.Text;
		switch (effect.Keyword)
		{
			case "set_country_flag":
				country?.Flags.Set(name);
				return true;
			case "clr_country_flag":
				country?.Flags.Clear(name);
				return true;
			case "set_province_flag":
				here.Flags.Set(name);
				return true;
			case "clr_province_flag":
				here.Flags.Clear(name);
				return true;
			case "set_global_flag":
				state.Flags.Set(name);
				return true;
			case "clr_global_flag":
				state.Flags.Clear(name);
				return true;
			case "set_variable":
			case "change_variable":
				if (country is not null && CEffects.Field(effect, "which") is { } which)
				{
					CFixedPoint value = CEffects.Field(effect, "value")?.Amount ?? CFixedPoint.Zero;
					country.Variables[which.Text] = effect.Keyword == "set_variable"
						? value
						: country.Variables.GetValueOrDefault(which.Text) + value;
				}
				return true;
			case "add_country_modifier":
				if (country is not null && Given(state, effect) is { } forCountry && !Holds(country.EventModifiers, forCountry))
				{
					country.EventModifiers.Add(CTimedModifier.Given(state, forCountry, Days(effect)));
				}
				return true;
			case "add_province_modifier":
				if (Given(state, effect) is { } forProvince && !Holds(here.EventModifiers, forProvince))
				{
					here.EventModifiers.Add(CTimedModifier.Given(state, forProvince, Days(effect)));
					// A province's modifier is rebuilt at once; a country's waits for its rebuild.
					CProvinceModifiers.RebuildOne(state, here);
				}
				return true;
			case "remove_country_modifier":
				country?.EventModifiers.RemoveAll(given => given.Modifier.Name == name);
				return true;
			case "remove_province_modifier":
				here.EventModifiers.RemoveAll(given => given.Modifier.Name == name);
				return true;
			default:
				return false;
		}
	}

	/// <summary>Whether a list of given modifiers has that one, whatever its time.</summary>
	public static bool Holds(List<CTimedModifier> given, CModifier modifier)
	{
		return given.Exists(held => held.Modifier == modifier);
	}

	private static CModifier? Given(CGameState state, CEffect effect)
	{
		return CEffects.Field(effect, "name") is { } name ? state.FindStaticModifier(name.Text) : null;
	}

	private static int Days(CEffect effect)
	{
		return (int)CConditions.WholeOf(CEffects.Field(effect, "duration")?.Amount ?? CFixedPoint.Zero);
	}
}
