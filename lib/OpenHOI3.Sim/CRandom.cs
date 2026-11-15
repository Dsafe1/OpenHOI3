namespace OpenHOI3.Sim;

/// <summary>
/// The game's one source of chance. Everything that rolls draws from it in turn, so a
/// game replayed from the same commands rolls the same.
///
/// It is the generator the original uses, a Mersenne Twister with two things about it
/// that are the original's own: the shifts to the right keep the sign, and the twist
/// leaves a word alone that is both odd and negative - its test is a signed remainder,
/// y % 2 == 1. A draw is never negative, which is why the original can write a roll as a
/// plain remainder.
///
/// The seed turns into the first state as the usual way does but for the shift, which
/// keeps the sign here too (<c>reversing/findings/FINDINGS-espionage.md</c>, the record's
/// <c>MT19937Seed</c>; read 2026-11-07 - until then the usual, unsigned shift was used).
/// See <c>docs/mechanics/landcombat.md</c>, section 8.
/// </summary>
public sealed class CRandom
{
	private const int Size = 624;
	private const int Offset = 397;
	private const uint TwistConstant = 0x9908B0DF;
	private const uint SeedMultiplier = 1812433253;

	private readonly uint[] _words = new uint[Size];
	private int _index;

	public CRandom(uint seed = 1)
	{
		Seed(seed);
	}

	/// <summary>How many draws have been made since the last seed.</summary>
	public long Draws { get; private set; }

	public void Seed(uint seed)
	{
		_words[0] = seed;
		for (int i = 1; i < Size; i++)
		{
			// The shift keeps the sign, as every shift of the original's generator does [exe].
			_words[i] = SeedMultiplier * (_words[i - 1] ^ (uint)((int)_words[i - 1] >> 30)) + (uint)i;
		}
		_index = 0;
		Draws = 0;
	}

	/// <summary>The next draw: a whole number from 0 up.</summary>
	public int Next()
	{
		if (_index == 0)
		{
			Twist();
		}
		int y = (int)_words[_index];
		y ^= y >> 11;
		y ^= (int)(((uint)y & 0xFF3A58AD) << 7);
		y ^= (int)(((uint)y & 0xFFFFDF8C) << 15);
		y ^= y >> 18;
		_index = (_index + 1) % Size;
		Draws++;
		return y;
	}

	/// <summary>Moves on by that many draws, as if they had been made: where a replay stands in for what drew them.</summary>
	public void Skip(long draws)
	{
		for (long i = 0; i < draws; i++)
		{
			Next();
		}
	}

	/// <summary>
	/// Its state, to put back with <see cref="Restore"/>: for what only shows what a roll would give - a
	/// dialog's chance - and must leave the game's rolls as they were.
	/// </summary>
	public (uint[] Words, int Index, long Draws) Snapshot()
	{
		return ((uint[])_words.Clone(), _index, Draws);
	}

	/// <summary>Puts back a state <see cref="Snapshot"/> took.</summary>
	public void Restore((uint[] Words, int Index, long Draws) saved)
	{
		saved.Words.CopyTo(_words, 0);
		_index = saved.Index;
		Draws = saved.Draws;
	}

	/// <summary>A draw from 0 to one less than <paramref name="sides"/>.</summary>
	public int Next(int sides)
	{
		return sides <= 0 ? 0 : Next() % sides;
	}

	/// <summary>
	/// Where a draw made for a country is answered from instead, a value the original drew for it, or null to leave the
	/// draw this generator's own: a measuring instrument that plays a run of the original's back draw for draw
	/// (<c>docs/replay.md</c>, section 9). Nothing in a game sets it; the generator moves on by the draw either way.
	/// </summary>
	public System.Func<Countries.CCountryTag, string?, int?>? Feed { get; set; }

	/// <summary>The kind of a draw the hourly trading makes, which the original makes inside the hour's pass.</summary>
	public const string TradeKind = "trade";

	/// <summary>
	/// The next draw, made for that country: <see cref="Next()"/>, unless <see cref="Feed"/> answers it. A draw made apart
	/// from the country's pass in the original - its hourly trading's - names its <paramref name="kind"/>.
	/// </summary>
	public int Next(Countries.CCountryTag who, string? kind = null)
	{
		int own = Next();
		return Feed?.Invoke(who, kind) ?? own;
	}

	/// <summary>A draw from 0 to one less than <paramref name="sides"/>, made for that country.</summary>
	public int Next(int sides, Countries.CCountryTag who, string? kind = null)
	{
		return sides <= 0 ? 0 : Next(who, kind) % sides;
	}

	private void Twist()
	{
		for (int i = 0; i < Size; i++)
		{
			uint next = _words[(i + 1) % Size];
			uint y = ((next ^ _words[i]) & 0x7FFFFFFF) ^ _words[i];
			uint word = _words[(i + Offset) % Size] ^ (uint)((int)y >> 1);
			if ((y & 0x80000001) == 1)
			{
				word ^= TwistConstant;
			}
			_words[i] = word;
		}
	}
}
