using System;
using System.Text;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Sim.Trade;

/// <summary>
/// What a trade route moves each day, in thousandths: <see cref="From"/> is what its <c>from</c>
/// hands over and <see cref="To"/> what its <c>to</c> does. One block holds goods and the other
/// money, their price. Two terms are equal where every figure of both blocks is. See
/// <c>docs/mechanics/trade.md</c>, section 1.
/// </summary>
public sealed class CTradeTerms : IEquatable<CTradeTerms>
{
	private readonly long[] _from;
	private readonly long[] _to;

	public CTradeTerms()
		: this(new long[CGoodsPool.Count], new long[CGoodsPool.Count])
	{
	}

	private CTradeTerms(long[] from, long[] to)
	{
		_from = from;
		_to = to;
	}

	/// <summary>Terms of nothing either way.</summary>
	public static CTradeTerms None { get; } = new();

	/// <summary>What the route's <c>from</c> hands over a day of a good.</summary>
	public long From(EGoods goods) => _from[(int)goods];

	/// <summary>What the route's <c>to</c> hands over a day of a good.</summary>
	public long To(EGoods goods) => _to[(int)goods];

	/// <summary>The same terms with one figure of a block changed.</summary>
	public CTradeTerms With(bool fromBlock, EGoods goods, long amount)
	{
		var from = (long[])_from.Clone();
		var to = (long[])_to.Clone();
		(fromBlock ? from : to)[(int)goods] = amount;
		return new CTradeTerms(from, to);
	}

	/// <summary>Terms made from two blocks of seven, in the goods' order.</summary>
	public static CTradeTerms Of(long[] from, long[] to)
	{
		if (from.Length != CGoodsPool.Count || to.Length != CGoodsPool.Count)
		{
			throw new ArgumentException("a block of terms has seven goods");
		}
		return new CTradeTerms((long[])from.Clone(), (long[])to.Clone());
	}

	public bool Equals(CTradeTerms? other)
	{
		return other is not null && _from.AsSpan().SequenceEqual(other._from) && _to.AsSpan().SequenceEqual(other._to);
	}

	public override bool Equals(object? obj) => Equals(obj as CTradeTerms);

	public override int GetHashCode()
	{
		var hash = new HashCode();
		foreach (long figure in _from)
		{
			hash.Add(figure);
		}
		foreach (long figure in _to)
		{
			hash.Add(figure);
		}
		return hash.ToHashCode();
	}

	public override string ToString()
	{
		var text = new StringBuilder("from");
		for (int i = 0; i < CGoodsPool.Count; i++)
		{
			text.Append(' ').Append(_from[i]);
		}
		text.Append(" to");
		for (int i = 0; i < CGoodsPool.Count; i++)
		{
			text.Append(' ').Append(_to[i]);
		}
		return text.ToString();
	}
}
