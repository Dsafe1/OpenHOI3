using System;
using OpenHOI3.Sim;

namespace OpenHOI3.Interface;

/// <summary>
/// The sliders of one screen, which between them always hold the whole of what they
/// divide - a country's IC, or its leadership. Each holds an amount and not a share,
/// as the original's do; when one is moved the others that are free to move take up
/// the difference in equal parts. See <c>docs/mechanics/sliders.md</c>, section 3.
///
/// Amounts are thousandths, so nothing is lost in the sharing: the original works in
/// floats and repeats the step up to ten times a frame until what is left is under
/// half a hundredth.
/// </summary>
public sealed class CSliderGroup
{
	private readonly long[] _values;
	private readonly bool[] _held;
	private readonly long[] _limits;
	private long _total;

	/// <summary>
	/// The least excess over a limit that <see cref="ClampToLimits"/> takes back, in thousandths: the original rounds the
	/// amount to the hundredth, so anything under 0.005 of an IC comes to nothing (sliders.md, section 3).
	/// </summary>
	private const long LeastClamped = 5;

	public CSliderGroup(int count)
	{
		_values = new long[count];
		_held = new bool[count];
		_limits = new long[count];
		Array.Fill(_limits, -1);
	}

	/// <summary>
	/// The most a slider may hold below the total, or none: the original's limit of a share, which only lend-lease has -
	/// nought where its country gives lend-lease to nobody (<c>reversing/findings/FINDINGS-lendleaseslider.md</c>).
	/// </summary>
	public void SetLimit(int index, CFixedPoint? limit)
	{
		_limits[index] = limit is { } most ? Math.Max(most.Raw, 0) : -1;
	}

	/// <summary>
	/// Puts every slider over its limit down to it, the others that are free taking up what it held in equal parts, as the
	/// original's production screen does each frame it is open. A locked slider is left where it is, as the original's
	/// is. Answers whether anything moved.
	/// </summary>
	public bool ClampToLimits()
	{
		bool moved = false;
		for (int index = 0; index < _values.Length; index++)
		{
			if (_limits[index] >= 0 && !_held[index] && _values[index] - _limits[index] >= LeastClamped)
			{
				_values[index] = _limits[index];
				Settle(index);
				moved = true;
			}
		}
		return moved;
	}

	/// <summary>The most a slider may hold: the total, or its limit where that is less.</summary>
	private long Ceiling(int index)
	{
		return _limits[index] >= 0 ? Math.Min(_total, _limits[index]) : _total;
	}

	public int Count => _values.Length;

	/// <summary>What the sliders divide: the most any one of them can hold.</summary>
	public CFixedPoint Total => new(_total);

	/// <summary>What one slider holds.</summary>
	public CFixedPoint this[int index] => new(_values[index]);

	/// <summary>A slider that is held stays where it is when another is moved.</summary>
	public bool IsHeld(int index)
	{
		return _held[index];
	}

	public void SetHeld(int index, bool held)
	{
		_held[index] = held;
	}

	/// <summary>
	/// Puts every slider where a country's shares say. What the shares leave over - they
	/// are cut off, not rounded - goes to the sliders that are free.
	/// </summary>
	public void Load(CFixedPoint total, ReadOnlySpan<CFixed15> shares)
	{
		_total = Math.Max(total.Raw, 0);
		for (int index = 0; index < _values.Length; index++)
		{
			long share = index < shares.Length ? shares[index].Raw : 0;
			_values[index] = Math.Clamp((share * _total) >> CFixed15.Places, 0, _total);
		}
		Settle(-1);
	}

	/// <summary>The player moves one slider, no further than its limit. The free ones take up the difference.</summary>
	public void Move(int index, CFixedPoint value)
	{
		_values[index] = Math.Clamp(value.Raw, 0, Ceiling(index));
		Settle(index);
	}

	/// <summary>The share each slider stands for, cut off so that they never come to more than the whole.</summary>
	public CFixed15[] Shares()
	{
		var shares = new CFixed15[_values.Length];
		for (int index = 0; index < shares.Length; index++)
		{
			shares[index] = _total <= 0 ? CFixed15.Zero : new CFixed15((_values[index] << CFixed15.Places) / _total);
		}
		return shares;
	}

	/// <summary>
	/// Hands what the sliders are short of the total, or over it, to the ones free to
	/// move - not held, and not the one just moved - in equal parts, a part a slider
	/// cannot take going round again. If none of them can take it the moved slider
	/// gives way instead.
	/// </summary>
	private void Settle(int moved)
	{
		long left = _total - Sum();
		while (left != 0)
		{
			int able = 0;
			for (int index = 0; index < _values.Length; index++)
			{
				if (IsFree(index, moved) && CanTake(index, left))
				{
					able++;
				}
			}
			if (able == 0)
			{
				break;
			}

			// Whole thousandths only: what does not divide goes to the first few, one each.
			long part = left / able;
			long odd = Math.Abs(left % able);
			long step = Math.Sign(left);
			for (int index = 0; index < _values.Length; index++)
			{
				if (!IsFree(index, moved) || !CanTake(index, left))
				{
					continue;
				}
				long wanted = part;
				if (odd > 0)
				{
					wanted += step;
					odd--;
				}
				long before = _values[index];
				_values[index] = Math.Clamp(before + wanted, 0, Ceiling(index));
				left -= _values[index] - before;
			}
		}

		if (left != 0 && moved >= 0)
		{
			_values[moved] = Math.Clamp(_values[moved] + left, 0, Ceiling(moved));
		}
	}

	private bool IsFree(int index, int moved)
	{
		return index != moved && !_held[index];
	}

	private bool CanTake(int index, long left)
	{
		return left > 0 ? _values[index] < Ceiling(index) : _values[index] > 0;
	}

	private long Sum()
	{
		long sum = 0;
		foreach (long value in _values)
		{
			sum += value;
		}
		return sum;
	}
}
