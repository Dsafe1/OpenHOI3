namespace OpenHOI3.Sim;

/// <summary>
/// The game's one source of chance. Everything that rolls draws from it in turn, so a
/// game replayed from the same commands rolls the same.
///
/// It is the generator the original uses, a Mersenne Twister with two things about it
/// that are the original's own: the shifts to the right keep the sign, and the twist
/// leaves a word alone that is both odd and negative. A draw is never negative, which
/// is why the original can write a roll as a plain remainder.
///
/// How the original turns a seed into its first state was not read; the usual way is
/// used here. See <c>docs/mechanics/landcombat.md</c>, section 8.
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
			_words[i] = SeedMultiplier * (_words[i - 1] ^ (_words[i - 1] >> 30)) + (uint)i;
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

	/// <summary>A draw from 0 to one less than <paramref name="sides"/>.</summary>
	public int Next(int sides)
	{
		return sides <= 0 ? 0 : Next() % sides;
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
