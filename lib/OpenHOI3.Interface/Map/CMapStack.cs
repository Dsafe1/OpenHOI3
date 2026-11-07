using System.Collections.Generic;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The counters of the units doing one thing in one province, the bottom one first.
/// The last is the one on top, and the only one that carries a flag and figures.
/// </summary>
public sealed class CMapStack
{
	public CMapStack(int province, EMapStack kind)
	{
		Province = province;
		Kind = kind;
	}

	public int Province { get; }

	public EMapStack Kind { get; }

	public List<CMapCounter> Counters { get; } = new();

	/// <summary>Whether it is drawn fanned, up to six counters: where the player knows its top unit at 3 or more.</summary>
	public bool Fanned => Counters.Count == 0 || Counters[^1].Intel >= CUnitIntel.TypeShown;

	public CMapCounter Top => Counters[^1];

	/// <summary>
	/// The unit a click on the stack selects. Only the counter on top takes a click; it
	/// selects the unit on top, and each click after that the next one down, coming
	/// round to the top again after the bottom. So clicking on goes through every unit
	/// of the stack.
	/// </summary>
	/// <param name="selected">The unit selected now, or null.</param>
	public CUnit Next(CUnit? selected)
	{
		for (int index = 0; index < Counters.Count; index++)
		{
			if (ReferenceEquals(Counters[index].Unit, selected))
			{
				return Counters[index == 0 ? Counters.Count - 1 : index - 1].Unit;
			}
		}
		return Top.Unit;
	}
}
