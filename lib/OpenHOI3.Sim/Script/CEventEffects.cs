using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Events;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// <c>country_event = N</c>: another event fired at once for the scope's country, where its own trigger holds. See
/// <c>docs/mechanics/effects.md</c>, section 9.
/// </summary>
public static class CEventEffects
{
	/// <summary>The thirty bits of a product a fired event's scope is seeded with.</summary>
	private const int SeedMask = 0x3FFFFFFF;

	public static bool TryRun(CGameState state, CEffect effect, CEventScope scope)
	{
		if (effect.Keyword != "country_event")
		{
			return false;
		}
		// A fresh scope: the scope's country, FROM the root's country - or, where the root has none, its province - and a
		// state of chance from two steps of the scope's own, which the scope keeps having taken.
		uint seed = (uint)(scope.NextProduct() & SeedMask);
		CEventScope root = scope.Root;
		CCountryTag? from = root.Country;
		CEventScope fired = CEventScope.Start(scope.Country, 0, fromCountry: from,
			fromProvince: from is null ? root.Province : 0, randomState: seed);
		int id = (int)effect.Argument.Amount.ToInt();
		foreach (CEvent candidate in state.Events)
		{
			if (candidate.Id != id)
			{
				continue;
			}
			// Only where the event's own trigger holds; one that does not, or an id no event has, does nothing.
			if (CConditions.Holds(state, candidate.Trigger, fired))
			{
				CEventFiring.Fire(state, candidate, fired);
			}
			break;
		}
		return true;
	}
}
