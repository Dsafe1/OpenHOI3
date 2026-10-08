using System;

namespace OpenHOI3.Sim;

/// <summary>
/// An amount of each of the seven goods. A province's production is one, a country's
/// daily home production another, a stockpile a third. See
/// <c>docs/mechanics/resources.md</c>.
/// </summary>
public sealed class CGoodsPool
{
	public const int Count = 7;

	/// <summary>The names the files and the savegame use, in the order of <see cref="EGoods"/>.</summary>
	private static readonly string[] Keys =
	{
		"supplies", "fuel", "money", "crude_oil", "metal", "energy", "rare_materials",
	};

	private readonly CFixedPoint[] _amounts = new CFixedPoint[Count];

	public CFixedPoint this[EGoods goods]
	{
		get => _amounts[(int)goods];
		set => _amounts[(int)goods] = value;
	}

	public static string KeyOf(EGoods goods)
	{
		return Keys[(int)goods];
	}

	public static bool TryParseKey(string? key, out EGoods goods)
	{
		for (int i = 0; i < Keys.Length; i++)
		{
			if (Keys[i].Equals(key, StringComparison.OrdinalIgnoreCase))
			{
				goods = (EGoods)i;
				return true;
			}
		}
		goods = default;
		return false;
	}

	public void Clear()
	{
		Array.Clear(_amounts);
	}

	public void CopyFrom(CGoodsPool other)
	{
		Array.Copy(other._amounts, _amounts, Count);
	}

	public void Add(CGoodsPool other)
	{
		for (int i = 0; i < Count; i++)
		{
			_amounts[i] += other._amounts[i];
		}
	}

	public override string ToString()
	{
		var parts = new System.Collections.Generic.List<string>();
		for (int i = 0; i < Count; i++)
		{
			if (_amounts[i] != CFixedPoint.Zero)
			{
				parts.Add($"{Keys[i]} {_amounts[i]}");
			}
		}
		return string.Join(", ", parts);
	}
}
