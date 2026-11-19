using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Construction;

/// <summary>
/// The AI's placing of a finished unit, for the countries nobody plays: once a day, in an hour of the country's own, each
/// unit waiting is sent to join a fitting unit near the capital where it is a single brigade, ship or wing, and otherwise
/// to the capital's area's best province. The original's <c>CEU3AI::DeployFinishedUnits</c>; every figure here is its
/// own. See <c>docs/mechanics/ministers.md</c>, section 6.
/// </summary>
public static class CAiDeployment
{
	/// <summary>The placing's place in the day: its country's number plus eight.</summary>
	private const int HourOffset = 8;

	/// <summary>A score at or above this is no candidate.</summary>
	private const long NoScore = 100000;

	/// <summary>The most brigades a land unit may already hold, and the most wings an air unit, to take one more.</summary>
	private const int MostBrigades = 3;
	private const int MostWings = 2;

	private const string LocalIc = "local_ic";
	private const string FortLevel = "fort_level";
	private const string CoastalFortLevel = "coastal_fort_level";
	private const string TransportShip = "transport_ship";
	private const string Submarine = "submarine";

	/// <summary>
	/// Every country nobody plays that owns land or is in exile places its waiting units in its hour of the day,
	/// <c>(number + 8) mod 24</c>. Nothing while a game is being made.
	/// </summary>
	public static void RunHourly(CGameState state)
	{
		if (state.IsBeingMade)
		{
			return;
		}
		int hour = (int)(state.TotalHours % CGameState.HoursPerDay);
		foreach (CCountry country in state.Countries)
		{
			// A played country whose player has handed the deploying over places its units every hour, where any other's
			// are placed in its minister's hour (frontend.md, section 4).
			bool played = state.Played.Contains(country.Tag);
			bool due = played
				? state.Automation.Has(Settings.EAutomation.Deployment)
				: CResearchAi.Hour(country, HourOffset) == hour;
			if (!country.AiPaused && country.Deployments.Count > 0 && due
				&& (COwnedCounts.Of(state).Count(country.Tag) > 0 || country.InExile))
			{
				Deploy(state, country);
			}
		}
	}

	/// <summary>Posts a placing for each of the country's waiting units, in the order they finished.</summary>
	public static void Deploy(CGameState state, CCountry country)
	{
		// What this pass has sent each unit already, which counts against its room.
		var sent = new Dictionary<CUnit, List<CSubUnitType>>();
		bool posted = false;
		foreach (CUnit unit in country.Deployments)
		{
			if (unit.SubUnits.Count == 1 && Join(state, country, unit, sent) is { } target)
			{
				if (!sent.TryGetValue(target, out List<CSubUnitType>? types))
				{
					sent[target] = types = new List<CSubUnitType>();
				}
				types.Add(unit.SubUnits[0].Type);
				state.Outbox.Add(new CDeployUnitCommand(country.Tag, unit.Id, target.Location, target.Id));
				posted = true;
				continue;
			}
			if (Place(state, country, unit, posted) is { } province)
			{
				state.Outbox.Add(new CDeployUnitCommand(country.Tag, unit.Id, province));
				posted = true;
			}
		}
	}

	/// <summary>
	/// The unit a single brigade, ship or wing joins: of the country's own, standing where the new one may be placed,
	/// that takes it, the lowest score - its brigades, ten each, times what it makes of the brigade, less its plan's want
	/// of the type, which is nought - under 100000, the first of equals. Null for none.
	/// </summary>
	public static CUnit? Join(CGameState state, CCountry country, CUnit unit, Dictionary<CUnit, List<CSubUnitType>> sent)
	{
		CSubUnitType type = unit.SubUnits[0].Type;
		CUnit? best = null;
		long bestScore = NoScore;
		foreach (CUnit target in country.UnitsInOrder())
		{
			if (target.Kind != Kind(unit) && !(IsLand(unit) && IsLand(target)) || target.IsRemoved || target.Location <= 0)
			{
				continue;
			}
			sent.TryGetValue(target, out List<CSubUnitType>? already);
			int count = target.SubUnits.Count + (already?.Count ?? 0);
			int factor = Factor(unit, type, target, already);
			if (factor == 0)
			{
				continue;
			}
			long score = count * 10L * factor / 2;
			if (score < bestScore && CDeployment.MayPlace(state, country, unit, target.Location))
			{
				best = target;
				bestScore = score;
			}
		}
		return best;
	}

	/// <summary>
	/// What a unit makes of one more of a type, in halves, or 0 where it may not take it: a wing an air unit of at most
	/// two wings - a carrier's group only a unit on a carrier with room, any other wing never a carrier's unit or a group;
	/// a ship a fleet - a warship never one of transports, a submarine only one all of submarines; a brigade a land unit
	/// of at most three that takes its role, with at most one narrow and, holding three wide, only one of no width.
	/// </summary>
	private static int Factor(CUnit unit, CSubUnitType type, CUnit target, List<CSubUnitType>? already)
	{
		var types = new List<CSubUnitType>();
		foreach (CSubUnit brigade in target.SubUnits)
		{
			types.Add(brigade.Type);
		}
		if (already is not null)
		{
			types.AddRange(already);
		}
		switch (unit.Kind)
		{
			case EUnitKind.Air:
			{
				bool group = CCarriers.IsCarrierAirGroup(target);
				if (CCarriers.IsCarrierAirGroup(unit))
				{
					return target.CarriedBy is { } ship && CCarriers.HasRoom(ship, unit) ? 2 : 0;
				}
				return group || target.CarriedBy is not null || types.Count > MostWings ? 0 : 2;
			}
			case EUnitKind.Navy:
			{
				bool transports = types.Count > 0 && types.TrueForAll(ship => ship.Key == TransportShip);
				bool submarines = types.TrueForAll(ship => ship.Key == Submarine);
				if (type.Key == Submarine)
				{
					return submarines ? 2 : 0;
				}
				return type.Key != TransportShip && transports ? 0 : 2;
			}
			default:
			{
				if (types.Count > MostBrigades)
				{
					return 0;
				}
				int narrow = 0;
				int wide = 0;
				foreach (CSubUnitType each in types)
				{
					if (each.Stats[CSubUnitType.CombatWidth].Raw < CFixedPoint.Scale)
					{
						narrow++;
					}
					else
					{
						wide++;
					}
				}
				long width = type.Stats[CSubUnitType.CombatWidth].Raw;
				if (width < CFixedPoint.Scale && narrow > 0 || wide == MostBrigades && width != 0)
				{
					return 0;
				}
				return CArmyRole.FactorInHalves(CArmyRole.Of(target), CArmyRole.Of(type));
			}
		}
	}

	/// <summary>
	/// Where a unit that joins nothing goes: the province of the acting capital's area, in the area's order, with the
	/// strictly highest <c>local_ic / 1000 + 2 x whole forts + 2 x whole coastal forts</c>, ten more for the capital,
	/// from -1, where it may be placed. A carrier's group goes only to the province of the first fleet with a carrier
	/// that has none aboard, and never to an airfield: with none it waits. **Once this pass has posted anything, it looks
	/// at the first fleet alone** - the original's flag of a posting, which nothing clears between its deployments
	/// (FINDINGS-liveread.md, section 135). Null for none: it waits for the next day.
	/// </summary>
	public static int? Place(CGameState state, CCountry country, CUnit unit, bool posted = false)
	{
		if (CCarriers.IsCarrierAirGroup(unit))
		{
			foreach (CUnit fleet in country.UnitsInOrder())
			{
				if (fleet.Kind != EUnitKind.Navy || fleet.IsRemoved)
				{
					continue;
				}
				if (CarrierWithNoGroup(fleet))
				{
					return fleet.Location;
				}
				if (posted)
				{
					return null;
				}
			}
			return null;
		}
		if (country.ActingCapital <= 0)
		{
			return null;
		}
		int? best = null;
		long bestScore = -CFixedPoint.Scale;
		// The area in the network's own order, which is the original's list: the first of equals wins (ministers.md, section 6).
		if (CSupplyNetworks.Refresh(state).AreaOf(country.ActingCapital) is not { } area)
		{
			return null;
		}
		foreach (int id in area.Provinces)
		{
			CProvince province = state.Province(id);
			long score = province.Modifier[LocalIc].Raw
				+ 2 * (province.Modifier[FortLevel].Raw / CFixedPoint.Scale) * CFixedPoint.Scale
				+ 2 * (province.Modifier[CoastalFortLevel].Raw / CFixedPoint.Scale) * CFixedPoint.Scale
				+ (id == country.ActingCapital ? 10 * CFixedPoint.Scale : 0);
			if (score > bestScore && CDeployment.MayPlace(state, country, unit, id))
			{
				best = id;
				bestScore = score;
			}
		}
		return best;
	}

	private static bool CarrierWithNoGroup(CUnit fleet)
	{
		foreach (CSubUnit ship in fleet.SubUnits)
		{
			if (ship.Stats[Combat.CShipCensus.CarrierSize].Raw > 0 && ship.Carried.Count == 0)
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsLand(CUnit unit) => unit.Kind != EUnitKind.Navy && unit.Kind != EUnitKind.Air;

	private static EUnitKind Kind(CUnit unit) => unit.Kind;
}
