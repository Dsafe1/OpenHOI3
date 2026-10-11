using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim;

/// <summary>
/// The sort of the original's C++ runtime, <c>std::sort</c>, step for step: an
/// insertion sort below 33 elements; above, a three-way partition about a median of three
/// - of nine over 40 - recursing into the smaller part, with a depth budget that starts at
/// the count and is cut to three quarters at each level, and a heap sort where it runs
/// out. **It is not stable**, and where the original sorts with it the order of equal
/// elements is whatever it leaves, so that order is reproduced by doing exactly what it
/// does. Checked against the original's own code run on 1,360 lists of leaders
/// (<c>docs/mechanics/leaders.md</c>, section 12).
/// </summary>
public static class CStdSort
{
	/// <summary>The most elements the insertion sort is left with.</summary>
	private const int InsertionMax = 32;

	/// <summary>Above this many the pivot is a median of nine.</summary>
	private const int NinetherAbove = 40;

	/// <summary>Sorts the whole list by a strict "comes before".</summary>
	public static void Sort<T>(IList<T> items, Func<T, T, bool> before)
	{
		Sort(items, 0, items.Count, items.Count, before);
	}

	private static void Sort<T>(IList<T> a, int first, int last, int ideal, Func<T, T, bool> before)
	{
		int count;
		while ((count = last - first) > InsertionMax && ideal > 0)
		{
			(int middleFirst, int middleLast) = Partition(a, first, last, before);
			ideal /= 2;
			ideal += ideal / 2;
			if (middleFirst - first < last - middleLast)
			{
				Sort(a, first, middleFirst, ideal, before);
				first = middleLast;
			}
			else
			{
				Sort(a, middleLast, last, ideal, before);
				last = middleFirst;
			}
		}
		if (count > InsertionMax)
		{
			MakeHeap(a, first, last, before);
			SortHeap(a, first, last, before);
		}
		else if (count > 1)
		{
			InsertionSort(a, first, last, before);
		}
	}

	private static void InsertionSort<T>(IList<T> a, int first, int last, Func<T, T, bool> before)
	{
		for (int next = first + 1; next < last; next++)
		{
			T value = a[next];
			if (before(value, a[first]))
			{
				for (int i = next; i > first; i--)
				{
					a[i] = a[i - 1];
				}
				a[first] = value;
			}
			else
			{
				int hole = next;
				while (before(value, a[hole - 1]))
				{
					a[hole] = a[hole - 1];
					hole--;
				}
				a[hole] = value;
			}
		}
	}

	private static void Swap<T>(IList<T> a, int i, int j)
	{
		(a[i], a[j]) = (a[j], a[i]);
	}

	private static void MedianOfThree<T>(IList<T> a, int first, int middle, int last, Func<T, T, bool> before)
	{
		if (before(a[middle], a[first]))
		{
			Swap(a, middle, first);
		}
		if (before(a[last], a[middle]))
		{
			Swap(a, last, middle);
		}
		if (before(a[middle], a[first]))
		{
			Swap(a, middle, first);
		}
	}

	/// <summary><paramref name="last"/> is the last element, not one past it.</summary>
	private static void Median<T>(IList<T> a, int first, int middle, int last, Func<T, T, bool> before)
	{
		if (last - first > NinetherAbove)
		{
			int step = (last - first + 1) / 8;
			MedianOfThree(a, first, first + step, first + 2 * step, before);
			MedianOfThree(a, middle - step, middle, middle + step, before);
			MedianOfThree(a, last - 2 * step, last - step, last, before);
			MedianOfThree(a, first + step, middle, last - step, before);
		}
		else
		{
			MedianOfThree(a, first, middle, last, before);
		}
	}

	/// <summary>
	/// Leaves everything before the pivot's equals ahead of them and everything after
	/// behind, and answers where the equals begin and end.
	/// </summary>
	private static (int First, int Last) Partition<T>(IList<T> a, int first, int last, Func<T, T, bool> before)
	{
		int middle = first + (last - first) / 2;
		Median(a, first, middle, last - 1, before);
		int pivotFirst = middle;
		int pivotLast = pivotFirst + 1;
		while (first < pivotFirst && !before(a[pivotFirst - 1], a[pivotFirst]) && !before(a[pivotFirst], a[pivotFirst - 1]))
		{
			pivotFirst--;
		}
		while (pivotLast < last && !before(a[pivotLast], a[pivotFirst]) && !before(a[pivotFirst], a[pivotLast]))
		{
			pivotLast++;
		}
		int greaterFirst = pivotLast;
		int lessLast = pivotFirst;
		while (true)
		{
			for (; greaterFirst < last; greaterFirst++)
			{
				if (before(a[pivotFirst], a[greaterFirst]))
				{
					continue;
				}
				if (before(a[greaterFirst], a[pivotFirst]))
				{
					break;
				}
				Swap(a, pivotLast++, greaterFirst);
			}
			for (; first < lessLast; lessLast--)
			{
				if (before(a[lessLast - 1], a[pivotFirst]))
				{
					continue;
				}
				if (before(a[pivotFirst], a[lessLast - 1]))
				{
					break;
				}
				Swap(a, --pivotFirst, lessLast - 1);
			}
			if (lessLast == first && greaterFirst == last)
			{
				return (pivotFirst, pivotLast);
			}
			if (lessLast == first)
			{
				// No room below: the pivot's run moves up one.
				if (pivotLast != greaterFirst)
				{
					Swap(a, pivotFirst, pivotLast);
				}
				pivotLast++;
				Swap(a, pivotFirst++, greaterFirst++);
			}
			else if (greaterFirst == last)
			{
				// No room above: it moves down one.
				if (--lessLast != --pivotFirst)
				{
					Swap(a, lessLast, pivotFirst);
				}
				Swap(a, pivotFirst, --pivotLast);
			}
			else
			{
				Swap(a, greaterFirst++, --lessLast);
			}
		}
	}

	private static void PushHeap<T>(IList<T> a, int first, int hole, int top, T value, Func<T, T, bool> before)
	{
		for (int parent = (hole - 1) / 2; top < hole && before(a[first + parent], value); parent = (hole - 1) / 2)
		{
			a[first + hole] = a[first + parent];
			hole = parent;
		}
		a[first + hole] = value;
	}

	private static void AdjustHeap<T>(IList<T> a, int first, int hole, int bottom, T value, Func<T, T, bool> before)
	{
		int top = hole;
		int child = 2 * hole + 2;
		for (; child < bottom; child = 2 * child + 2)
		{
			if (before(a[first + child], a[first + child - 1]))
			{
				child--;
			}
			a[first + hole] = a[first + child];
			hole = child;
		}
		if (child == bottom)
		{
			a[first + hole] = a[first + bottom - 1];
			hole = bottom - 1;
		}
		PushHeap(a, first, hole, top, value, before);
	}

	private static void MakeHeap<T>(IList<T> a, int first, int last, Func<T, T, bool> before)
	{
		int bottom = last - first;
		for (int hole = bottom / 2; hole > 0;)
		{
			hole--;
			AdjustHeap(a, first, hole, bottom, a[first + hole], before);
		}
	}

	private static void SortHeap<T>(IList<T> a, int first, int last, Func<T, T, bool> before)
	{
		for (; last - first > 1; last--)
		{
			T value = a[last - 1];
			a[last - 1] = a[first];
			AdjustHeap(a, first, 0, last - 1 - first, value, before);
		}
	}
}
