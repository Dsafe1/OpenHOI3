using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// How well the player knows a unit, 0 to 9, which is what its counter shows of it: 9 for the player's own units and its
/// friends' - a country of the same real faction, or one with an alliance with it - and otherwise what the player knows of
/// the province the unit stands in. With nobody playing, everything is known. See <c>docs/mechanics/mapunits.md</c>,
/// section 3, and <c>reversing/findings/FINDINGS-counters.md</c>, section 5.
/// </summary>
public static class CUnitIntel
{
	/// <summary>Full knowledge.</summary>
	public const int Full = 9;

	/// <summary>Under this a stack is not drawn at all.</summary>
	public const int StackShown = 2;

	/// <summary>Under this a stack draws one counter, and that with the unknown picture.</summary>
	public const int TypeShown = 3;

	/// <summary>Under this the name is <c>???</c>.</summary>
	public const int NameShown = 5;

	/// <summary>Under this the two figures are <c>?</c>.</summary>
	public const int FiguresShown = 7;

	/// <summary>The type key a counter shows a unit it knows too little of by: <c>GFX_small_counter_unknown</c> and the near one.</summary>
	public const string UnknownType = "unknown";

	/// <summary>The name a counter shows a unit it knows too little of by.</summary>
	public const string UnknownName = "???";

	public static int Level(CGameState state, CUnit unit, CCountryTag? player)
	{
		if (player is not { } us || IsSeenAsOwn(state, unit, us))
		{
			return Full;
		}
		return unit.Location > 0 && unit.Location < state.Provinces.Count
			? CProvinceIntel.Level(state, state.Provinces[unit.Location], us)
			: 0;
	}

	/// <summary>Whether the player sees a unit as its own: its own, or its owner's real faction is the player's, or the two are allied.</summary>
	public static bool IsSeenAsOwn(CGameState state, CUnit unit, CCountryTag player)
	{
		if (unit.Owner == player)
		{
			return true;
		}
		if (state.Country(unit.Owner) is not { } owner || state.Country(player) is not { } us)
		{
			return false;
		}
		return (owner.Faction.Length > 0 && owner.Faction == us.Faction) || CCountryLists.HasAlliance(state, owner.Tag, us.Tag);
	}
}
