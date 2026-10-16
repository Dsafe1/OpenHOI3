using System.Collections.Generic;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// The diplomatic actions the foreign minister's scripts make, by the original's class names: each made with
/// <c>Name(actor, recipient)</c> or <c>Name.Create(actor, recipient)</c> - the call to arms with the enemy third - and
/// each with the base's methods: its value, whether it is valid and may be pressed, as its own kind answers, and what the
/// recipient's AI makes of it. Posted by <c>CAI:PostAction</c> or proposed by <c>CAIForeignMinister:Propose</c>. See
/// <c>docs/mechanics/foreignminister.md</c>, section 3.
/// </summary>
public static class CDiplomaticActionClass
{
	/// <summary>Each class the scripts make, and the kind of offer it is.</summary>
	public static readonly IReadOnlyList<(string Name, EOfferKind Kind)> Kinds = new[]
	{
		("CAllianceAction", EOfferKind.Alliance),
		("CNapAction", EOfferKind.NonAggressionPact),
		("CGuaranteeAction", EOfferKind.Guarantee),
		("CMilitaryAccessAction", EOfferKind.AskMilitaryAccess),
		("COfferMilitaryAccessAction", EOfferKind.OfferMilitaryAccess),
		("CEmbargoAction", EOfferKind.Embargo),
		("CInfluenceNation", EOfferKind.Influence),
		("CInfluenceAllianceLeader", EOfferKind.Align),
		("CDebtAction", EOfferKind.Debt),
		("CRequestLendLeaseAction", EOfferKind.RequestLendLease),
		("COfferLendLeaseAction", EOfferKind.OfferLendLease),
		("CFactionAction", EOfferKind.Faction),
		("CCallAllyAction", EOfferKind.CallAlly),
	};

	public static CLuaClass Define(CGameApi api, string name, EOfferKind kind)
	{
		CLuaClass luaClass = new CLuaClass(name)
			.Method("SetValue", call =>
			{
				Of(call, name).Value = call.Boolean(2);
				return 0;
			})
			.Method("GetValue", call => call.Return(Of(call, name).Value))
			// The stage, 0 for proposed, which every action a script makes is.
			.Method("GetType", call =>
			{
				Of(call, name);
				return call.Return(0);
			})
			.Method("IsValid", call => call.Return(COfferRules.IsValid(api.State, Of(call, name).Offer)))
			.Method("IsSelectable", call => call.Return(COfferRules.IsSelectable(api.State, Of(call, name).Offer)))
			.Method("GetAIAcceptance", call => call.Return(CPactPasses.Score(api.State, Of(call, name).Offer, api.Scripts)))
			.Method("GetActor", call => call.Return(api.Tag(Of(call, name).Actor)))
			.Method("GetRecipient", call => call.Return(api.Tag(Of(call, name).Recipient)));
		if (kind == EOfferKind.CallAlly)
		{
			luaClass
				.Method("GetVersus", call => call.Return(api.Tag(Of(call, name).Versus)))
				.Method("SetVersus", call =>
				{
					Of(call, name).Versus = call.Object<CCountryTag>(2, "CCountryTag");
					return 0;
				});
		}
		// Made with the class called, its table first, or with Create, which has none.
		luaClass.Static("Create", call => Make(api, call, luaClass, kind, 1));
		return luaClass.Constructs(call => Make(api, call, luaClass, kind, 2));
	}

	private static int Make(CGameApi api, CLuaCall call, CLuaClass luaClass, EOfferKind kind, int first)
	{
		CCountryTag actor = call.Object<CCountryTag>(first, "CCountryTag");
		CCountryTag recipient = call.Object<CCountryTag>(first + 1, "CCountryTag");
		var action = new CLuaDiplomaticAction(kind, actor, recipient, api.State.TotalHours);
		if (kind == EOfferKind.CallAlly)
		{
			action.Versus = call.Object<CCountryTag>(first + 2, "CCountryTag");
		}
		return call.Return(new CLuaObject(luaClass, action));
	}

	private static CLuaDiplomaticAction Of(CLuaCall call, string name) => call.Object<CLuaDiplomaticAction>(1, name);
}
