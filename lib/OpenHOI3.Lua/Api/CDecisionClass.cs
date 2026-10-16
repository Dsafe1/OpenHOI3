using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CDecision</c>, a decision the foreign minister is asked about - its name, and its <c>potential</c> and its
/// <c>allow</c> in a scope - and <c>CEventScope</c>, the scope it is asked in, which a script only hands back. See
/// <c>docs/mechanics/foreignminister.md</c>, section 3.
/// </summary>
public static class CDecisionClass
{
	public const string DecisionName = "CDecision";

	public const string ScopeName = "CEventScope";

	public static CLuaClass DefineDecision(CGameApi api)
	{
		return new CLuaClass(DecisionName)
			.Method("GetKey", call => call.Return(api.String(call.Object<CDecision>(1, DecisionName).Name)))
			.Method("IsPotential", call => call.Return(CConditions.Holds(api.State,
				call.Object<CDecision>(1, DecisionName).Potential, call.Object<CEventScope>(2, ScopeName))))
			.Method("IsAllowed", call => call.Return(CConditions.Holds(api.State,
				call.Object<CDecision>(1, DecisionName).Allow, call.Object<CEventScope>(2, ScopeName))));
	}

	public static CLuaClass DefineScope() => new(ScopeName);
}
