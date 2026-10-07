using System;

namespace OpenHOI3.Sim;

/// <summary>
/// The original's second kind of number: a 64-bit value with 15 binary places, so that
/// 32768 is one. It is what a share of IC or of leadership is held in, and the day's
/// arithmetic on shares is done in it and only turned into thousandths at the end.
///
/// It is its own type because the two kinds round differently, and a rule worked out in
/// one must not be computed in the other. See <c>docs/mechanics/numbers.md</c>, section 2.
/// </summary>
public readonly record struct CFixed15(long Raw) : IComparable<CFixed15>
{
	public const int Places = 15;
	public const long Scale = 1L << Places;

	public static readonly CFixed15 Zero = new(0);
	public static readonly CFixed15 One = new(Scale);

	public static CFixed15 FromInt(long value)
	{
		return new CFixed15(value << Places);
	}

	/// <summary>A thousandths value as this kind. What does not fit in the 15 places is dropped.</summary>
	public static CFixed15 FromThousandths(CFixedPoint value)
	{
		return new CFixed15(value.Raw * Scale / CFixedPoint.Scale);
	}

	/// <summary>This as thousandths, rounded down.</summary>
	public CFixedPoint ToThousandths()
	{
		return new CFixedPoint((Raw * CFixedPoint.Scale) >> Places);
	}

	public static CFixed15 operator +(CFixed15 a, CFixed15 b) => new(a.Raw + b.Raw);

	public static CFixed15 operator -(CFixed15 a, CFixed15 b) => new(a.Raw - b.Raw);

	/// <summary>The product, with what falls below the 15 places dropped.</summary>
	public static CFixed15 operator *(CFixed15 a, CFixed15 b) => new((a.Raw * b.Raw) >> Places);

	public static bool operator <(CFixed15 a, CFixed15 b) => a.Raw < b.Raw;

	public static bool operator >(CFixed15 a, CFixed15 b) => a.Raw > b.Raw;

	public static bool operator <=(CFixed15 a, CFixed15 b) => a.Raw <= b.Raw;

	public static bool operator >=(CFixed15 a, CFixed15 b) => a.Raw >= b.Raw;

	public int CompareTo(CFixed15 other) => Raw.CompareTo(other.Raw);

	/// <summary>Five decimals, as the original's savegame writes a share.</summary>
	public override string ToString()
	{
		long hundredThousandths = (Math.Abs(Raw) * 100000 + Scale / 2) / Scale;
		return $"{(Raw < 0 ? "-" : "")}{hundredThousandths / 100000}.{hundredThousandths % 100000:D5}";
	}
}
