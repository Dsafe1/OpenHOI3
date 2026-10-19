using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// An agent's air groups: each wing that breaks its group's mix put in an air unit of its own, and a group of fewer than
/// three wings ordered to join another it mixes with. The regrouping of the original's <c>CAIUnit_ManageAirUnits</c>. See
/// <c>docs/mechanics/agents.md</c>, section 5.3.
/// </summary>
public static class CAirRegroup
{
	private const string Interceptor = "interceptor";
	private const string MultiRole = "multi_role";
	private const string Cas = "cas";
	private const string Cag = "cag";
	private const string Tactical = "tactical_bomber";
	private const string Naval = "naval_bomber";
	private const string Strategic = "strategic_bomber";
	private const string TransportPlane = "transport_plane";

	/// <summary>A wing with more strategic attack than this, in thousandths, is a long-range one.</summary>
	private const long LongRange = 50000;

	/// <summary>A group of fewer wings than this looks for another to join.</summary>
	private const int Small = 3;

	/// <summary>The shares of strength and organisation a wing needs, at the offensive air stance and otherwise, in thousandths.</summary>
	private const long OffensiveStrength = 500;

	private const long OffensiveOrganisation = 750;

	private const long OtherStrength = 700;

	private const long OtherOrganisation = 850;

	/// <param name="taken">The groups the air body's first two phases have already given something to do.</param>
	public static void Run(CGameState state, CCountry country, CAgent agent, bool force, IEnumerable<CUnit>? taken = null)
	{
		if (agent.Unit is not { } headquarters || agent.Air.Count == 0)
		{
			return;
		}
		if (!agent.RegroupAirPending && !force)
		{
			return;
		}
		if (!country.UnitAi.RunsUnits && !headquarters.Plan.AllowHqReorg)
		{
			return;
		}
		agent.RegroupAirPending = false;
		var handled = new HashSet<CUnit>(taken ?? System.Array.Empty<CUnit>());
		var fit = new List<CUnit>();
		foreach (CUnit group in agent.Air)
		{
			if (group.IsRemoved || group.IsRetreating)
			{
				continue;
			}
			if (CJoinOrders.IsJoin(group.Order) && group.Mission.LiveTarget is { } joining)
			{
				handled.Add(group);
				handled.Add(joining);
				continue;
			}
			if (IsFit(state, headquarters, group) && !CCommandLadder.IsBlocked(group) && !group.IsFighting)
			{
				fit.Add(group);
			}
			else
			{
				Trace(state, country, $"air regroup: {group.Name}#{group.Id} unfit, blocked or fighting");
			}
		}
		foreach (CUnit group in fit)
		{
			if (group.IsFighting || group.Base == 0 || group.Location != group.Base || CCommandLadder.IsBlocked(group)
				|| handled.Contains(group) || group.SubUnits.Count == 0)
			{
				Trace(state, country, $"air regroup: {group.Name}#{group.Id} away, taken or empty");
				continue;
			}
			Dictionary<string, int> counts = Count(group);
			if (counts.GetValueOrDefault(Cag) > 0)
			{
				continue;
			}
			int left = group.SubUnits.Count;
			foreach (CSubUnit wing in group.SubUnits.Count > 1 ? group.SubUnits : new List<CSubUnit>())
			{
				if (!Breaks(wing, counts))
				{
					continue;
				}
				int id = CFleetRegroup.NewId(state);
				state.Outbox.Add(new CCreateUnitCommand(country.Tag, id, EUnitKind.Air, group.Location, group.Base,
					headquarters.Id));
				state.Outbox.Add(new CTransferSubUnitCommand(country.Tag, wing.Id, id));
				counts[wing.Type.Key] = counts.GetValueOrDefault(wing.Type.Key) - 1;
				left--;
				agent.RegroupAirPending = true;
			}
			if (left >= Small || IsLongRange(group))
			{
				continue;
			}
			foreach (CUnit partner in agent.Air)
			{
				if (ReferenceEquals(partner, group) || handled.Contains(partner) || partner.IsRemoved
					|| partner.SubUnits.Count == 0 || IsLongRange(partner))
				{
					continue;
				}
				var together = new Dictionary<string, int>(counts);
				foreach ((string key, int count) in Count(partner))
				{
					together[key] = together.GetValueOrDefault(key) + count;
				}
				if (!MayJoin(together))
				{
					continue;
				}
				state.Outbox.Add(new CJoinUnitCommand(country.Tag, group.Id, partner.Id));
				Trace(state, country, $"air regroup: {group.Name}#{group.Id} joins {partner.Name}#{partner.Id}");
				handled.Add(group);
				handled.Add(partner);
				agent.RegroupAirPending = true;
				break;
			}
			if (!handled.Contains(group))
			{
				Trace(state, country, $"air regroup: {group.Name}#{group.Id} finds no partner");
			}
		}
	}

	private static void Trace(CGameState state, CCountry country, string line)
	{
		if (state.AiTrace is { } trace && trace.Country == country.Tag)
		{
			trace.Write($"    {state.Date} {line}");
		}
	}

	/// <summary>
	/// Every wing at the shares of strength and organisation the plan's air stance asks - halved for a group with a
	/// carrier's wing or an interceptor. Ours: the original's two pairs of thresholds taken as one.
	/// </summary>
	public static bool IsFit(CGameState state, CUnit headquarters, CUnit group)
	{
		bool offensive = headquarters.Plan.AirStance == CUnitPlan.Offensive;
		long strength = offensive ? OffensiveStrength : OtherStrength;
		long organisation = offensive ? OffensiveOrganisation : OtherOrganisation;
		Dictionary<string, int> counts = Count(group);
		if (counts.GetValueOrDefault(Cag) > 0 || counts.GetValueOrDefault(Interceptor) > 0)
		{
			strength /= 2;
			organisation /= 2;
		}
		foreach (CSubUnit wing in group.SubUnits)
		{
			if (CFleetRegroup.StrengthShare(wing) < strength || CFleetRegroup.OrganisationShare(state, wing) < organisation)
			{
				return false;
			}
		}
		return true;
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

	private static bool IsLongRange(CUnit group)
	{
		return group.SubUnits.Count > 0 && group.SubUnits[0].Stats[Missions.CBombingOrders.StrategicAttack].Raw > LongRange;
	}

	/// <summary>Whether a wing breaks its group's mix: each role's own cap, and the roles it may not fly beside.</summary>
	private static bool Breaks(CSubUnit wing, Dictionary<string, int> counts)
	{
		int strategic = counts.GetValueOrDefault(Strategic);
		int tactical = counts.GetValueOrDefault(Tactical);
		int naval = counts.GetValueOrDefault(Naval);
		int transport = counts.GetValueOrDefault(TransportPlane);
		int cas = counts.GetValueOrDefault(Cas);
		int interceptors = counts.GetValueOrDefault(Interceptor);
		int multiRole = counts.GetValueOrDefault(MultiRole);
		return wing.Type.Key switch
		{
			Strategic => strategic > 3,
			Tactical => tactical > 2 || strategic > 0 || naval > 0 || transport > 0 || cas > 0,
			Naval => naval > 2 || strategic > 0 || tactical > 0 || transport > 0 || cas > 0,
			TransportPlane => transport > 1 || strategic > 0 || tactical > 0 || naval > 0 || cas > 0,
			Cas => cas > 2 || strategic > 0 || tactical > 0 || naval > 0 || transport > 0,
			Interceptor => Total(counts) - interceptors - multiRole > 0,
			_ => wing.Stats[Missions.CBombingOrders.StrategicAttack].Raw > LongRange || multiRole > 1 || strategic > 0
				|| interceptors > 0 || cas > 0,
		};
	}

	private static int Total(Dictionary<string, int> counts)
	{
		int total = 0;
		foreach (int count in counts.Values)
		{
			total += count;
		}
		return total;
	}

	/// <summary>Whether two groups' wings together make a mix a joined group may have.</summary>
	private static bool MayJoin(Dictionary<string, int> counts)
	{
		int total = Total(counts);
		int strategic = counts.GetValueOrDefault(Strategic);
		int tactical = counts.GetValueOrDefault(Tactical);
		int naval = counts.GetValueOrDefault(Naval);
		int transport = counts.GetValueOrDefault(TransportPlane);
		int cas = counts.GetValueOrDefault(Cas);
		int interceptors = counts.GetValueOrDefault(Interceptor);
		int multiRole = counts.GetValueOrDefault(MultiRole);
		int bombers = tactical + naval + transport;
		int kinds = (tactical > 0 ? 1 : 0) + (naval > 0 ? 1 : 0) + (transport > 0 ? 1 : 0);
		if (counts.GetValueOrDefault(Cag) > 0)
		{
			return false;
		}
		if (strategic > 3 || (strategic > 0 && bombers > 0))
		{
			return false;
		}
		if (bombers > 2 || kinds > 1 || transport > 1)
		{
			return false;
		}
		if (multiRole > 1 || (multiRole > 0 && (strategic > 0 || interceptors > 0)))
		{
			return false;
		}
		if (interceptors > 3 || (interceptors > 0 && strategic + bombers > 0))
		{
			return false;
		}
		return cas == 0 || (cas <= 2 && cas == total);
	}
}
