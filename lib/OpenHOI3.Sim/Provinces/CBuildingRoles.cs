using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// The five buildings the original knows by their role and not their name - the air base, the naval base, anti-aircraft,
/// industry and the underground: each is the last building in the file whose modifier is the role's, so a mod that moves
/// a role's modifier to another building moves the role with it. BlackICE's `air_base` carries no modifier and its
/// `fake_air_base` carries `air_capacity`: its air base is the second. See <c>docs/mechanics/buildinglines.md</c>, section 1.1, and
/// <c>reversing/findings/FINDINGS-aifields.md</c>, section 4.
/// </summary>
public static class CBuildingRoles
{
	/// <summary>Each role's name as Their Finest Hour's file gives the building, and the modifier that makes a building it.</summary>
	private static readonly (string Name, string Modifier)[] Roles =
	{
		("air_base", "air_capacity"),
		("naval_base", "naval_capacity"),
		("anti_air", "local_anti_air"),
		("industry", "ic"),
		("underground", "local_underground"),
	};

	/// <summary>
	/// The building a rule asking for <paramref name="key"/> means: for one of the five roles' names the building that has
	/// the role, otherwise the building of that name. Null where there is none.
	/// </summary>
	public static CBuilding? Find(CGameState state, string key) => Find(state.Buildings, key);

	/// <summary>The same over the buildings as the file declares them, for a reading done before there is a game.</summary>
	public static CBuilding? Find(IReadOnlyList<CBuilding> buildings, string key)
	{
		foreach ((string name, string modifier) in Roles)
		{
			if (name.Equals(key, StringComparison.OrdinalIgnoreCase))
			{
				CBuilding? role = null;
				foreach (CBuilding building in buildings)
				{
					if (building.ModifierKey.Equals(modifier, StringComparison.OrdinalIgnoreCase))
					{
						role = building;
					}
				}
				// Ours: a file in which no building carries the role's modifier falls back to the name; the original's
				// role would then be its built-in building of nothing, and no province would have one.
				if (role is not null)
				{
					return role;
				}
				break;
			}
		}
		foreach (CBuilding building in buildings)
		{
			if (building.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
			{
				return building;
			}
		}
		return null;
	}

	/// <summary>Whether a building has the role a rule asks for by <paramref name="key"/>.</summary>
	public static bool Is(CGameState state, CBuilding building, string key)
	{
		return ReferenceEquals(Find(state, key), building);
	}
}
