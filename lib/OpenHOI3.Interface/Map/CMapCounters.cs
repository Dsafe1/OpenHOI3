using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// Which units have a counter on the map and how they stack. See
/// <c>docs/mechanics/mapunits.md</c>, section 3.
/// </summary>
public static class CMapCounters
{
	/// <summary>The most counters of a stack that are drawn, one on another.</summary>
	public const int MostFanned = 6;

	/// <summary>
	/// The ring a unit's counter wears while units are selected, or none are: each selected unit's own, and one of
	/// another colour for every unit under any of them that is not itself selected, however
	/// far down. The original gives the rings out when a unit is selected and takes
	/// them back when it no longer is; here they follow the order of battle as it is.
	/// </summary>
	public static ECounterRing RingOf(CUnit unit, IReadOnlySet<CUnit> selected)
	{
		// With several selected: every selected unit white, every other under any of them the second colour
		// (selection.md, section 4).
		if (selected.Count == 0)
		{
			return ECounterRing.None;
		}
		if (selected.Contains(unit))
		{
			return ECounterRing.Selected;
		}
		for (CUnit? above = unit.Parent; above is not null; above = above.Parent)
		{
			if (selected.Contains(above))
			{
				return ECounterRing.Under;
			}
		}
		return ECounterRing.None;
	}

	/// <summary>
	/// Whether a unit is shown by a counter of its own. A fleet in port and an air unit
	/// at its base are not: the original shows those by the port and the airfield. Nor is
	/// a land unit aboard a fleet in port, shown by nothing but the port; at sea it has its
	/// own (<c>transport.md</c>, section 12).
	/// </summary>
	public static bool HasCounter(CGameState state, CUnit unit)
	{
		if (unit.Location <= 0 || unit.CarriedBy is not null)
		{
			return false;
		}
		return unit.Kind switch
		{
			EUnitKind.Navy => state.Province(unit.Location).IsSea,
			EUnitKind.Air => unit.Location != unit.Base,
			_ => unit.Transport is null || state.Province(unit.Location).IsSea,
		};
	}

	/// <summary>
	/// Whether a unit is still one of its country's and in some province: what a unit
	/// has to be to stay selected, counter or none.
	/// </summary>
	public static bool IsSomewhere(CGameState state, CUnit unit)
	{
		if (unit.Location <= 0 || state.Country(unit.Owner) is not { } country)
		{
			return false;
		}
		foreach (CUnit other in country.AllUnits())
		{
			if (ReferenceEquals(other, unit))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Which of its province's stacks a unit that has a counter is in, by the original's tests in its order: an air unit,
	/// or a land unit aboard one (<c>transport.md</c>, section 13.6); a unit in a battle, by its side; a unit with a path;
	/// and every other (mapunits.md, section 3).
	/// </summary>
	public static EMapStack StackOf(CGameState state, CUnit unit)
	{
		if (unit.Kind == EUnitKind.Air || unit.Plane is not null)
		{
			return EMapStack.Air;
		}
		if (unit.IsFighting)
		{
			return IsAttackerHere(state, unit) ? EMapStack.Attacking : EMapStack.Defending;
		}
		// A land unit aboard a fleet stands with the moving counters while the fleet has a
		// path, whatever its own (transport.md, section 12).
		if (unit.Transport is { } fleet)
		{
			return fleet.IsMoving ? EMapStack.Moving : EMapStack.Standing;
		}
		return unit.IsMoving ? EMapStack.Moving : EMapStack.Standing;
	}

	/// <summary>
	/// Which side of a battle a fighting unit's counter stands with: the original asks the first battle of the province's
	/// kind - a land battle on land, a sea battle at sea - fought in the province the unit stands in, else the unit's own
	/// first battle, whether the unit's owner is among that battle's attackers still in the fight. So an attacker standing
	/// in a province that is itself attacked stands with the defenders (mapunits.md, section 3).
	/// </summary>
	public static bool IsAttackerHere(CGameState state, CUnit unit)
	{
		if (unit.Combats.Count == 0)
		{
			return false;
		}
		ECombatKind kind = state.Province(unit.Location).IsSea ? ECombatKind.Naval : ECombatKind.Land;
		CCombat combat = state.Combats.FirstOrDefault(battle => battle.Province == unit.Location && battle.Kind == kind)
			?? unit.Combats[0];
		return combat.Attacker.Standing.Contains(unit.Owner);
	}

	/// <summary>
	/// A stack's counters from the top down as the original draws them: from the first of the province's list - or, where
	/// a unit of the province is selected, from that unit's place in it - on round the list, so the selected unit is on top
	/// and the units after it come under it (<c>mapunits.md</c>, section 3).
	/// </summary>
	/// <param name="cursor">The list place of the province's selected unit, or 0.</param>
	public static List<CMapCounter> TopDown(CMapStack stack, int cursor)
	{
		return stack.Counters.OrderBy(counter => counter.ListIndex >= cursor ? counter.ListIndex - cursor
			: counter.ListIndex - cursor + int.MaxValue / 2).ToList();
	}

	/// <summary>
	/// Every stack on the map the player may see. Within one the first of the province's list is on top - the player's own,
	/// then the higher rank (<see cref="CProvinceUnitOrder"/>) - until a unit is selected (<see cref="TopDown"/>). What the player knows of a unit (<see cref="CUnitIntel"/>) decides what its counter shows - under 3 the unknown
	/// picture, under 5 the name <c>???</c>, under 7 the figures <c>?</c> - and of its top unit whether the stack shows at
	/// all, under 2 not (mapunits.md, section 3).
	/// </summary>
	public static List<CMapStack> Build(CGameState state, CCountryTag? player = null)
	{
		var byPlace = new Dictionary<(int, EMapStack), CMapStack>();
		var stacks = new List<CMapStack>();
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (!HasCounter(state, unit))
				{
					continue;
				}
				(int, EMapStack) place = (unit.Location, StackOf(state, unit));
				if (!byPlace.TryGetValue(place, out CMapStack? stack))
				{
					stack = new CMapStack(place.Item1, place.Item2);
					byPlace[place] = stack;
					stacks.Add(stack);
				}
				int intel = CUnitIntel.Level(state, unit, player);
				string typeKey = CCounterText.TypeKey(unit);
				stack.Counters.Add(new CMapCounter
				{
					Unit = unit,
					Colour = country.Color,
					TypeKey = intel >= CUnitIntel.TypeShown && typeKey.Length > 0 ? typeKey : CUnitIntel.UnknownType,
					Size = CCounterText.Size(unit),
					Figures = intel >= CUnitIntel.FiguresShown ? CCounterText.Figures(unit) : CCounterText.Figures(-1, -1),
					Name = intel >= CUnitIntel.NameShown ? CCounterText.Name(unit) : CUnitIntel.UnknownName,
					Intel = intel,
				});
			}
		}
		// Each province's units in the original's order, the first on top: so a stack's list runs from its last to its first.
		foreach (IGrouping<int, CMapStack> province in stacks.GroupBy(stack => stack.Province))
		{
			List<CMapCounter> counters = province.SelectMany(stack => stack.Counters).ToList();
			List<CUnit> order = CProvinceUnitOrder.Sort(counters.Select(counter => counter.Unit), player,
				!state.Province(province.Key).IsSea);
			foreach (CMapCounter counter in counters)
			{
				counter.ListIndex = order.IndexOf(counter.Unit);
			}
			foreach (CMapStack stack in province)
			{
				stack.Counters.Sort((a, b) => b.ListIndex.CompareTo(a.ListIndex));
			}
		}
		// A stack whose top unit the player knows under 2 is not there for it: not drawn, not clicked.
		stacks.RemoveAll(stack => stack.Counters[^1].Intel < CUnitIntel.StackShown);
		return stacks;
	}
}
