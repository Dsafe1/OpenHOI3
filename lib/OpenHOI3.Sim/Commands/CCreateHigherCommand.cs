using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A new headquarters one level above a unit, over it: one <c>hq_brigade</c> at a twentieth of its strength, the
/// country charged a twentieth of its men, under the unit's commander where that ranks above, named by its kind and given
/// a leader. Its ids are taken as it is posted - three, of which two are used. The AI's agents post it as they make their
/// commands (<c>docs/mechanics/agents.md</c>, section 4.4).
/// </summary>
public sealed class CCreateHigherCommand : CCommand
{
	/// <summary>A new headquarters' brigade's share of its strength and of its men, in thousandths: a twentieth.</summary>
	private const long Twentieth = 50;

	private readonly CCountryTag _owner;
	private readonly int[] _units;
	private readonly int _id;

	/// <summary>Takes the ids as it is posted: the headquarters', its brigade's, and one left unused.</summary>
	public CCreateHigherCommand(CGameState state, CCountryTag owner, int unit)
	{
		_owner = owner;
		_units = new[] { unit };
		_id = state.LastUnitId + 1;
		state.LastUnitId += 3;
	}

	/// <summary>The player's, for one unit or several: the ids are taken as it runs, so that a replay comes out the same (attaching.md, section 5).</summary>
	public CCreateHigherCommand(CCountryTag owner, int[] units)
	{
		_owner = owner;
		_units = units;
	}

	public int Id => _id;

	public override bool MayExecute(CGameState state)
	{
		if (_units.Length == 0 || state.Country(_owner) is not { } country
			|| !state.SubUnitTypes.TryGetValue(CTheatreHeadquarters.HqBrigade, out CSubUnitType? type))
		{
			return false;
		}
		foreach (int id in _units)
		{
			if (CUnitFinder.Find(state, _owner, id) is not { } unit || CAgentUnits.Level(unit) < 1)
			{
				return false;
			}
		}
		return country.Manpower.Raw >= _units.Length * type.Stats[CSubUnitStats.BuildCostManpower].Raw;
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_owner)!;
		var units = new List<CUnit>();
		foreach (int each in _units)
		{
			units.Add(CUnitFinder.Find(state, _owner, each)!);
		}
		CUnit top = units[0];
		foreach (CUnit each in units)
		{
			if (each.Kind < top.Kind)
			{
				top = each;
			}
		}
		CSubUnitType type = state.SubUnitTypes[CTheatreHeadquarters.HqBrigade];
		int place = top.Location;
		if (place > 0 && state.Province(place).IsSea && top.Transport is { } fleet)
		{
			place = fleet.Base;
		}
		if (place <= 0 || state.Province(place).IsSea)
		{
			return;
		}
		int id = _id > 0 ? _id : state.LastUnitId + 1;
		if (_id == 0)
		{
			state.LastUnitId += 3;
		}
		var unit = new CUnit((EUnitKind)((int)top.Kind - 1), country.Tag) { Id = id, Location = place };
		// Its technologies at the levels a new brigade is given - every one at nought - and not its country's: the
		// original sets the type and applies the technologies with nothing between (theatres.md, section 10).
		var brigade = new CSubUnit(type) { Id = id + 1, Unit = unit, Experience = CFixedPoint.Zero };
		CUnits.RebuildStats(brigade);
		long most = brigade.Stats[CSubUnitType.MaxStrength].Raw;
		brigade.Strength = new CFixedPoint(Math.Max(0, most * Twentieth / CFixedPoint.Scale));
		brigade.Highest = CFixedPoint.Max(brigade.Highest, brigade.Strength);
		brigade.Organisation = CFixedPoint.Min(brigade.Organisation, COrganisation.Ceiling(state, country, brigade));
		long men = CBuildQueue.ManpowerOf(country, brigade, isReserve: false).Raw * Twentieth / CFixedPoint.Scale;
		country.Manpower = CFixedPoint.Max(CFixedPoint.Zero, country.Manpower - new CFixedPoint(men));
		unit.AddSubUnit(brigade);
		brigade.Name = CUnitNames.ForSubUnit(state, country, type);
		CUnitNames.Register(country, brigade.Name);
		// Put in its province from nowhere, it brings its days of supplies and fuel (supply.md, section 14). The original
		// places it after giving it its place in the order of battle, but works out what it uses before its province is
		// written, when no commander's reach comes down to it - as it is here, before it is attached to anything.
		CSupplyCarry.Bring(state, country, unit, from: 0, place, beforeGame: state.IsBeingMade);

		CUnit? above = top.Parent;
		if (above is not null && above.Kind == unit.Kind)
		{
			above = above.Parent;
		}
		country.Units.Add(unit);
		if (above is not null && above.Kind < unit.Kind)
		{
			CAgentUnits.Attach(state, unit, above);
		}
		CAgentUnits.Attach(state, top, unit);
		foreach (CUnit other in units)
		{
			if (!ReferenceEquals(other, top) && CDeployment.MayTakeSubordinate(unit, other))
			{
				CAgentUnits.Attach(state, other, unit);
			}
		}

		// An army group's is a theatre, and a new theatre that holds nothing but it (attaching.md, section 4).
		if (unit.Kind == EUnitKind.Theatre)
		{
			unit.Name = CUnitNames.ForTheatre(state, country, CTheatreHeadquarters.NamingProvince(state, state.Province(place))?.Name);
			CTheatre theatre = CTheatreProvinces.New(state, country.Tag);
			theatre.Headquarters = unit.Id;
			country.Strategy.Theatres.Add(theatre);
		}
		else
		{
			unit.Name = CUnitNames.ForKind(state, country, unit.Kind, type);
		}
		CUnitNames.Register(country, unit.Name);
		state.AddToPositions(unit);
		if (country.AutoAssignLeaders)
		{
			CLeaders.AssignBest(state, country, unit);
		}
		CCombats.CheckForCombat(state, unit, place);
	}
}
