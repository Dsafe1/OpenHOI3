using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A save's ids for units, brigades and lines of the queue - pairs of a serial and a type - as this game's one count.
/// What the history made is type 41, and its serials are the numbers a new game here gives, so they are kept; the
/// player's own minting is type 4713, numbered after the save's <c>unit</c> counter in the order it minted; any other
/// type after both. The same pair always answers the same number. See <c>docs/mechanics/savegame.md</c>, section 5.
/// </summary>
public sealed class CSaveIds
{
	/// <summary>The type of what the history made.</summary>
	public const int History = 41;

	/// <summary>The type single player mints in play.</summary>
	public const int Played = 4713;

	private readonly Dictionary<(int Serial, int Type), int> _others = new();
	private int _historyNext = 1;
	private int _playedHighest;

	/// <summary>The highest number given.</summary>
	public int Highest { get; private set; }

	/// <summary>The highest number the history's and the player's minting can take: a number above it is free.</summary>
	public int Reserved => _historyNext - 1 + _playedHighest;

	/// <summary>The save's <c>unit</c> counter, the next serial of what the history made.</summary>
	public int HistoryNext => _historyNext;

	/// <summary>
	/// A number given back as the save's pair: below the history's counter, the history's own serial of type 41; from
	/// it on, minted in play, type 4713 and counted from one at the counter. Nought is the null pair. Another peer's minting
	/// is not told apart, and is written as the player's (<c>savegame.md</c>, *Ours*).
	/// </summary>
	public static (int Serial, int Type) Pair(int id, int historyNext)
	{
		if (id <= 0)
		{
			return (0, 0);
		}
		return id < historyNext ? (id, History) : (id - historyNext + 1, Played);
	}

	/// <summary>The save's <c>unit</c> counter: the next serial of what the history made.</summary>
	public void SetHistoryNext(int next)
	{
		_historyNext = System.Math.Max(_historyNext, next);
	}

	/// <summary>A top-level <c>id</c> block: the highest serial of a type minted in play.</summary>
	public void SetHighest(int serial, int type)
	{
		if (type == Played)
		{
			_playedHighest = System.Math.Max(_playedHighest, serial);
		}
	}

	/// <summary>The number of an <c>{ id = N type = T }</c> block; nought for none or the null pair.</summary>
	public int Of(ClausewitzNode? pair)
	{
		if (pair is null || !pair.IsBlock)
		{
			return 0;
		}
		return Of(pair.GetInt("id"), pair.GetInt("type"));
	}

	/// <summary>Where every pair is given a new number after this one, in the order met; -1 for a save's numbering.</summary>
	private int _freshAfter = -1;

	/// <summary>
	/// Numbers for a file read into a game under way - a <c>load_oob</c> file written in a save's form: every pair, of
	/// whatever type, takes the next number after the game's last, in the order it is met, the same pair the same number.
	/// *Ours*: the original keeps the file's own pairs, which may be a live unit's (<c>units.md</c>, "A file of units as
	/// a game runs").
	/// </summary>
	public static CSaveIds After(int last)
	{
		return new CSaveIds { _freshAfter = last, Highest = last };
	}

	public int Of(int serial, int type)
	{
		if (serial <= 0 && type <= 0)
		{
			return 0;
		}
		int id;
		if (_freshAfter >= 0)
		{
			if (!_others.TryGetValue((serial, type), out id))
			{
				id = _freshAfter + 1 + _others.Count;
				_others[(serial, type)] = id;
			}
		}
		else if (type == History)
		{
			id = serial;
		}
		else if (type == Played)
		{
			id = _historyNext - 1 + serial;
		}
		else if (!_others.TryGetValue((serial, type), out id))
		{
			id = Reserved + 1 + _others.Count;
			_others[(serial, type)] = id;
		}
		if (id > Highest)
		{
			Highest = id;
		}
		return id;
	}
}
