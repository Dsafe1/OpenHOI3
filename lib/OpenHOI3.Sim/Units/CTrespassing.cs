using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// The original's trespassing mark: a unit standing where its country may no longer go - ground it walked onto as a
/// friend's, by a pact or by access since ended, or ground that has changed hands under it. The original keeps the
/// mark on the unit and makes it again in every unit's hour, at a change between two countries and where a province
/// changes hands; here it is asked as the unit stands, which is the same answer. Never a rebel unit's. A trespasser
/// starts no battle and joins none as the one asking, is fought only in a war, takes no province, is not slowed for
/// want of fuel, and may walk on through land of the owner of the ground it stands on. Nothing moves it of itself;
/// the AI brings its own home. See <c>docs/mechanics/war.md</c>, section 16.
/// </summary>
public static class CTrespassing
{
	public static bool Is(CGameState state, CUnit unit)
	{
		return unit.Location > 0 && !Revolts.CRebelFactions.IsRebels(unit.Owner) && state.Country(unit.Owner) is { } country
			&& !CMovementRules.MayEnter(state, country, state.Province(unit.Location));
	}

	/// <summary>
	/// The trespass half of the original's test of whether one unit may fight another: the one asking never while it
	/// trespasses, and a trespasser fought only in a war between the two - declared, or undeclared and covering the
	/// province the trespasser stands in - so not by the rebels, who are at war with nobody by any war.
	/// </summary>
	public static bool AllowsFight(CGameState state, CUnit asking, CUnit other)
	{
		if (Is(state, asking))
		{
			return false;
		}
		return !Is(state, other) || CCountryLists.AtWar(state, asking.Owner, other.Owner)
			|| Wars.CUndeclaredWars.Covers(state, asking.Owner, other.Owner, other.Location);
	}

	/// <summary>The same for a unit whose country is at hand.</summary>
	public static bool Is(CGameState state, CCountry country, CUnit unit)
	{
		return unit.Location > 0 && !Revolts.CRebelFactions.IsRebels(country.Tag)
			&& !CMovementRules.MayEnter(state, country, state.Province(unit.Location));
	}
}
