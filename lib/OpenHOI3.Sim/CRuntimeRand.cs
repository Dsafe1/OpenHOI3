namespace OpenHOI3.Sim;

/// <summary>
/// The C runtime's <c>rand()</c> as the original's compiler ships it: <c>x = x * 214013 + 2531011</c>,
/// answering bits 16 to 30 of <c>x</c>, 0 to 32767. Not the game's generator (<see cref="CRandom"/>):
/// the original uses it in a few places of its own, each with its own seed - the player's automate-trade
/// seeds it to 1 every hour (<c>docs/mechanics/trade.md</c>, section 12).
/// </summary>
public sealed class CRuntimeRand
{
	private uint _state;

	public CRuntimeRand(uint seed)
	{
		_state = seed;
	}

	public int Next()
	{
		_state = unchecked(_state * 214013u + 2531011u);
		return (int)((_state >> 16) & 0x7FFF);
	}
}
