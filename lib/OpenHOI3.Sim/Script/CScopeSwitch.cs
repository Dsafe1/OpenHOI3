using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The scope a block names - a tag, a province's number or one of nine words - made from the scope
/// it is asked in: <c>docs/mechanics/triggers.md</c>, section 2.
/// </summary>
public static class CScopeSwitch
{
	/// <summary>The scope the argument names, or null where it cannot be made.</summary>
	public static CEventScope? Narrow(CGameState state, CEventScope scope, CScriptArgument named, CScriptReport? report)
	{
		if (CEventScope.Known(named.Tag) is { } tag)
		{
			return scope.With(tag, 0);
		}
		if (named.Province != 0)
		{
			return scope.With(null, named.Province);
		}
		CProvince here = state.Province(scope.Province);
		switch (named.Text.ToLowerInvariant())
		{
			case "owner":
				return here.Owner is { } owner ? scope.With(owner, 0) : null;
			case "controller":
				return here.Controller is { } controller ? scope.With(controller, 0) : null;
			case "from":
				return scope.With(scope.FromCountry, scope.FromProvince);
			case "this":
				return scope.With(scope.Root.Country, scope.Root.Province);
			case "ally":
			{
				CCountry? country = state.Country(scope.Country);
				List<CCountry> allies = country is null ? new List<CCountry>() : CCountryLists.Allied(state, country);
				return allies.Count == 0 ? null : scope.With(allies[scope.Draw(allies.Count)].Tag, scope.Province);
			}
			case "local_enemy":
			{
				var owners = new List<CCountryTag>();
				foreach (CUnit unit in state.UnitsIn(scope.Province))
				{
					if (unit.Combats.Count > 0 && unit.Owner != here.Owner)
					{
						owners.Add(unit.Owner);
					}
				}
				return owners.Count == 0 ? null : scope.With(owners[scope.Draw(owners.Count)], scope.Province);
			}
			case "capital_scope":
				return scope.With(null, state.Country(scope.Country)?.ActingCapital ?? 0);
			case "overlord":
				return scope.With(state.Country(scope.Country)?.Overlord, 0);
			case "sea_zone":
				report?.Add(named.Text, EScriptGap.Unread);
				return null;
			default:
				throw new InvalidOperationException($"'{named.Text}' names no scope");
		}
	}
}
