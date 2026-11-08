namespace OpenHOI3.Sim.StrategicWarfare;

/// <summary>A record's naval, air or land figures: a ring of 30 days, one of 12 months and one of the game's years. See <c>docs/mechanics/strategicwarfare.md</c>, section 2.</summary>
public sealed class CWarfareCategory
{
	public const int Days = 30;
	public const int Months = 12;

	public CWarfareCategory(int years)
	{
		DayRing = new CWarfareRing(Days, offset: 1);
		MonthRing = new CWarfareRing(Months);
		YearRing = new CWarfareRing(years);
	}

	public CWarfareRing DayRing { get; }

	public CWarfareRing MonthRing { get; }

	public CWarfareRing YearRing { get; }

	/// <summary>The ring a window's duration shows: the last month's days, the last year's months, or the conflict's years.</summary>
	public CWarfareRing Ring(int duration) => duration switch
	{
		0 => DayRing,
		1 => MonthRing,
		_ => YearRing,
	};

	/// <summary>Adds to today's element.</summary>
	public void Add(int column, CFixed15 value)
	{
		DayRing[DayRing.Current, column] += value.Raw;
	}

	public void AdvanceDay()
	{
		// The sums read the slot about to be emptied, so a month's element is a rolling thirty days.
		if (DayRing.Advance())
		{
			DayRing.SumInto(MonthRing, MonthRing.Offset);
			if (MonthRing.Advance())
			{
				MonthRing.SumInto(YearRing, YearRing.Offset);
				YearRing.Advance();
			}
			MonthRing.EmptyCurrent();
		}
		else
		{
			DayRing.SumInto(MonthRing, MonthRing.Offset);
			MonthRing.SumInto(YearRing, YearRing.Offset);
		}
		DayRing.EmptyCurrent();
	}

	public void Zero()
	{
		DayRing.Zero();
		MonthRing.Zero();
		YearRing.Zero();
	}
}
