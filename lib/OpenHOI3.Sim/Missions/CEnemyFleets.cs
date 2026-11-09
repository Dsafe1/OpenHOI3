using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// The look an intercepting fleet and one on a sortie take at a sea province: an enemy
/// fleet there that they sight and that is no stronger than they are. See
/// <c>docs/mechanics/navalmissions.md</c>, section 1.
/// </summary>
public static class CEnemyFleets
{
	public const string SeaDefence = "sea_defence";

	/// <summary>What every ship is worth before its guns and its armour, in thousandths.</summary>
	private const long ShipWorth = 500;

	/// <summary>
	/// The fleet to go for in a sea province: of the fleets at war with this one's
	/// country that it sights and that have no more power than it has, the one with the
	/// most strength in all its ships; the first of equals. The sighting is asked from
	/// wherever the fleet is - from its own port, with no roll.
	/// </summary>
	public static CUnit? FindIn(CGameState state, CProvince province, CUnit unit)
	{
		if (!province.IsSea)
		{
			return null;
		}
		CUnit? best = null;
		foreach (CUnit other in state.UnitsIn(province.Id))
		{
			if (other.Kind != EUnitKind.Navy || other.IsRemoved || !CMovementRules.IsEnemyIn(state, other.Owner, unit.Owner, other.Location))
			{
				continue;
			}
			if (unit.SubUnits.Count == 0 || !CNavalSpotting.ShouldStart(state, unit, other)
				|| Power(other) > Power(unit))
			{
				continue;
			}
			if (best is null || Strength(other) > Strength(best))
			{
				best = other;
			}
		}
		return best;
	}

	/// <summary>The strength of every ship of a fleet added up, in thousandths.</summary>
	public static long Strength(CUnit unit)
	{
		long strength = 0;
		foreach (CSubUnit ship in unit.SubUnits)
		{
			strength += ship.Strength.Raw;
		}
		return strength;
	}

	/// <summary>
	/// What a fleet is worth to somebody weighing it up: for each ship a hundredth of its
	/// strength times a hundredth of its organisation times its type's worth, which for
	/// a ship is half a point and its <c>sea_attack</c> and <c>sea_defence</c>, times its
	/// hull.
	/// </summary>
	public static long Power(CUnit unit)
	{
		long power = 0;
		foreach (CSubUnit ship in unit.SubUnits)
		{
			long worth = (ShipWorth + ship.Stats[CNavalFire.SeaAttack].Raw + ship.Stats[SeaDefence].Raw)
				* ship.Stats[CNavalBattle.Hull].Raw / CFixedPoint.Scale;
			long strength = ship.Strength.Raw * CFixedPoint.Scale / 100000;
			long organisation = ship.Organisation.Raw * CFixedPoint.Scale / 100000;
			power += strength * worth / CFixedPoint.Scale * organisation / CFixedPoint.Scale;
		}
		return power;
	}
}
