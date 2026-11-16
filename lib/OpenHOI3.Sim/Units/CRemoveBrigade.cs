using System;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// <c>remove_brigade</c>: the first brigade, ship or wing of the country whose name is the text, case and all, its units
/// taken in the order the original keeps them and each unit's brigades in theirs. With others in its unit it is taken out
/// at once and disposed of - a ship onto the list of the sunk with its carrier's air groups let go, anything else gone;
/// as its unit's only one it stays, and the unit is queued for the end of the hour (<see cref="CPendingUnitRemovals"/>).
/// So a second line of the same name the same hour finds the same brigade and queues the same unit again. Nothing is
/// given back: no officers, no men. See <c>docs/mechanics/effects.md</c>, section 9.
/// </summary>
public static class CRemoveBrigade
{
	public static void Run(CGameState state, CCountry? country, string name)
	{
		if (First(country, name) is not { Unit: { } unit } brigade)
		{
			return;
		}
		if (unit.SubUnits.Count == 1)
		{
			CPendingUnitRemovals.Queue(state, unit);
			return;
		}
		unit.SubUnits.Remove(brigade);
		brigade.Unit = null;
		if (unit.Kind == EUnitKind.Navy)
		{
			// The original's DisposeSubUnit: a ship is kept on the list and its air groups go to an air base of theirs.
			Transport.CCarriers.LetGo(state, brigade);
			CSunkShips.Add(state, unit.Owner, brigade);
		}
		state.ForgetPositions();
	}

	/// <summary>
	/// The first brigade of the country with exactly that name, its units taken in the order they were made - the walk
	/// <c>remove_brigade</c> and <c>brigade_exist</c> share.
	/// </summary>
	public static CSubUnit? First(CCountry? country, string name)
	{
		if (country is null)
		{
			return null;
		}
		foreach (CUnit unit in country.UnitsInOrder())
		{
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				if (brigade.Name.Equals(name, StringComparison.Ordinal))
				{
					return brigade;
				}
			}
		}
		return null;
	}
}
