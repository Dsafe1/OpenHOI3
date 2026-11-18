using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// What a plan wants built and what it is short of - the original's <c>ComputePlanWantedSubUnits</c> and <c>CUnit::UpdatePlanForceNeeds</c>. The theatres screen
/// asks here as it is filled (<c>docs/mechanics/theatresscreen.md</c>, section 6); and once a day a theatre whose plan is not active has its powers and its
/// <c>min</c> written from here, as the original's daily unit update writes them - which gives a small AI country's first power its post when its agent binds
/// (<see cref="Refresh"/>; <c>reversing/findings/FINDINGS-liveread.md</c>, section 76).
/// A shortage is taken off twice, and one mountain and one garrison step are taken with no call for them: the original's, kept.
/// </summary>
public static class CPlanWants
{
	private const long Whole = 1000;
	private const long AtWarScale = 1000;
	private const long PeaceScale = 200;
	private const long LeastScale = 100;
	private const long OneAndAHalf = 1500;
	private const int HoldScoreCounted = 25;
	private const long RoleUnit = 3;
	private const string Mountain = "mountain";
	private const string Democracy = "democracy";
	private const string TacticalBomber = "tactical_bomber";
	private const string Interceptor = "interceptor";
	private const string InfantryKey = "infantry_brigade";

	/// <summary>
	/// Every headquarters' day: <see cref="Refresh"/> for each, as the original's <c>CUnit::UpdateDaily</c> does at midnight.
	/// </summary>
	public static void RefreshDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.UnitsInOrder())
			{
				if (!unit.IsRemoved)
				{
					Refresh(state, unit);
				}
			}
		}
	}

	/// <summary>
	/// The original's <c>CUnit::RefreshPlanForceNeeds</c> and <c>UpdatePlanForceNeeds</c> as far as they change the game: a theatre,
	/// or a headquarters with an agent, whose chain's top is not a theatre with an active plan has its own plan's powers and
	/// <c>min</c> made again from the whole order of battle under it, with no command. Run by the day and by the commands that
	/// set a plan's area, its forces or its stance - so an AI theatre's plan, its area set before it is switched on, holds its
	/// order of battle's figure when the first power update asks it, and that update posts the small figure it works out
	/// (<c>reversing/findings/FINDINGS-liveread.md</c>, section 76).
	/// </summary>
	public static void Refresh(CGameState state, CUnit unit)
	{
		if (unit.Kind is EUnitKind.Division or EUnitKind.Navy or EUnitKind.Air
			|| (unit.Kind != EUnitKind.Theatre && unit.Agent is null))
		{
			return;
		}
		CUnit top = unit;
		while (top.Parent is not null)
		{
			top = top.Parent;
		}
		if (top.Plan.Active && top.Kind == EUnitKind.Theatre)
		{
			return;
		}
		CPlanWanted wanted = Compute(state, unit, null);
		unit.Plan.OurPower = wanted.OurPower;
		unit.Plan.TheirPower = wanted.TheirPower;
		unit.Plan.Min.Clear();
		unit.Plan.Min.AddRange(wanted.Wanted);
	}

	/// <summary>Whether the top of the unit's chain has an active plan, which leaves the plan's own figures standing.</summary>
	public static bool IsAiPlan(CUnit unit)
	{
		CUnit top = unit;
		while (top.Parent is not null)
		{
			top = top.Parent;
		}
		return top.Plan.Active && top.Kind == EUnitKind.Theatre;
	}

	/// <summary>
	/// What the plan is short of by type, each entry the positive part of wanted less had, lower types first and held to what its group is still short of.
	/// Given provinces, it is asked of those alone; a top plan that is active answers from its own figures either way. Null where nothing is wanted.
	/// </summary>
	public static int[]? Needed(CGameState state, CUnit unit, IReadOnlyList<int>? provinces)
	{
		int size = state.SubUnitTypes.Count + 1;
		int[] wanted = IsAiPlan(unit) ? PaddedMin(unit.Plan, size) : Compute(state, unit, provinces).Wanted;
		if (wanted.Length == 0)
		{
			return null;
		}
		var have = new int[size];
		Count(unit, have);
		var byIndex = new CSubUnitType?[size];
		foreach (CSubUnitType type in state.SubUnitTypes.Values)
		{
			byIndex[type.DatabaseIndex] = type;
		}
		var difference = new int[size];
		var group = new int[size];
		for (int index = 0; index < size; index++)
		{
			difference[index] = wanted[index] - have[index];
			if (difference[index] != 0 && byIndex[index] is { } type)
			{
				group[GroupOf(state, type).DatabaseIndex] += difference[index];
			}
		}
		var needed = new int[size];
		bool any = false;
		for (int index = 0; index < size; index++)
		{
			if (difference[index] <= 0 || byIndex[index] is not { } type)
			{
				continue;
			}
			int representative = GroupOf(state, type).DatabaseIndex;
			int taken = Math.Min(difference[index], group[representative]);
			if (taken > 0)
			{
				needed[index] = taken;
				group[representative] -= taken;
				any = true;
			}
		}
		return any ? needed : new int[size];
	}

	/// <summary>
	/// A plan's want less what it has, summed onto each group's representative type: what the unit panel's desired assets row names.
	/// All noughts where nothing is wanted.
	/// </summary>
	public static int[] GroupDifference(CGameState state, CUnit unit)
	{
		int size = state.SubUnitTypes.Count + 1;
		var group = new int[size];
		int[] wanted = IsAiPlan(unit) ? PaddedMin(unit.Plan, size) : Compute(state, unit, null).Wanted;
		if (wanted.Length == 0)
		{
			return group;
		}
		var have = new int[size];
		Count(unit, have);
		foreach (CSubUnitType type in state.SubUnitTypes.Values)
		{
			group[GroupOf(state, type).DatabaseIndex] += wanted[type.DatabaseIndex] - have[type.DatabaseIndex];
		}
		return group;
	}

	private static int[] PaddedMin(CUnitPlan plan, int size)
	{
		var min = new int[size];
		for (int index = 0; index < size && index < plan.Min.Count; index++)
		{
			min[index] = plan.Min[index];
		}
		return min;
	}

	private static void Count(CUnit unit, int[] have)
	{
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			have[brigade.Type.DatabaseIndex]++;
		}
		foreach (CUnit child in unit.Children)
		{
			Count(child, have);
		}
	}

	/// <summary>
	/// The representative of a type's group, which a surplus of one type cancels a shortage of another within. Worked out
	/// once a type a game: it asks only the type's own figures, flags and key and the types there are, none of which change.
	/// </summary>
	public static CSubUnitType GroupOf(CGameState state, CSubUnitType type)
	{
		// Asked side by side from the AI's workers: two that find one type's group at once find the same.
		state.PlanGroups ??= new System.Collections.Concurrent.ConcurrentDictionary<CSubUnitType, CSubUnitType>();
		return state.PlanGroups.GetOrAdd(type, FindGroup, state);
	}

	private static CSubUnitType FindGroup(CSubUnitType type, CGameState state)
	{
		CSubUnitType ByKey(string key) => state.SubUnitTypes.TryGetValue(key, out CSubUnitType? found) ? found : type;
		if (type.Branch == EUnitBranch.Naval)
		{
			if (type.Stats[Combat.CShipCensus.CarrierSize].Raw > 0)
			{
				return ByKey("carrier");
			}
			if (type.Key == "heavy_cruiser")
			{
				return type;
			}
			if (type.Flag(Combat.CShipCensus.CapitalFlag))
			{
				return ByKey("battleship");
			}
			return type.Flag(Combat.CShipCensus.TransportFlag) || type.Flag(Combat.CShipCensus.SubmarineFlag) ? type : ByKey("destroyer");
		}
		if (type.Branch != EUnitBranch.Land || type.Stats[CSubUnitType.CombatWidth].Raw <= 0 || type.Key == "hq_brigade")
		{
			return type;
		}
		if (CArmyRole.Of(type) == CArmyRole.Armour)
		{
			return ByKey("armor_brigade");
		}
		return type.Flag("can_paradrop") || type.Key is "bergsjaeger_brigade" or "marine_brigade" ? type : ByKey(InfantryKey);
	}

	/// <summary>The first of the keys the country can use; the last where it can use none, as the chains all end in infantry.</summary>
	private static CSubUnitType Chain(CGameState state, CCountry country, params string[] keys)
	{
		CSubUnitType? last = null;
		foreach (string key in keys)
		{
			if (!state.SubUnitTypes.TryGetValue(key, out CSubUnitType? type))
			{
				continue;
			}
			last = type;
			if (CUnitAvailability.IsUnlocked(state, country, type) && CUnitAvailability.IsUsableBy(country, type))
			{
				return type;
			}
		}
		return last ?? state.SubUnitTypes.Values.First();
	}

	/// <summary>The whole calculation. With no provinces it is the plan's own area and the unit's level that set its size.</summary>
	public static CPlanWanted Compute(CGameState state, CUnit unit, IReadOnlyList<int>? provinces)
	{
		var wanted = new int[state.SubUnitTypes.Count + 1];
		if (state.Country(unit.Owner) is not { } country)
		{
			return new CPlanWanted(wanted, 0, 0);
		}
		bool useArea = provinces is not null;
		IReadOnlyList<int> area = provinces ?? unit.Plan.Area;
		CSubUnitType armour = Chain(state, country, "armor_brigade", "light_armor_brigade", "mechanized_brigade", "motorized_brigade", InfantryKey);
		CSubUnitType fast = Chain(state, country, "mechanized_brigade", "motorized_brigade", "cavalry_brigade", InfantryKey);
		CSubUnitType mountain = Chain(state, country, "bergsjaeger_brigade", InfantryKey);
		CSubUnitType garrison = Chain(state, country, "garrison_brigade", InfantryKey);
		CSubUnitType infantry = Chain(state, country, InfantryKey);

		var own = new Roles();
		long our = 0;
		foreach (CSubUnit brigade in Collect(state, unit, useArea ? area : null))
		{
			Tally(brigade, own);
			// Over the brigade's own maxima, as the original's slot 12 and organisation ceiling give them: the reserve penalty and
			// the commander's bonus counted - a reserve brigade at its reduced strength is whole (FINDINGS-liveread.md, section 76).
			our += CUnitPower.Value(brigade) * Ratio(brigade.Strength.Raw, CUnits.MaxStrength(state, country, brigade).Raw) / Whole
				* Ratio(brigade.Organisation.Raw, COrganisation.Ceiling(state, country, brigade).Raw) / Whole;
		}

		(Roles enemy, long their, int mountainEnemy) = Census(state, country, area);
		int mountainOwn = area.Count(id => IsMountain(state.Province(id)));
		long garrisonTarget = GarrisonTarget(state, country, unit, area, useArea);
		long scale = Scale(state, country, useArea);
		long stance = unit.Plan.Stance switch { 2 => 1500, 3 => 2000, 4 => 2500, _ => Whole };

		long lowE = enemy.Land * stance / Whole;
		long lowA = enemy.Armour * stance / Whole;
		long lowF = enemy.Fast * stance / Whole;
		long factor = OneAndAHalf * stance / Whole;
		long highE = lowE * factor / Whole;
		long highA = lowA * factor / Whole;
		long highF = lowF * factor / Whole;

		long size = BaseSize(state, country, unit, area, useArea, scale);
		long high = Math.Max(size, highE);
		long low = Math.Max(size / 2, lowE);
		bool armourFound = armour != infantry;
		long highArmour = Math.Max(armourFound ? high / 5 : 0, highA);
		long lowArmour = Math.Max(armourFound ? low / 5 : 0, lowA);
		long highFast = Math.Max(high / 4, highF);
		long lowFast = Math.Max(low / 4, lowF);
		long highInfantry = high - highFast - highArmour;
		long lowInfantry = low - lowFast - lowArmour;

		Add(wanted, armour, lowArmour, highArmour, own.Armour);
		Add(wanted, fast, lowFast, highFast, own.Fast);

		long mountainTarget = RoleUnit * Math.Max(mountainOwn, mountainEnemy);
		long remaining = lowInfantry;
		for (long step = own.Infantry; step < highInfantry; step++)
		{
			CSubUnitType type = mountainTarget-- >= own.Mountain ? mountain : garrisonTarget-- >= own.Garrison ? garrison : infantry;
			if (remaining-- > own.Infantry)
			{
				wanted[type.DatabaseIndex]++;
			}
		}
		AddAircraft(state, unit, wanted, scale, enemy.Air, own);
		return new CPlanWanted(wanted, our, their);
	}

	private sealed class Roles
	{
		public long Land;
		public long Armour;
		public long Fast;
		public long Garrison;
		public long Mountain;
		public long Infantry;
		public long Air;
		public long Tactical;
		public long Interceptors;
	}

	private static long Ratio(long value, long full) => full == 0 ? -1 : value * Whole / full;

	private static void Add(int[] wanted, CSubUnitType type, long low, long high, long have)
	{
		long added = Math.Max(0, Math.Min(low, high) - have);
		wanted[type.DatabaseIndex] += (int)added;
	}

	private static void Tally(CSubUnit brigade, Roles roles)
	{
		CSubUnitType type = brigade.Type;
		if (type.Branch != EUnitBranch.Land)
		{
			if (type.Key == TacticalBomber)
			{
				roles.Tactical++;
			}
			else if (type.Key == Interceptor)
			{
				roles.Interceptors++;
			}
			return;
		}
		switch (CArmyRole.Of(type))
		{
			case CArmyRole.Headquarters:
				break;
			case CArmyRole.Armour:
				roles.Armour++;
				break;
			case CArmyRole.Fast:
				roles.Fast++;
				break;
			case CArmyRole.Garrison:
				roles.Garrison++;
				break;
			case CArmyRole.Mountain:
				roles.Mountain++;
				break;
			default:
				roles.Infantry++;
				break;
		}
	}

	/// <summary>A unit's brigades and its children's. In a given area an army counts only where it is bound for it; fleets and air units always.</summary>
	private static IEnumerable<CSubUnit> Collect(CGameState state, CUnit unit, IReadOnlyList<int>? area)
	{
		bool land = unit.Kind is not EUnitKind.Navy and not EUnitKind.Air;
		if (!land || area is null || area.Contains(unit.Path.Count > 0 ? unit.Path[^1] : unit.Location))
		{
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				yield return brigade;
			}
		}
		foreach (CUnit child in unit.Children)
		{
			foreach (CSubUnit brigade in Collect(state, child, area))
			{
				yield return brigade;
			}
		}
	}

	private static bool IsMountain(CProvince province) => province.Terrain?.Name == Mountain;

	/// <summary>The unfriendly land beside the area, each province once: its mountains, what stands on it and its worth.</summary>
	private static (Roles Enemy, long Their, int Mountains) Census(CGameState state, CCountry country, IReadOnlyList<int> area)
	{
		var enemy = new Roles();
		long their = 0;
		int mountains = 0;
		var seen = new HashSet<int>();
		foreach (int id in area)
		{
			foreach (CProvinceEdge edge in state.Province(id).Edges)
			{
				CProvince beside = state.Province(edge.To);
				if (edge.Kind == EProvinceEdgeKind.Impassable || beside.IsSea || beside.Controller is not { } holder
					|| CCountryLists.IsFriend(state, country, holder, true) || !seen.Add(beside.Id))
				{
					continue;
				}
				if (IsMountain(beside))
				{
					mountains++;
				}
				foreach (CUnit standing in state.Positions.Of(beside.Id))
				{
					if (CCountryLists.IsFriend(state, country, standing.Owner, true))
					{
						continue;
					}
					foreach (CSubUnit brigade in standing.SubUnits)
					{
						if (brigade.Type.Branch == EUnitBranch.Naval)
						{
							continue;
						}
						their += CUnitPower.Value(brigade);
						if (brigade.Type.Branch == EUnitBranch.Air)
						{
							enemy.Air++;
							continue;
						}
						enemy.Land++;
						int role = CArmyRole.Of(brigade.Type);
						enemy.Armour += role == CArmyRole.Armour ? 1 : 0;
						enemy.Fast += role == CArmyRole.Fast ? 1 : 0;
					}
				}
			}
		}
		return (enemy, their, mountains);
	}

	/// <summary>The area's held provinces worth a garrison; asked of the plan, the theatre's, if an agent runs it, and none otherwise.</summary>
	private static long GarrisonTarget(CGameState state, CCountry country, CUnit unit, IReadOnlyList<int> area, bool useArea)
	{
		IEnumerable<int> provinces = useArea ? area : TheatreProvinces(state, country, unit);
		return provinces.Count(id => state.Province(id).Controller == country.Tag && CPlanHoldScore.Of(state, unit, state.Province(id)) >= HoldScoreCounted);
	}

	private static IEnumerable<int> TheatreProvinces(CGameState state, CCountry country, CUnit unit)
	{
		for (CUnit? at = unit; at is not null; at = at.Parent)
		{
			foreach (CAgent agent in country.UnitAi.Agents)
			{
				if (ReferenceEquals(agent.Unit, at) && CAgents.TheatreOf(state, country, agent) is { } theatre)
				{
					return theatre.AllProvinces;
				}
			}
		}
		return Array.Empty<int>();
	}

	/// <summary>In an area a fifth of the whole, whoever is at war (the lookup that would raise it finds the country itself); of a plan by war and the top of its lists.</summary>
	private static long Scale(CGameState state, CCountry country, bool useArea)
	{
		long scale;
		if (useArea)
		{
			scale = PeaceScale;
		}
		else if (CCountryLists.Enemies(state, country).Count > 0)
		{
			scale = AtWarScale;
		}
		else
		{
			scale = PeaceScale;
			if (country.Strategy.Threat.Entries.Count > 0 && country.Strategy.Threat.Entries[0].Weight > 0)
			{
				scale = country.Strategy.Threat.Entries[0].Weight * 5L;
			}
			if (country.Strategy.Antagonize.Entries.Count > 0)
			{
				scale = Math.Max(scale, country.Strategy.Antagonize.Entries[0].Weight * 5L);
			}
		}
		return Math.Max(Math.Min(scale, Whole), LeastScale);
	}

	private static long BaseSize(CGameState state, CCountry country, CUnit unit, IReadOnlyList<int> area, bool useArea, long scale)
	{
		long size = useArea ? area.Count * scale * 9 / Whole : LevelBrigades(unit) * scale / Whole;
		if (CCountryLists.Enemies(state, country).Count == 0)
		{
			size = Math.Min(size, country.TotalIc * Whole * PeaceMultiple(state, country));
		}
		return Math.Max(size, RoleUnit);
	}

	private static long PeaceMultiple(CGameState state, CCountry country)
	{
		long multiple = country.Faction.Length == 0 ? 1 : CCountryLists.FactionMembers(state, country.Faction) is { Count: > 0 } members && members[0] == country ? 10 : 4;
		string? group = state.Ideology(country.RulingIdeology)?.Group?.Name;
		return group is null || group.Equals(Democracy, StringComparison.OrdinalIgnoreCase) ? multiple : multiple * 4;
	}

	/// <summary>Three a division, nine a corps, 27 an army and 81 an army group; a theatre the sum of what stands directly under it, at least nine.</summary>
	private static long LevelBrigades(CUnit unit)
	{
		switch (unit.Kind)
		{
			case EUnitKind.Division:
				return 3;
			case EUnitKind.Corps:
				return 9;
			case EUnitKind.Army:
				return 27;
			case EUnitKind.ArmyGroup:
				return 81;
			case EUnitKind.Theatre:
				return Math.Max(9, unit.Children.Sum(LevelBrigades));
			default:
				return 0;
		}
	}

	private static void AddAircraft(CGameState state, CUnit unit, int[] wanted, long scale, long enemyAir, Roles own)
	{
		long count = unit.Kind switch { EUnitKind.Army => 3, EUnitKind.Corps => 0, _ => 9 };
		long high = count * scale / Whole;
		long low = high / 2;
		if (unit.Kind is EUnitKind.Theatre or EUnitKind.ArmyGroup)
		{
			high = Math.Max(high, 3);
			low = Math.Max(low, 1);
		}
		else if (unit.Kind == EUnitKind.Army)
		{
			high = Math.Max(high, 1);
		}
		high = Math.Max(high, enemyAir);
		low = Math.Max(low, enemyAir);
		if (state.SubUnitTypes.TryGetValue(TacticalBomber, out CSubUnitType? tactical))
		{
			Add(wanted, tactical, low - low / 3, high - high / 3, own.Tactical);
		}
		if (state.SubUnitTypes.TryGetValue(Interceptor, out CSubUnitType? interceptor))
		{
			Add(wanted, interceptor, low / 3, high / 3, own.Interceptors);
		}
	}
}
