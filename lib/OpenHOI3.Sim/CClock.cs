using System;

namespace OpenHOI3.Sim;

/// <summary>
/// Turns real time into hours due. It paces a game and is no part of one: nothing
/// here is saved, and two machines running the same commands agree whatever their
/// clocks say. That is why this is the one place a <c>double</c> appears.
///
/// The original's: each frame's real seconds are added up, and once they reach the
/// current speed's threshold they are set back to nought and one hour is due - the
/// excess dropped, and never more than one hour a frame. The pause is our own.
/// See <c>docs/mechanics/tick.md</c>, section 2.
/// </summary>
public sealed class CClock
{
	public const int SpeedCount = 5;

	/// <summary>
	/// Real seconds for one game hour at each speed, the original's five: 5, 2, 1 and half a second, and at the top
	/// speed a ten-thousandth - an hour every frame (<c>reversing/findings/FINDINGS-tick.md</c>).
	/// </summary>
	private static readonly double[] SecondsPerHour = { 5.0, 2.0, 1.0, 0.5, 0.0001 };

	private int _speed = 2;
	private double _owed;

	/// <summary>0 is the slowest and <see cref="SpeedCount"/> - 1 the fastest.</summary>
	public int Speed
	{
		get => _speed;
		set => _speed = Math.Clamp(value, 0, SpeedCount - 1);
	}

	/// <summary>
	/// Whether the clock is at the top speed, where a client running hours beside its frames runs them one after another
	/// and not one a frame (<see cref="CSimulation.BeginAdvance"/>).
	/// </summary>
	public bool IsTopSpeed => _speed == SpeedCount - 1;

	/// <summary>A game starts paused, as the original's does.</summary>
	public bool Paused { get; set; } = true;

	/// <summary>
	/// Whether an hour has come due after another frame of real time: the frame's seconds added up until they reach the
	/// speed's threshold, then set back to nought. So a slow frame brings its hour at once and no more, and what passed
	/// beyond the threshold is not carried over - the original's.
	/// </summary>
	public bool HourDue(double realSeconds)
	{
		if (Paused)
		{
			_owed = 0;
			return false;
		}

		_owed += Math.Max(realSeconds, 0);
		if (_owed < SecondsPerHour[_speed])
		{
			return false;
		}
		_owed = 0;
		return true;
	}
}
