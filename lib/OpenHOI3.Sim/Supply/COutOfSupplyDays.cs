using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Supply;

/// <summary>
/// The days in a row a province has drawn on no depot, counted where the original gives a province its depot: one more
/// where it draws on none and nought where it draws on one - each day in the province's own day, on a change of its
/// controller, and once for each province as a game is entered; and on the first day out, in an area with no working
/// port, <c>on_encirclement</c>. See <c>docs/mechanics/supply.md</c>, section 18.
/// </summary>
public static class COutOfSupplyDays
{
	/// <summary>The province's count made again: one more where it draws on no depot, else nought.</summary>
	private static bool Count(CSupplyNetwork network, CProvince province)
	{
		bool none = network.DepotOf(province.Id) == 0;
		province.OutOfSupplyDays = none ? province.OutOfSupplyDays + 1 : 0;
		return none;
	}

	/// <summary>
	/// The province's day: its count, and on the first day out, where its area has no working port, the on_action - a
	/// province counted on a change of hands earlier in the day is past one by now and fires none.
	/// </summary>
	public static void RunDaily(CGameState state, CProvince province)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		if (Count(network, province) && province.OutOfSupplyDays == 1 && network.AreaOf(province.Id) is { IsLandlocked: true })
		{
			Events.COnActions.Encirclement(state, province.Id);
		}
	}

	/// <summary>
	/// The provinces whose controller changed in the hour, counted as the original counts each at the change - here at the
	/// hour's end, from the network the hour left, so that none is left waiting between hours.
	/// </summary>
	public static void CountChangesOfHands(CGameState state)
	{
		CSupplyNetwork? network = null;
		foreach (CProvince province in state.Provinces)
		{
			if (!province.ControllerChanged)
			{
				continue;
			}
			network ??= CSupplyNetworks.Refresh(state);
			province.ControllerChanged = false;
			Count(network, province);
		}
	}

	/// <summary>
	/// A game entered: every province counted once more, as the original's entering gives a depot to each province still
	/// without one - after a load every province, none being saved. A new game's history has counted each of its changes
	/// of controller already, as the loader's replay of the province history made it. Every change of hands the making
	/// left marked is forgotten.
	/// </summary>
	public static void Enter(CGameState state)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state, force: true);
		foreach (CProvince province in state.Provinces)
		{
			province.ControllerChanged = false;
			if (province.Id == 0)
			{
				continue;
			}
			Count(network, province);
		}
	}
}
