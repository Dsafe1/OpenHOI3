using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// An agent's air units: the groups too worn to fly called off or left to rest; the rest sent to the best of the air
/// bases listed for their role; regrouped (<see cref="CAirRegroup"/>); and given close air support over a battle on the
/// front or an invasion's beach, an intercept over every province they may enter, a logistical strike behind a front they
/// are losing, and a strike at an enemy fleet at sea or in port. The original's <c>CAIUnit_ManageAirUnits</c>, its four
/// phases in order. See <c>docs/mechanics/moving.md</c>, section 17.
/// </summary>
public static class CAirBody
{
	private const string Interceptor = "interceptor";
	private const string Cas = "cas";
	private const string Cag = "cag";
	private const string Tactical = "tactical_bomber";
	private const string Naval = "naval_bomber";
	private const string Strategic = "strategic_bomber";
	private const string TransportPlane = "transport_plane";

	/// <summary>The score a group's own base starts the rebase at: any listed base below it is better.</summary>
	private const float NoBase = 1e6f;

	/// <summary>A wing with more strategic attack than this, in thousandths, is a long-range one.</summary>
	private const long LongRange = 50000;

	/// <summary>A close-support wing with less strategic attack than this, in thousandths, never flies a strategic arm.</summary>
	private const long LeastStrategicAttack = 1000;

	/// <summary>Wings with a sea attack sent at ships for each province threatened from the sea.</summary>
	private const int NavalBudgetPerThreat = 4;

	/// <summary>The worst weather the AI flies in, as the air's weather penalty in thousandths, and over an invasion's beach.</summary>
	private const long WeatherCap = 330;

	private const long InvasionWeatherCap = 400;

	/// <summary>The AI interdicts only while its power on the plan is below this share of the enemy's, in thousandths.</summary>
	private const long InterdictionPowerCap = 750;

	/// <summary>The least infrastructure, in thousandths, of a province worth a logistical strike.</summary>
	private const long LeastInfrastructure = 200;

	/// <summary>The least daylight left, in hours, for a mission flown to sunset.</summary>
	private const int LeastDaylight = 3;

	private const long InterceptHours = 120;

	private const long StrikeHours = 24;

	private const int LeastIntelligence = 2;

	public static void Run(CGameState state, CCountry country, CAgent agent, bool full)
	{
		if (agent.Unit is not { } headquarters || agent.Air.Count == 0)
		{
			return;
		}
		bool atWar = state.IsAtWar(country.Tag);
		var handled = new HashSet<CUnit>();
		List<CUnit> candidates = Triage(state, country, agent, headquarters, atWar, handled, ref full);
		if (full)
		{
			Rebase(state, country, agent, headquarters, candidates, handled);
		}
		CAirRegroup.Run(state, country, agent, full, handled, candidates);
		if (agent.RegroupAirPending || (!full && !atWar) || headquarters.Plan.AirStance == 0)
		{
			return;
		}
		var support = new List<CUnit>();
		var strategic = new List<CUnit>();
		var naval = new List<CUnit>();
		var interceptors = new List<CUnit>();
		int budget = agent.Threatened.Count * NavalBudgetPerThreat;
		foreach (CUnit group in candidates)
		{
			if (group.IsFighting || !CAirMissions.IsAtOwnBase(group) || CCommandLadder.IsBlocked(group)
				|| handled.Contains(group) || group.SubUnits.Count == 0)
			{
				continue;
			}
			Dictionary<string, int> counts = Count(group);
			CSubUnit first = group.SubUnits[0];
			if (counts.GetValueOrDefault(Tactical) > 0 || counts.GetValueOrDefault(Cas) > 0 || counts.GetValueOrDefault(Cag) > 0)
			{
				if (budget > 0 && first.Stats[CNavalFire.SeaAttack].Raw > 0)
				{
					naval.Add(group);
					budget--;
				}
				else if (agent.Area.Count > 0 || !atWar || first.Stats[CBombingOrders.StrategicAttack].Raw < LeastStrategicAttack
					|| counts.GetValueOrDefault(Cag) > 0)
				{
					support.Add(group);
				}
				else
				{
					strategic.Add(group);
				}
			}
			else if (counts.GetValueOrDefault(Strategic) > 0 || first.Stats[CBombingOrders.StrategicAttack].Raw > LongRange)
			{
				strategic.Add(group);
			}
			else if (counts.GetValueOrDefault(Naval) > 0)
			{
				naval.Add(group);
			}
			else if (counts.GetValueOrDefault(Interceptor) > 0)
			{
				interceptors.Add(group);
			}
		}
		GroundAttack(state, country, agent, support);
		if (atWar)
		{
			Intercept(state, country, interceptors);
		}
		if (headquarters.Plan.AirStance != CUnitPlan.Offensive)
		{
			return;
		}
		LogisticalStrike(state, country, agent, headquarters.Plan, strategic);
		NavalStrike(state, country, agent, naval);
	}

	/// <summary>
	/// The first phase: a group joining a live partner is set aside with it; in no war, or with no base, any other is a
	/// candidate; otherwise one whose airfield an enemy marches on is called off and kept; a worn group under an order is
	/// called off - a carrier's group put to protecting its carrier - and one at rest at its base is left there; a group on
	/// the move, under an order or fighting is passed over.
	/// </summary>
	private static List<CUnit> Triage(CGameState state, CCountry country, CAgent agent, CUnit headquarters, bool atWar,
		HashSet<CUnit> handled, ref bool force)
	{
		var candidates = new List<CUnit>();
		foreach (CUnit group in agent.Air)
		{
			if (group.IsRemoved || group.IsRetreating)
			{
				continue;
			}
			if (group.Order == EUnitOrder.JoinAir)
			{
				if (CJoinOrders.SeenPartner(group) is { } partner)
				{
					handled.Add(group);
					handled.Add(partner);
					continue;
				}
				state.Outbox.Add(new CDropOrderCommand(country.Tag, group.Id));
			}
			// In no war, declared or not, or with no base, a group is a candidate whatever its state: the tests below are a
			// country at war's (FINDINGS-liveread.md, section 56).
			if (!(atWar || Wars.CUndeclaredWars.Involves(state, country.Tag)) || group.Base <= 0)
			{
				candidates.Add(group);
				continue;
			}
			bool busy = group.Order != EUnitOrder.None;
			if (atWar && group.Base > 0 && IsMarchedOn(state, country, group.Base))
			{
				force = true;
				if (busy && group.Order != EUnitOrder.RebaseAir)
				{
					state.Outbox.Add(new CDropOrderCommand(country.Tag, group.Id));
				}
				candidates.Add(group);
				continue;
			}
			if (!CAirRegroup.IsFit(state, headquarters, group) && busy)
			{
				if (group.CarriedBy?.Unit is not null && AllCag(group))
				{
					state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, group.Id, EUnitOrder.CarrierProtection, group.Base));
				}
				else
				{
					state.Outbox.Add(new CDropOrderCommand(country.Tag, group.Id));
				}
				continue;
			}
			// A group with a wing damaged or tired past the higher bar rests at its base (FINDINGS-liveread.md, section 27).
			if (!busy && CAirMissions.IsAtOwnBase(group) && CAirRegroup.WantsRest(state, headquarters, group))
			{
				continue;
			}
			if (CCommandLadder.IsBlocked(group) || group.IsFighting)
			{
				continue;
			}
			candidates.Add(group);
		}
		return candidates;
	}

	/// <summary>
	/// The second phase, on a full pass or with an airfield marched on: each group but a carrier's goes to the air base of
	/// its role's list that scores lowest, no enemy marching on it - the list's score, averaged with the distance from the
	/// headquarters for a group under anything but a theatre, and multiplied by the base's crowding where its groups would
	/// outnumber its working levels. A group sent goes to the third and fourth phases no more.
	/// </summary>
	private static void Rebase(CGameState state, CCountry country, CAgent agent, CUnit headquarters, List<CUnit> candidates,
		HashSet<CUnit> handled)
	{
		CBuilding? airBase = CTheatreFigures.AirBase(state);
		// Every air unit based at a province, nested under a headquarters or not: the air base's own list
		// (FINDINGS-liveread.md, section 26).
		var stationed = new Dictionary<int, int>();
		foreach (CCountry anyone in state.Countries)
		{
			foreach (CUnit unit in anyone.AllUnits())
			{
				if (!unit.IsRemoved && unit.Kind == EUnitKind.Air && unit.Base > 0)
				{
					stationed[unit.Base] = stationed.GetValueOrDefault(unit.Base) + unit.SubUnits.Count;
				}
			}
		}
		var moved = new Dictionary<int, int>();
		CProvince here = state.Province(headquarters.Location);
		foreach (CUnit group in candidates)
		{
			if (handled.Contains(group) || group.SubUnits.Count == 0)
			{
				continue;
			}
			Dictionary<string, int> counts = Count(group);
			if (counts.GetValueOrDefault(Cag) >= 1)
			{
				continue;
			}
			bool transport = counts.GetValueOrDefault(TransportPlane) > 0;
			List<(int Province, float Score)> list =
				counts.GetValueOrDefault(Strategic) > 0 || group.SubUnits[0].Stats[CBombingOrders.StrategicAttack].Raw > LongRange
					? agent.AirBasesStrategic
					: counts.GetValueOrDefault(Naval) > 0 ? agent.AirBasesNaval
					: transport ? agent.AirBasesTransport
					: agent.AirBasesDefault;
			int best = group.Base;
			float bestScore = NoBase;
			bool underTheatre = group.Parent is null || CAgentUnits.Level(group.Parent) == 0;
			foreach ((int id, float listed) in list)
			{
				if (IsMarchedOn(state, country, id))
				{
					continue;
				}
				CProvince province = state.Province(id);
				float score = listed;
				if (!underTheatre)
				{
					score = (score + CMapDistance.Between(here, province, state.MapWidth)) * 0.5f;
				}
				if (!transport && airBase is not null)
				{
					float load = moved.GetValueOrDefault(id) + stationed.GetValueOrDefault(id)
						+ (group.Base != id ? group.SubUnits.Count : 0);
					load /= province.BuildingLevel(airBase).Raw / 1000f;
					if (load > 1f)
					{
						score *= load;
					}
				}
				if (bestScore > score)
				{
					best = id;
					bestScore = score;
				}
			}
			if (best > 0 && best != group.Base)
			{
				state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, group.Id, EUnitOrder.RebaseAir, best));
				moved[best] = moved.GetValueOrDefault(best) + group.SubUnits.Count;
				moved[group.Base] = moved.GetValueOrDefault(group.Base) - group.SubUnits.Count;
				handled.Add(group);
			}
		}
	}

	/// <summary>
	/// The original's <c>CollectUnitsMovingIntoProvince</c> with enemies only: a land unit of a country at war with this one,
	/// standing in a land province beside it, whose next step is into it.
	/// </summary>
	private static bool IsMarchedOn(CGameState state, CCountry country, int province)
	{
		foreach (CProvinceEdge edge in state.Province(province).Edges)
		{
			if (state.Province(edge.To).IsSea)
			{
				continue;
			}
			foreach (CUnit unit in state.Positions.Of(edge.To))
			{
				if (CTheatres.IsLand(unit) && unit.Path.Count > 0 && unit.Path[0] == province
					&& CMovementRules.IsEnemy(state, country.Tag, unit.Owner))
				{
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>
	/// Close air support till sunset: over the beach of one of its country's invasions where an enemy land unit stands, in
	/// weather to 400; else over a battle on its front its country sees into, in weather to 330, where at most one of the
	/// agent's groups is already - each battle given to one group a pass.
	/// </summary>
	private static void GroundAttack(CGameState state, CCountry country, CAgent agent, List<CUnit> groups)
	{
		if (groups.Count == 0)
		{
			return;
		}
		var battles = new List<int>();
		foreach (int id in agent.FrontProvinces)
		{
			CProvince province = state.Province(id);
			if (CProvinceIntel.Level(state, province, country.Tag) < LeastIntelligence)
			{
				continue;
			}
			foreach (CUnit unit in state.Positions.Of(id))
			{
				if (CTheatres.IsLand(unit) && unit.IsFighting)
				{
					battles.Add(id);
					break;
				}
			}
		}
		foreach (CUnit group in groups)
		{
			if (!Daylight(state, group, out long end))
			{
				continue;
			}
			int target = 0;
			foreach (int id in agent.InvasionTargets)
			{
				CProvince province = state.Province(id);
				if (EnemyLandUnits(state, country, id) && CAirModifiers.Weather(state, province) <= InvasionWeatherCap
					&& CUnitOrders.IsInRange(state, group, EUnitOrder.GroundAttack, province))
				{
					target = id;
					break;
				}
			}
			if (target == 0)
			{
				foreach (int id in battles)
				{
					CProvince province = state.Province(id);
					if (CAirModifiers.Weather(state, province) <= WeatherCap && AlreadyOver(agent, id) <= 1
						&& CUnitOrders.IsInRange(state, group, EUnitOrder.GroundAttack, province))
					{
						target = id;
						battles.Remove(id);
						break;
					}
				}
			}
			if (target != 0)
			{
				state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, group.Id, EUnitOrder.GroundAttack, target, stance: CMission.Defensive, endHour: end));
			}
		}
	}

	/// <summary>
	/// An intercept for 120 hours over every province in reach that the country may enter and sees into, but an enemy's
	/// air base; whatever the weather.
	/// </summary>
	private static void Intercept(CGameState state, CCountry country, List<CUnit> groups)
	{
		CBuilding? airBase = CTheatreFigures.AirBase(state);
		foreach (CUnit group in groups)
		{
			var provinces = new List<int>();
			foreach (CProvince province in state.Provinces)
			{
				if (province.Id == 0 || !CUnitOrders.IsInRange(state, group, EUnitOrder.AirIntercept, province)
					|| !CMovementRules.MayEnter(state, country, province)
					|| CProvinceIntel.Level(state, province, country.Tag) < LeastIntelligence)
				{
					continue;
				}
				if (airBase is not null && province.BuildingMaxLevel(airBase).Raw > 0
					&& CMovementRules.IsEnemy(state, country.Tag, province.Controller))
				{
					continue;
				}
				provinces.Add(province.Id);
			}
			if (provinces.Count > 0)
			{
				state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, group.Id, EUnitOrder.AirIntercept, provinces[0],
					provinces: provinces, stance: CMission.Defensive, endHour: state.TotalHours + InterceptHours));
			}
		}
	}

	/// <summary>
	/// At the offensive air stance, while the plan's power is below three quarters of the enemy's: a logistical strike for
	/// 24 hours at the first province in reach, in weather to 330, one step behind the enemy's front - enemy-held land beside
	/// an enemy-held front province, with infrastructure of 0.2 or more, touching none of its holder's enemies.
	/// </summary>
	private static void LogisticalStrike(CGameState state, CCountry country, CAgent agent, CUnitPlan plan, List<CUnit> groups)
	{
		if (groups.Count == 0 || PowerRatio(plan) >= InterdictionPowerCap)
		{
			return;
		}
		var behind = new List<int>();
		foreach (int id in agent.FrontProvinces)
		{
			CProvince front = state.Province(id);
			if (!CMovementRules.IsEnemy(state, country.Tag, front.Controller))
			{
				continue;
			}
			foreach (CProvinceEdge edge in front.Edges)
			{
				if (edge.Kind == EProvinceEdgeKind.Impassable)
				{
					continue;
				}
				CProvince next = state.Province(edge.To);
				if (next.IsSea || next.Id == 0 || behind.Contains(next.Id)
					|| !CMovementRules.IsEnemy(state, country.Tag, next.Controller)
					|| CSupplyFigures.MostInfrastructure(state, next) < LeastInfrastructure
					|| CTheatreFigures.IsFrontProvince(state, next, withUnits: false))
				{
					continue;
				}
				behind.Add(next.Id);
			}
		}
		foreach (CUnit group in groups)
		{
			foreach (int id in behind)
			{
				CProvince province = state.Province(id);
				if (CAirModifiers.Weather(state, province) <= WeatherCap
					&& CUnitOrders.IsInRange(state, group, EUnitOrder.LogisticalStrike, province))
				{
					state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, group.Id, EUnitOrder.LogisticalStrike, id,
						stance: CMission.Defensive, endHour: state.TotalHours + StrikeHours));
					break;
				}
			}
		}
	}

	/// <summary>
	/// At the offensive air stance, till sunset: a naval strike at the first enemy fleet handed to the agent at sea, or the
	/// sea it sails into next, in reach and in weather to 330; with none, a port strike at the first in a harbour.
	/// </summary>
	private static void NavalStrike(CGameState state, CCountry country, CAgent agent, List<CUnit> groups)
	{
		if (groups.Count == 0)
		{
			return;
		}
		var atSea = new List<int>();
		var inPort = new List<int>();
		foreach (CUnit fleet in agent.EnemyFleets)
		{
			if (fleet.IsRemoved || !CMovementRules.IsEnemy(state, country.Tag, fleet.Owner)
				|| CAirModifiers.Weather(state, state.Province(fleet.Location)) > WeatherCap)
			{
				continue;
			}
			if (!state.Province(fleet.Location).IsSea)
			{
				inPort.Add(fleet.Location);
				continue;
			}
			atSea.Add(fleet.Location);
			if (fleet.Path.Count > 0)
			{
				atSea.Add(fleet.Path[0]);
			}
		}
		foreach (CUnit group in groups)
		{
			if (!Daylight(state, group, out long end))
			{
				continue;
			}
			if (First(state, group, atSea, EUnitOrder.NavalStrike) is { } sea)
			{
				state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, group.Id, EUnitOrder.NavalStrike, sea, stance: CMission.Defensive, endHour: end));
			}
			else if (First(state, group, inPort, EUnitOrder.PortStrike) is { } port)
			{
				state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, group.Id, EUnitOrder.PortStrike, port, stance: CMission.Defensive, endHour: end));
			}
		}
	}

	private static int? First(CGameState state, CUnit group, List<int> provinces, EUnitOrder order)
	{
		foreach (int id in provinces)
		{
			if (CUnitOrders.IsInRange(state, group, order, state.Province(id)))
			{
				return id;
			}
		}
		return null;
	}

	/// <summary>
	/// Whether a mission flown till sunset may go: the sunset over the group's base less the game's hour, at least three
	/// hours, gives its end.
	/// </summary>
	private static bool Daylight(CGameState state, CUnit group, out long end)
	{
		end = 0;
		if (group.Base <= 0)
		{
			return false;
		}
		int left = CDaylight.SunsetHour(state, state.Province(group.Base)) - state.Hour;
		if (left < LeastDaylight)
		{
			return false;
		}
		end = state.TotalHours + left;
		return true;
	}

	/// <summary>The original's <c>CUnitPlan::GetPowerRatio</c>: 1000 with no enemy power below nought, -1 at nought.</summary>
	public static long PowerRatio(CUnitPlan plan)
	{
		if (plan.TheirPower < 0)
		{
			return 1000;
		}
		return plan.TheirPower == 0 ? -1 : plan.OurPower * 1000 / plan.TheirPower;
	}

	private static bool EnemyLandUnits(CGameState state, CCountry country, int province)
	{
		foreach (CUnit unit in state.Positions.Of(province))
		{
			if (CTheatres.IsLand(unit) && CMovementRules.IsEnemy(state, country.Tag, unit.Owner))
			{
				return true;
			}
		}
		return false;
	}

	private static int AlreadyOver(CAgent agent, int province)
	{
		int count = 0;
		foreach (CUnit group in agent.Air)
		{
			if (group.Order == EUnitOrder.GroundAttack && group.OrderProvince == province)
			{
				count++;
			}
		}
		return count;
	}

	private static bool AllCag(CUnit group)
	{
		foreach (CSubUnit wing in group.SubUnits)
		{
			if (wing.Type.Key != Cag)
			{
				return false;
			}
		}
		return group.SubUnits.Count > 0;
	}

	private static Dictionary<string, int> Count(CUnit group)
	{
		var counts = new Dictionary<string, int>();
		foreach (CSubUnit wing in group.SubUnits)
		{
			counts[wing.Type.Key] = counts.GetValueOrDefault(wing.Type.Key) + 1;
		}
		return counts;
	}
}
