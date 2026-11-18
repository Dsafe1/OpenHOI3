using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// A walk of units, each before what its ships carry and then the units under it: a country's every unit
/// (<see cref="Countries.CCountry.AllUnits"/>) or a unit and everything below it (<see cref="CUnit.WithAllBelow"/>). An
/// air group aboard a carrier is one of its fleet's units and is met as that; only one with no unit above it is met
/// through its ship.
///
/// The walk is live, as it has always been: the country's top units are read as the walk starts, and a unit's units and
/// what its ships carry only once the walk is asked for the next unit after it - so what a caller does to a unit as it
/// meets it is what the walk then finds below it. It is a value walked by one enumerator, its stack borrowed from the
/// thread's own and given back when the walk is done, so a walk makes nothing; it is asked for by every hour's many
/// walks of every country's units. The default walk meets nothing.
/// </summary>
public readonly struct CUnitWalk : IEnumerable<CUnit>
{
	private readonly List<CUnit>? _tops;
	private readonly CUnit? _from;

	/// <summary>Every unit from the tops down, the first of them first.</summary>
	public CUnitWalk(List<CUnit> tops)
	{
		_tops = tops;
		_from = null;
	}

	/// <summary>A unit and every unit below it, itself first.</summary>
	public CUnitWalk(CUnit from)
	{
		_tops = null;
		_from = from;
	}

	public Enumerator GetEnumerator() => new(_tops, _from);

	IEnumerator<CUnit> IEnumerable<CUnit>.GetEnumerator() => GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	/// <summary>The thread's stacks not in use, each given to a walk as it starts.</summary>
	[ThreadStatic]
	private static Stack<Stack<CUnit>>? t_spare;

	private static Stack<CUnit> Borrow()
	{
		return t_spare is { Count: > 0 } spare ? spare.Pop() : new Stack<CUnit>(64);
	}

	private static void GiveBack(Stack<CUnit> stack)
	{
		stack.Clear();
		(t_spare ??= new Stack<Stack<CUnit>>()).Push(stack);
	}

	public struct Enumerator : IEnumerator<CUnit>
	{
		private readonly List<CUnit>? _tops;
		private readonly CUnit? _from;
		private Stack<CUnit>? _stack;
		private CUnit? _current;
		private bool _done;

		internal Enumerator(List<CUnit>? tops, CUnit? from)
		{
			_tops = tops;
			_from = from;
			_stack = null;
			_current = null;
			_done = false;
		}

		public readonly CUnit Current => _current!;

		readonly object IEnumerator.Current => _current!;

		public bool MoveNext()
		{
			if (_done)
			{
				return false;
			}
			if (_stack is null)
			{
				// The walk starts: the tops as they stand now, the first on top.
				_stack = Borrow();
				if (_tops is not null)
				{
					for (int i = _tops.Count - 1; i >= 0; i--)
					{
						_stack.Push(_tops[i]);
					}
				}
				else if (_from is not null)
				{
					_stack.Push(_from);
				}
			}
			else if (_current is { } unit)
			{
				// What is below the unit met last, as it stands now that the caller has had it.
				for (int i = unit.Children.Count - 1; i >= 0; i--)
				{
					_stack.Push(unit.Children[i]);
				}
				// Only a fleet's ships carry anything - a carrier's air groups (transport.md, section 13.1) - so another
				// unit's brigades are not looked through.
				if (unit.Kind == EUnitKind.Navy)
				{
					for (int s = unit.SubUnits.Count - 1; s >= 0; s--)
					{
						List<CUnit> carried = unit.SubUnits[s].Carried;
						for (int i = carried.Count - 1; i >= 0; i--)
						{
							if (carried[i].Parent is null)
							{
								_stack.Push(carried[i]);
							}
						}
					}
				}
			}
			if (_stack.Count == 0)
			{
				_current = null;
				_done = true;
				return false;
			}
			_current = _stack.Pop();
			return true;
		}

		public void Dispose()
		{
			if (_stack is not null)
			{
				GiveBack(_stack);
				_stack = null;
			}
			_current = null;
			_done = true;
		}

		public void Reset()
		{
			throw new NotSupportedException();
		}
	}
}
