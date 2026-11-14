using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Movement;

/// <summary>
/// The message that a unit has arrived, as the original's <c>CUnit::AnnounceArrival</c> decides it: for a played country's
/// unit only, a land unit or a fleet wherever it arrives and an air unit only at its own base, and never for a fleet on
/// patrol, escorting convoys or raiding them, which arrive again and again. What it says is the client's
/// (<c>UNITARRIVED_LAND</c>, <c>_NAVAL</c> or <c>_AIR</c>). See <c>docs/mechanics/movement.md</c>, section 6.
/// </summary>
public static class CArrival
{
	/// <summary>The three message types, by the kind of unit.</summary>
	public const string Land = "UNITARRIVED_LAND";
	public const string Naval = "UNITARRIVED_NAVAL";
	public const string Air = "UNITARRIVED_AIR";

	/// <summary>Whether the unit's arrival is told to its player, its country being played.</summary>
	public static bool IsTold(CGameState state, CUnit unit)
	{
		if (!state.Played.Contains(unit.Owner))
		{
			return false;
		}
		if (unit.Kind == EUnitKind.Air && (unit.Base <= 0 || unit.Base != unit.Location))
		{
			return false;
		}
		return unit.Order is not (EUnitOrder.Patrol or EUnitOrder.ConvoyEscort or EUnitOrder.ConvoyRaid);
	}

	/// <summary>Tells the unit's arrival where it is told.</summary>
	public static void Announce(CGameState state, CUnit unit)
	{
		if (IsTold(state, unit))
		{
			state.RaiseUnitArrived(unit);
		}
	}

	/// <summary>The message type of the unit's arrival, by its kind.</summary>
	public static string TypeOf(CUnit unit)
	{
		return unit.Kind switch { EUnitKind.Navy => Naval, EUnitKind.Air => Air, _ => Land };
	}
}
