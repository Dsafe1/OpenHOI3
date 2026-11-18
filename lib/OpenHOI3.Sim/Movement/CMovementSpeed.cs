using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Weather;
using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Sim.Movement;

/// <summary>
/// How fast a unit moves: what an hour adds to its progress. The slowest of its
/// brigades sets the pace, a run of multipliers each cut to thousandths follows, and a
/// define scales the whole by the unit's kind. See <c>docs/mechanics/movement.md</c>,
/// section 5.
/// </summary>
public static class CMovementSpeed
{
	/// <summary>The key of <c>"infrastructure"</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey InfrastructureKey = CModifierKey.Of("infrastructure");

	/// <summary>The key of <c>"local_infrastructure"</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey LocalInfrastructureKey = CModifierKey.Of("local_infrastructure");

	/// <summary>The key of <c>"global_infrastructure"</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey GlobalInfrastructureKey = CModifierKey.Of("global_infrastructure");

	/// <summary>The key of <c>MaximumSpeed</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey MaximumSpeedKey = CModifierKey.Of(MaximumSpeed);

	/// <summary>The key of <c>LocalUnitSpeed</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey LocalUnitSpeedKey = CModifierKey.Of(LocalUnitSpeed);

	/// <summary>The key of <c>MobileSpeed</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey MobileSpeedKey = CModifierKey.Of(MobileSpeed);

	/// <summary>The key of <c>MovementAdjuster</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey MovementAdjusterKey = CModifierKey.Of(MovementAdjuster);

	/// <summary>The key of <c>CUnitSupply.FuelStatistic</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey FuelStatisticKey = CModifierKey.Of(CUnitSupply.FuelStatistic);

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

	/// <summary>The share of its speed an attacker walks on at while it fights.</summary>
	public const string CombatMovementSpeed = "COMBAT_MOVEMENT_SPEED";

	/// <summary>The least share of its speed a unit in a battle keeps, in thousandths.</summary>
	private const long SlowestInBattle = 10;

	/// <summary>A unit falling back walks this much faster, in thousandths.</summary>
	private const long RetreatHaste = 100;

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
		// A land unit going ashore from the sea moves at the landing's speed and nothing
		// else touches it (transport.md, section 8).
		if (IsLand(unit) && here.IsSea && next is { IsSea: false })
		{
			return Transport.CLanding.Speed(state, unit);
		}
		long speed = IsLand(unit) && unit.Order == EUnitOrder.StrategicRedeployment
			? Redeployment(state, here)
			: SlowestBrigade(unit).Raw * Modifier(state, country, unit, here, next) / Scale;
		string define = unit.Kind switch
		{
			EUnitKind.Navy => NavalSpeedModifier,
			EUnitKind.Air => AirSpeedModifier,
			_ => LandSpeedModifier,
		};
		return new CFixedPoint(state.Defines[CManpower.MilitarySection, define].Raw * speed / Scale);
	}

	/// <summary>
	/// A strategic redeployment's speed: <c>STRAT_REDEP_BASE_SPEED</c> by the province's infrastructure, its
	/// infrastructure modifier raised by the local and global ones, held between a hundredth and a whole
	/// (<c>movement.md</c>, section 5.1).
	/// </summary>
	private static long Redeployment(CGameState state, CProvince here)
	{
		long infrastructure = here.Modifier[InfrastructureKey].Raw
			* (Scale + here.Modifier[LocalInfrastructureKey].Raw + here.Modifier[GlobalInfrastructureKey].Raw) / Scale;
		infrastructure = System.Math.Clamp(infrastructure, 10, Scale);
		return state.Defines[CManpower.MilitarySection, StratRedeployBaseSpeed].Raw * infrastructure / Scale;
	}

	public const string StratRedeployBaseSpeed = "STRAT_REDEP_BASE_SPEED";

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
			CFixedPoint speed = subUnit.Stats[MaximumSpeedKey];
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
	///     on land, the province it is in: its local_unit_speed and what its weather
	///     does, the two added and not multiplied;
	///     on land, the country's amm_movement_speed times the share of its brigades
	///     that are mobile.
	///
	/// Before all of those, a unit in a battle walks at a fraction of its speed - the
	/// combat movement speed for an attacker, a hundredth for anyone else - and after
	/// them a unit falling back is a tenth faster, and is not slowed for want of fuel.
	/// The leaders' traits come into two of them: combat_move_speed into an attacker's
	/// pace in a battle, and terrain_speed for the next province's ground.
	/// </summary>
	public static long Modifier(CGameState state, CCountry country, CUnit unit)
	{
		return Modifier(state, country, unit, state.Province(unit.Location),
			unit.IsMoving ? state.Province(unit.Path[0]) : null);
	}

	/// <summary>The same for a unit in one province and bound for another, or for none.</summary>
	public static long Modifier(CGameState state, CCountry country, CUnit unit, CProvince here, CProvince? next)
	{
		long modifier = Scale;
		bool isLand = IsLand(unit);

		// In a battle a unit walks on at a fraction of its speed where it is attacking,
		// and all but stands where it is being attacked.
		if (unit.IsFighting && unit.IsMoving)
		{
			long inBattle = SlowestInBattle;
			foreach (CCombat combat in unit.Combats)
			{
				if (combat.Attacker.Units.Contains(unit))
				{
					long quicker = CAttackDelay.Quicker(state, country, unit);
					long pace = state.Defines[CManpower.MilitarySection, CombatMovementSpeed].Raw + combat.TacticSpeed.Raw;
					inBattle = System.Math.Min(Scale, System.Math.Max(SlowestInBattle, quicker * pace / Scale));
					break;
				}
			}
			modifier = modifier * inBattle / Scale;
		}

		if (unit.Kind != EUnitKind.Air && !unit.IsRetreating && BurnsFuel(unit))
		{
			modifier = modifier * (unit.FuelReceived.Raw * SpeedFromFuel / Scale + SpeedWithoutFuel) / Scale;
		}

		if (next is not null)
		{
			if (next.Terrain is { } terrain)
			{
				modifier = modifier * (Scale + Adjuster(unit, terrain.Name)) / Scale;
				// The leaders' terrain_speed for that ground, down the chain of command.
				modifier = modifier * (Scale + CCommandReach.TerrainEffect(state, unit, ETraitEffect.TerrainSpeed, terrain.Name)) / Scale;
			}
			if (here.EdgeTo(next.Id) is { Kind: EProvinceEdgeKind.Crossing or EProvinceEdgeKind.River })
			{
				modifier = modifier * (Scale + Adjuster(unit, RiverAdjuster)) / Scale;
			}
		}

		if (isLand)
		{
			long weather = CWeatherEffects.Movement(state, here.Weather, country);
			modifier = modifier * (Scale + weather + here.Modifier[LocalUnitSpeedKey].Raw) / Scale;

			long share = MobileShare(unit);
			if (share > 0)
			{
				long bonus = country.Modifier[MobileSpeedKey].Raw * share / Scale;
				modifier = modifier * (Scale + bonus) / Scale;
			}
		}
		if (unit.IsRetreating)
		{
			modifier = modifier * (Scale + RetreatHaste) / Scale;
		}
		return modifier;
	}

	/// <summary>
	/// The terms the speed tooltip writes, each worked out again by itself as the original's tooltip does it - which is
	/// not quite the modifier's way: in a battle a unit on no attacking side is given the whole of the quickness over
	/// one, held at one, where the modifier gives it a hundredth; and the weather and the province's own
	/// <c>local_unit_speed</c> are two terms where the modifier adds them into one.
	/// </summary>
	public static CSpeedTerms Terms(CGameState state, CCountry country, CUnit unit)
	{
		CProvince here = state.Province(unit.Location);
		CProvince? next = unit.IsMoving ? state.Province(unit.Path[0]) : null;
		long combat = Scale;
		if (unit.IsFighting && unit.IsMoving)
		{
			long pace = Scale;
			foreach (CCombat battle in unit.Combats)
			{
				if (battle.Attacker.Units.Contains(unit))
				{
					pace = state.Defines[CManpower.MilitarySection, CombatMovementSpeed].Raw + battle.TacticSpeed.Raw;
					break;
				}
			}
			combat = System.Math.Max(SlowestInBattle, System.Math.Min(Scale, CAttackDelay.Quicker(state, country, unit) * pace / Scale));
		}
		long fuel = unit.Kind != EUnitKind.Air && !unit.IsRetreating && BurnsFuel(unit)
			? unit.FuelReceived.Raw * SpeedFromFuel / Scale + SpeedWithoutFuel
			: Scale;
		long terrain = Scale, trait = Scale, adjacency = Scale;
		if (next is not null)
		{
			if (next.Terrain is { } ground)
			{
				terrain = Scale + Adjuster(unit, ground.Name);
				trait = Scale + CCommandReach.TerrainEffect(state, unit, ETraitEffect.TerrainSpeed, ground.Name);
			}
			if (here.EdgeTo(next.Id) is { Kind: EProvinceEdgeKind.Crossing or EProvinceEdgeKind.River })
			{
				adjacency = Scale + Adjuster(unit, RiverAdjuster);
			}
		}
		long weather = Scale, local = Scale, mobile = Scale;
		if (IsLand(unit))
		{
			weather = Scale + CWeatherEffects.Movement(state, here.Weather, country);
			local = Scale + here.Modifier[LocalUnitSpeedKey].Raw;
			long share = MobileShare(unit);
			if (share > 0)
			{
				mobile = Scale + country.Modifier[MobileSpeedKey].Raw * share / Scale;
			}
		}
		long retreating = unit.IsRetreating ? Scale + RetreatHaste : Scale;
		return new CSpeedTerms(combat, fuel, terrain, trait, adjacency, weather, local, mobile, retreating);
	}

	/// <summary>
	/// What a named circumstance does to the unit's movement: the sum of the figures of its
	/// brigades that count towards it over how many brigades it has, all of them, in
	/// thousandths (<c>CUnits.CountsTowardsItsUnit</c>).
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
			if (CUnits.CountsTowardsItsUnit(unit, subUnit) && subUnit.Stats.Adjusters.TryGetValue(name, out Modifiers.CModifier? adjuster))
			{
				sum += adjuster[MovementAdjusterKey].Raw;
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
			if (subUnit.Stats[FuelStatisticKey] > CFixedPoint.Zero)
			{
				return true;
			}
		}
		return false;
	}
}
