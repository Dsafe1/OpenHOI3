using System;

namespace OpenHOI3.Sim;

/// <summary>
/// Who may stop the clock and set its speed, and the one way to do either. Every pause and every change of speed a
/// player, a screen or the game's end asks for comes through here, and nothing else writes
/// <see cref="CClock.Paused"/> or <see cref="CClock.Speed"/> while a game is played.
///
/// It is one place because a game between several players has rules a game of one has not
/// (<c>docs/multiplayer.md</c>, section 2): anybody may pause and resume, only the host sets the speed, and nothing
/// pauses by itself - not the in-game menu, not the Statistics screen, not a message whose type pauses. The clock
/// that paces such a game is the host's; how a client's pause reaches it is the networking's, which is not built.
/// </summary>
public sealed class CPace
{
	private readonly CClock _clock;

	public CPace(CClock clock)
	{
		_clock = clock;
	}

	/// <summary>Whether several players share the game. A game of one until the networking says otherwise.</summary>
	public bool Multiplayer { get; set; }

	/// <summary>Whether this machine is the host of a game between several players; a game of one is its own host.</summary>
	public bool IsHost { get; set; } = true;

	public bool Paused => _clock.Paused;

	public int Speed => _clock.Speed;

	/// <summary>Whether this machine's player may set the speed: always in a game of one, the host alone otherwise.</summary>
	public bool MaySetSpeed => !Multiplayer || IsHost;

	/// <summary>
	/// On a machine that follows a host's game: where a player's pause or resume goes, the clock that paces the game
	/// being the host's. This machine's own clock then only shows what the host's says (<see cref="Set"/>).
	/// </summary>
	public Action? AskHostToTogglePause { get; set; }

	/// <summary>A player's own pause or resume - the key, the top bar. Anybody's to give.</summary>
	public void TogglePause()
	{
		if (AskHostToTogglePause is { } ask)
		{
			ask();
			return;
		}
		_clock.Paused = !_clock.Paused;
	}

	/// <summary>The clock stopped where it runs, as a player's pause is: a host's, for a player who has dropped.</summary>
	public void Pause()
	{
		_clock.Paused = true;
	}

	/// <summary>A player's change of speed by so many steps. Answers whether it was his to make.</summary>
	public bool ChangeSpeed(int steps)
	{
		if (!MaySetSpeed)
		{
			return false;
		}
		_clock.Speed += steps;
		return true;
	}

	/// <summary>
	/// A pause nobody asked for by name - a menu or a screen opened, a message that pauses. Taken in a game of one only.
	/// Answers whether it stopped a running clock, which is what its caller hands to <see cref="ResumeAfter"/>.
	/// </summary>
	public bool PauseByItself()
	{
		if (Multiplayer || _clock.Paused)
		{
			return false;
		}
		_clock.Paused = true;
		return true;
	}

	/// <summary>Lets the clock run again where <see cref="PauseByItself"/> had stopped it and it still stands.</summary>
	public void ResumeAfter(bool stoppedIt)
	{
		if (stoppedIt && _clock.Paused)
		{
			_clock.Paused = false;
		}
	}

	/// <summary>
	/// Stops the clock because the game can go no further - its end, a replay run out. Not a player's pause and not one
	/// a screen makes: it holds for a game between several players too.
	/// </summary>
	public void Stop()
	{
		_clock.Paused = true;
	}

	/// <summary>The pause and the speed as given from outside play: a session taking up another's, a developer's switch.</summary>
	public void Set(bool paused, int speed)
	{
		_clock.Paused = paused;
		_clock.Speed = speed;
	}
}
