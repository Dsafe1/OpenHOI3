using System;
using System.Collections.Generic;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The credits rolling: thirty-five boxes twenty apart, the first forty below the band's lower edge, each moving up
/// twenty a second; a box is shown below the lower edge and hidden above the upper, and one that leaves at the top takes
/// the next line and comes back in seven hundred lower, under the lowest. With no line left a box stops a hundred above
/// the upper edge, hidden, and nothing more happens. Every number is the original's own. See
/// <c>docs/mechanics/frontend.md</c>, section 10.
/// </summary>
public sealed class CCreditsScroll
{
	public const int SlotCount = 35;
	public const float Spacing = 20f;

	/// <summary>What a box moves up in a second.</summary>
	public const float Speed = 20f;

	/// <summary>Above this a box is hidden.</summary>
	public const float Top = -86f;

	/// <summary>Below this a box is shown.</summary>
	public const float Bottom = 220f;

	private const float FirstBelow = 40f;
	private const float Parked = Top - 100f;

	private readonly int _lineCount;

	/// <param name="lineCount">How many lines there are. Ours: with fewer than the boxes the rest show nothing, where the original reads past its lines.</param>
	public CCreditsScroll(int lineCount)
	{
		_lineCount = lineCount;
		var slots = new CCreditsSlot[SlotCount];
		for (int i = 0; i < SlotCount; i++)
		{
			slots[i] = new CCreditsSlot { Y = Bottom + FirstBelow + Spacing * i, Line = i < lineCount ? i : -1 };
		}
		Slots = slots;
		NextLine = Math.Min(SlotCount, lineCount);
	}

	public IReadOnlyList<CCreditsSlot> Slots { get; }

	/// <summary>The next line a box leaving at the top takes.</summary>
	public int NextLine { get; private set; }

	/// <summary>The time passed, in seconds: every box moved, shown or hidden, and given its next line where it left.</summary>
	public void Step(float seconds)
	{
		foreach (CCreditsSlot slot in Slots)
		{
			slot.Y -= seconds * Speed;
			if (slot.Y < Top)
			{
				slot.Shown = false;
			}
			else if (slot.Y < Bottom)
			{
				slot.Shown = true;
			}
			if (slot.Y < Top && NextLine < _lineCount)
			{
				// A new box, hidden until its next frame finds it in the band.
				slot.Y += SlotCount * Spacing;
				slot.Line = NextLine++;
				slot.Shown = false;
			}
			else if (slot.Y < Parked)
			{
				slot.Y = Parked;
			}
		}
	}
}
