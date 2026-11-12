using System.Linq;
using OpenHOI3.Sim;
using Xunit;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The walk of the original runtime's <c>stdext::hash_map&lt;int, ...&gt;</c>: each order is what the running game's
/// sealift vote walked, its keys added in the order given (<c>reversing/findings/FINDINGS-liveread.md</c>, section 62).
/// </summary>
public class StdHashMapTests
{
	[Theory]
	[InlineData(new[] { 6509, 6536, 10328, 10262, 10302, 6518, 6582, 6545, 6439 },
		new[] { 6509, 6536, 10328, 6518, 6582, 10262, 10302, 6545, 6439 })]
	[InlineData(new[] { 9590, 9569, 5002 }, new[] { 9590, 9569, 5002 })]
	[InlineData(new[] { 6173, 9961, 10636 }, new[] { 6173, 9961, 10636 })]
	[InlineData(new[] { 5138, 10731 }, new[] { 5138, 10731 })]
	public void ItWalksAsTheOriginal(int[] added, int[] walked)
	{
		var map = new CStdHashMap();
		foreach (int key in added)
		{
			map.Add(key, 1);
		}
		Assert.Equal(walked, map.Walk().Select(entry => entry.Key).ToArray());
	}

	[Fact]
	public void AKeyAddedAgainSumsInPlace()
	{
		var map = new CStdHashMap();
		foreach (int key in new[] { 7238, 7238, 7238, 7238, 7238 })
		{
			map.Add(key, 1);
		}
		Assert.Equal((7238, 5L), map.Walk().Single());
	}

	[Fact]
	public void TheRunsOfABucketKeepAscending()
	{
		// The Australian walk of another run: 6431 and 6439 share a bucket, as 5920 and 6536 do.
		Assert.Equal(CStdHashMap.BucketOf(6431), CStdHashMap.BucketOf(6439));
		Assert.Equal(CStdHashMap.BucketOf(5920), CStdHashMap.BucketOf(6536));
		var map = new CStdHashMap();
		foreach (int key in new[] { 6439, 5905, 6536, 6431, 5920 })
		{
			map.Add(key, 1);
		}
		Assert.Equal(new[] { 6431, 6439, 5905, 5920, 6536 }, map.Walk().Select(entry => entry.Key).ToArray());
	}
}
