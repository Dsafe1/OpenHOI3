using System.Collections.Generic;

namespace OpenHOI3.Sim;

/// <summary>
/// The original's own ways of changing a vector where the order left behind shows. Its usual removal of a value overwrites
/// the first entry equal to it with the last entry and drops the last, so the entries after it do not close up in order:
/// a list 4, 30, 31, 27, 28 that loses 30 reads 4, 28, 31, 27. Read on the listing and seen in the garrison split's drop on
/// the running game (<c>reversing/findings/FINDINGS-liveread.md</c>, section 71).
/// </summary>
public static class CStdVector
{
	/// <summary>Removes the first entry equal to the value by moving the last into its place; whether one was found.</summary>
	public static bool SwapRemove<T>(List<T> list, T value) where T : class
	{
		int at = list.FindIndex(entry => ReferenceEquals(entry, value));
		if (at < 0)
		{
			return false;
		}
		list[at] = list[^1];
		list.RemoveAt(list.Count - 1);
		return true;
	}

	/// <summary>The same for a vector of numbers - provinces: the first entry equal to the value overwritten by the last.</summary>
	public static bool SwapRemove(List<int> list, int value)
	{
		int at = list.IndexOf(value);
		if (at < 0)
		{
			return false;
		}
		list[at] = list[^1];
		list.RemoveAt(list.Count - 1);
		return true;
	}
}
