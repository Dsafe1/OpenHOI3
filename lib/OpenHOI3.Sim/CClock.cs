using System;

namespace OpenHOI3.Sim;

/// <summary>
/// Turns real time into hours owed. It paces a game and is no part of one: nothing
/// here is saved, and two machines running the same commands agree whatever their
/// clocks say. That is why this is the one place a <c>double</c> appears.
///
/// The original keeps an accumulator and a table of five thresholds, one per speed.
/// The thresholds here are our own, as are the separate pause and the cap on time owed.
/// See <c>docs/mechanics/tick.md</c>, section 2.
/// </summary>
public sealed class CClock
{
	public const int SpeedCount = 5;

	/// <summary>Returned for the top speed: as many hours as the caller has time for.</summary>
	public const int Unlimited = int.MaxValue;

	/// <summary>Real seconds for one game hour at each speed. Zero means no waiting at all.</summary>
	private static readonly double[] SecondsPerHour = { 0.5, 0.25, 0.1, 0.04, 0.0 };

	/// <summary>
	/// The most real time that may be owed. Without it a stall - a long load, a window
	/// being dragged - would be repaid as a burst of hours the moment it ended. At the
	/// slow speeds the cap is two hours' worth instead, because a cap of less than that
	/// would swallow the remainder an ordinary frame carries over.
	/// </summary>
	private const double MostOwed = 0.5;

	private int _speed = 2;
	private double _owed;

	/// <summary>0 is the slowest and <see cref="SpeedCount"/> - 1 the fastest.</summary>
	public int Speed
	{
		get => _speed;
		set => _speed = Math.Clamp(value, 0, SpeedCount - 1);
	}

	/// <summary>A game starts paused, as the original's does.</summary>
	public bool Paused { get; set; } = true;

	/// <summary>
	/// How many hours have come due after another stretch of real time. Time left over
	/// is carried to the next call, so a slow frame is caught up and not lost.
	/// </summary>
	public int HoursDue(double realSeconds)
	{
		if (Paused)
		{
			_owed = 0;
			return 0;
		}

		double perHour = SecondsPerHour[_speed];
		if (perHour <= 0)
		{
			_owed = 0;
			return Unlimited;
		}

		_owed = Math.Min(_owed + Math.Max(realSeconds, 0), Math.Max(MostOwed, perHour * 2));
		int due = (int)(_owed / perHour);
		_owed -= due * perHour;
		return due;
	}
}
