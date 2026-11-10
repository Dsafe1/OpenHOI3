using OpenHOI3.Sim;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A generator set from the game's seed and the hour, for a test that wants one roll of a pass and the same one each time.
/// A game's generator is seeded once and runs on (<c>docs/mechanics/tick.md</c>, section 3).
/// </summary>
internal static class CTestSeeding
{
	public static void SeedHour(this CGameState state)
	{
		unchecked
		{
			state.Random.Seed(state.Seed * 2654435761u + (uint)state.TotalHours * 40503u + (uint)(state.TotalHours >> 32));
		}
	}
}
