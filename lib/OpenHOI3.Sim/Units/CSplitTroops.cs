using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// <c>split_troops = F</c>: THIS country hands units - each by a roll under F of a hundred, until F of its brigades have
/// gone - and F of its manpower to the scope's country. The Spanish Civil War's armies are made so. See
/// <c>docs/mechanics/effects.md</c>, section 9.
/// </summary>
public static class CSplitTroops
{
	private const long Scale = CFixedPoint.Scale;

	public static void Run(CGameState state, CEffect effect, CEventScope scope)
	{
		if (state.Country(scope.Root.Country) is not { } giver || state.Country(scope.Country) is not { } receiver)
		{
			return;
		}
		Split(state, giver, receiver, effect.Argument.Amount, scope);
	}

	public static void Split(CGameState state, CCountry giver, CCountry receiver, CFixedPoint share, CEventScope scope)
	{
		long f = share.Raw;
		long percent = f * 100 / Scale;
		// The budget of brigades, in thousandths of one: F of all the giver's. (The original keeps a second for land units
		// alone, charged only by them; it never runs out first.)
		List<CUnit> units = giver.AllUnits().ToList();
		long budget = units.Sum(unit => (long)unit.SubUnits.Count) * f;
		foreach (CUnit unit in units)
		{
			if (!MayGo(unit, giver))
			{
				continue;
			}
			// Once F of the brigades have gone, no more go, whatever the roll.
			if (budget <= 0)
			{
				break;
			}
			if (scope.Draw(100) >= percent)
			{
				continue;
			}
			Hand(state, giver, receiver, unit);
			budget -= unit.SubUnits.Count * Scale;
			if (CMovementRules.IsLandUnit(unit))
			{
				StepAside(state, unit, giver, receiver);
			}
		}
		// F of the manpower, whatever went.
		CFixedPoint manpower = giver.Manpower * share;
		receiver.Manpower = CFixedPoint.Max(CFixedPoint.Zero, receiver.Manpower + manpower);
		giver.Manpower = CFixedPoint.Max(CFixedPoint.Zero, giver.Manpower - manpower);
		state.ForgetPositions();
		Intelligence.CProvinceIntel.Rebuild(state);
	}

	/// <summary>
	/// Whether a unit may be handed over: aboard nothing and carrying nothing, a fleet with nothing under it, in no
	/// battle, and no theatre.
	/// </summary>
	private static bool MayGo(CUnit unit, CCountry giver)
	{
		if (unit.IsRemoved || unit.Owner != giver.Tag || unit.Kind == EUnitKind.Theatre || unit.IsFighting)
		{
			return false;
		}
		if (CMovementRules.IsLandUnit(unit))
		{
			return unit.Transport is null && unit.Plane is null;
		}
		if (unit.Kind == EUnitKind.Navy && unit.Children.Count > 0)
		{
			return false;
		}
		return unit.Passengers.Count == 0;
	}

	/// <summary>
	/// The unit goes over: those under it leave it, its brigades' officers go with it at the giver's officer ratio, its
	/// leader stays behind, and it leaves its place in the giver's order of battle for the receiver's list.
	/// </summary>
	private static void Hand(CGameState state, CCountry giver, CCountry receiver, CUnit unit)
	{
		foreach (CUnit child in unit.Children.ToList())
		{
			LeaveParent(giver, child);
		}
		long officers = 0;
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			officers += brigade.Type.Stats[CSubUnitType.Officers].Raw * giver.OfficerRatio.Raw / Scale;
		}
		giver.Officers = new CFixedPoint(System.Math.Max(0, giver.Officers.Raw - officers));
		receiver.Officers = new CFixedPoint(receiver.Officers.Raw + officers);
		CLeaders.SetLeader(state, unit, null, resetTraitHours: false, organisationHit: false);
		LeaveParent(giver, unit);
		giver.Units.Remove(unit);
		unit.Owner = receiver.Tag;
		receiver.Units.Add(unit);
	}

	private static void LeaveParent(CCountry owner, CUnit unit)
	{
		if (unit.Parent is not { } parent)
		{
			return;
		}
		parent.Children.Remove(unit);
		unit.Parent = null;
		owner.Units.Add(unit);
	}

	/// <summary>
	/// A land unit handed over steps into the first province beside it, by the map's order, that either side holds and
	/// where no unit of another country stands; with none, it stays. Where anyone is left behind, it marches straight back.
	/// </summary>
	private static void StepAside(CGameState state, CUnit unit, CCountry giver, CCountry receiver)
	{
		if (unit.Location <= 0)
		{
			return;
		}
		foreach (CProvinceEdge edge in state.Province(unit.Location).Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable)
			{
				continue;
			}
			CProvince next = state.Province(edge.To);
			if ((next.Controller != giver.Tag && next.Controller != receiver.Tag)
				|| state.UnitsIn(next.Id).Any(other => other.Owner != unit.Owner))
			{
				continue;
			}
			int left = unit.Location;
			unit.Path.Clear();
			CMovement.Enter(state, unit, next.Id);
			// Where anyone is left in the province it stepped out of, it is sent straight back - the original posts the
			// march where this machine may post commands, which a game here always may.
			if (state.UnitsIn(left).Count > 0)
			{
				state.Outbox.Add(new Commands.CMoveUnitCommand(unit.Owner, unit.Id, left, append: true));
			}
			return;
		}
	}
}
