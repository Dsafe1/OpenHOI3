using System;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A brigade changed into another type. <b>The brigade is gone the moment the order runs</b>: its men and its name go to a line
/// of the queue, and cancelling the line gives back the men and not the brigade. Neither the men nor the IC are asked for, and
/// whether the type is on the brigade's list is left to the interface. See <c>docs/mechanics/upgrades.md</c>, section 2.
/// </summary>
public sealed class CUpgradeRegimentCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _brigade;
	private readonly string _target;

	public CUpgradeRegimentCommand(CCountryTag owner, int brigade, string target)
	{
		_owner = owner;
		_brigade = brigade;
		_target = target;
	}

	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.FindSubUnit(state, _owner, _brigade) is { Unit: not null } brigade
			&& CBrigadeUpgrades.MayUpgradeWhereItStands(state, brigade)
			&& state.SubUnitTypes.TryGetValue(_target, out CSubUnitType? target)
			&& state.Country(_owner) is { } country && CUnitAvailability.StillBuildable(state, country, target) >= 1;
	}

	public override void Execute(CGameState state)
	{
		CSubUnit brigade = CUnitFinder.FindSubUnit(state, _owner, _brigade)!;
		CUnit unit = brigade.Unit!;
		CCountry owner = state.Country(unit.Owner)!;

		var line = new CConstruction(owner.Tag) { Size = 1, Target = unit.Id, IsReserve = brigade.IsReserve, Builder = owner.Tag };
		line.Id = ++state.LastUnitId;
		line.UnitId = ++state.LastUnitId;
		state.LastUnitId += 2;

		long most = brigade.Stats[CSubUnitType.MaxStrength].Raw;
		long share = most == 0 ? -1 : Math.Min(brigade.Strength.Raw * CFixedPoint.Scale / most, CFixedPoint.Scale);

		var order = new CBrigadeOrder(state.SubUnitTypes[_target])
		{
			Id = ++state.LastUnitId,
			Name = brigade.Name,
			Experience = new CFixedPoint(Math.Max(brigade.Experience.Raw, 0)
				+ (brigade.IsPride ? state.Defines[CManpower.MilitarySection, CReinforcement.PrideBonus].Raw : 0)),
			Upgrade = CBrigadeUpgrades.Order(state, brigade.Type, share),
		};
		CBuildQueue.SetModelToCountrys(owner, order);
		line.Brigades.Add(order);

		// The men it had go back as the share it still held, and the new one's are taken; short of them it comes to nothing.
		long back = CBuildQueue.ManpowerOf(owner, brigade, brigade.IsReserve).Raw * share / CFixedPoint.Scale;
		owner.Manpower = new CFixedPoint(Math.Max(0, owner.Manpower.Raw + back));
		line.Manpower = CBuildQueue.ManpowerOf(owner, CBuildQueue.AsBuilt(order, line.IsReserve, out long _), line.IsReserve);
		owner.Manpower = new CFixedPoint(Math.Max(0, owner.Manpower.Raw - line.Manpower.Raw));

		if (line.IsReserve)
		{
			line.ReservesFactor = CBuildQueue.ReservesFactor(owner);
		}
		CBuildQueue.Recalculate(owner, line);
		owner.Constructions.Add(line);
		CBuildQueue.UpdateCost(owner);
		Economy.CSpareManpower.Update(state, owner);

		unit.UpgradeLines.Add(line);
		unit.SubUnits.Remove(brigade);
		brigade.Unit = null;
		state.ForgetPositions();
	}
}
