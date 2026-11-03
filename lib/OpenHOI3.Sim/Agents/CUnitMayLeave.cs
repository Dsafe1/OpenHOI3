using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// Whether the AI may take a land unit away from where it stands - the original's <c>CAIUnit_UnitMayLeave</c> - and the
/// count of idle divisions beside it that the answer hangs on, <c>CAIUnit_CountIdleDivisionsBesideWarCandidate</c>. A
/// land battle holds a unit only where the caller asks about the side it defends; what holds a unit on a border is the
/// enemy across it. See <c>docs/mechanics/moving.md</c>, section 7.6.
/// </summary>
public static class CUnitMayLeave
{
	/// <summary>The count where no neighbour is held by a war candidate: as good as any number of divisions.</summary>
	public const int NoBorder = 1000;

	/// <summary>A landing's progress, in thousandths, below which the unit is still coming ashore.</summary>
	private const long Ashore = 1000;

	/// <summary>
	/// Whether the unit may be sent elsewhere. <paramref name="strict"/> asks that more idle divisions stay than there
	/// are enemy units across the border; <paramref name="notDefending"/> holds a unit defending a battle.
	/// </summary>
	public static bool Allows(CGameState state, CCountry country, CUnit unit, ICollection<CUnit>? exclude, bool strict,
		bool notDefending)
	{
		if (unit.IsRetreating)
		{
			return false;
		}
		if (unit.IsFighting)
		{
			if (CLanding.IsLandingOnEnemy(state, unit) && CLanding.Progress(state, unit) < Ashore)
			{
				return false;
			}
			foreach (CCombat combat in unit.Combats)
			{
				// A land or an air battle lets anybody go; a sea battle not in its first four hours.
				if (combat.Kind == ECombatKind.Naval && combat.Duration <= CNavalDisengage.RoundsBeforeLeaving)
				{
					return false;
				}
				if (notDefending && combat.Defender.Units.Contains(unit))
				{
					return false;
				}
			}
		}
		int enemies = 0;
		int incoming = 0;
		foreach (CProvinceEdge edge in state.Province(unit.Location).Edges)
		{
			CProvince beside = state.Province(edge.To);
			if (edge.Kind == EProvinceEdgeKind.Impassable || beside.IsSea || beside.Controller is not { } holder
				|| !CCountryLists.IsEnemy(state, country, holder))
			{
				continue;
			}
			foreach (CUnit other in state.Positions.Of(edge.To))
			{
				if (!CMovementRules.IsLandUnit(other))
				{
					continue;
				}
				if (CCountryLists.IsEnemy(state, country, other.Owner))
				{
					enemies++;
				}
				else if (state.Country(other.Owner) is { } friend && CSupplyRelations.CanOperateIn(state, country, friend)
					&& CAgentUnits.Destination(other) == unit.Location)
				{
					incoming++;
				}
			}
		}
		if (!strict && incoming <= 0)
		{
			return true;
		}
		int idle = IdleBeside(state, country, unit, exclude);
		if (incoming > 0 && idle < 1)
		{
			return false;
		}
		return !strict || idle - enemies >= 1;
	}

	/// <summary>
	/// Whether the agent asks strictly: every agent below a theatre does, a theatre's own only where its friends out
	/// in the enemy's land are coming back.
	/// </summary>
	public static bool IsStrict(CAgent agent)
	{
		return agent.Unit is { Kind: not EUnitKind.Theatre };
	}

	/// <summary>
	/// The other divisions in the unit's province, whoever's, with nowhere to go and no way left, and not on the list -
	/// or <see cref="NoBorder"/> where no neighbour is held by a war candidate.
	/// </summary>
	public static int IdleBeside(CGameState state, CCountry country, CUnit unit, ICollection<CUnit>? exclude)
	{
		bool border = false;
		foreach (CProvinceEdge edge in state.Province(unit.Location).Edges)
		{
			CProvince beside = state.Province(edge.To);
			if (edge.Kind != EProvinceEdgeKind.Impassable && !beside.IsSea && beside.Controller is { } holder
				&& CFrontWorthy.IsWarCandidate(state, country, holder))
			{
				border = true;
				break;
			}
		}
		if (!border)
		{
			return NoBorder;
		}
		int idle = 0;
		foreach (CUnit other in state.Positions.Of(unit.Location))
		{
			if (!ReferenceEquals(other, unit) && other.Kind == EUnitKind.Division && CAgentUnits.Destination(other) <= 0
				&& other.Path.Count == 0 && (exclude is null || !exclude.Contains(other)))
			{
				idle++;
			}
		}
		return idle;
	}
}
