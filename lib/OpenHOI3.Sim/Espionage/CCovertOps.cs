using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Espionage;

/// <summary>
/// Covert operations in a province, paid for with the covert-ops points a country's spies have earned about the
/// province's controller. See <c>docs/mechanics/espionage.md</c>, section 11.
/// </summary>
public static class CCovertOps
{
	/// <summary>
	/// Whether the province window lets the operation be chosen: a real province, the actor's whole points about its
	/// controller up to the cost, and its <c>is_selectable</c> holding where it says anything.
	/// </summary>
	public static bool MaySelect(CGameState state, CCountry actor, CProvince province, CCovertOperation operation)
	{
		if (province.Id == 0 || state.Country(province.Controller) is not { } controller)
		{
			return false;
		}
		if (CGameState.Presence(actor, controller).CovertOpsPoints / 1000 < operation.Cost)
		{
			return false;
		}
		return operation.IsSelectable.Children.Count == 0
			|| CConditions.Holds(state, operation.IsSelectable, CEventScope.Start(null, province.Id, actor.Tag));
	}

	/// <summary>The operation carried out: its cost off the points, with no floor, and its effect run on the province.</summary>
	public static void Send(CGameState state, CCountry actor, CProvince province, CCovertOperation operation)
	{
		if (state.Country(province.Controller) is { } controller)
		{
			CGameState.Presence(actor, controller).CovertOpsPoints -= operation.Cost * 1000L;
		}
		if (operation.Effects.Count > 0)
		{
			CEffects.Run(state, operation.Effects, CEventScope.Start(null, province.Id, actor.Tag));
		}
	}
}
