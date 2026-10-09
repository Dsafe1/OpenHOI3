using System.Collections.Generic;
using OpenHOI3.Sim;
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
	/// Whether a unit is shown by a counter of its own. A fleet in port and an air unit
	/// at its base are not: the original shows those by the port and the airfield.
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
			_ => true,
		};
	}

	/// <summary>Which of its province's stacks a unit that has a counter is in.</summary>
	public static EMapStack StackOf(CUnit unit)
	{
		// Nothing moves or fights yet, so of the five only two are ever used.
		return unit.Kind == EUnitKind.Air ? EMapStack.Air : EMapStack.Standing;
	}

	/// <summary>
	/// Every stack on the map. Within one the higher rank is on top, and of two of a
	/// rank the one made later.
	/// </summary>
	public static List<CMapStack> Build(CGameState state)
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
				(int, EMapStack) place = (unit.Location, StackOf(unit));
				if (!byPlace.TryGetValue(place, out CMapStack? stack))
				{
					stack = new CMapStack(place.Item1, place.Item2);
					byPlace[place] = stack;
					stacks.Add(stack);
				}
				stack.Counters.Add(new CMapCounter
				{
					Unit = unit,
					Colour = country.Color,
					TypeKey = CCounterText.TypeKey(unit),
					Size = CCounterText.Size(unit),
					Figures = CCounterText.Figures(unit),
					Name = CCounterText.Name(unit),
				});
			}
		}
		foreach (CMapStack stack in stacks)
		{
			stack.Counters.Sort((a, b) => a.Size != b.Size ? a.Size.CompareTo(b.Size) : a.Unit.Id.CompareTo(b.Unit.Id));
		}
		return stacks;
	}
}
