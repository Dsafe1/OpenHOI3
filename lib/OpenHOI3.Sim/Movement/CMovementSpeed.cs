using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Movement;

/// <summary>
/// How fast a unit moves: what an hour adds to its progress. The slowest of its
/// brigades sets the pace, a run of multipliers each cut to thousandths follows, and a
/// define scales the whole by the unit's kind. See <c>docs/mechanics/movement.md</c>,
/// section 5.
/// </summary>
public static class CMovementSpeed
{
	public const string MaximumSpeed = "maximum_speed";
	public const string IsMobile = "is_mobile";

	/// <summary>The key an adjuster holds what it does to movement under.</summary>
	public const string MovementAdjuster = "movement";

	/// <summary>The adjuster of a river, which a crossing through another province is counted as too.</summary>
	public const string RiverAdjuster = "river";

	public const string LocalUnitSpeed = "local_unit_speed";
	public const string MobileSpeed = "amm_movement_speed";

	public const string LandSpeedModifier = "LAND_SPEED_MODIFIER";
	public const string NavalSpeedModifier = "NAVAL_SPEED_MODIFIER";
	public const string AirSpeedModifier = "AIR_SPEED_MODIFIER";

	/// <summary>The share of its speed a unit that got no fuel keeps, and the share fuel gives back.</summary>
	private const long SpeedWithoutFuel = 100;
	private const long SpeedFromFuel = 900;

	private const long Scale = CFixedPoint.Scale;

	public static bool IsLand(CUnit unit)
	{
		return unit.Kind != EUnitKind.Navy && unit.Kind != EUnitKind.Air;
	}

	/// <summary>What an hour adds to the unit's progress.</summary>
	public static CFixedPoint Speed(CGameState state, CCountry country, CUnit unit)
	{
		return Speed(state, country, unit, state.Province(unit.Location),
			unit.IsMoving ? state.Province(unit.Path[0]) : null);
	}

	/// <summary>
	/// What an hour would add were the unit in one province and bound for another: the
	/// speed of a leg of its path it has not come to yet.
	/// </summary>
	public static CFixedPoint Speed(CGameState state, CCountry country, CUnit unit, CProvince here, CProvince? next)
	{
		long speed = SlowestBrigade(unit).Raw * Modifier(country, unit, here, next) / Scale;
		string define = unit.Kind switch
		{
			EUnitKind.Navy => NavalSpeedModifier,
			EUnitKind.Air => AirSpeedModifier,
			_ => LandSpeedModifier,
		};
		return new CFixedPoint(state.Defines[CManpower.MilitarySection, define].Raw * speed / Scale);
	}

	/// <summary>
	/// The unit's own top speed: the lowest <c>maximum_speed</c> among its brigades,
	/// ships or wings.
	/// </summary>
	public static CFixedPoint SlowestBrigade(CUnit unit)
	{
		bool any = false;
		CFixedPoint slowest = CFixedPoint.Zero;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			CFixedPoint speed = subUnit.Stats[MaximumSpeed];
			if (!any || speed < slowest)
			{
				slowest = speed;
				any = true;
			}
		}
		return slowest;
	}

	/// <summary>
	/// Everything the top speed is multiplied by, as thousandths where 1000 changes
	/// nothing. In the original's order, each step cut:
	///
	///     fuel - a land unit or a fleet that burns fuel keeps a tenth of its speed
	///     with none and all of it with its whole ration;
	///     the next province's terrain, by the unit's own adjuster for it;
	///     a river or a crossing on the way there, by its adjuster for rivers;
	///     on land, the province it is in: its local_unit_speed;
	///     on land, the country's amm_movement_speed times the share of its brigades
	///     that are mobile.
	///
	/// The original has four more that wait for what they read: a battle, a leader's
	/// trait for the terrain, the weather, and a retreat.
	/// </summary>
	public static long Modifier(CGameState state, CCountry country, CUnit unit)
	{
		return Modifier(country, unit, state.Province(unit.Location),
			unit.IsMoving ? state.Province(unit.Path[0]) : null);
	}

	/// <summary>The same for a unit in one province and bound for another, or for none.</summary>
	public static long Modifier(CCountry country, CUnit unit, CProvince here, CProvince? next)
	{
		long modifier = Scale;
		bool isLand = IsLand(unit);

		if (unit.Kind != EUnitKind.Air && BurnsFuel(unit))
		{
			modifier = modifier * (unit.FuelReceived.Raw * SpeedFromFuel / Scale + SpeedWithoutFuel) / Scale;
		}

		if (next is not null)
		{
			if (next.Terrain is { } terrain)
			{
				modifier = modifier * (Scale + Adjuster(unit, terrain.Name)) / Scale;
			}
			if (here.EdgeTo(next.Id) is { Kind: EProvinceEdgeKind.Crossing or EProvinceEdgeKind.River })
			{
				modifier = modifier * (Scale + Adjuster(unit, RiverAdjuster)) / Scale;
			}
		}

		if (isLand)
		{
			modifier = modifier * (Scale + here.Modifier[LocalUnitSpeed].Raw) / Scale;

			long share = MobileShare(unit);
			if (share > 0)
			{
				long bonus = country.Modifier[MobileSpeed].Raw * share / Scale;
				modifier = modifier * (Scale + bonus) / Scale;
			}
		}
		return modifier;
	}

	/// <summary>
	/// What a named circumstance does to the unit's movement: the sum of its brigades'
	/// figures over how many brigades it has, in thousandths.
	/// </summary>
	public static long Adjuster(CUnit unit, string name)
	{
		if (unit.SubUnits.Count == 0)
		{
			return 0;
		}
		long sum = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (subUnit.Stats.Adjusters.TryGetValue(name, out Modifiers.CModifier? adjuster))
			{
				sum += adjuster[MovementAdjuster].Raw;
			}
		}
		return sum / unit.SubUnits.Count;
	}

	/// <summary>The share of the unit's brigades that are mobile, in thousandths.</summary>
	public static long MobileShare(CUnit unit)
	{
		if (unit.SubUnits.Count == 0)
		{
			return 0;
		}
		long mobile = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (subUnit.Type.Flag(IsMobile))
			{
				mobile++;
			}
		}
		return mobile * Scale * Scale / (unit.SubUnits.Count * Scale);
	}

	private static bool BurnsFuel(CUnit unit)
	{
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (subUnit.Stats[CUnitSupply.FuelStatistic] > CFixedPoint.Zero)
			{
				return true;
			}
		}
		return false;
	}
}
