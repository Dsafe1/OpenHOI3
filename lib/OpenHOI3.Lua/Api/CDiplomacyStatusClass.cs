using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CDiplomacyStatus</c>, what one country holds towards another: relation, threat, the other's tag
/// and the agreements between them that the simulation holds. See <c>docs/mechanics/lua.md</c>,
/// section 4.
/// </summary>
public static class CDiplomacyStatusClass
{
	private const string Name = "CDiplomacyStatus";

	private const string TruceUnbuilt = "truces: no war has ended";

	public static CLuaClass Define(CGameApi api)
	{
		return new CLuaClass(Name)
			.Method("GetValue", call => call.Return(api.Fixed(Of(call).Relation)))
			.Method("GetThreat", call => call.Return(api.Fixed(Of(call).Threat)))
			.Method("GetTarget", call => call.Return(api.Tag(Of(call).Other)))
			.Method("HasAlliance", call =>
			{
				CDiplomacyStatus status = Of(call);
				return call.Return(CCountryLists.HasAlliance(api.State, status.Holder, status.Other));
			})
			.Method("HasWar", call =>
			{
				CDiplomacyStatus status = Of(call);
				return call.Return(CCountryLists.AtWar(api.State, status.Holder, status.Other));
			})
			.Method("IsGuaranteed", call => call.Return(Guarantees(api, Of(call).Other, Of(call).Holder)))
			.Method("IsGuaranting", call => call.Return(Guarantees(api, Of(call).Holder, Of(call).Other)))
			.Method("IsFightingWarTogether", call =>
			{
				CDiplomacyStatus status = Of(call);
				return call.Return(CCountryLists.FightingTogether(api.State, status.Holder, status.Other));
			})
			.Method("HasFriendlyAgreement", call => call.Return(HasFriendlyAgreement(api, Of(call))))
			.Method("HasMilitaryAccess", call => call.Return(Of(call).HasMilitaryAccess))
			.Method("HasTruce", call => Unbuilt(api, call, TruceUnbuilt))
			.Method("AllowDebts", call => call.Return(Of(call).AllowDebts));
	}

	private static CDiplomacyStatus Of(CLuaCall call) => call.Object<CDiplomacyStatus>(1, Name);

	private static int Unbuilt(CGameApi api, CLuaCall call, string what)
	{
		Of(call);
		api.ReportUnbuilt(what);
		return call.Return(false);
	}

	private static bool Guarantees(CGameApi api, CCountryTag guarantor, CCountryTag guaranteed)
	{
		foreach (CGuarantee guarantee in api.State.Guarantees)
		{
			if (guarantee.Guarantor == guarantor && guarantee.Guaranteed == guaranteed)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>An alliance, military access, either the other's overlord, or a guarantee either way - in the original's order.</summary>
	private static bool HasFriendlyAgreement(CGameApi api, CDiplomacyStatus status)
	{
		if (CCountryLists.HasAlliance(api.State, status.Holder, status.Other))
		{
			return true;
		}
		if (status.HasMilitaryAccess)
		{
			return true;
		}
		CCountry? holder = api.State.Country(status.Holder);
		CCountry? other = api.State.Country(status.Other);
		if (holder?.Overlord == status.Other || other?.Overlord == status.Holder)
		{
			return true;
		}
		return CCountryLists.HasGuarantee(api.State, status.Holder, status.Other);
	}
}
