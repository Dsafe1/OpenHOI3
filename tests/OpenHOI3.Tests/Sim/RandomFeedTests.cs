using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The generator's feed, the measuring instrument that plays a run of the original's back draw for draw: a draw made for a
/// country is answered from it, the generator moves on either way, and with nothing set nothing changes
/// (<c>docs/replay.md</c>, section 9).
/// </summary>
public class RandomFeedTests
{
	private static readonly CCountryTag Germany = new("GER");
	private static readonly CCountryTag Italy = new("ITA");

	[Fact]
	public void WithNoFeedADrawForACountryIsThePlainDraw()
	{
		var plain = new CRandom(7);
		var named = new CRandom(7);
		for (int i = 0; i < 5; i++)
		{
			Assert.Equal(plain.Next(), named.Next(Germany));
		}
		Assert.Equal(plain.Next(6), named.Next(6, Italy, CRandom.TradeKind));
	}

	[Fact]
	public void TheFeedAnswersByCountryAndKindAndTheGeneratorMovesOn()
	{
		var queues = new Dictionary<(string, string?), Queue<int>>
		{
			[("GER", null)] = new(new[] { 11, 12 }),
			[("ITA", CRandom.TradeKind)] = new(new[] { 5 }),
		};
		var fed = new CRandom(7)
		{
			Feed = (who, kind) => queues.TryGetValue((who.Value, kind), out Queue<int>? queue) && queue.Count > 0 ? queue.Dequeue() : null,
		};
		var plain = new CRandom(7);

		Assert.Equal(11, fed.Next(Germany));
		Assert.Equal(5 % 6, fed.Next(6, Italy, CRandom.TradeKind));
		Assert.Equal(12, fed.Next(Germany));
		plain.Skip(3);
		// Italy's pass has nothing left, so the draw is the generator's own - three along, as the fed ones moved it.
		Assert.Equal(plain.Next(), fed.Next(Italy));
		Assert.Equal(plain.Draws, fed.Draws);
	}
}
