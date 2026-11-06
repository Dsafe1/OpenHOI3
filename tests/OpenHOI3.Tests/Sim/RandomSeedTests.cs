using OpenHOI3.Sim;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// How the game's generator turns a seed into its first state: the usual recurrence, its shift keeping the sign as the
/// original's does (<c>reversing/findings/FINDINGS-espionage.md</c>, the record's <c>MT19937Seed</c>).
/// </summary>
public class RandomSeedTests
{
	[Fact]
	public void SeedsShiftKeepsTheSign()
	{
		// 0x80000000 shifted 30 keeping the sign is 0xFFFFFFFE; without it, 2.
		uint[] words = new CRandom(0x80000000).Snapshot().Words;
		Assert.Equal(0x80000000u, words[0]);
		Assert.Equal(unchecked(0x6C078965u * (0x80000000u ^ 0xFFFFFFFEu) + 1u), words[1]);
	}

	[Fact]
	public void SmallSeedIsTheUsualRecurrenceWhileTheWordsArePositive()
	{
		// 42's first word has no top bit, so its second is the textbook one.
		uint[] words = new CRandom(42).Snapshot().Words;
		Assert.Equal(unchecked(0x6C078965u * (42u ^ (42u >> 30)) + 1u), words[1]);
	}
}
