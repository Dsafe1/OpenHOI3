using System;
using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Interface.Events;

/// <summary>
/// An event's or a decision's text: a key of the localisation with its <c>$NAME$</c> variables
/// filled from the scope. A variable nothing gives is left as written. See
/// <c>docs/mechanics/events.md</c>, section 11.
/// </summary>
public static class CEventText
{
	public static string Localise(Func<string, string> localise, CGameState state, string key, CEventScope scope)
	{
		string text = localise(key);
		foreach ((string name, string value) in Variables(localise, state, scope))
		{
			text = text.Replace("$" + name + "$", value);
		}
		return text;
	}

	/// <summary>The variables a scope gives, by name.</summary>
	public static Dictionary<string, string> Variables(Func<string, string> localise, CGameState state, CEventScope scope)
	{
		var values = new Dictionary<string, string>();
		if (state.Country(scope.Root.Country) is { } root)
		{
			values["THISCOUNTRY"] = root.Name;
		}
		if (state.Country(scope.FromCountry) is { } from)
		{
			values["FROMCOUNTRY"] = from.Name;
			values["FROMCOUNTRY_ADJ"] = Adjective(localise, from);
		}
		CProvince here = state.Province(scope.Province);
		// The country is the scope's, or its province's controller where it has none.
		if (state.Country(scope.Country ?? here.Controller) is { } country)
		{
			values["COUNTRY"] = country.Name;
			values["COUNTRYNAME"] = country.Name;
			values["COUNTRY_ADJ"] = Adjective(localise, country);
			values["CAPITAL"] = state.Province(country.Capital).Name;
		}
		values["YEAR"] = state.Date.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);
		if (scope.Province != 0)
		{
			values["PROVINCENAME"] = here.Name;
			values["REGIONNAME"] = here.Regions.Count > 0 && here.Regions[0] < state.Regions.Count
				? localise(state.Regions[here.Regions[0]].Name)
				: "";
			values["CONTINENTNAME"] = here.Continent;
			values["OWNERNAME"] = state.Country(here.Owner)?.Name ?? "";
		}
		return values;
	}

	/// <summary>A country's adjective: its tag with <c>_ADJ</c>, localised.</summary>
	public static string Adjective(Func<string, string> localise, CCountry country)
	{
		return localise(country.Tag.Value + "_ADJ");
	}
}
