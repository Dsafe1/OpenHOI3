using System;
using System.Globalization;

namespace OpenHOI3.Sim;

/// <summary>
/// A number held as a whole count of thousandths, which is how the original holds
/// nearly every quantity in the game: 237964.731 is the integer 237964731.
///
/// Nothing in the game state is a <c>float</c> or a <c>double</c>. Those do not give
/// the same answer on every machine, and a simulation that players share has to. The
/// rules this type follows, and which of them are the original's and which are ours,
/// are in <c>docs/mechanics/numbers.md</c>.
/// </summary>
public readonly record struct CFixedPoint(long Raw) : IComparable<CFixedPoint>
{
	/// <summary>How many raw units make one.</summary>
	public const int Scale = 1000;

	public static readonly CFixedPoint Zero = new(0);
	public static readonly CFixedPoint One = new(Scale);

	public static CFixedPoint FromInt(long whole)
	{
		return new CFixedPoint(whole * Scale);
	}

	/// <summary>The whole part, dropping the fraction: 2.9 is 2 and -2.9 is -2.</summary>
	public long ToInt()
	{
		return Raw / Scale;
	}

	/// <summary>
	/// Reads a decimal number as the data files write one. The first three fractional
	/// digits are kept and any further ones dropped; a sign applies to the whole
	/// number, so <c>-0.5</c> is minus a half.
	/// </summary>
	public static bool TryParse(string? text, out CFixedPoint value)
	{
		value = Zero;
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}

		ReadOnlySpan<char> span = text.AsSpan().Trim();
		bool negative = false;
		if (span[0] == '-' || span[0] == '+')
		{
			negative = span[0] == '-';
			span = span[1..];
		}

		int point = span.IndexOf('.');
		ReadOnlySpan<char> whole = point < 0 ? span : span[..point];
		ReadOnlySpan<char> fraction = point < 0 ? ReadOnlySpan<char>.Empty : span[(point + 1)..];
		if (whole.Length == 0 && fraction.Length == 0)
		{
			return false;
		}

		long raw = 0;
		foreach (char c in whole)
		{
			if (!char.IsAsciiDigit(c) || raw > long.MaxValue / 10 / Scale)
			{
				return false;
			}
			raw = raw * 10 + (c - '0');
		}
		raw *= Scale;

		int place = Scale / 10;
		foreach (char c in fraction)
		{
			if (!char.IsAsciiDigit(c))
			{
				return false;
			}
			raw += (c - '0') * place;
			place /= 10;                                     // reaches 0 after three digits
		}

		value = new CFixedPoint(negative ? -raw : raw);
		return true;
	}

	public static CFixedPoint Parse(string text)
	{
		return TryParse(text, out CFixedPoint value)
			? value
			: throw new FormatException($"'{text}' is not a number");
	}

	public static CFixedPoint operator +(CFixedPoint a, CFixedPoint b) => new(a.Raw + b.Raw);

	public static CFixedPoint operator -(CFixedPoint a, CFixedPoint b) => new(a.Raw - b.Raw);

	public static CFixedPoint operator -(CFixedPoint a) => new(-a.Raw);

	/// <summary>Truncates toward zero, as integer division does.</summary>
	public static CFixedPoint operator *(CFixedPoint a, CFixedPoint b) => new(a.Raw * b.Raw / Scale);

	/// <summary>Truncates toward zero, as integer division does.</summary>
	public static CFixedPoint operator /(CFixedPoint a, CFixedPoint b) => new(a.Raw * Scale / b.Raw);

	/// <summary>Scaling by a whole number is exact.</summary>
	public static CFixedPoint operator *(CFixedPoint a, long b) => new(a.Raw * b);

	/// <summary>Truncates toward zero.</summary>
	public static CFixedPoint operator /(CFixedPoint a, long b) => new(a.Raw / b);

	public static bool operator <(CFixedPoint a, CFixedPoint b) => a.Raw < b.Raw;

	public static bool operator >(CFixedPoint a, CFixedPoint b) => a.Raw > b.Raw;

	public static bool operator <=(CFixedPoint a, CFixedPoint b) => a.Raw <= b.Raw;

	public static bool operator >=(CFixedPoint a, CFixedPoint b) => a.Raw >= b.Raw;

	public int CompareTo(CFixedPoint other)
	{
		return Raw.CompareTo(other.Raw);
	}

	public static CFixedPoint Min(CFixedPoint a, CFixedPoint b) => a.Raw <= b.Raw ? a : b;

	public static CFixedPoint Max(CFixedPoint a, CFixedPoint b) => a.Raw >= b.Raw ? a : b;

	public static CFixedPoint Clamp(CFixedPoint value, CFixedPoint low, CFixedPoint high)
	{
		return Max(low, Min(value, high));
	}

	/// <summary>Always three decimals, as the savegame writes them: <c>5.000</c>.</summary>
	public override string ToString()
	{
		long magnitude = Math.Abs(Raw);
		string sign = Raw < 0 ? "-" : "";
		return string.Create(CultureInfo.InvariantCulture,
			$"{sign}{magnitude / Scale}.{magnitude % Scale:000}");
	}
}
