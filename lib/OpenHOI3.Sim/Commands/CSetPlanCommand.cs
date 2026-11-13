using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A unit's plan switched on, with its stances and powers: a stance of <see cref="Unchanged"/>, an air or naval stance of
/// <see cref="UnchangedAirOrNaval"/> and a negative power are left as they are. The original's
/// <c>CSetPlanAttributesCommand</c> (<c>docs/mechanics/agents.md</c>, section 7).
/// </summary>
public sealed class CSetPlanCommand : CCommand
{
	public const int Unchanged = 5;

	public const int UnchangedAirOrNaval = 3;

	/// <summary>The power that leaves a power as it is.</summary>
	public const long UnchangedPower = -1000;

	private readonly CCountryTag _owner;
	private readonly int _unit;
	private readonly int _stance;
	private readonly int _airStance;
	private readonly int _navalStance;
	private readonly long _ourPower;
	private readonly long _theirPower;

	public CSetPlanCommand(CCountryTag owner, int unit, int stance, int airStance, int navalStance,
		long ourPower = UnchangedPower, long theirPower = UnchangedPower)
	{
		_owner = owner;
		_unit = unit;
		_stance = stance;
		_airStance = airStance;
		_navalStance = navalStance;
		_ourPower = ourPower;
		_theirPower = theirPower;
	}

	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.Find(state, _owner, _unit) is not null;
	}

	public override void Execute(CGameState state)
	{
		CUnit unit = CUnitFinder.Find(state, _owner, _unit)!;
		CUnitPlan plan = unit.Plan;
		plan.Active = true;
		if (_stance != Unchanged && plan.Stance != _stance)
		{
			plan.Stance = _stance;
			// A change of stance makes the plan's forces again (FINDINGS-liveread.md, section 76).
			Agents.CPlanWants.Refresh(state, unit);
		}
		if (_airStance != UnchangedAirOrNaval)
		{
			plan.AirStance = _airStance;
		}
		if (_navalStance != UnchangedAirOrNaval)
		{
			plan.NavalStance = _navalStance;
		}
		if (_ourPower >= 0)
		{
			plan.OurPower = _ourPower;
		}
		if (_theirPower >= 0)
		{
			plan.TheirPower = _theirPower;
		}
	}
}
