namespace OpenHOI3.Sim.Combat;

/// <summary>
/// The rolls of one side's shooting in one hour: who is shot at, whether a fraction of
/// a shot is fired, and how hard a hit lands. The original gives each side a small
/// generator of its own for these, started from one draw of the game's, so that a
/// side's hour costs the game exactly one draw however many shots it fires.
/// </summary>
public sealed class CCombatStream
{
	private const uint Multiplier = 0x343FD;
	private const uint Increment = 0x269EC3;

	private uint _state;

	/// <summary>Where the rolls stand, for a tool's trace.</summary>
	public uint State => _state;

	public CCombatStream(int seed)
	{
		_state = (uint)seed;
	}

	/// <summary>A draw: two steps of the generator, the upper fifteen bits of each multiplied.</summary>
	public long Next()
	{
		uint first = Step();
		uint second = Step();
		return (long)first * second;
	}

	/// <summary>A draw from 0 to one less than <paramref name="sides"/>.</summary>
	public int Next(long sides)
	{
		return sides <= 0 ? 0 : (int)(Next() % sides);
	}

	private uint Step()
	{
		_state = unchecked(_state * Multiplier + Increment);
		return (_state >> 16) & 0x7FFF;
	}
}
