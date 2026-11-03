namespace OpenHOI3.Sim.Replay;

/// <summary>
/// FNV-1a over 64 bits, fed whole numbers and text: the same things in the same order give the same value on any machine.
/// For <see cref="CStateHash"/>, which is not a defence against anyone, only a check that two runs agree.
/// </summary>
public sealed class CHasher
{
	private const ulong Offset = 14695981039346656037;
	private const ulong Prime = 1099511628211;

	private ulong _value;
	private bool _started;

	public ulong Value => _started ? _value : Offset;

	public void Add(long value)
	{
		if (!_started)
		{
			_value = Offset;
			_started = true;
		}
		unchecked
		{
			for (int i = 0; i < 8; i++)
			{
				_value ^= (byte)(value >> (i * 8));
				_value *= Prime;
			}
		}
	}

	public void Add(int value)
	{
		Add((long)value);
	}

	public void Add(ulong value)
	{
		Add(unchecked((long)value));
	}

	public void Add(bool value)
	{
		Add(value ? 1L : 0L);
	}

	public void Add(string? text)
	{
		text ??= "";
		Add(text.Length);
		foreach (char c in text)
		{
			Add((long)c);
		}
	}
}
