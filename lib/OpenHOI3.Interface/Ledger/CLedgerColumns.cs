using System.Collections.Generic;

namespace OpenHOI3.Interface.Ledger;

/// <summary>
/// Where each page's columns stand, from the table's left edge: figures the original has in its code for each page, some of them overlapping and some leaving gaps, kept as
/// they are. See <c>docs/mechanics/statistics.md</c>, section 3.
/// </summary>
public static class CLedgerColumns
{
	/// <summary>The strategic resources' pages share this much among their columns, each stopping 12 short.</summary>
	private const int SharedRoom = 996;
	private const int SharedGap = 12;
	private const int ExtraWidth = 75;

	public static IReadOnlyList<(int Left, int Right)> Of(int page, int columns)
	{
		List<(int Left, int Right)> spans = page switch
		{
			0 => Running(columns, 130, 75),
			1 => Touching(150, 250, 350, 425, 500, 575, 625),
			2 => Stepped(columns, (-12, 72), 45, 28, 29),
			3 => Stepped(columns, (0, 100), 100, 58, 60),
			4 => Stepped(columns, (0, 128), 128, 65, 60),
			5 => Pairs(0, 150, 150, 375, 385, 450, 500, 700, 700, 800, 800, 900),
			6 => Touching(180, 255, 320, 405, 480, 555, 720),
			7 => Running(columns, 150, 50),
			8 => Running(columns, 130, 70),
			9 or 10 => Shared(columns),
			11 => Touching(85, 170, 230, 290, 350, 410, 470, 540, 600),
			12 => Touching(150, 325, 400, 600, 825),
			13 => Pairs(0, 150, 150, 200, 200, 250, 250, 300, 300, 375, 375, 500, 525, 650, 675, 750, 750, 825, 825, 900, 900, 975),
			14 => Touching(150, 200, 250, 300, 380, 555, 655, 850),
			15 => Touching(150, 200, 250, 300, 400, 520, 680, 900),
			16 => BrigadeFigures(),
			17 => ShipFigures(),
			18 => PlaneFigures(),
			19 or 20 or 21 => Pairs(0, 125, 130, 200, 200, 250, 250, 325, 325, 500, 505, 605, 610, 700, 705, 895, 800, 995),
			_ => new List<(int Left, int Right)>(),
		};
		while (spans.Count < columns)
		{
			int right = spans.Count == 0 ? 0 : spans[^1].Right;
			spans.Add((right, right + ExtraWidth));
		}
		return spans;
	}

	private static List<(int, int)> Pairs(params int[] edges)
	{
		var spans = new List<(int, int)>();
		for (int index = 0; index + 1 < edges.Length; index += 2)
		{
			spans.Add((edges[index], edges[index + 1]));
		}
		return spans;
	}

	private static List<(int, int)> Touching(params int[] rights)
	{
		var spans = new List<(int, int)>();
		int left = 0;
		foreach (int right in rights)
		{
			spans.Add((left, right));
			left = right;
		}
		return spans;
	}

	/// <summary>A title's width and then one width for the rest, each column starting where the last stopped.</summary>
	private static List<(int, int)> Running(int columns, int title, int width)
	{
		var spans = new List<(int, int)>();
		int left = 0;
		for (int index = 0; index < columns; index++)
		{
			int right = left + (index == 0 ? title : width);
			spans.Add((left, right));
			left = right;
		}
		return spans;
	}

	/// <summary>A first column of its own and then the rest a step apart.</summary>
	private static List<(int, int)> Stepped(int columns, (int, int) first, int start, int step, int width)
	{
		var spans = new List<(int, int)> { first };
		for (int index = 1; index < columns; index++)
		{
			int left = start + step * index;
			spans.Add((left, left + width));
		}
		return spans;
	}

	private static List<(int, int)> Shared(int columns)
	{
		var spans = new List<(int, int)>();
		int width = columns == 0 ? 0 : SharedRoom / columns;
		for (int index = 0; index < columns; index++)
		{
			spans.Add((index * width, (index + 1) * width - SharedGap));
		}
		return spans;
	}

	private static List<(int, int)> BrigadeFigures()
	{
		var spans = Touching(150, 200, 275);
		int left = 275;
		for (int index = 0; index < 16; index++)
		{
			spans.Add((left, left + 43));
			left += 43;
		}
		return spans;
	}

	private static List<(int, int)> ShipFigures()
	{
		var spans = Touching(130, 180, 285);
		AddFortyWide(spans, 13);
		int left = spans[^1].Item2 + 1;
		for (int index = 0; index < 3; index++)
		{
			spans.Add((left, left + 42));
			left += 43;
		}
		return spans;
	}

	private static List<(int, int)> PlaneFigures()
	{
		var spans = Touching(150, 200, 285);
		AddFortyWide(spans, 14);
		return spans;
	}

	private static void AddFortyWide(List<(int, int)> spans, int count)
	{
		const int First = 287;
		for (int index = 0; index < count; index++)
		{
			int left = First + 41 * index;
			spans.Add((left, left + 40));
		}
	}
}
