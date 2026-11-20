using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenHOI3.Interface.Chat;

/// <summary>
/// The chat in play as one machine holds it, with no engine: each channel's lines, which channels the player is in
/// and which is the current one, the unread marks, the lines sent before for Up and Down, and what a typed line
/// comes to. Kept for a game between machines across the loads that bring them into step. See
/// <c>docs/mechanics/chat.md</c>; <c>reversing/findings/FINDINGS-ingamechat.md</c>.
/// </summary>
public sealed class CChatLog
{
	/// <summary>How many lines a channel keeps: ours, the original's limit not being read.</summary>
	public const int MostLines = 200;

	private readonly List<CChatLine>[] _lines = CChatChannels.Keys.Select(_ => new List<CChatLine>()).ToArray();
	private readonly bool[] _unread = new bool[CChatChannels.Keys.Count];
	private readonly List<string> _sent = new();
	private int _sentAt;

	/// <summary>The channel a typed line goes to and whose lines are shown.</summary>
	public int Current { get; private set; } = CChatChannels.General;

	/// <summary>The faction channel the player is in beside General; General where none has been said.</summary>
	public int FactionChannel { get; private set; } = CChatChannels.General;

	/// <summary>Whether the window is open: a line that comes into the current channel is then read at once.</summary>
	public bool IsOpen { get; set; }

	/// <summary>The player who last sent this one a line alone, for <c>/reply</c>; null where nobody has.</summary>
	public string? LastWhisperer { get; private set; }

	/// <summary>Raised each time anything shown may have changed.</summary>
	public int Version { get; private set; }

	/// <summary>The channels the player is in, in their order: General, then its faction's.</summary>
	public IReadOnlyList<int> Joined => FactionChannel == CChatChannels.General
		? new[] { CChatChannels.General }
		: new[] { CChatChannels.General, FactionChannel };

	public IReadOnlyList<CChatLine> Lines(int channel) => _lines[channel];

	public bool Unread(int channel) => _unread[channel];

	/// <summary>Whether a channel the player is in holds a line not read: the inbox button's second frame.</summary>
	public bool AnyUnread => Joined.Any(channel => _unread[channel]);

	/// <summary>
	/// The player's faction channel as its country now stands. One left is left at once and its lines put away from
	/// the player - ours: the original was not seen taking a player out of the channel of a faction he left.
	/// </summary>
	public void SetFactionChannel(int channel)
	{
		if (channel == FactionChannel || !CChatChannels.Is(channel))
		{
			return;
		}
		if (Current == FactionChannel)
		{
			Current = CChatChannels.General;
		}
		FactionChannel = channel;
		Version++;
	}

	/// <summary>A line that came. A whisper is shown in the current channel, having none of its own.</summary>
	public void Add(CChatLine line)
	{
		int channel = line.Kind is EChatLineKind.WhisperIn or EChatLineKind.WhisperOut || !CChatChannels.Is(line.Channel)
			? Current : line.Channel;
		List<CChatLine> lines = _lines[channel];
		lines.Add(line with { Channel = channel });
		if (lines.Count > MostLines)
		{
			lines.RemoveAt(0);
		}
		if (line.Kind == EChatLineKind.WhisperIn)
		{
			LastWhisperer = line.Name;
		}
		if (!IsOpen || channel != Current)
		{
			_unread[channel] = true;
		}
		Version++;
	}

	/// <summary>The window opened: the current channel's lines are read.</summary>
	public void Open()
	{
		IsOpen = true;
		_unread[Current] = false;
		Version++;
	}

	public void Close()
	{
		IsOpen = false;
		Version++;
	}

	/// <summary>A channel the player is in made the current one, and read.</summary>
	public void Select(int channel)
	{
		if (!Joined.Contains(channel))
		{
			return;
		}
		Current = channel;
		if (IsOpen)
		{
			_unread[channel] = false;
		}
		Version++;
	}

	/// <summary>Ctrl and Tab: the next channel the player is in becomes the current one, with Shift the one before.</summary>
	public void Next(bool back)
	{
		IReadOnlyList<int> joined = Joined;
		int at = Math.Max(0, joined.ToList().IndexOf(Current));
		Select(joined[(at + (back ? joined.Count - 1 : 1)) % joined.Count]);
	}

	/// <summary>
	/// What a line typed comes to: trimmed, an empty one nothing; one that begins with a slash a command -
	/// <c>/whisper name text</c> and <c>/reply text</c> a line to one player, with the original's errors for what is
	/// missing, and any other the unknown-command error (the other nine commands' bodies are not read, and are not
	/// built); anything else a line to the current channel. A line that is sent is kept for Up and Down.
	/// </summary>
	/// <param name="users">The names of the players in the game, to find the one whispered to.</param>
	public (EChatTyped Kind, string Target, string Text) Read(string typed, IEnumerable<string> users)
	{
		string text = typed.Trim();
		if (text.Length == 0)
		{
			return (EChatTyped.Nothing, "", "");
		}
		_sent.Add(text);
		_sentAt = _sent.Count;
		if (text[0] != '/')
		{
			return (EChatTyped.Line, "", text);
		}
		int space = text.IndexOf(' ');
		string command = (space < 0 ? text : text[..space]).ToLowerInvariant();
		string rest = space < 0 ? "" : text[(space + 1)..].Trim();
		switch (command)
		{
			case "/whisper":
			{
				// The name is matched whole against the players', the longest first, since a name may hold spaces.
				string? target = users.OrderByDescending(user => user.Length).FirstOrDefault(user =>
					rest.StartsWith(user, StringComparison.OrdinalIgnoreCase)
					&& (rest.Length == user.Length || rest[user.Length] == ' '));
				if (rest.Length == 0)
				{
					return (EChatTyped.Error, "", "CHAT_ERROR_WHISPER_MISSINGARG");
				}
				if (target is null)
				{
					int end = rest.IndexOf(' ');
					return (EChatTyped.Error, end < 0 ? rest : rest[..end], "CHAT_ERROR_UNKNOWNUSER");
				}
				string message = rest[target.Length..].Trim();
				return message.Length == 0 ? (EChatTyped.Error, "", "CHAT_ERROR_WHISPER_MISSINGARG") : (EChatTyped.Whisper, target, message);
			}
			case "/reply":
				if (rest.Length == 0)
				{
					return (EChatTyped.Error, "", "CHAT_ERROR_REPLY_MISSINGARG");
				}
				return LastWhisperer is { } whisperer && users.Contains(whisperer)
					? (EChatTyped.Whisper, whisperer, rest)
					: (EChatTyped.Error, "", "CHAT_ERROR_REPLY_MISSINGUSER");
			default:
				return (EChatTyped.Error, "", "CHAT_ERROR_UNKNOWNSLASH");
		}
	}

	/// <summary>Up or Down in the box: the line sent before the one shown, or after it; null where there is none that way.</summary>
	public string? Recall(bool up)
	{
		int at = _sentAt + (up ? -1 : 1);
		if (at < 0 || at > _sent.Count)
		{
			return null;
		}
		_sentAt = at;
		return at == _sent.Count ? "" : _sent[at];
	}
}
