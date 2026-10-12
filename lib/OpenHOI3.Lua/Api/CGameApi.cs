using System;
using System.Collections.Generic;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// The classes the original hands its scripts, by its names, with the methods built so far on the
/// simulation. A method a script calls that is not here fails as a Lua error naming it, so the
/// script stops there and the call it was part of is reported. See <c>docs/mechanics/lua.md</c>,
/// section 4.
/// </summary>
public sealed class CGameApi
{
	private readonly Func<CGameState> _state;
	private readonly Dictionary<string, CLuaNamed> _governments = new(StringComparer.Ordinal);
	private readonly Dictionary<string, CLuaNamed> _factions = new(StringComparer.Ordinal);

	public CGameApi(Func<CGameState> state)
	{
		_state = state;
		Government = new CLuaClass("CGovernment");
		Faction = new CLuaClass("CFaction");
		Country = new CLuaClass("CCountry")
			.Method("GetCountryTag", call => call.Return(Tag(call.Object<CCountry>(1, "CCountry").Tag)))
			.Method("GetRelation", GetRelation)
			.Method("IsAtWar", call => call.Return(_state().IsAtWar(call.Object<CCountry>(1, "CCountry").Tag)))
			.Method("GetGovernment", call =>
				call.Return(new CLuaObject(Government, Named(_governments, call.Object<CCountry>(1, "CCountry").GovernmentType))))
			.Method("GetFaction", call =>
				call.Return(new CLuaObject(Faction, Named(_factions, call.Object<CCountry>(1, "CCountry").Faction))));
		CountryTag = new CLuaClass("CCountryTag")
			.Method("GetCountry", call => call.Return(
				_state().Country(call.Object<CCountryTag>(1, "CCountryTag")) is { } country ? Of(country) : null));
		DiplomacyStatus = new CLuaClass("CDiplomacyStatus")
			.Method("HasAlliance", call =>
			{
				CDiplomacyStatus status = call.Object<CDiplomacyStatus>(1, "CDiplomacyStatus");
				return call.Return(CCountryLists.HasAlliance(_state(), status.Holder, status.Other));
			})
			.Method("HasWar", call =>
			{
				CDiplomacyStatus status = call.Object<CDiplomacyStatus>(1, "CDiplomacyStatus");
				return call.Return(CCountryLists.AtWar(_state(), status.Holder, status.Other));
			})
			// A truce is the hundred days after a war ends, and no war ends yet.
			.Method("HasTruce", call =>
			{
				call.Object<CDiplomacyStatus>(1, "CDiplomacyStatus");
				return call.Return(false);
			});
	}

	public CLuaClass Country { get; }

	public CLuaClass CountryTag { get; }

	public CLuaClass DiplomacyStatus { get; }

	public CLuaClass Government { get; }

	public CLuaClass Faction { get; }

	public IEnumerable<CLuaClass> Classes => new[] { Country, CountryTag, DiplomacyStatus, Government, Faction };

	/// <summary>A country, as a script is handed one.</summary>
	public CLuaObject Of(CCountry country) => new(Country, country);

	/// <summary>A tag, as a script is handed one: a fresh copy each time, as the original pushes it.</summary>
	public CLuaObject Tag(CCountryTag tag) => new(CountryTag, tag);

	private int GetRelation(CLuaCall call)
	{
		CCountry country = call.Object<CCountry>(1, "CCountry");
		CCountryTag tag = call.Object<CCountryTag>(2, "CCountryTag");
		return call.Return(_state().Country(tag) is { } other
			? new CLuaObject(DiplomacyStatus, CGameState.Status(country, other))
			: null);
	}

	private static CLuaNamed Named(Dictionary<string, CLuaNamed> known, string name)
	{
		if (!known.TryGetValue(name, out CLuaNamed? named))
		{
			known[name] = named = new CLuaNamed(name);
		}
		return named;
	}
}
