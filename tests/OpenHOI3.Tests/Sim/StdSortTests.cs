using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using Xunit;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The original runtime's <c>std::sort</c>. The two long orders are what the original's
/// own sort of the leader window gave for the same lists, run on its code
/// (<c>docs/mechanics/leaders.md</c>, section 12) - which of equals comes first is the
/// whole point.
/// </summary>
public class StdSortTests
{
	[Fact]
	public void ItSortsWhatever()
	{
		var random = new Random(1936);
		for (int trial = 0; trial < 200; trial++)
		{
			List<int> items = Enumerable.Range(0, random.Next(0, 400)).Select(_ => random.Next(0, 50)).ToList();
			CStdSort.Sort(items, (a, b) => a < b);
			for (int i = 1; i < items.Count; i++)
			{
				Assert.True(items[i - 1] <= items[i]);
			}
		}
	}

	[Fact]
	public void ShortListsKeepTheOrderOfEquals()
	{
		var items = new List<(int Key, int Index)> { (2, 0), (1, 1), (2, 2), (1, 3), (2, 4) };

		CStdSort.Sort(items, (a, b) => a.Key > b.Key);

		Assert.Equal(new[] { 0, 2, 4, 1, 3 }, items.Select(item => item.Index));
	}

	[Fact]
	public void SixtyLeadersBySkillComeOutInTheOriginalsOrder()
	{
		// Leader i has skill i x 7 mod 4: fifteen of each skill.
		List<int> leaders = Enumerable.Range(0, 60).ToList();

		CStdSort.Sort(leaders, (a, b) => a * 7 % 4 > b * 7 % 4);

		Assert.Equal(new[]
		{
			1, 25, 49, 5, 9, 45, 13, 29, 41, 17, 33, 21, 37, 53, 57, 14, 2, 6, 30, 46, 10, 26, 42, 18, 38, 22, 34, 50, 54, 58,
			7, 31, 35, 23, 39, 43, 47, 3, 51, 59, 11, 15, 55, 19, 27, 12, 8, 36, 48, 4, 28, 20, 24, 40, 16, 0, 56, 32, 44, 52,
		}, leaders);
	}

	[Fact]
	public void FortyFiveNamesComeOutInTheOriginalsOrder()
	{
		string[] stems = { "Ab", "Abc", "B", "Ba", "Abd", "C", "Ab" };
		string[] names = Enumerable.Range(0, 45).Select(i => stems[i % 7] + new string('x', i % 3)).ToArray();
		List<int> leaders = Enumerable.Range(0, 45).ToList();

		CStdSort.Sort(leaders, (a, b) => string.CompareOrdinal(names[a], names[b]) < 0);

		Assert.Equal(new[]
		{
			0, 6, 42, 21, 27, 36, 15, 1, 22, 43, 29, 8, 18, 39, 4, 25, 11, 32, 34, 28, 7, 13, 20, 14, 35, 41, 30, 9, 3, 24,
			31, 10, 17, 38, 16, 37, 2, 23, 44, 33, 12, 19, 40, 26, 5,
		}, leaders);
	}
}
