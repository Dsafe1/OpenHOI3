using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A unit block as the original's <c>CUnit</c> writes it, with its brigades, the units under it, its order and its plan:
/// each key in the original's order and only where the original writes it. An air group aboard a carrier is written
/// inside its ship and nowhere else; a land unit aboard a fleet or an aeroplane inside its carrier, naming its land
/// superior. What the game does not hold is not written. See <c>docs/mechanics/savegame.md</c>, section 9.
/// </summary>
public static class CSaveUnitWriter
{
	/// <summary>The type of a leader's id.</summary>
	private const int LeaderType = 37;

	/// <summary>A unit under the key of its level.</summary>
	public static void Write(CSaveTextWriter writer, CGameState state, CUnit unit)
	{
		writer.Begin(KeyOf(unit.Kind));
		WriteBody(writer, state, unit);
		writer.End();
	}

	/// <summary>Whether a unit is written where it hangs in the tree: one aboard is written by what carries it.</summary>
	public static bool IsWrittenInPlace(CUnit unit)
	{
		return unit.CarriedBy is null && unit.Transport is null && unit.Plane is null;
	}

	public static string KeyOf(EUnitKind kind)
	{
		return CSaveUnitReader.UnitKinds.First(pair => pair.Value == kind).Key;
	}

	private static void WriteBody(CSaveTextWriter writer, CGameState state, CUnit unit)
	{
		writer.IdPair("id", Pair(state, unit.Id));
		writer.Quoted("name", unit.Name);
		bool landUnit = unit.Kind is not (EUnitKind.Navy or EUnitKind.Air);
		// A fleet's or an air unit's base, where it has one; a carrier's group has none written.
		if (!landUnit && unit.CarriedBy is null && unit.Base > 0)
		{
			writer.Int("base", unit.Base);
		}
		if (unit.Leader is { } leader)
		{
			writer.IdPair("leader", leader.Id, LeaderType);
		}
		if (unit.Previous > 0)
		{
			writer.Int("previous", unit.Previous);
		}
		writer.Fixed("movement_progress", unit.MovementProgress);
		if (unit.Path.Count > 0)
		{
			writer.IndentedList("path", unit.Path.Select(Text));
		}
		if (unit.Location > 0)
		{
			writer.Int("location", unit.Location);
		}
		if (unit.IsRetreating)
		{
			writer.Bool("retreat", true);
		}
		if (unit.DigIn.Raw > 0)
		{
			writer.Fixed("dig_in", unit.DigIn);
		}
		if (unit.AttackDelay.Raw > 0)
		{
			writer.Fixed("attack_delay", unit.AttackDelay);
		}
		if (unit.HoursAttacking > 0)
		{
			writer.Int("combat_hours", unit.HoursAttacking);
		}
		writer.Bool("is_prioritized", unit.IsPrioritised);
		writer.Bool("can_reinforce", unit.MayReinforce);
		writer.Bool("can_upgrade", unit.MayUpgrade);
		writer.Fixed("fuel", unit.FuelReceived);
		writer.Fixed("supplies", unit.SuppliesReceived);
		string keyword = unit.Kind switch
		{
			EUnitKind.Navy => "ship",
			EUnitKind.Air => "wing",
			_ => "regiment",
		};
		// In a battle, and not in a bombing.
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			WriteSubUnit(writer, state, keyword, subUnit, unit.IsFighting);
		}
		foreach (CUnit child in unit.Children)
		{
			if (IsWrittenInPlace(child))
			{
				Write(writer, state, child);
			}
		}
		if (unit.Order != EUnitOrder.None)
		{
			CSaveOrders.Write(writer, state, unit);
		}
		// The two hours with the count towards breaking off between them.
		if (unit.NotBeforeHour is { } start)
		{
			writer.Date("start_date", start);
		}
		if (unit.Disengage > 0)
		{
			writer.Int("disengage", unit.Disengage);
			writer.Int("possible_retreat", unit.DisengageTo);
		}
		if (unit.ArriveNotBeforeHour is { } end)
		{
			writer.Date("end_date", end);
		}
		if (unit.IsShadowed)
		{
			writer.Bool("track", true);
		}
		if (unit.WasParadropped)
		{
			writer.Bool("was_paradropped", true);
		}
		// Only while the plan is active, and only of a corps or above.
		if (unit.Plan.Active && unit.Kind <= EUnitKind.Corps)
		{
			WritePlan(writer, unit.Plan);
		}
		if (unit.Lender is { } lender)
		{
			writer.Quoted("expeditionary_owner", lender.Value);
			writer.Date("expeditionary_date", unit.RecallHour ?? state.EndDateHours);
		}
		if (unit.HasOwnAggression)
		{
			writer.Bool("aggression_enabled", true);
			writer.Fixed("aggression_setting", unit.Aggression);
		}
		// Each level's own tail: a land unit's sealift target, its staging province and, aboard, its land superior; what
		// a fleet or an aeroplane carries; and a fleet's wait after a battle, always last.
		if (landUnit)
		{
			if (unit.SealiftTarget > 0)
			{
				writer.Int("target", unit.SealiftTarget);
			}
			if (unit.Staging > 0)
			{
				writer.Int("staging_province", unit.Staging);
			}
			if ((unit.Transport is not null || unit.Plane is not null) && unit.Parent is { } superior)
			{
				writer.IdPair("parent", Pair(state, superior.Id));
			}
			return;
		}
		foreach (CUnit passenger in unit.Passengers)
		{
			Write(writer, state, passenger);
		}
		if (unit.Kind == EUnitKind.Navy)
		{
			writer.Int("at_sea", unit.EngageWait);
		}
	}

	/// <summary>
	/// A brigade, ship or wing: its distance only while its unit is in a battle, then a line for each of its type's
	/// technologies, and a carrier's air groups last.
	/// </summary>
	public static void WriteSubUnit(CSaveTextWriter writer, CGameState state, string keyword, CSubUnit subUnit, bool fighting)
	{
		writer.Begin(keyword);
		writer.IdPair("id", Pair(state, subUnit.Id));
		writer.Quoted("name", subUnit.Name);
		writer.Quoted("type", subUnit.Type.Key);
		writer.Fixed("organisation", subUnit.Organisation);
		writer.Fixed("strength", subUnit.Strength);
		writer.Fixed("highest", subUnit.Highest);
		writer.Fixed("experience", subUnit.Experience);
		if (subUnit.IsReserve)
		{
			writer.Bool("is_reserve", true);
		}
		if (subUnit.IsPride)
		{
			writer.Bool("pride", true);
		}
		if (fighting)
		{
			writer.Fixed("current_distance", subUnit.Distance);
		}
		if (subUnit.SunkById != 0)
		{
			writer.IdPair("sunk_by", Pair(state, subUnit.SunkById));
		}
		foreach (Sim.Research.CTechnology technology in subUnit.Type.Technologies)
		{
			int level = subUnit.TechnologyLevels.GetValueOrDefault(technology.Name);
			CFixedPoint progress = subUnit.TechnologyProgress.GetValueOrDefault(technology.Name);
			writer.Key(technology.Name);
			writer.Line("{" + Text(level) + " " + CSaveTextWriter.FixedText(progress.Raw) + "}");
		}
		foreach (CUnit group in subUnit.Carried)
		{
			writer.Begin("air");
			WriteBody(writer, state, group);
			writer.End();
		}
		writer.End();
	}

	/// <summary>A theatre's plan, as <c>CUnitPlan</c> writes it: the lists only where they hold anything.</summary>
	private static void WritePlan(CSaveTextWriter writer, CUnitPlan plan)
	{
		writer.Begin("plan");
		writer.Bool("active", plan.Active);
		if (plan.AllowHqReorg)
		{
			writer.Bool("allow_hq_reorg", true);
		}
		writer.Int("stance", plan.Stance);
		writer.Int("air_stance", plan.AirStance);
		writer.Int("naval_stance", plan.NavalStance);
		if (plan.Path.Count > 0)
		{
			writer.IndentedList("path", plan.Path.Select(Text));
		}
		if (plan.Path2.Count > 0)
		{
			writer.IndentedList("path2", plan.Path2.Select(Text));
		}
		writer.Fixed("our_power", new CFixedPoint(plan.OurPower));
		writer.Fixed("their_power", new CFixedPoint(plan.TheirPower));
		writer.Int("embark", plan.Embark);
		writer.Int("debark", plan.Debark);
		if (plan.Area.Count > 0)
		{
			writer.Numbers("ops_area", plan.Area.Select(province => (long)province));
		}
		foreach (CObjective objective in plan.Objectives)
		{
			writer.Begin("objective");
			writer.Int("province", objective.Province);
			writer.Int("priority", objective.Priority);
			writer.End();
		}
		if (plan.Min.Count > 0)
		{
			writer.Numbers("min", plan.Min.Select(value => (long)value));
		}
		writer.End();
	}

	/// <summary>A number of this game as the save's pair.</summary>
	public static (int Serial, int Type) Pair(CGameState state, int id)
	{
		return CSaveIds.Pair(id, state.HistoryUnitNext);
	}

	private static string Text(int value)
	{
		return value.ToString(CultureInfo.InvariantCulture);
	}
}
