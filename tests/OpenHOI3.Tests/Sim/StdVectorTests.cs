using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using Xunit;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The original's removal from a vector, which moves the last entry into the place of the one taken out: China's Yan'an HQ
/// dropping two of five garrisons on the running game (<c>reversing/findings/FINDINGS-liveread.md</c>, section 71).
/// </summary>
public class StdVectorTests
{
	[Fact]
	public void TheLastTakesThePlaceOfTheOneRemoved()
	{
		var names = new[] { "4. Jun", "30. Jun", "31. Jun", "27. Jun", "28. Jun" };
		List<string> list = names.ToList();
		Assert.True(CStdVector.SwapRemove(list, names[1]));
		Assert.Equal(new[] { "4. Jun", "28. Jun", "31. Jun", "27. Jun" }, list);
		Assert.True(CStdVector.SwapRemove(list, names[4]));
		Assert.Equal(new[] { "4. Jun", "27. Jun", "31. Jun" }, list);
	}

	[Fact]
	public void TheLastRemovedLeavesTheRestAsTheyWere()
	{
		var names = new[] { "a", "b", "c" };
		List<string> list = names.ToList();
		Assert.True(CStdVector.SwapRemove(list, names[2]));
		Assert.Equal(new[] { "a", "b" }, list);
	}

	[Fact]
	public void AnEntryNotThereChangesNothing()
	{
		var names = new[] { "a", "b" };
		List<string> list = names.ToList();
		Assert.False(CStdVector.SwapRemove(list, "c"));
		Assert.Equal(names, list);
	}
}
