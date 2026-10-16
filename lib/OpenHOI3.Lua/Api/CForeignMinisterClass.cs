using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CAIForeignMinister</c>, which <c>ForeignMinister_Tick</c> and the decision script are handed: its country, its tag
/// and its AI; the decisions pass; the day's proposal; the proposed war; and how much of a country is occupied. See
/// <c>docs/mechanics/foreignminister.md</c>, sections 2, 3 and 7.
/// </summary>
public static class CForeignMinisterClass
{
	public const string Name = "CAIForeignMinister";

	public static CLuaClass Define(CGameApi api)
	{
		return new CLuaClass(Name)
			.Method("GetCountryTag", call => call.Return(api.Tag(Of(call).Tag)))
			.Method("GetCountry", call => call.Return(api.Of(Of(call))))
			.Method("GetOwnerAI", call => call.Return(new CLuaObject(api.Ai, new CLuaAi(Of(call)))))
			// A decision that failed ends the pass and the script that called it, as the original's error does.
			.Method("ExecuteDiploDecisions", call =>
			{
				if (CForeignAi.ExecuteDiploDecisions(api.State, Of(call)) is { } error)
				{
					throw new CLuaError(error);
				}
				return 0;
			})
			.Method("Propose", call =>
			{
				CCountry country = Of(call);
				CForeignAi.Propose(api.State, country, CAiClass.OfferOf(call, 2), (float)call.Number(3));
				return 0;
			})
			.Method("ProposeWar", call =>
			{
				CForeignAi.ProposeWar(Of(call), call.Object<CCountryTag>(2, "CCountryTag"), (float)call.Number(3));
				return 0;
			})
			.Method("ClearWarProposal", call =>
			{
				CForeignAi.ClearWarProposal(Of(call));
				return 0;
			})
			.Method("GetProposedWarTarget", call => call.Return(api.Tag(Of(call).ForeignMinister.ProposedWar)))
			.Method("PercOccupied", call => call.Return(CForeignAi.PercOccupied(api.State,
				call.Object<CCountryTag>(2, "CCountryTag"))));
	}

	private static CCountry Of(CLuaCall call) => call.Object<CLuaAi>(1, Name).Country;
}
