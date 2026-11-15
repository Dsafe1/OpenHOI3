using System;
using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Occupation;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A pair block - what a country holds of another, under the other's tag in its own block: the relation, the threat,
/// military access, the last hour of the diplomat's way, the hour before which no war goal may be added, the last hour the
/// two were at war, the occupation policy, debts allowed, the debt and its daily repayment. Every key the original's
/// loader names, in its writer's order (<c>CDiplomacyStatus::LoadKey</c> and <c>SaveContents</c>). See
/// <c>docs/mechanics/savegame.md</c>, section 19.
/// </summary>
public static class CSaveStatus
{
	private static readonly HashSet<string> Keys = new(StringComparer.Ordinal)
	{
		"value", "threat", "military_access", "last_send_diplomat", "next_wargoal_date", "last_war", "occupation_policy",
		"debtaction_action", "debt", "dailyrepay",
	};

	/// <summary>
	/// A pair block laid over what the record already holds, as the original's <c>CDiplomacyStatus::LoadKey</c> takes each key
	/// it meets and leaves the rest: how an order of battle's block under a country's tag - Their Finest Hour's German files
	/// of 1941 and after open with <c>SWE = { military_access = yes }</c> - is read, the file going through the country's own
	/// loader (<c>docs/mechanics/units.md</c>, section 3). The relation held to +-200; military access <c>yes</c> or not; a
	/// policy only where it names one; any other key handed to <paramref name="unknown"/>.
	/// </summary>
	public static void Apply(CGameState state, CDiplomacyStatus status, ClausewitzNode node, Action<string> unknown)
	{
		foreach (ClausewitzNode entry in node.Children)
		{
			switch (entry.Key)
			{
				case "value":
					status.Relation = CRelations.Clamp(CSaveValues.Fixed(entry.Value));
					break;
				case "threat":
					status.Threat = CSaveValues.Fixed(entry.Value);
					break;
				case "military_access":
					status.HasMilitaryAccess = entry.Value == "yes";
					break;
				case "last_send_diplomat":
					status.DiplomatUntil = CSaveOrders.Hour(entry.Value) ?? long.MinValue;
					break;
				case "next_wargoal_date":
					status.NextWarGoal = CSaveOrders.Hour(entry.Value) ?? long.MinValue;
					break;
				case "last_war":
					status.LastWar = CSaveOrders.Hour(entry.Value);
					break;
				case "occupation_policy":
					if (COccupationPolicies.Find(state, entry.Value?.Trim('"') ?? "") is { } policy)
					{
						status.OccupationPolicy = policy.Index;
					}
					break;
				case "debtaction_action":
					status.AllowDebts = entry.Value == "yes";
					break;
				case "debt":
					status.Debt = CSaveValues.Fixed(entry.Value).Raw;
					break;
				case "dailyrepay":
					status.DailyRepay = CSaveValues.Fixed(entry.Value).Raw;
					break;
				default:
					if (entry.Key is { } key)
					{
						unknown(key);
					}
					break;
			}
		}
	}

	/// <summary>
	/// A pair block read: the relation a whole number; military access where written; each date where it is one, else
	/// none; the policy, none written <c>no_occupation</c>; any other key reported.
	/// </summary>
	public static void Read(CGameState state, CDiplomacyStatus status, ClausewitzNode node, CSaveReport report)
	{
		status.Relation = CFixedPoint.FromInt(CSaveValues.Whole(node, "value"));
		status.Threat = CSaveValues.Fixed(node, "threat");
		status.HasMilitaryAccess = node.GetBool("military_access");
		status.DiplomatUntil = CSaveOrders.Hour(node.Get("last_send_diplomat")?.Value) ?? long.MinValue;
		status.NextWarGoal = CSaveOrders.Hour(node.Get("next_wargoal_date")?.Value) ?? long.MinValue;
		status.LastWar = CSaveOrders.Hour(node.Get("last_war")?.Value);
		string policy = node.Get("occupation_policy")?.Value?.Trim('"') ?? COccupationPolicies.NoOccupation;
		status.OccupationPolicy = COccupationPolicies.Find(state, policy)?.Index ?? 0;
		status.AllowDebts = node.GetBool("debtaction_action");
		status.Debt = CSaveValues.Fixed(node, "debt").Raw;
		status.DailyRepay = CSaveValues.Fixed(node, "dailyrepay").Raw;
		foreach (ClausewitzNode entry in node.Children)
		{
			if (entry.Key is { } key && !Keys.Contains(key))
			{
				report.Unread("pair", key);
			}
		}
	}

	/// <summary>
	/// A pair block written under the other's tag: the relation whole, the threat, then each key only where it holds
	/// anything - military access, the three dates, the policy where it is not the first, debts allowed, the debt and the
	/// repayment.
	/// </summary>
	public static void Write(CSaveTextWriter writer, CGameState state, CDiplomacyStatus status, string otherTag)
	{
		writer.Begin(otherTag);
		writer.Int("value", status.Relation.Raw / CFixedPoint.Scale);
		writer.Fixed("threat", status.Threat);
		if (status.HasMilitaryAccess)
		{
			writer.Bool("military_access", true);
		}
		if (status.DiplomatUntil != long.MinValue)
		{
			writer.Date("last_send_diplomat", status.DiplomatUntil);
		}
		if (status.NextWarGoal != long.MinValue)
		{
			writer.Date("next_wargoal_date", status.NextWarGoal);
		}
		if (status.LastWar is { } lastWar)
		{
			writer.Date("last_war", lastWar);
		}
		if (status.OccupationPolicy != 0 && status.OccupationPolicy < state.OccupationPolicies.Count)
		{
			writer.Quoted("occupation_policy", state.OccupationPolicies[status.OccupationPolicy].Name);
		}
		if (status.AllowDebts)
		{
			writer.Bool("debtaction_action", true);
		}
		if (status.Debt != 0)
		{
			writer.Fixed("debt", new CFixedPoint(status.Debt));
		}
		if (status.DailyRepay != 0)
		{
			writer.Fixed("dailyrepay", new CFixedPoint(status.DailyRepay));
		}
		writer.End();
	}
}
